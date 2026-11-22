using Microsoft.EntityFrameworkCore;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>
/// Katalog odbiorców. Wpisy startowe opisują ogólne TYPY odbiorców (oznaczone jako przykład) –
/// nie wskazują konkretnych firm ani funduszy. Instytucja korzystająca z radaru zastępuje je
/// lub uzupełnia własnymi partnerami.
/// </summary>
public sealed class RecipientService(RadarDbContext db) : IRecipientService
{
    public static IReadOnlyList<Recipient> Examples { get; } =
    [
        Example("Szkoły ponadpodstawowe w regionie", RecipientType.Szkoly,
            "Szkoły, które mogą przejąć sprawdzone rozwiązania edukacyjne i organizacyjne innych szkół.",
            "edukacja", "szkoła, uczniowie, lekcje, nauczyciele",
            [DevelopmentPath.TransferDoInstytucji, DevelopmentPath.WdrozenieWSektorzePublicznym], ProjectStatus.Prototype),
        Example("Samorządy – wydziały edukacji", RecipientType.Samorzady,
            "Organy prowadzące szkoły: finansowanie i upowszechnianie rozwiązań w podległych jednostkach.",
            "edukacja, administracja publiczna", "szkoła, edukacja, uczniowie",
            [DevelopmentPath.TransferDoInstytucji, DevelopmentPath.WdrozenieWSektorzePublicznym], ProjectStatus.Prototype),
        Example("Samorządy – usługi dla mieszkańców i środowisko", RecipientType.Samorzady,
            "Urzędy gmin i powiatów wdrażające rozwiązania dla mieszkańców, jakości powietrza i środowiska.",
            "administracja publiczna, środowisko, jakość życia, energia", "mieszkańcy, powietrze, smog, energia, odpady",
            [DevelopmentPath.WdrozenieWSektorzePublicznym, DevelopmentPath.TransferDoInstytucji], ProjectStatus.Prototype),
        Example("Placówki ochrony zdrowia i opieki", RecipientType.InstytucjePubliczne,
            "Szpitale, przychodnie i ośrodki opieki – odbiorcy rozwiązań diagnostycznych, rehabilitacyjnych i dla seniorów.",
            "zdrowie, jakość życia", "pacjent, rehabilitacja, diagnostyka, senior",
            [DevelopmentPath.WdrozenieWSektorzePublicznym, DevelopmentPath.Komercjalizacja], ProjectStatus.Prototype),
        Example("Centra transferu technologii uczelni", RecipientType.Uczelnie,
            "Ocena zdolności patentowej, ochrona własności intelektualnej i licencjonowanie wyników badań.",
            "ICT, przemysł, zdrowie, energia, motoryzacja, lotnictwo, kosmos", "patent, prototyp, badania",
            [DevelopmentPath.OchronaIpIAkceleracja, DevelopmentPath.Komercjalizacja, DevelopmentPath.DalszyRozwoj],
            ProjectStatus.Concept),
        Example("Rzecznicy patentowi i ochrona własności intelektualnej", RecipientType.ProgramyWsparcia,
            "Badanie stanu techniki i wybór formy ochrony przed publikacją lub komercjalizacją.",
            "ICT, przemysł, motoryzacja, lotnictwo, kosmos, energia, zdrowie", "wynalazek, urządzenie, metoda",
            [DevelopmentPath.OchronaIpIAkceleracja], ProjectStatus.Concept),
        Example("Programy akceleracyjne dla startupów technologicznych", RecipientType.Akceleratory,
            "Mentoring, walidacja rynku i pierwsze finansowanie dla zespołów z prototypem.",
            "ICT, motoryzacja, lotnictwo, kosmos, jakość życia, energia", "startup, prototyp, aplikacja, urządzenie",
            [DevelopmentPath.OchronaIpIAkceleracja, DevelopmentPath.Komercjalizacja, DevelopmentPath.DalszyRozwoj],
            ProjectStatus.InProgress),
        Example("Laboratoria i pracownie prototypowania dla młodych innowatorów", RecipientType.ProgramyWsparcia,
            "Dostęp do sprzętu i opieki mentora dla młodych twórców (16–30 lat) na etapie pomysłu i prototypu.",
            "ICT, przemysł, energia, edukacja", "prototyp, druk 3D, elektronika, czujniki, uczniowie, studenci",
            [DevelopmentPath.DalszyRozwoj, DevelopmentPath.OchronaIpIAkceleracja, DevelopmentPath.UzupelnienieInformacji],
            ProjectStatus.Concept),
        Example("Firmy sektora lotniczego i kosmicznego", RecipientType.Firmy,
            "Odbiorcy technologii lotniczych, dronowych i satelitarnych – partnerzy pilotaży i licencji.",
            "lotnictwo, kosmos, przemysł", "dron, satelita, lotnictwo, aero",
            [DevelopmentPath.Komercjalizacja, DevelopmentPath.OchronaIpIAkceleracja], ProjectStatus.Prototype),
        Example("Firmy sektora motoryzacyjnego", RecipientType.Firmy,
            "Producenci i dostawcy branży motoryzacyjnej, elektromobilność i systemy pojazdów.",
            "motoryzacja, przemysł, energia", "pojazd, samochód, elektromobilność, akumulator",
            [DevelopmentPath.Komercjalizacja, DevelopmentPath.OchronaIpIAkceleracja], ProjectStatus.Prototype),
        Example("Firmy ICT i software house'y", RecipientType.Firmy,
            "Partnerzy rozwoju i wdrożenia oprogramowania, systemów danych i rozwiązań AI.",
            "ICT", "aplikacja, oprogramowanie, dane, sztuczna inteligencja, platforma",
            [DevelopmentPath.Komercjalizacja, DevelopmentPath.TransferDoInstytucji], ProjectStatus.Prototype),
        Example("Inwestorzy na wczesnym etapie (fundusze zalążkowe, aniołowie biznesu)", RecipientType.Inwestorzy,
            "Finansowanie zespołów z prototypem i pierwszymi użytkownikami.",
            "ICT, motoryzacja, lotnictwo, kosmos, zdrowie, energia", "startup, skalowanie, klienci",
            [DevelopmentPath.Komercjalizacja], ProjectStatus.Prototype),
        Example("Organizacje pozarządowe działające na rzecz jakości życia", RecipientType.OrganizacjePozarzadowe,
            "Partnerzy upowszechniania innowacji społecznych i edukacyjnych w społecznościach lokalnych.",
            "jakość życia, edukacja, środowisko", "społeczność, wolontariat, seniorzy, dzieci, integracja",
            [DevelopmentPath.TransferDoInstytucji, DevelopmentPath.DalszyRozwoj], ProjectStatus.Concept)
    ];

    public async Task<IReadOnlyList<Recipient>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.Recipients
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Type)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<OperationResult> SaveAsync(RecipientForm form, CancellationToken cancellationToken = default)
    {
        var name = form.Name?.Trim() ?? string.Empty;
        if (name.Length is < 3 or > ContentLimits.MaxTitleLength)
        {
            return OperationResult.Fail("Nazwa odbiorcy musi mieć od 3 do 500 znaków.");
        }

        if (!string.IsNullOrWhiteSpace(form.Website) &&
            (!Uri.TryCreate(form.Website.Trim(), UriKind.Absolute, out var uri) ||
             (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            return OperationResult.Fail("Strona www musi być pełnym adresem http lub https.");
        }

        if (await db.Recipients.AnyAsync(x => x.Name == name && x.Id != form.Id, cancellationToken))
        {
            return OperationResult.Fail("Odbiorca o tej nazwie już istnieje.");
        }

        Recipient recipient;
        if (form.Id is null)
        {
            recipient = new Recipient { CreatedAt = DateTime.UtcNow };
            db.Recipients.Add(recipient);
        }
        else
        {
            recipient = await db.Recipients.FirstOrDefaultAsync(x => x.Id == form.Id, cancellationToken)
                        ?? throw new InvalidOperationException("Nie znaleziono odbiorcy.");

            // Edytowany przykład staje się wpisem użytkownika.
            recipient.IsExample = false;
        }

        recipient.Name = name;
        recipient.Type = form.Type;
        recipient.Description = Limit(form.Description, ContentLimits.MaxErrorLength);
        recipient.Sectors = Limit(form.Sectors, 500);
        recipient.Keywords = Limit(form.Keywords, 1000);
        recipient.Paths = string.Join(", ", form.Paths.Distinct());
        recipient.MinimumStage = form.MinimumStage;
        recipient.Region = string.IsNullOrWhiteSpace(form.Region) ? null : Limit(form.Region, 100);
        recipient.Website = string.IsNullOrWhiteSpace(form.Website) ? null : form.Website.Trim();
        recipient.IsActive = form.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok($"Zapisano odbiorcę „{name}”.");
    }

    public async Task<OperationResult> SetActiveAsync(int recipientId, bool isActive, CancellationToken cancellationToken = default)
    {
        var recipient = await db.Recipients.FirstOrDefaultAsync(x => x.Id == recipientId, cancellationToken);
        if (recipient is null)
        {
            return OperationResult.Fail("Nie znaleziono odbiorcy.");
        }

        recipient.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok(isActive ? "Odbiorca aktywny." : "Odbiorca wyłączony z dopasowań.");
    }

    public async Task<int> SeedExamplesAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Recipients.AnyAsync(cancellationToken))
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        foreach (var example in Examples)
        {
            db.Recipients.Add(new Recipient
            {
                Name = example.Name,
                Type = example.Type,
                Description = example.Description,
                Sectors = example.Sectors,
                Keywords = example.Keywords,
                Paths = example.Paths,
                MinimumStage = example.MinimumStage,
                IsExample = true,
                IsActive = true,
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return Examples.Count;
    }

    private static Recipient Example(
        string name,
        RecipientType type,
        string description,
        string sectors,
        string keywords,
        DevelopmentPath[] paths,
        ProjectStatus minimumStage) => new()
    {
        Name = name,
        Type = type,
        Description = description,
        Sectors = sectors,
        Keywords = keywords,
        Paths = string.Join(", ", paths),
        MinimumStage = minimumStage,
        IsExample = true
    };

    private static string Limit(string? value, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
