namespace RadarInnowacji.Core.Enums;

public enum ProcessingStatus
{
    Pending,
    RuleFiltered,
    AiQueued,
    AiProcessed,
    AiFailed,
    Skipped
}
