namespace RadarInnowacji.Infrastructure.Configuration;

/// <summary>Ustawienia lokalnego silnika AI (Ollama). AI jest opcjonalne.</summary>
public sealed class AiOptions
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "http://127.0.0.1:11434";
    public string Model { get; set; } = "SpeakLeash/bielik-1.5b-v3.0-instruct:Q8_0";
    public int TimeoutSeconds { get; set; } = 90;
    public int IntervalMinutes { get; set; } = 30;
    public int StartDelaySeconds { get; set; } = 20;
    public int BatchSize { get; set; } = 5;
    public int MaxContentCharacters { get; set; } = 4000;
}
