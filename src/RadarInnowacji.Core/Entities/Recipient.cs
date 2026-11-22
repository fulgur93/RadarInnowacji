using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Entities;

/// <summary>
/// Pozycja katalogu odbiorców, partnerów lub inwestorów. Wpisy startowe są ogólnymi typami
/// odbiorców oznaczonymi jako przykład (<see cref="IsExample"/>) – nie wskazują konkretnych organizacji.
/// </summary>
public sealed class Recipient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public RecipientType Type { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>Sektory (rozdzielone przecinkami), np. „ICT, edukacja”.</summary>
    public string Sectors { get; set; } = string.Empty;

    /// <summary>Słowa kluczowe zainteresowań (rozdzielone przecinkami).</summary>
    public string Keywords { get; set; } = string.Empty;

    /// <summary>Obsługiwane ścieżki (nazwy <see cref="DevelopmentPath"/> rozdzielone przecinkami).</summary>
    public string Paths { get; set; } = string.Empty;

    /// <summary>Najwcześniejszy etap projektu, którym odbiorca się zajmuje.</summary>
    public ProjectStatus MinimumStage { get; set; } = ProjectStatus.Unknown;
    public string? Region { get; set; }
    public string? Website { get; set; }
    public bool IsExample { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
