using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Web.Components;

/// <summary>Stan formularza zapytania radaru repozytoriów (listy jako tekst rozdzielany przecinkami).</summary>
public sealed class RepositoryQueryForm
{
    public string Keywords { get; set; } = string.Join(", ", RepositoryQuery.DefaultKeywords);
    public string Topics { get; set; } = string.Empty;
    public string Languages { get; set; } = string.Empty;
    public int CreatedWithinDays { get; set; } = RepositoryQuery.DefaultCreatedWithinDays;

    public static bool Supports(SourceType type) => type is SourceType.GitHubApi or SourceType.GitLabApi;

    public static string DefaultUrl(SourceType type) => type switch
    {
        SourceType.GitHubApi => "https://api.github.com",
        SourceType.GitLabApi => "https://gitlab.com",
        _ => string.Empty
    };

    public RepositoryQuery ToQuery() => new RepositoryQuery
    {
        Keywords = RepositoryQuery.ParseList(Keywords),
        Topics = RepositoryQuery.ParseList(Topics),
        Languages = RepositoryQuery.ParseList(Languages),
        CreatedWithinDays = CreatedWithinDays
    }.Normalize();

    public static RepositoryQueryForm From(RepositoryQuery query) => new()
    {
        Keywords = string.Join(", ", query.Keywords),
        Topics = string.Join(", ", query.Topics),
        Languages = string.Join(", ", query.Languages),
        CreatedWithinDays = query.CreatedWithinDays
    };
}
