using System.Globalization;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Infrastructure.Assessment;

namespace RadarInnowacji.Web.Components;

public static class UiLabels
{
    private static readonly CultureInfo Polski = new("pl-PL");

    public static string Radar(RadarKind radar) => radar switch
    {
        RadarKind.Uczelnia => "Radar uczelni",
        RadarKind.Repozytorium => "Radar repozytoriów",
        RadarKind.Wiadomosci => "Radar wiadomości",
        RadarKind.Crowdfunding => "Radar portali crowdfundingowych",
        RadarKind.Zgloszenia => "Zgłoszenia instytucji",
        _ => radar.ToString()
    };

    public static string Status(DiscoveryStatus status) => status switch
    {
        DiscoveryStatus.Candidate => "Kandydat",
        DiscoveryStatus.Interesting => "Interesujące",
        DiscoveryStatus.Verified => "Zweryfikowane",
        _ => status.ToString()
    };

    public static string Processing(ProcessingStatus status) => status switch
    {
        ProcessingStatus.Pending => "Oczekuje na AI",
        ProcessingStatus.RuleFiltered => "Po filtrze regułowym",
        ProcessingStatus.AiQueued => "W kolejce AI",
        ProcessingStatus.AiProcessed => "Przeanalizowane",
        ProcessingStatus.AiFailed => "Błąd AI",
        ProcessingStatus.Skipped => "Pominięte",
        _ => status.ToString()
    };

    public static string Project(ProjectStatus status) => status switch
    {
        ProjectStatus.Unknown => "Nieznany",
        ProjectStatus.Concept => "Koncepcja",
        ProjectStatus.InProgress => "W trakcie",
        ProjectStatus.Prototype => "Prototyp",
        ProjectStatus.Deployed => "Wdrożony",
        _ => status.ToString()
    };

    public static string Scan(ScanStatus status) => status switch
    {
        ScanStatus.Started => "W toku",
        ScanStatus.Succeeded => "Zakończony",
        ScanStatus.Failed => "Nieudany",
        ScanStatus.Partial => "Częściowy",
        _ => status.ToString()
    };

    public static string Zrodlo(SourceType type) => type switch
    {
        SourceType.OaiPmh => "OAI-PMH (uczelnia)",
        SourceType.Dspace => "DSpace API (uczelnia)",
        SourceType.Rss => "RSS",
        SourceType.Atom => "Atom",
        SourceType.GitHubApi => "GitHub API",
        SourceType.GitLabApi => "GitLab API",
        SourceType.HuggingFaceApi => "Hugging Face API",
        SourceType.KickstarterApi => "Kickstarter (wyszukiwanie)",
        SourceType.HtmlPage => "Strona HTML",
        SourceType.Manual => "Wpis ręczny",
        SourceType.Demo => "Dane demonstracyjne",
        _ => type.ToString()
    };

    public static string Instytucja(InstitutionType type) => type switch
    {
        InstitutionType.Nieznany => "Nieznany",
        InstitutionType.Szkola => "Szkoła",
        InstitutionType.Uczelnia => "Uczelnia",
        InstitutionType.Samorzad => "Samorząd (JST)",
        InstitutionType.InstytucjaPubliczna => "Inna instytucja publiczna",
        InstitutionType.OrganizacjaPozarzadowa => "Organizacja pozarządowa",
        InstitutionType.Firma => "Firma",
        InstitutionType.Inna => "Inna",
        _ => type.ToString()
    };

    public static string Rodzaj(InnovationKind kind) => kind switch
    {
        InnovationKind.Nieokreslona => "Nieokreślona",
        InnovationKind.Technologiczna => "Technologiczna",
        InnovationKind.Organizacyjna => "Organizacyjna",
        InnovationKind.Edukacyjna => "Edukacyjna",
        InnovationKind.Spoleczna => "Społeczna",
        InnovationKind.Procesowa => "Procesowa",
        _ => kind.ToString()
    };

    /// <summary>Etap rozwoju w języku zgłoszeń (Etap 9).</summary>
    public static string EtapRozwoju(ProjectStatus status) => status switch
    {
        ProjectStatus.Unknown => "Nieznany",
        ProjectStatus.Concept => "Pomysł",
        ProjectStatus.InProgress => "W realizacji",
        ProjectStatus.Prototype => "Prototyp",
        ProjectStatus.Deployed => "Wdrożone w macierzystej instytucji",
        _ => status.ToString()
    };

    public static string EtapProcesu(PipelineStage stage) => PipelineRules.Name(stage);

    public static string Sciezka(DevelopmentPath path) => DevelopmentPathCatalog.Get(path).Name;

    public static string Odbiorca(RecipientType type) => type switch
    {
        RecipientType.Szkoly => "Szkoły",
        RecipientType.Uczelnie => "Uczelnie",
        RecipientType.Samorzady => "Samorządy",
        RecipientType.InstytucjePubliczne => "Instytucje publiczne",
        RecipientType.Firmy => "Firmy",
        RecipientType.Inwestorzy => "Inwestorzy",
        RecipientType.ProgramyWsparcia => "Programy wsparcia",
        RecipientType.Akceleratory => "Akceleratory",
        RecipientType.OrganizacjePozarzadowe => "Organizacje pozarządowe",
        _ => type.ToString()
    };

    /// <summary>Klasa koloru dla wyniku oceny 0–100.</summary>
    public static string WynikKlasa(double? score) => score switch
    {
        null => "bg-secondary",
        >= 65 => "bg-success",
        >= 45 => "bg-primary",
        >= 30 => "bg-warning text-dark",
        _ => "bg-secondary"
    };

    public static string Data(DateTime? value) => value is null
        ? "brak danych"
        : value.Value.ToLocalTime().ToString("dd.MM.yyyy", Polski);

    public static string DataCzas(DateTime? value) => value is null
        ? "brak danych"
        : value.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm", Polski);
}
