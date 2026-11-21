using System.Text.RegularExpressions;

namespace RadarInnowacji.Infrastructure.Adapters;

/// <summary>
/// Szuka linku do działającej demonstracji projektu: pole homepage albo link z README
/// prowadzący do typowego hostingu aplikacji lub opisany jako demo.
/// </summary>
public static partial class DemoLinkDetector
{
    // Strony projektów (GitHub/GitLab Pages) są demem tylko, gdy należą do właściciela repozytorium;
    // inaczej to zwykle dokumentacja cudzej biblioteki.
    private static readonly string[] PagesHostSuffixes = [".github.io", ".gitlab.io"];

    private static readonly string[] DemoHostSuffixes =
    [
        ".vercel.app", ".netlify.app", ".streamlit.app", ".pages.dev",
        ".herokuapp.com", ".onrender.com", ".fly.dev", ".web.app", ".firebaseapp.com", ".replit.app",
        ".gradio.live", ".hf.space"
    ];

    // Hosty, które nie są demonstracją (odznaki, obrazki, rejestry pakietów, samo repozytorium).
    private static readonly string[] IgnoredHosts =
    [
        "github.com", "gitlab.com", "img.shields.io", "shields.io", "badge.fury.io", "travis-ci.org",
        "travis-ci.com", "codecov.io", "pypi.org", "www.npmjs.com", "npmjs.com", "crates.io", "nuget.org",
        "www.nuget.org", "opensource.org", "choosealicense.com", "creativecommons.org", "raw.githubusercontent.com",
        // Publikacje i archiwa danych: wartościowy sygnał badawczy, ale nie demonstracja.
        "doi.org", "dx.doi.org", "zenodo.org", "arxiv.org", "www.researchgate.net", "scholar.google.com",
        "orcid.org", "figshare.com", "osf.io"
    ];

    /// <param name="owner">Właściciel repozytorium (pierwszy człon pełnej nazwy), do rozpoznania jego stron Pages.</param>
    public static string? Find(string? homepage, string? readme, string? owner = null)
    {
        if (IsUsableHomepage(homepage))
        {
            return homepage!.Trim();
        }

        if (string.IsNullOrWhiteSpace(readme))
        {
            return null;
        }

        string? labelled = null;
        foreach (Match match in MarkdownLink().Matches(readme))
        {
            var label = match.Groups["label"].Value;
            var url = match.Groups["url"].Value;
            if (!TryParse(url, out var uri) || IsIgnored(uri) || IsImage(match.Value))
            {
                continue;
            }

            if (IsDemoHost(uri, owner))
            {
                return uri.ToString();
            }

            if (labelled is null && DemoLabel().IsMatch(label))
            {
                labelled = uri.ToString();
            }
        }

        if (labelled is not null)
        {
            return labelled;
        }

        foreach (Match match in BareUrl().Matches(readme))
        {
            if (TryParse(match.Value.TrimEnd('.', ',', ')', ';'), out var uri) && !IsIgnored(uri) && IsDemoHost(uri, owner))
            {
                return uri.ToString();
            }
        }

        return null;
    }

    private static bool IsUsableHomepage(string? homepage) =>
        TryParse(homepage?.Trim(), out var uri) && !IsIgnored(uri);

    private static bool TryParse(string? value, out Uri uri)
    {
        uri = null!;
        return !string.IsNullOrWhiteSpace(value) &&
               Uri.TryCreate(value, UriKind.Absolute, out uri!) &&
               uri.Scheme is "http" or "https";
    }

    private static bool IsIgnored(Uri uri) =>
        IgnoredHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase) ||
        uri.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
        uri.AbsolutePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

    private static bool IsDemoHost(Uri uri, string? owner) =>
        (owner is not null && PagesHostSuffixes.Any(suffix =>
            uri.Host.Equals(owner + suffix, StringComparison.OrdinalIgnoreCase))) ||
        DemoHostSuffixes.Any(suffix => uri.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) ||
        (uri.Host.Equals("huggingface.co", StringComparison.OrdinalIgnoreCase) &&
         uri.AbsolutePath.StartsWith("/spaces/", StringComparison.OrdinalIgnoreCase)) ||
        uri.Host.Equals("colab.research.google.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsImage(string markdown) => markdown.StartsWith('!');

    [GeneratedRegex(@"!?\[(?<label>[^\]]{0,200})\]\((?<url>https?://[^)\s]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLink();

    [GeneratedRegex(@"\b(demo|live|try it|wypróbuj|zobacz online|playground|showcase)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DemoLabel();

    [GeneratedRegex(@"https?://[^\s<>""'\]]+", RegexOptions.CultureInvariant)]
    private static partial Regex BareUrl();
}
