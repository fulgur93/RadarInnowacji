using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Tests;

internal static class SourceTestFactory
{
    public static Source Create(SourceType type, string url, string name = "Testowe źródło") => new()
    {
        Name = name,
        Url = url,
        Radar = RadarKind.Uczelnia,
        SourceType = type,
        FetchMethod = SourceDefaults.FetchMethodFor(type),
        IsActive = true,
        IsDemo = false,
        ScanIntervalMinutes = 60,
        Configuration = "{}"
    };
}
