using RadarInnowacji.Core.Entities;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Tests;

public class FreshnessCalculatorTests
{
    [Fact]
    public void Swiezosc_RosnieWrazZ_OstatniaAktywnoscia()
    {
        var now = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
        var recent = CreateProject(now.AddDays(-2), now.AddDays(-300));
        var old = CreateProject(now.AddDays(-500), now.AddDays(-300));

        var recentScore = FreshnessCalculator.Calculate(recent, now);
        var oldScore = FreshnessCalculator.Calculate(old, now);

        Assert.True(recentScore > oldScore + 20, $"Świeżość: nowa {recentScore}, stara {oldScore}.");
        Assert.InRange(recentScore, 0, 100);
        Assert.InRange(oldScore, 0, 100);
    }

    [Fact]
    public void Swiezosc_NieUzywaOcenyAIAniStatusu()
    {
        var now = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
        var project = CreateProject(now.AddDays(-10), now.AddDays(-60));
        var baseline = FreshnessCalculator.Calculate(project, now);

        project.Description = "Zupełnie inny opis bez wpływu na daty.";

        Assert.Equal(baseline, FreshnessCalculator.Calculate(project, now));
    }

    [Fact]
    public void Swiezosc_BezDatyPowstaniaNieWymyslaDaty()
    {
        var now = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
        var project = new InnovationProject
        {
            Title = "Projekt bez daty powstania",
            FirstDiscoveredAt = now.AddDays(-30),
            EarliestKnownDate = null
        };

        var score = FreshnessCalculator.Calculate(project, now);

        Assert.Null(project.EarliestKnownDate);
        Assert.InRange(score, 0, 100);
        Assert.True(score > 0);
    }

    private static InnovationProject CreateProject(DateTime? lastActivity, DateTime? earliestKnown) => new()
    {
        Title = "Projekt",
        FirstDiscoveredAt = earliestKnown ?? DateTime.UtcNow.AddDays(-100),
        EarliestKnownDate = earliestKnown,
        LastActivityAt = lastActivity
    };
}
