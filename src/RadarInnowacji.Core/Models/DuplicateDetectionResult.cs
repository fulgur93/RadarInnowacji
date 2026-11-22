namespace RadarInnowacji.Core.Models;

/// <summary>Wynik przeszukania bazy pod kątem tego samego projektu w kilku źródłach.</summary>
public sealed class DuplicateDetectionResult
{
    public int ProjectsAnalyzed { get; init; }
    public int CandidatesFound { get; init; }
    public int AutoLinked { get; init; }
    public int ProposalsCreated { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<DuplicateCandidate> Candidates { get; init; } = Array.Empty<DuplicateCandidate>();
}

public sealed class DuplicateCandidate
{
    public int SourceProjectId { get; init; }
    public string SourceTitle { get; init; } = string.Empty;
    public int TargetProjectId { get; init; }
    public string TargetTitle { get; init; } = string.Empty;
    public double Similarity { get; init; }
    public string Signals { get; init; } = string.Empty;
    public string Decision { get; init; } = string.Empty;
    public bool IsAutomatic { get; init; }
}
