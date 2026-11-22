using System.Globalization;
using System.Text;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>
/// Proste, deterministyczne cechy tekstu po polsku: słowa, prefiksy słów (częściowo
/// neutralizujące odmianę) oraz pary sąsiednich słów. Bez słowników i bez chmury.
/// </summary>
public static class TextFeatures
{
    public const int EmbeddingDimensions = 256;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "aby", "albo", "ani", "ale", "bez", "bo", "by", "być", "byli", "było", "był", "can", "czy",
        "dla", "do", "from", "i", "ich", "in", "jak", "jako", "je", "jej", "jest", "jestem", "jeśli",
        "już", "kiedy", "która", "które", "który", "lat", "lub", "ma", "mają", "make", "mamy", "mi", "na",
        "nad", "nam", "nasz", "nie", "nowa", "nowe", "nowy", "nowych", "o", "od", "oraz", "osoba", "po",
        "pod", "ponieważ", "przez", "przy", "projekt", "projekty", "się", "swoją", "system",
        "systemu", "systemy", "tak", "te", "tego", "ten", "to", "tym", "u", "w", "web", "with", "www", "za",
        "zaprojektowano", "został", "została", "zostało", "opracowano", "stworzono", "skonstruowano",
        "nową", "metoda", "metody", "technologia", "technologie", "rozwiązanie", "rozwiązania", "prototyp",
        "badania", "praca", "model", "dane", "naukowy", "naukowa", "http", "https", "com", "org", "pl",
        "project", "projects", "prototype", "prototypes", "portfolio", "repository", "repositories",
        "demo", "example", "examples", "sample", "starter", "template", "boilerplate", "readme",
        "app", "application", "website", "site", "github", "gitlab", "installation", "usage", "license",
        "build", "version", "source", "code", "library", "framework", "tool", "tools", "simple",
        "test", "tests", "using", "based", "this", "that", "the", "and", "for", "you", "your", "are", "will", "has", "have"
    };

    /// <summary>Tokeny po normalizacji: małe litery, bez znaków diakrytycznych.</summary>
    public static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var normalized = Normalize(text);
        var tokens = new List<string>();
        var current = new StringBuilder();
        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Append(character);
                continue;
            }

            AddToken(current, tokens);
            current.Clear();
        }

        AddToken(current, tokens);
        if (tokens.Count > 4000)
        {
            return tokens.Take(4000).ToList();
        }

        return tokens;
    }

    /// <summary>Tekst bez diakrytyków, wykorzystywany do podpisów grup.</summary>
    public static string Normalize(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : ' ');
        }

        return builder.ToString();
    }

    /// <summary>Wagi cech reckonych z tekstu; klucz zawiera prefiks rodzaju cechy.</summary>
    public static IReadOnlyDictionary<string, double> Features(string? text)
    {
        var tokens = Tokenize(text);
        var features = new Dictionary<string, double>(StringComparer.Ordinal);
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            Add(features, "w:" + token, 1.0);
            if (token.Length >= 5)
            {
                Add(features, "p:" + token[..5], 0.5);
            }

            if (i > 0 && tokens[i - 1].Length >= 3 && token.Length >= 3)
            {
                Add(features, "b:" + tokens[i - 1] + "_" + token, 0.6);
            }
        }

        return features;
    }

    private static void AddToken(StringBuilder buffer, List<string> tokens)
    {
        if (buffer.Length < 3)
        {
            return;
        }

        var token = buffer.ToString();
        if (!StopWords.Contains(token) && token.Length <= 30)
        {
            tokens.Add(token);
        }
    }

    private static void Add(IDictionary<string, double> features, string key, double weight)
    {
        features.TryGetValue(key, out var current);
        features[key] = current + weight;
    }
}
