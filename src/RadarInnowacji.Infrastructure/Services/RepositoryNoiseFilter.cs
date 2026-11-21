using System.Text.RegularExpressions;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>Powód odrzucenia: krótki kod (do statystyk skanu) i opis dla użytkownika.</summary>
public sealed record NoiseVerdict(string Code, string Label);

/// <summary>
/// Reguły odsiewania repozytoriów, które prawie nigdy nie są innowacją. Reguły są celowo
/// zachowawcze: oceniają nazwę repozytorium i jawne flagi, a nie temat, więc np.
/// „machine-learning” czy projekt z zajęć uczelnianych nie są odrzucane.
/// </summary>
public static partial class RepositoryNoiseFilter
{
    private static readonly HashSet<string> CourseworkSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "homework", "homeworks", "assignment", "assignments", "coursework", "exercise", "exercises",
        "excercise", "excercises", "leetcode", "hackerrank", "codewars", "kata", "katas", "tutorial",
        "tutorials", "bootcamp", "practice", "practise", "100daysofcode", "cs50", "freecodecamp",
        "cwiczenia", "zadanie", "zadania", "zaliczenie", "kolokwium"
    };

    private static readonly HashSet<string> DotfilesNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotfiles", "dot-files", "vimrc", "zshrc", "bashrc", "nvim-config", "neovim-config", "config", "configs"
    };

    private static readonly HashSet<string> PersonalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "portfolio", "my-portfolio", "personal-website", "personal-site", "resume", "cv", "my-website",
        "hello-world", "helloworld", "hello", "test", "test-repo", "testing", "first-repo", "my-first-repo",
        "demo-repo", "sandbox", "playground", "learning", "notes", "my-notes"
    };

    private static readonly string[] CourseworkPhrases =
    [
        "homework", "assignment for", "course assignment", "class assignment", "my dotfiles", "my personal portfolio",
        "my portfolio website", "praca domowa", "zadanie domowe", "zadania z laboratorium", "na zaliczenie"
    ];

    /// <summary>Szablony README generowane przez narzędzia i platformy, zostawiane bez zmian.</summary>
    private static readonly (string Marker, string Name)[] ReadmeTemplates =
    [
        ("To make it easy for you to get started with GitLab", "szablon GitLab"),
        ("This project was bootstrapped with [Create React App]", "szablon Create React App"),
        ("This template provides a minimal setup to get React working in Vite", "szablon Vite + React"),
        ("This template should help get you started developing with Vue", "szablon Vite + Vue"),
        ("project bootstrapped with [`create-next-app`]", "szablon Next.js"),
        ("This project was generated with [Angular CLI]", "szablon Angular CLI"),
        ("A new Flutter project.", "szablon Flutter"),
        ("Laravel is a web application framework with expressive, elegant syntax", "szablon Laravel"),
        ("Everything you need to build a Svelte project, powered by", "szablon SvelteKit")
    ];

    /// <summary>Ocena na podstawie listy wyników (przed zapisem): flagi platformy i nazwa repozytorium.</summary>
    public static NoiseVerdict? ClassifyListing(SourceItem item)
    {
        var repo = item.Repository;
        if (repo is null)
        {
            return null;
        }

        if (repo.IsFork) return new("fork", "fork innego repozytorium");
        if (repo.IsMirror) return new("mirror", "kopia lustrzana");
        if (repo.IsTemplate) return new("template", "repozytorium-szablon");
        if (repo.IsArchived) return new("archived", "repozytorium zarchiwizowane");
        // GitLab zmienia nazwę projektu oznaczonego do usunięcia na „…-deletion_scheduled-<ID>”.
        if (repo.FullName.Contains("deletion_scheduled", StringComparison.OrdinalIgnoreCase))
        {
            return new("deleted", "projekt zaplanowany do usunięcia");
        }

        var name = RepositoryName(repo.FullName);
        var owner = Owner(repo.FullName);
        var segments = Segments(name);

        if (segments.Length > 0 && segments[0].Equals("awesome", StringComparison.OrdinalIgnoreCase))
        {
            return new("awesome", "lista linków (awesome)");
        }

        if (DotfilesNames.Contains(name) || name.StartsWith('.'))
        {
            return new("dotfiles", "pliki konfiguracyjne (dotfiles)");
        }

        if (PersonalNames.Contains(name) ||
            (owner is not null && (name.Equals($"{owner}.github.io", StringComparison.OrdinalIgnoreCase) ||
                                   name.Equals($"{owner}.gitlab.io", StringComparison.OrdinalIgnoreCase) ||
                                   name.Equals(owner, StringComparison.OrdinalIgnoreCase))))
        {
            return new("personal", "strona osobista lub repozytorium testowe");
        }

        if (segments.Any(CourseworkSegments.Contains) || LabNumber().IsMatch(name) ||
            CourseworkPhrases.Any(phrase => item.Description.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
        {
            return new("coursework", "zadanie, ćwiczenie lub materiał szkoleniowy");
        }

        return null;
    }

    /// <summary>Ocena po pobraniu README: brak treści, szczątkowe README lub niezmieniony szablon.</summary>
    public static NoiseVerdict? ClassifyReadme(string? description, string? readme)
    {
        var hasDescription = (description?.Trim().Length ?? 0) >= 30;
        if (string.IsNullOrWhiteSpace(readme))
        {
            return string.IsNullOrWhiteSpace(description) ? new("no-readme", "brak README i opisu") : null;
        }

        if (!hasDescription)
        {
            foreach (var (marker, templateName) in ReadmeTemplates)
            {
                if (readme.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    return new("readme-template", $"niezmieniony {templateName} README");
                }
            }

            if (MeaningfulText(readme).Length < 120)
            {
                return new("readme-trivial", "szczątkowe README bez opisu projektu");
            }
        }

        return null;
    }

    /// <summary>Treść README bez kodu, obrazków, adresów, znaczników markdown i HTML.</summary>
    public static string MeaningfulText(string readme)
    {
        var text = CodeFence().Replace(readme, " ");
        text = Image().Replace(text, " ");
        text = LinkTarget().Replace(text, "$1");
        text = Url().Replace(text, " ");
        text = HtmlTag().Replace(text, " ");
        text = MarkdownSyntax().Replace(text, " ");
        return Whitespace().Replace(text, " ").Trim();
    }

    private static string RepositoryName(string fullName)
    {
        var slash = fullName.LastIndexOf('/');
        return (slash >= 0 ? fullName[(slash + 1)..] : fullName).Trim();
    }

    private static string? Owner(string fullName)
    {
        var slash = fullName.IndexOf('/');
        return slash > 0 ? fullName[..slash] : null;
    }

    private static string[] Segments(string name) =>
        SegmentSeparator().Split(CamelCase().Replace(name, "$1-$2"))
            .Where(x => x.Length > 0)
            .ToArray();

    [GeneratedRegex(@"[-_.\s]+")]
    private static partial Regex SegmentSeparator();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex CamelCase();

    // lab1, lab-02, lab_3, laboratorium5, hw3, lista-4
    [GeneratedRegex(@"(^|[-_.])(lab|laboratorium|lista|hw)[-_]?\d{1,2}($|[-_.])", RegexOptions.IgnoreCase)]
    private static partial Regex LabNumber();

    [GeneratedRegex("```[\\s\\S]*?```")]
    private static partial Regex CodeFence();

    [GeneratedRegex(@"!\[[^\]]*\]\([^)]*\)")]
    private static partial Regex Image();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex LinkTarget();

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex Url();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"[#>*_`|~\-=]+")]
    private static partial Regex MarkdownSyntax();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
