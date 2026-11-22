using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Core.Interfaces;

/// <summary>Cykliczne grupowanie projektów, propozycje nowych kategorii i historia zmian.</summary>
public interface ICategorizationService
{
    Task<CategorizationResult> AnalyzeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryProposalModel>> GetProposalsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectCluster>> GetClustersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryHistoryItem>> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task<bool> AcceptProposalAsync(int proposalId, CancellationToken cancellationToken = default);
    Task<bool> RejectProposalAsync(int proposalId, CancellationToken cancellationToken = default);
}
