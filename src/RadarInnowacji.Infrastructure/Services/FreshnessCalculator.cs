using RadarInnowacji.Core.Entities;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>
/// Ocena świeżości projektu wyłącznie na podstawie dat. Nie wykorzystuje oceny AI
/// ani statusu innowacyjności – świeżość jest od niej niezależna.
/// </summary>
public static class FreshnessCalculator
{
    /// <summary>Wynik w zakresie 0–100. Brakujące daty obniżają ocenę, ale nie są wymyślane.</summary>
    public static double Calculate(InnovationProject project, DateTime? now = null)
    {
        var reference = now ?? DateTime.UtcNow;
        var discoveries = project.DiscoveryLinks
            .Select(x => x.Discovery)
            .Where(x => x is not null)
            .ToList();

        var activityDates = new List<DateTime?>
        {
            project.LastActivityAt,
            discoveries.MinOrDefault(x => x.LastActivityAt),
            discoveries.MaxOrDefault(x => x.PublishedAt)
        };
        var latestActivity = activityDates.Where(x => x is not null).Select(x => x!.Value).DefaultIfEmpty().Max();
        if (latestActivity == default)
        {
            latestActivity = discoveries.Select(x => x.DiscoveredAt).DefaultIfEmpty(project.FirstDiscoveredAt).Min();
        }

        var originDates = new List<DateTime?>
        {
            project.EarliestKnownDate,
            discoveries.MinOrDefault(x => x.EarliestKnownDate),
            discoveries.MinOrDefault(x => x.PublishedAt)
        };
        var earliestOrigin = originDates.Where(x => x is not null).Select(x => x!.Value).DefaultIfEmpty().Min();

        var discovered = discoveries.Select(x => x.DiscoveredAt)
            .DefaultIfEmpty(project.FirstDiscoveredAt)
            .Min();

        var recencyDays = Math.Max(0, (reference - latestActivity).TotalDays);
        var discoveryDays = Math.Max(0, (reference - discovered).TotalDays);
        var originScore = earliestOrigin == default
            ? 50.0
            : 100.0 * Math.Exp(-Math.Max(0, (reference - earliestOrigin).TotalDays) / 365.0);

        var score = 0.60 * (100.0 * Math.Exp(-recencyDays / 90.0))
                    + 0.25 * originScore
                    + 0.15 * (100.0 * Math.Exp(-discoveryDays / 730.0));

        return Math.Round(Math.Clamp(score, 0, 100), 1);
    }

    private static DateTime? MinOrDefault<T>(this IEnumerable<T> source, Func<T, DateTime?> selector) =>
        source.Select(selector).Where(x => x is not null).Select(x => x!.Value).DefaultIfEmpty().Min();

    private static DateTime? MaxOrDefault<T>(this IEnumerable<T> source, Func<T, DateTime?> selector) =>
        source.Select(selector).Where(x => x is not null).Select(x => x!.Value).DefaultIfEmpty().Max();
}
