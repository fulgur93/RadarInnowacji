using RadarInnowacji.Core.Entities;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>
/// Sygnały używane przy rozpoznawaniu tego samego projektu w różnych źródłach:
/// ExternalId, ContentHash oraz podobieństwo tytułów i opisów (gotowe embeddingi
/// są porównywane przez <see cref="EmbeddingMath"/>).
/// </summary>
public static class ProjectSimilarity
{
    public static string NormalizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = TextFeatures.Normalize(value);
        return new string(normalized.Where(char.IsLetterOrDigit).ToArray());
    }

    public static double TokenSimilarity(string? first, string? second)
    {
        var a = TextFeatures.Tokenize(first).ToHashSet(StringComparer.Ordinal);
        var b = TextFeatures.Tokenize(second).ToHashSet(StringComparer.Ordinal);
        return Dice(a, b);
    }

    public static double TitleSimilarity(InnovationProject first, InnovationProject second)
    {
        var firstTitles = Titles(first);
        var secondTitles = Titles(second);
        var best = 0.0;
        foreach (var a in firstTitles)
        {
            foreach (var b in secondTitles)
            {
                best = Math.Max(best, TokenSimilarity(a, b));
            }
        }

        return best;
    }

    public static double DescriptionSimilarity(InnovationProject first, InnovationProject second) =>
        TokenSimilarity(DescriptionText(first), DescriptionText(second));

    /// <summary>Opisy projektu wraz z krótkim fragmentem materiałów źródłowych.</summary>
    public static string DescriptionText(InnovationProject project)
    {
        var parts = new List<string> { project.Description };
        foreach (var link in project.DiscoveryLinks.Take(4))
        {
            var discovery = link.Discovery;
            parts.Add(discovery.Title);
            parts.Add(discovery.Description);
            if (!string.IsNullOrWhiteSpace(discovery.RawContent))
            {
                var content = discovery.RawContent;
                parts.Add(content.Length <= 600 ? content : content[..600]);
            }
        }

        return string.Join(" ", parts);
    }

    private static IEnumerable<string> Titles(InnovationProject project)
    {
        yield return project.Title;
        foreach (var link in project.DiscoveryLinks.Take(8))
        {
            yield return link.Discovery.Title;
        }
    }

    private static double Dice(HashSet<string> first, HashSet<string> second)
    {
        if (first.Count == 0 || second.Count == 0)
        {
            return 0;
        }

        var intersection = first.Count < second.Count
            ? first.Count(x => second.Contains(x))
            : second.Count(x => first.Contains(x));
        return 2.0 * intersection / (first.Count + second.Count);
    }
}
