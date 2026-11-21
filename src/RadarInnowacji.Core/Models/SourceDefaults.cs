using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Models;

/// <summary>Mapowanie typu źródła na domyślną metodę pobierania.</summary>
public static class SourceDefaults
{
    public static IReadOnlyList<SourceType> SupportedTypes { get; } =
    [
        SourceType.OaiPmh,
        SourceType.Dspace,
        SourceType.Rss,
        SourceType.Atom,
        SourceType.GitHubApi,
        SourceType.GitLabApi,
        SourceType.HuggingFaceApi,
        SourceType.KickstarterApi,
        SourceType.HtmlPage
    ];

    public static FetchMethod FetchMethodFor(SourceType sourceType) => sourceType switch
    {
        SourceType.OaiPmh => FetchMethod.OaiPmh,
        SourceType.Rss => FetchMethod.Rss,
        SourceType.Atom => FetchMethod.Atom,
        SourceType.HtmlPage => FetchMethod.Html,
        SourceType.Dspace or SourceType.GitHubApi or SourceType.GitLabApi or SourceType.HuggingFaceApi
            or SourceType.KickstarterApi => FetchMethod.Api,
        _ => FetchMethod.Manual
    };
}
