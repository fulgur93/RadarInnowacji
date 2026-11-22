using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Core.Interfaces;

/// <summary>Etapy procesu innowacji, skierowania do odbiorców, lejek i eksport.</summary>
public interface IPipelineService
{
    Task<OperationResult> ChangeStageAsync(
        int projectId,
        PipelineStage stage,
        string comment,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ReferAsync(
        int projectId,
        int recipientId,
        string note,
        CancellationToken cancellationToken = default);

    Task<FunnelModel> GetFunnelAsync(FunnelFilter filter, CancellationToken cancellationToken = default);

    Task<string> ExportCsvAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FunnelItem>> ExportItemsAsync(CancellationToken cancellationToken = default);
}
