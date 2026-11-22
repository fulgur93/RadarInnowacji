namespace RadarInnowacji.Core.Models;

/// <summary>Podsumowanie przetworzonej kolejki analiz AI.</summary>
public sealed record AiQueueResult(int Processed, int Failed, string? LastError)
{
    public static AiQueueResult Success(int count = 1) => new(count, 0, null);

    public static AiQueueResult Failure(string error, int count = 1) => new(0, count, error);
}
