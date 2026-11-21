using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Web.Components;

/// <summary>Stan formularza wyszukiwania kampanii Kickstartera (listy jako tekst rozdzielany przecinkami).</summary>
public sealed class KickstarterQueryForm
{
    public string Keywords { get; set; } = string.Join(", ", KickstarterQuery.DefaultKeywords);
    public string Categories { get; set; } = "16";
    public string State { get; set; } = KickstarterQuery.DefaultState;
    public string Sort { get; set; } = KickstarterQuery.DefaultSort;
    public int LaunchedWithinDays { get; set; } = KickstarterQuery.DefaultLaunchedWithinDays;

    public static IReadOnlyList<KickstarterCategory> CategoriesAvailable => KickstarterCategories.All;

    public static bool Supports(SourceType type) => type == SourceType.KickstarterApi;

    public static string DefaultUrl(SourceType type) =>
        type == SourceType.KickstarterApi ? "https://www.kickstarter.com" : string.Empty;

    public static string StateLabel(string state) => state switch
    {
        "live" => "Aktywne",
        "upcoming" => "Zapowiedziane",
        "successful" => "Zakończone sukcesem",
        "failed" => "Nieudane",
        "canceled" => "Anulowane",
        _ => state
    };

    public static string SortLabel(string sort) => sort switch
    {
        "newest" => "Od najnowszych",
        "end_date" => "Według daty zakończenia",
        _ => sort
    };

    public KickstarterQuery ToQuery() => new KickstarterQuery
    {
        Keywords = KickstarterQuery.ParseList(Keywords),
        CategoryIds = KickstarterQuery.ParseCategoryIds(Categories),
        State = State,
        Sort = Sort,
        LaunchedWithinDays = LaunchedWithinDays
    }.Normalize();

    public static KickstarterQueryForm From(KickstarterQuery query) => new()
    {
        Keywords = string.Join(", ", query.Keywords),
        Categories = string.Join(", ", query.CategoryIds),
        State = query.State,
        Sort = query.Sort,
        LaunchedWithinDays = query.LaunchedWithinDays
    };
}
