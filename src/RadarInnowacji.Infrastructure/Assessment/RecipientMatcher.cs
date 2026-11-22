using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Assessment;

/// <summary>
/// Dopasowanie odbiorców z katalogu do projektu. Wynik 0–100 jest sumą jawnych składników,
/// a każdy składnik ma opis widoczny w interfejsie.
/// </summary>
public static class RecipientMatcher
{
    public const int MinimumScore = 30;

    public static IReadOnlyList<string> Rules { get; } =
    [
        "do 35 pkt – wspólne sektory projektu i odbiorcy (sektor rozpoznany w tekście projektu)",
        "do 15 pkt – słowa kluczowe odbiorcy występujące w opisie projektu (porównanie rdzeni słów)",
        "30 pkt – odbiorca obsługuje rekomendowaną ścieżkę (15 pkt – ścieżkę drugorzędną); 10 pkt – typ odbiorcy typowy dla ścieżki",
        "10 pkt – etap projektu nie niższy niż minimalny etap odbiorcy (projekt za wczesny: 0 pkt i adnotacja)",
        "10 pkt – ten sam region (gdy region odbiorcy jest określony)",
        $"wyświetlane są dopasowania z wynikiem ≥ {MinimumScore}"
    ];

    /// <summary>
    /// Rdzeń słowa kluczowego: bez diakrytyków i bez dwóch ostatnich znaków (min. 5),
    /// aby „szkoła” pasowała do „szkole”, a „rehabilitacja” do „rehabilitacji”.
    /// </summary>
    public static string Stem(string keyword)
    {
        var normalized = SectorDictionary.Normalize(keyword).Trim();
        if (normalized.Length <= 5)
        {
            return normalized;
        }

        return normalized[..Math.Max(5, normalized.Length - 2)];
    }

    public static IReadOnlyList<RecipientMatch> Match(
        RecipientMatchInput input,
        IEnumerable<Recipient> recipients,
        int limit = 6)
    {
        var text = SectorDictionary.Normalize(input.Text);
        var projectSectors = input.Sectors.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var typicalTypes = input.Paths
            .SelectMany(path => DevelopmentPathCatalog.Get(path).TypicalRecipients)
            .ToHashSet();
        var result = new List<RecipientMatch>();

        foreach (var recipient in recipients.Where(x => x.IsActive))
        {
            var score = 0;
            var reasons = new List<string>();

            var sectors = SectorDictionary.SplitList(recipient.Sectors);
            var shared = sectors.Where(projectSectors.Contains).ToList();
            if (shared.Count > 0)
            {
                var points = Math.Min(35, 20 + 15 * (shared.Count - 1));
                score += points;
                reasons.Add($"+{points} wspólne sektory: {string.Join(", ", shared)}");
            }

            var keywords = SectorDictionary.SplitList(recipient.Keywords)
                .Where(keyword => text.Contains(" " + Stem(keyword), StringComparison.Ordinal))
                .ToList();
            if (keywords.Count > 0)
            {
                var points = Math.Min(15, 5 * keywords.Count);
                score += points;
                reasons.Add($"+{points} słowa kluczowe: {string.Join(", ", keywords.Take(4))}");
            }

            var recipientPaths = SectorDictionary.SplitList(recipient.Paths)
                .Select(x => Enum.TryParse<DevelopmentPath>(x, true, out var path) ? path : (DevelopmentPath?)null)
                .Where(x => x is not null)
                .Select(x => x!.Value)
                .ToHashSet();
            var primary = input.Paths.FirstOrDefault(x => x != DevelopmentPath.UzupelnienieInformacji);
            if (input.Paths.Count > 0 && recipientPaths.Contains(primary))
            {
                score += 30;
                reasons.Add($"+30 obsługuje rekomendowaną ścieżkę: {DevelopmentPathCatalog.Get(primary).Name}");
            }
            else if (input.Paths.Any(recipientPaths.Contains))
            {
                var secondary = input.Paths.First(recipientPaths.Contains);
                score += 15;
                reasons.Add($"+15 obsługuje ścieżkę: {DevelopmentPathCatalog.Get(secondary).Name}");
            }

            if (typicalTypes.Contains(recipient.Type))
            {
                score += 10;
                reasons.Add("+10 typ odbiorcy typowy dla rekomendowanej ścieżki");
            }

            if (input.Stage >= recipient.MinimumStage || recipient.MinimumStage == ProjectStatus.Unknown)
            {
                score += 10;
                reasons.Add("+10 etap projektu odpowiedni dla odbiorcy");
            }
            else
            {
                reasons.Add($"0 odbiorca zajmuje się projektami od etapu: {RecommendationEngine.StageName(recipient.MinimumStage)}");
            }

            if (!string.IsNullOrWhiteSpace(recipient.Region) && !string.IsNullOrWhiteSpace(input.Region) &&
                string.Equals(recipient.Region.Trim(), input.Region.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
                reasons.Add($"+10 ten sam region: {recipient.Region}");
            }

            if (score < MinimumScore)
            {
                continue;
            }

            result.Add(new RecipientMatch
            {
                RecipientId = recipient.Id,
                Name = recipient.Name,
                Type = recipient.Type,
                Description = recipient.Description,
                Website = recipient.Website,
                IsExample = recipient.IsExample,
                Score = Math.Min(100, score),
                Reasons = reasons
            });
        }

        return result
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Name, StringComparer.CurrentCulture)
            .Take(Math.Clamp(limit, 1, 20))
            .ToList();
    }
}
