using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Core.Interfaces;

/// <summary>Karta oceny potencjału, rekomendacje ścieżek i dopasowanie odbiorców.</summary>
public interface IAssessmentService
{
    /// <summary>Przelicza ocenę (zapisuje nową migawkę tylko przy zmianie wyniku) i zwraca kartę.</summary>
    Task<ProjectAssessmentCard?> GetCardAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>Przelicza ocenę projektu. Zwraca true, jeśli powstała nowa migawka.</summary>
    Task<bool> AssessAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>Ocenia projekty bez oceny lub z nieaktualną oceną (zadanie w tle i przycisk w lejku).</summary>
    Task<int> AssessPendingAsync(CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateProfileAsync(int projectId, ProjectProfile profile, CancellationToken cancellationToken = default);

    /// <summary>Ocena eksperta dla kryterium; <paramref name="score"/> = null przywraca ocenę automatyczną.</summary>
    Task<OperationResult> SetExpertScoreAsync(
        int projectId,
        AssessmentCriterion criterion,
        int? score,
        string comment,
        CancellationToken cancellationToken = default);
}
