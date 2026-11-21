using System.Text.Json;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Adapters;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>Przenosi metadane i szczegóły repozytorium do encji odkrycia.</summary>
public static class RepositoryContent
{
    /// <summary>Ile znaków README czytać (więcej niż limit zapisu, żeby wiedzieć, że treść została skrócona).</summary>
    public const int MaxReadmeChars = ContentLimits.MaxRawContentLength * 2;

    private static readonly JsonSerializerOptions LanguagesJson = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Języki o mniejszym udziale nie trafiają do listy technologii odkrycia.</summary>
    public const double MinLanguageShare = 5;

    /// <summary>Po tylu dniach aktywności od pobrania szczegółów README jest pobierane ponownie.</summary>
    public static readonly TimeSpan DetailsRefreshAfterActivity = TimeSpan.FromDays(7);

    public static RepositoryInfo Create(RepositoryMetadata metadata, DateTime now)
    {
        var info = new RepositoryInfo();
        UpdateMetadata(info, metadata, now);
        return info;
    }

    public static void UpdateMetadata(RepositoryInfo info, RepositoryMetadata metadata, DateTime now)
    {
        info.Platform = metadata.Platform;
        info.ProjectId = Limit(metadata.ProjectId, ContentLimits.MaxTitleLength)!;
        info.FullName = Limit(metadata.FullName, ContentLimits.MaxTitleLength)!;
        info.Stars = metadata.Stars;
        info.Forks = metadata.Forks;
        info.OpenIssues = metadata.OpenIssues;
        info.License = Limit(metadata.License, 200) ?? info.License;
        info.HomepageUrl = Limit(NullIfEmpty(metadata.HomepageUrl), ContentLimits.MaxSourceUrlLength);
        info.DefaultBranch = Limit(metadata.DefaultBranch, 200) ?? info.DefaultBranch;
        info.ReadmePath = Limit(metadata.ReadmePath, 500) ?? info.ReadmePath;
        info.IsFork = metadata.IsFork;
        info.IsArchived = metadata.IsArchived;
        info.MetadataUpdatedAt = now;
        info.DemoUrl = Limit(DemoLinkDetector.Find(info.HomepageUrl, null, Owner(info)), ContentLimits.MaxSourceUrlLength) ?? info.DemoUrl;
    }

    /// <summary>Czy projekt był aktywny na tyle długo po pobraniu README, że warto pobrać je ponownie.</summary>
    public static bool NeedsDetailsRefresh(RepositoryInfo info, DateTime? lastActivityAt) =>
        info.DetailsFetchedAt is { } fetched &&
        lastActivityAt is { } activity &&
        activity > fetched + DetailsRefreshAfterActivity;

    public static void ApplyDetails(RepositoryInfo info, RepositoryDetails? details, string? error, DateTime now)
    {
        info.DetailsFetchedAt = now;
        if (details is null)
        {
            info.DetailsError = Limit(error ?? "Nie udało się pobrać szczegółów.", ContentLimits.MaxErrorLength);
            return;
        }

        var readme = NullIfEmpty(details.Readme);
        info.DetailsError = null;
        info.HasReadme = readme is not null;
        info.ReadmeLength = readme?.Length;
        info.Languages = JsonSerializer.Serialize(details.Languages, LanguagesJson);
        info.License = Limit(details.License, 200) ?? info.License;
        info.DefaultBranch = Limit(details.DefaultBranch, 200) ?? info.DefaultBranch;
        info.DemoUrl = Limit(DemoLinkDetector.Find(info.HomepageUrl, readme, Owner(info)), ContentLimits.MaxSourceUrlLength);

        var discovery = info.Discovery;
        var content = string.IsNullOrWhiteSpace(discovery.Description)
            ? readme ?? string.Empty
            : readme is null ? discovery.Description : $"{discovery.Description}\n\n{readme}";
        discovery.ContentLength = content.Length;
        discovery.IsContentTruncated = content.Length > ContentLimits.MaxRawContentLength;
        discovery.RawContent = Limit(content, ContentLimits.MaxRawContentLength)!;
        discovery.Technologies = MergeTechnologies(discovery.Technologies, details.Languages);
    }

    /// <summary>Dopisuje do listy technologii (JSON) języki o udziale co najmniej <see cref="MinLanguageShare"/>%.</summary>
    public static string MergeTechnologies(string technologiesJson, IReadOnlyDictionary<string, double> languages)
    {
        List<string> technologies;
        try
        {
            technologies = JsonSerializer.Deserialize<List<string>>(technologiesJson) ?? [];
        }
        catch (JsonException)
        {
            technologies = [];
        }

        technologies.AddRange(languages.Where(x => x.Value >= MinLanguageShare).Select(x => x.Key));
        return JsonSerializer.Serialize(technologies.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    public static IReadOnlyDictionary<string, double> ReadLanguages(string? json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, double>>(json ?? "{}") ?? new Dictionary<string, double>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, double>();
        }
    }

    private static string? Owner(RepositoryInfo info)
    {
        var slash = info.FullName.IndexOf('/');
        return slash > 0 ? info.FullName[..slash] : null;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? Limit(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
