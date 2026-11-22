namespace RadarInnowacji.Core.Enums;

/// <summary>Etap procesu: co dzieje się z innowacją po jej zidentyfikowaniu.</summary>
public enum PipelineStage
{
    Wykryta = 0,
    Zgloszona = 1,
    WOcenie = 2,
    Oceniona = 3,
    Rekomendowana = 4,
    Skierowana = 5,
    Pilotaz = 6,
    Wdrozona = 7,
    Odlozona = 8,
    Odrzucona = 9
}
