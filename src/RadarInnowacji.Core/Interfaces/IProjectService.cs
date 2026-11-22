using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Core.Interfaces;

/// <summary>Wspólny radar projektów, deduplikacja i decyzje o łączeniu projektów.</summary>
public interface IProjectService
{
    Task<ProjectPage> GetPageAsync(ProjectFilter filter, CancellationToken cancellationToken = default);
    Task<ProjectDetailsModel?> GetDetailsAsync(int projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SimilarProjectItem>> GetSimilarProjectsAsync(
        int projectId,
        int limit = 8,
        CancellationToken cancellationToken = default);
    /// <summary>Podobne projekty dla jeszcze niezapisanego tekstu (np. formularza zgłoszenia).</summary>
    Task<IReadOnlyList<SimilarProjectItem>> FindSimilarToTextAsync(
        string title,
        string description,
        int limit = 5,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Projekty o podobnym problemie (łagodniejsze kryterium niż duplikaty) – używane w karcie oceny
    /// do wskazania instytucji pracujących nad podobnym problemem.
    /// </summary>
    Task<IReadOnlyList<SimilarProjectItem>> GetRelatedProjectsAsync(
        int projectId,
        int limit = 8,
        CancellationToken cancellationToken = default);
    Task<DuplicateDetectionResult> DetectDuplicatesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectMergeProposalModel>> GetProposalsAsync(CancellationToken cancellationToken = default);
    Task<ProjectMergeProposalModel?> ProposeMergeAsync(
        int sourceProjectId,
        int targetProjectId,
        CancellationToken cancellationToken = default);
    Task<bool> AcceptProposalAsync(int proposalId, CancellationToken cancellationToken = default);
    Task<bool> RejectProposalAsync(int proposalId, CancellationToken cancellationToken = default);
}
