using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Infrastructure.Assessment;

/// <summary>Dozwolone przejścia między etapami procesu innowacji.</summary>
public static class PipelineRules
{
    private static readonly PipelineStage[] Closing = [PipelineStage.Odlozona, PipelineStage.Odrzucona];

    public static IReadOnlyDictionary<PipelineStage, PipelineStage[]> Transitions { get; } =
        new Dictionary<PipelineStage, PipelineStage[]>
        {
            [PipelineStage.Wykryta] = [PipelineStage.WOcenie, .. Closing],
            [PipelineStage.Zgloszona] = [PipelineStage.WOcenie, .. Closing],
            [PipelineStage.WOcenie] = [PipelineStage.Oceniona, .. Closing],
            [PipelineStage.Oceniona] = [PipelineStage.Rekomendowana, PipelineStage.WOcenie, .. Closing],
            [PipelineStage.Rekomendowana] = [PipelineStage.Skierowana, PipelineStage.Oceniona, .. Closing],
            [PipelineStage.Skierowana] = [PipelineStage.Pilotaz, PipelineStage.Rekomendowana, .. Closing],
            [PipelineStage.Pilotaz] = [PipelineStage.Wdrozona, PipelineStage.Skierowana, .. Closing],
            [PipelineStage.Wdrozona] = [],
            [PipelineStage.Odlozona] = [PipelineStage.WOcenie],
            [PipelineStage.Odrzucona] = [PipelineStage.WOcenie]
        };

    public static IReadOnlyList<PipelineStage> Allowed(PipelineStage from) =>
        Transitions.TryGetValue(from, out var next) ? next : [];

    public static bool CanMove(PipelineStage from, PipelineStage to) => Allowed(from).Contains(to);

    public static string Name(PipelineStage stage) => stage switch
    {
        PipelineStage.Wykryta => "Wykryta",
        PipelineStage.Zgloszona => "Zgłoszona",
        PipelineStage.WOcenie => "W ocenie",
        PipelineStage.Oceniona => "Oceniona",
        PipelineStage.Rekomendowana => "Rekomendowana",
        PipelineStage.Skierowana => "Skierowana do odbiorcy",
        PipelineStage.Pilotaz => "Pilotaż",
        PipelineStage.Wdrozona => "Wdrożona",
        PipelineStage.Odlozona => "Odłożona",
        PipelineStage.Odrzucona => "Odrzucona",
        _ => stage.ToString()
    };

    public static string Description(PipelineStage stage) => stage switch
    {
        PipelineStage.Wykryta => "znaleziona automatycznie przez radar",
        PipelineStage.Zgloszona => "zgłoszona przez instytucję lub twórcę",
        PipelineStage.WOcenie => "ekspert weryfikuje profil i kartę oceny",
        PipelineStage.Oceniona => "ocena zatwierdzona przez eksperta",
        PipelineStage.Rekomendowana => "przyjęto rekomendowaną ścieżkę",
        PipelineStage.Skierowana => "przekazana do odbiorcy, partnera lub inwestora",
        PipelineStage.Pilotaz => "trwa pilotaż u odbiorcy",
        PipelineStage.Wdrozona => "wdrożona poza miejscem powstania",
        PipelineStage.Odlozona => "wstrzymana – można wrócić do oceny",
        PipelineStage.Odrzucona => "odrzucona z uzasadnieniem – można wrócić do oceny",
        _ => string.Empty
    };
}
