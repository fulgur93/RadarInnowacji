using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Assessment;

/// <summary>Opis ścieżek: następne kroki, mierniki skuteczności i typowi odbiorcy.</summary>
public static class DevelopmentPathCatalog
{
    public static IReadOnlyList<DevelopmentPathInfo> Paths { get; } =
    [
        new(DevelopmentPath.UzupelnienieInformacji,
            "Uzupełnienie informacji",
            "Za mało danych, by ocena była wiarygodna. Najpierw trzeba poznać problem, etap i twórców.",
            [
                "Skontaktuj się z instytucją lub twórcą i uzupełnij profil projektu",
                "Opisz problem, rozwiązanie i etap rozwoju",
                "Dołącz link do materiałów (strona, repozytorium, publikacja)"
            ],
            [
                "Kompletność informacji ≥ 70% w ciągu 30 dni",
                "Ponowna ocena z pewnością ≥ 60%"
            ],
            [RecipientType.ProgramyWsparcia]),
        new(DevelopmentPath.TransferDoInstytucji,
            "Transfer do innych instytucji",
            "Rozwiązanie sprawdzone u siebie, które mogą przejąć inne szkoły, uczelnie lub urzędy o podobnym problemie.",
            [
                "Przygotuj opis wdrożenia: koszty, czas, potrzebne zasoby, efekty",
                "Nawiąż kontakt z instytucjami, które mają podobny problem (lista „Instytucje z podobnym problemem”)",
                "Uzgodnij pilotaż w 2–3 instytucjach z mentorem po stronie twórcy"
            ],
            [
                "Liczba instytucji pilotażowych w ciągu 6 miesięcy ≥ 2",
                "Odsetek pilotaży zakończonych decyzją o kontynuacji ≥ 50%",
                "Zmierzony efekt u odbiorcy (np. oszczędzony czas, liczba uczestników) zgodny z opisem wdrożenia"
            ],
            [RecipientType.Szkoly, RecipientType.Uczelnie, RecipientType.Samorzady, RecipientType.InstytucjePubliczne,
                RecipientType.OrganizacjePozarzadowe]),
        new(DevelopmentPath.WdrozenieWSektorzePublicznym,
            "Wdrożenie w sektorze publicznym",
            "Rozwiązanie działające w instytucji publicznej, które warto upowszechnić przez samorządy i ich jednostki.",
            [
                "Przedstaw rozwiązanie samorządowi prowadzącemu instytucję",
                "Sprawdź możliwość finansowania z budżetu jednostki lub programu regionalnego",
                "Przygotuj standard wdrożenia (procedura, szkolenie, materiały)"
            ],
            [
                "Decyzja o wdrożeniu w co najmniej 1 kolejnej jednostce w ciągu 12 miesięcy",
                "Liczba jednostek korzystających z rozwiązania po roku",
                "Liczba odbiorców końcowych (mieszkańców, uczniów, pacjentów)"
            ],
            [RecipientType.Samorzady, RecipientType.InstytucjePubliczne, RecipientType.Szkoly]),
        new(DevelopmentPath.OchronaIpIAkceleracja,
            "Ochrona własności intelektualnej i akceleracja",
            "Nowe rozwiązanie technologiczne na wczesnym etapie: najpierw zabezpieczenie praw, potem rozwój z akceleratorem.",
            [
                "Przed publikacją szczegółów sprawdź stan techniki w bazach patentowych (UPRP, EPO)",
                "Skonsultuj ochronę z centrum transferu technologii lub rzecznikiem patentowym",
                "Zgłoś projekt do programu akceleracyjnego lub laboratorium prototypowania"
            ],
            [
                "Raport ze stanu techniki w ciągu 2 miesięcy",
                "Decyzja o formie ochrony (zgłoszenie patentowe, wzór użytkowy, know-how) w ciągu 3 miesięcy",
                "Przyjęcie do programu akceleracyjnego lub działający prototyp w ciągu 6 miesięcy"
            ],
            [RecipientType.Akceleratory, RecipientType.ProgramyWsparcia, RecipientType.Uczelnie]),
        new(DevelopmentPath.Komercjalizacja,
            "Komercjalizacja",
            "Dojrzałe rozwiązanie z potwierdzonym zapotrzebowaniem: licencja, spółka lub sprzedaż usługi.",
            [
                "Określ model komercjalizacji: licencja, spółka spin-off/startup, usługa",
                "Przygotuj krótką ofertę wartości dla firm z dopasowanych sektorów",
                "Zorganizuj spotkania z firmami lub inwestorami z katalogu odbiorców"
            ],
            [
                "Co najmniej 1 list intencyjny lub umowa pilotażowa w ciągu 3 miesięcy",
                "Podpisana umowa licencyjna lub utworzona spółka w ciągu 12 miesięcy",
                "Pierwsi płacący klienci lub pozyskane finansowanie zewnętrzne"
            ],
            [RecipientType.Firmy, RecipientType.Inwestorzy, RecipientType.Akceleratory]),
        new(DevelopmentPath.DalszyRozwoj,
            "Dalszy rozwój",
            "Obiecujący pomysł, który potrzebuje czasu, zasobów lub opieki mentora, zanim będzie gotowy do transferu.",
            [
                "Zaproponuj twórcom mentoring i dostęp do laboratorium lub pracowni",
                "Ustal cel najbliższego etapu (prototyp, test z użytkownikami)",
                "Wróć do oceny po osiągnięciu celu"
            ],
            [
                "Osiągnięty kolejny etap dojrzałości w ciągu 6 miesięcy",
                "Test z co najmniej 5 użytkownikami docelowymi",
                "Ponowna ocena z wynikiem wyższym o ≥ 10 punktów"
            ],
            [RecipientType.ProgramyWsparcia, RecipientType.Uczelnie, RecipientType.Akceleratory]),
        new(DevelopmentPath.Obserwacja,
            "Obserwacja",
            "Przy obecnych informacjach potencjał jest niski. Projekt pozostaje w bazie i wraca do oceny po zmianach.",
            [
                "Pozostaw projekt w bazie – radar wykryje nowe materiały o nim",
                "Ponów ocenę po pojawieniu się nowych informacji"
            ],
            [
                "Ponowny przegląd po 6 miesiącach lub po wykryciu nowego materiału"
            ],
            [])
    ];

    public static DevelopmentPathInfo Get(DevelopmentPath path) => Paths.Single(x => x.Path == path);
}
