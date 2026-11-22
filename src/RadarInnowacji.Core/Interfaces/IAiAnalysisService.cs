using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Core.Interfaces;

/// <summary>Analiza treści odkrycia z użyciem lokalnego modelu AI.</summary>
public interface IAiAnalysisService
{
    Task<AiAnalysisResult> AnalyzeAsync(Discovery discovery, CancellationToken cancellationToken = default);

    Task<AiConnectionTest> TestConnectionAsync(CancellationToken cancellationToken = default);
}
