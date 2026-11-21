namespace RadarInnowacji.Core.Models;

/// <summary>
/// Limity zabezpieczające przed niekontrolowanym pobieraniem i przechowywaniem treści.
/// Pełne dokumenty mogą w przyszłości trafić do zewnętrznego magazynu;
/// w bazie przechowywany jest fragment o ograniczonej długości oraz metadane oryginału.
/// </summary>
public static class ContentLimits
{
    public const int MaxRawContentLength = 20000;
    public const int MaxDescriptionLength = 2000;
    public const int MaxTitleLength = 500;
    public const int MaxSourceUrlLength = 2048;
    public const int MaxErrorLength = 2000;
}
