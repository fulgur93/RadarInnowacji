namespace RadarInnowacji.Web.Services;

/// <summary>
/// Rzeczywisty adres, pod którym nasłuchuje aplikacja (port może różnić się od domyślnego,
/// jeśli był zajęty). Pusty w testach, gdy Kestrel wybiera port dynamicznie.
/// </summary>
public sealed record RadarRuntimeInfo(string Url);
