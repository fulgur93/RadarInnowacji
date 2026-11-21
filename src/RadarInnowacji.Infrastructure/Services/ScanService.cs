using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>Pobiera materiały z aktywnych źródeł i zapisuje je w ujednoliconym modelu.</summary>
public sealed class ScanService(
    RadarDbContext db,
    ISourceAdapterResolver adapterResolver,
    IOptions<RadarOptions> options,
    ILogger<ScanService> logger) : IScanService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<ScanResult> ScanSourceAsync(int sourceId, CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var source = await db.Sources.FirstOrDefaultAsync(x => x.Id == sourceId, cancellationToken);
            if (source is null)
            {
                return new ScanResult(sourceId, "Nieznane źródło", ScanStatus.Failed, 0, 0, "Nie znaleziono źródła.");
            }

            if (source.IsDemo)
            {
                return new ScanResult(source.Id, source.Name, ScanStatus.Succeeded, 0, 0, null);
            }

            var startedAt = DateTime.UtcNow;
            var history = new ScanHistory
            {
                SourceId = source.Id,
                StartedAt = startedAt,
                Status = ScanStatus.Started,
                IsDemo = false
            };
            db.ScanHistory.Add(history);
            await db.SaveChangesAsync(cancellationToken);

            try
            {
                var adapter = adapterResolver.Resolve(source);
                if (adapter is null)
                {
                    throw new InvalidOperationException(
                        $"Brak adaptera dla typu źródła {source.SourceType}.");
                }

                var maxItems = source.SourceType is SourceType.GitHubApi or SourceType.GitLabApi
                    ? Math.Clamp(options.Value.Repositories.MaxItemsPerScan, 1, 2000)
                    : Math.Clamp(options.Value.Scanning.MaxItemsPerSource, 1, 500);
                var fetched = await adapter.FetchResultAsync(source, maxItems, cancellationToken);
                var limited = fetched.Items.Take(maxItems).ToList();
                var added = 0;
                var filterNoise = options.Value.Repositories.FilterNoise;
                var skipped = new Dictionary<string, int>(StringComparer.Ordinal);
                var duplicates = 0;

                foreach (var item in limited)
                {
                    var hash = ComputeHash(source.Id, item);
                    var existing = await db.Discoveries
                        .Include(x => x.Repository)
                        .FirstOrDefaultAsync(
                            x => x.SourceId == source.Id && x.ExternalId == item.ExternalId,
                            cancellationToken);

                    if (existing is null && item.Repository is not null)
                    {
                        if (filterNoise && RepositoryNoiseFilter.ClassifyListing(item) is { } noise)
                        {
                            skipped[noise.Label] = skipped.GetValueOrDefault(noise.Label) + 1;
                            continue;
                        }

                        // Ten sam projekt znaleziony już przez inne źródło tej samej platformy.
                        var key = RepositoryContent.CanonicalKey(item);
                        if (await db.Repositories.AnyAsync(
                                x => x.CanonicalKey == key && x.Discovery.SourceId != source.Id,
                                cancellationToken))
                        {
                            duplicates++;
                            continue;
                        }
                    }

                    if (existing is null)
                    {
                        db.Discoveries.Add(CreateDiscovery(source, item, hash, startedAt));
                        added++;
                    }
                    else
                    {
                        UpdateDiscovery(existing, item, hash, startedAt);
                    }
                }

                // Postęp skanowania przyrostowego zapisujemy także po skanie częściowym.
                if (fetched.Cursors is not null)
                {
                    source.Configuration = SourceConfiguration.WithCursors(source.Configuration, fetched.Cursors);
                }

                await db.SaveChangesAsync(cancellationToken);
                var detailsNote = await EnrichRepositoriesAsync(source, adapter, cancellationToken);
                var filterNote = FilterNote(skipped, duplicates);

                var status = fetched.IsComplete ? ScanStatus.Succeeded : ScanStatus.Partial;
                var scanWarning = Limit(fetched.Warning);
                var warning = Limit(string.Join(" ", new[] { fetched.Warning, detailsNote, filterNote }.Where(x => x is not null)));
                warning = string.IsNullOrEmpty(warning) ? null : warning;

                var finishedAt = DateTime.UtcNow;
                source.LastScanDate = finishedAt;
                source.LastSuccessfulScan = finishedAt;
                source.LastError = fetched.IsComplete ? null : scanWarning;
                history.Status = status;
                history.ItemsFound = limited.Count;
                history.ItemsNew = added;
                history.FinishedAt = finishedAt;
                history.ErrorMessage = warning;

                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Skan źródła {Source} zakończony ({Status}): znaleziono {Found}, nowych {New}. {Warning}",
                    source.Name,
                    status,
                    limited.Count,
                    added,
                    warning);

                return new ScanResult(source.Id, source.Name, status, limited.Count, added, warning);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                history.Status = ScanStatus.Failed;
                history.ErrorMessage = "Skanowanie zostało przerwane.";
                history.FinishedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(CancellationToken.None);
                throw;
            }
            catch (Exception exception)
            {
                var message = exception.Message.Length <= ContentLimits.MaxErrorLength
                    ? exception.Message
                    : exception.Message[..ContentLimits.MaxErrorLength];

                var finishedAt = DateTime.UtcNow;
                source.LastScanDate = finishedAt;
                source.LastError = message;
                history.Status = ScanStatus.Failed;
                history.ErrorMessage = message;
                history.FinishedAt = finishedAt;

                await db.SaveChangesAsync(CancellationToken.None);
                logger.LogWarning(exception, "Skan źródła {Source} nie powiódł się.", source.Name);

                return new ScanResult(source.Id, source.Name, ScanStatus.Failed, 0, 0, message);
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<IReadOnlyList<ScanResult>> ScanDueSourcesAsync(CancellationToken cancellationToken = default)
    {
        var sources = await db.Sources
            .Where(x => x.IsActive && !x.IsDemo)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var results = new List<ScanResult>();

        foreach (var source in sources)
        {
            var interval = Math.Max(1, source.ScanIntervalMinutes);
            if (source.LastScanDate is not null && source.LastScanDate.Value.AddMinutes(interval) > now)
            {
                continue;
            }

            results.Add(await ScanSourceAsync(source.Id, cancellationToken));
        }

        return results;
    }

    /// <summary>
    /// Dociąga README i języki dla projektów tego źródła, które ich jeszcze nie mają (najnowsze najpierw),
    /// w ramach <see cref="RepositoryOptions.MaxDetailsPerScan"/>. Błąd tego kroku nie psuje skanu.
    /// </summary>
    private async Task<string?> EnrichRepositoriesAsync(
        Source source,
        ISourceAdapter adapter,
        CancellationToken cancellationToken)
    {
        if (adapter is not IRepositoryDetailsAdapter detailsAdapter)
        {
            return null;
        }

        var limit = Math.Clamp(options.Value.Repositories.MaxDetailsPerScan, 0, 500);
        if (limit == 0)
        {
            return null;
        }

        var pending = await db.Repositories
            .Include(x => x.Discovery)
            .Where(x => x.Discovery.SourceId == source.Id && x.DetailsFetchedAt == null)
            .OrderByDescending(x => x.Discovery.PublishedAt)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
        if (pending.Count == 0)
        {
            return null;
        }

        RepositoryDetailsBatch batch;
        try
        {
            batch = await detailsAdapter.FetchDetailsAsync(source, pending, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Nie udało się pobrać szczegółów repozytoriów źródła {Source}.", source.Name);
            return $"Nie udało się pobrać szczegółów repozytoriów: {exception.Message}";
        }

        var now = DateTime.UtcNow;
        foreach (var result in batch.Results)
        {
            RepositoryContent.ApplyDetails(result.Repository, result.Details, result.Error, now);
        }

        await db.SaveChangesAsync(cancellationToken);

        var linked = 0;
        try
        {
            linked = await new RepositoryDeduplicator(db).LinkDuplicatesAsync(
                batch.Results.Where(x => x.Details is not null).Select(x => x.Repository).ToList(),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Deduplikacja repozytoriów źródła {Source} nie powiodła się.", source.Name);
        }

        var noise = batch.Results.Count(x => x.Repository.NoiseReason is not null);
        var notes = new List<string>();
        if (noise > 0) notes.Add($"Oznaczono jako szum po README: {noise}.");
        if (linked > 0) notes.Add($"Połączono z tym samym projektem na innej platformie: {linked}.");
        logger.LogInformation(
            "Szczegóły repozytoriów źródła {Source}: {Done} z {Pending}. {Stop}",
            source.Name,
            batch.Results.Count,
            pending.Count,
            batch.StopReason);

        if (batch.StopReason is not null)
        {
            notes.Add($"README pobrano dla {batch.Results.Count} z {pending.Count} projektów; reszta w kolejnym skanie. {batch.StopReason}");
        }

        return notes.Count == 0 ? null : string.Join(" ", notes);
    }

    private static string? FilterNote(IReadOnlyDictionary<string, int> skipped, int duplicates)
    {
        var parts = new List<string>();
        if (skipped.Count > 0)
        {
            parts.Add($"Pominięto {skipped.Values.Sum()} ({string.Join(", ",
                skipped.OrderByDescending(x => x.Value).Select(x => $"{x.Key}: {x.Value}"))}).");
        }

        if (duplicates > 0)
        {
            parts.Add($"Duplikaty projektów z innych źródeł: {duplicates}.");
        }

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private static string? Limit(string? value) =>
        value is null ? null : value[..Math.Min(value.Length, ContentLimits.MaxErrorLength)];

    private static Discovery CreateDiscovery(Source source, SourceItem item, string hash, DateTime now) => new()
    {
        Repository = item.Repository is null ? null : RepositoryContent.Create(item, now),
        Title = item.Title,
        Description = item.Description,
        RawContent = item.Content,
        ContentLength = item.Content.Length,
        IsContentTruncated = item.Content.Length >= ContentLimits.MaxRawContentLength,
        SourceId = source.Id,
        SourceUrl = item.SourceUrl,
        ExternalId = item.ExternalId,
        PublishedAt = item.PublishedAt,
        LastActivityAt = item.LastActivityAt,
        CreatedAt = now,
        DiscoveredAt = now,
        ContentHash = hash,
        Author = item.Author,
        Organization = item.Organization,
        Technologies = JsonSerializer.Serialize(item.Technologies),
        CategoryIds = "[]",
        AIProcessed = false,
        IsPotentialInnovation = false,
        ProcessingStatus = ProcessingStatus.Pending,
        Status = DiscoveryStatus.Candidate,
        IsDemo = false
    };

    private static void UpdateDiscovery(Discovery discovery, SourceItem item, string hash, DateTime now)
    {
        discovery.Title = item.Title;
        discovery.Description = item.Description;

        // README pobrane wcześniej jest pełniejsze niż opis z listy wyników; nie nadpisujemy go.
        var hasDetails = discovery.Repository?.DetailsFetchedAt is not null;
        if (!hasDetails)
        {
            discovery.RawContent = item.Content;
            discovery.ContentLength = item.Content.Length;
            discovery.IsContentTruncated = item.Content.Length >= ContentLimits.MaxRawContentLength;
        }

        if (item.Repository is not null)
        {
            if (discovery.Repository is null)
            {
                discovery.Repository = RepositoryContent.Create(item, now);
            }
            else
            {
                RepositoryContent.UpdateMetadata(discovery.Repository, item, now);
                if (RepositoryContent.NeedsDetailsRefresh(discovery.Repository, item.LastActivityAt))
                {
                    discovery.Repository.DetailsFetchedAt = null;
                }
            }
        }

        discovery.SourceUrl = item.SourceUrl;
        discovery.PublishedAt = item.PublishedAt ?? discovery.PublishedAt;
        discovery.LastActivityAt = item.LastActivityAt ?? discovery.LastActivityAt;
        discovery.Author = item.Author ?? discovery.Author;
        discovery.Organization = item.Organization ?? discovery.Organization;
        discovery.Technologies = hasDetails
            ? RepositoryContent.MergeTechnologies(
                JsonSerializer.Serialize(item.Technologies),
                RepositoryContent.ReadLanguages(discovery.Repository!.Languages))
            : JsonSerializer.Serialize(item.Technologies);
        discovery.ContentHash = hash;
    }

    private static string ComputeHash(int sourceId, SourceItem item)
    {
        var input = $"{sourceId}\n{item.ExternalId}\n{item.Title}\n{item.Content}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }
}
