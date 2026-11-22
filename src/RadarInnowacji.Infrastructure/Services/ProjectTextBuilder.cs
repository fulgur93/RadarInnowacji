using System.Text;
using System.Text.Json;
using RadarInnowacji.Core.Entities;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>
/// Jednolity tekst projektu używany zarówno przy grupowaniu kategorii (Etap 6),
/// jak i przy wyszukiwaniu duplikatów i podobnych projektów (Etap 7).
/// </summary>
public static class ProjectTextBuilder
{
    public static string Build(InnovationProject project)
    {
        var text = new StringBuilder()
            .AppendLine(project.Title)
            .AppendLine(project.Description);

        foreach (var link in project.DiscoveryLinks.Take(12))
        {
            var discovery = link.Discovery;
            text.AppendLine(discovery.Title).AppendLine(discovery.Description);
            if (!string.IsNullOrWhiteSpace(discovery.RawContent))
            {
                var content = discovery.RawContent;
                text.AppendLine(content.Length <= 2500 ? content : content[..2500]);
            }

            foreach (var suggestion in SuggestedCategories(discovery).Take(5))
            {
                text.Append("proponowana kategoria AI: ").AppendLine(suggestion);
            }
        }

        return text.ToString();
    }

    private static IReadOnlyList<string> SuggestedCategories(Discovery discovery)
    {
        var record = discovery.AiAnalyses
            .Where(x => x.IsSuccess)
            .OrderByDescending(x => x.AnalyzedAt)
            .FirstOrDefault();
        if (record?.ResultJson is null)
        {
            return Array.Empty<string>();
        }

        try
        {
            using var document = JsonDocument.Parse(record.ResultJson);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!property.Name.Equals("suggestedCategories", StringComparison.OrdinalIgnoreCase) ||
                    property.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                return property.Value.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(x => x.Length > 0)
                    .ToList();
            }
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }

        return Array.Empty<string>();
    }
}
