using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Core.Interfaces;

/// <summary>Zgłaszanie innowacji przez instytucje i sprawdzanie, czy podobne rozwiązanie już istnieje.</summary>
public interface ISubmissionService
{
    Task<IReadOnlyList<SimilarProjectItem>> FindSimilarAsync(
        string title,
        string description,
        CancellationToken cancellationToken = default);

    Task<SubmissionResult> SubmitAsync(InnovationSubmission submission, CancellationToken cancellationToken = default);
}
