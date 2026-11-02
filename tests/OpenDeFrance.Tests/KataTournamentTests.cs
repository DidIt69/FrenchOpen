using OpenDeFrance.Core.Enums;
using OpenDeFrance.Core.Models;
using OpenDeFrance.Core.Services;

namespace OpenDeFrance.Tests;

public class KataTournamentTests
{
    private readonly KataTournamentManager _manager = new();

    [Fact]
    public void CalculateScore_With5Judges_ShouldDropMinAndMax_AndSumMiddle3()
    {
        var perf = new KataPerformance();
        // 5 notes : min=70, max=80, middle=72, 74, 75 -> Somme = 221
        var scores = new List<double> { 70, 75, 72, 80, 74 };

        _manager.CalculateScore(perf, scores);

        Assert.Equal(70, perf.DroppedLowestScore);
        Assert.Equal(80, perf.DroppedHighestScore);
        Assert.Equal(221, perf.FinalScore);
        Assert.Equal(MatchStatus.Finished, perf.Status);
    }

    [Fact]
    public void FinalizeRound1AndQualify_ShouldQualifyTop4Competitors()
    {
        var cat = new Category
        {
            Id = 1,
            Name = "Senior Hommes Kata",
            Type = CompetitionType.Kata,
            KataRound1MandatoryKata = "Gekisai Dai",
            KataRound2MandatoryKata = "Saifa",
            KataQualifiersCount = 4
        };

        var perfs = new List<KataPerformance>
        {
            new() { Id = 1, CompetitorId = 1, FinalScore = 210, Competitor = new Competitor { Id = 1, LastName = "A" } },
            new() { Id = 2, CompetitorId = 2, FinalScore = 225, Competitor = new Competitor { Id = 2, LastName = "B" } },
            new() { Id = 3, CompetitorId = 3, FinalScore = 230, Competitor = new Competitor { Id = 3, LastName = "C" } },
            new() { Id = 4, CompetitorId = 4, FinalScore = 195, Competitor = new Competitor { Id = 4, LastName = "D" } },
            new() { Id = 5, CompetitorId = 5, FinalScore = 220, Competitor = new Competitor { Id = 5, LastName = "E" } },
            new() { Id = 6, CompetitorId = 6, FinalScore = 200, Competitor = new Competitor { Id = 6, LastName = "F" } }
        };

        var ranked = _manager.FinalizeRound1AndQualify(cat, perfs);

        Assert.Equal(4, ranked.Count(p => p.IsQualifiedForTour2));
        // Les 4 qualifiés doivent être C (230), B (225), E (220), A (210)
        Assert.True(ranked.First(p => p.CompetitorId == 3).IsQualifiedForTour2);
        Assert.True(ranked.First(p => p.CompetitorId == 2).IsQualifiedForTour2);
        Assert.True(ranked.First(p => p.CompetitorId == 5).IsQualifiedForTour2);
        Assert.True(ranked.First(p => p.CompetitorId == 1).IsQualifiedForTour2);
        Assert.False(ranked.First(p => p.CompetitorId == 6).IsQualifiedForTour2);
        Assert.False(ranked.First(p => p.CompetitorId == 4).IsQualifiedForTour2);
    }

    [Fact]
    public void FinalizeRound1AndQualify_WithCustomQualifiersCount_ShouldQualifyConfiguredNumber()
    {
        var cat = new Category
        {
            Id = 1,
            Name = "Cadets Kata",
            Type = CompetitionType.Kata,
            KataQualifiersCount = 2
        };

        var perfs = new List<KataPerformance>
        {
            new() { Id = 1, CompetitorId = 1, FinalScore = 210, Competitor = new Competitor { Id = 1 } },
            new() { Id = 2, CompetitorId = 2, FinalScore = 225, Competitor = new Competitor { Id = 2 } },
            new() { Id = 3, CompetitorId = 3, FinalScore = 230, Competitor = new Competitor { Id = 3 } },
            new() { Id = 4, CompetitorId = 4, FinalScore = 195, Competitor = new Competitor { Id = 4 } }
        };

        var ranked = _manager.FinalizeRound1AndQualify(cat, perfs);

        Assert.Equal(2, ranked.Count(p => p.IsQualifiedForTour2));
        Assert.True(ranked.First(p => p.CompetitorId == 3).IsQualifiedForTour2);
        Assert.True(ranked.First(p => p.CompetitorId == 2).IsQualifiedForTour2);
        Assert.False(ranked.First(p => p.CompetitorId == 1).IsQualifiedForTour2);
        Assert.False(ranked.First(p => p.CompetitorId == 4).IsQualifiedForTour2);
    }

    [Fact]
    public void InitializeRound2_ShouldReverseOrderOfPassage()
    {
        var cat = new Category
        {
            Id = 1,
            Name = "Senior Hommes Kata",
            KataRound2MandatoryKata = "Saifa"
        };

        var r1 = new List<KataPerformance>
        {
            new() { CompetitorId = 1, RankInRound = 1, IsQualifiedForTour2 = true, Competitor = new Competitor { Id = 1 } },
            new() { CompetitorId = 2, RankInRound = 2, IsQualifiedForTour2 = true, Competitor = new Competitor { Id = 2 } },
            new() { CompetitorId = 3, RankInRound = 3, IsQualifiedForTour2 = true, Competitor = new Competitor { Id = 3 } },
            new() { CompetitorId = 4, RankInRound = 4, IsQualifiedForTour2 = true, Competitor = new Competitor { Id = 4 } }
        };

        var r2 = _manager.InitializeRound2(cat, r1);

        Assert.Equal(4, r2.Count);
        // Le 4e passe en 1er, le 1er passe en 4e
        Assert.Equal(4, r2[0].CompetitorId);
        Assert.Equal(3, r2[1].CompetitorId);
        Assert.Equal(2, r2[2].CompetitorId);
        Assert.Equal(1, r2[3].CompetitorId);
        Assert.Equal("Saifa", r2[0].KataName);
    }

    [Fact]
    public void InitializeRound1_ShouldInterleaveCountries_ToPreventConsecutivePassagesFromSameCountry()
    {
        var cat = new Category { Id = 1, Name = "Kata International", KataRound1MandatoryKata = "Gekisai Dai" };
        var competitors = new List<Competitor>
        {
            new() { Id = 1, LastName = "Dupont", Country = "France", Club = "Paris" },
            new() { Id = 2, LastName = "Martin", Country = "France", Club = "Lyon" },
            new() { Id = 3, LastName = "Tani", Country = "Japon", Club = "Tokyo" },
            new() { Id = 4, LastName = "Sanchez", Country = "Espagne", Club = "Madrid" }
        };

        var perfs = _manager.InitializeRound1(cat, competitors);

        Assert.Equal(4, perfs.Count);
        // Le 1er est France (pays avec 2 athlètes), le 2e est Japon ou Espagne, le 3e est l'autre, et le 4e est le 2e Français
        // Ainsi aucun Français ne passe consécutivement
        for (int i = 0; i < perfs.Count - 1; i++)
        {
            var c1 = perfs[i].Competitor!;
            var c2 = perfs[i + 1].Competitor!;
            Assert.NotEqual(c1.Country, c2.Country);
        }
    }
}
