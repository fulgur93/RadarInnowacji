using System.Text;

namespace RadarInnowacji.Core.Models;

/// <summary>Ustrukturyzowany wynik analizy materiału zwrócony przez model AI.</summary>
public sealed record AiAnalysisResult(
    bool IsPotentialInnovation,
    int Confidence,
    string InnovationType,
    string Problem,
    string Solution,
    string NoveltyDescription,
    IReadOnlyList<string> Technologies,
    string Reason,
    IReadOnlyList<string> SuggestedCategories,
    string MissingInformation)
{
    /// <summary>Wersja promptu zapisywana w historii analiz.</summary>
    public const string PromptVersion = "v1";

    /// <summary>Czytelny opis wyniku zapisywany przy odkryciu (bez przekazywania przypuszczeń jako faktów).</summary>
    public string ToDisplayText()
    {
        var text = new StringBuilder();
        Append(text, "Typ innowacji", InnovationType);
        Append(text, "Problem", Problem);
        Append(text, "Rozwiązanie", Solution);
        Append(text, "Co może być nowe", NoveltyDescription);
        Append(text, "Uzasadnienie", Reason);
        Append(text, "Technologie", Technologies);
        Append(text, "Proponowane kategorie", SuggestedCategories);
        Append(text, "Brakujące informacje", MissingInformation);
        return text.Length == 0 ? "Model nie zwrócił żadnych informacji." : text.ToString();
    }

    private static void Append(StringBuilder builder, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.Append(label).Append(": ").AppendLine(value.Trim());
        }
    }

    private static void Append(StringBuilder builder, string label, IReadOnlyList<string>? values)
    {
        if (values is { Count: > 0 })
        {
            Append(builder, label, string.Join(", ", values));
        }
    }
}
