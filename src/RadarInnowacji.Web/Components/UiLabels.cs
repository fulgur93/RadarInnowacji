using System.Globalization;
using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Web.Components;

public static class UiLabels
{
    private static readonly CultureInfo Polski = new("pl-PL");

    public static string Radar(RadarKind radar) => radar switch
    {
        RadarKind.Uczelnia => "Radar uczelni",
        RadarKind.Repozytorium => "Radar repozytoriów",
        RadarKind.Wiadomosci => "Radar wiadomości",
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

    public static string Data(DateTime? value) => value is null
        ? "brak danych"
        : value.Value.ToLocalTime().ToString("dd.MM.yyyy", Polski);

    public static string DataCzas(DateTime? value) => value is null
        ? "brak danych"
        : value.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm", Polski);
}
