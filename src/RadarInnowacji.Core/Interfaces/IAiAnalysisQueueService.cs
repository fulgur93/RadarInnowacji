using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Core.Interfaces;

/// <summary>Kolejka analiz AI: przetwarza odkrycia oczekujące na analizę.</summary>
public interface IAiAnalysisQueueService
{
    Task<AiQueueResult> ProcessPendingAsync(int maxItems, CancellationToken cancellationToken = default);

    Task<AiQueueResult> AnalyzeDiscoveryAsync(int discoveryId, CancellationToken cancellationToken = default);
}
