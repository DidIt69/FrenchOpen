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
    public void CalculateLiveScore_WithPartialScores_ShouldNotFinishAndKeepScores()
    {
        var perf = new KataPerformance { Status = MatchStatus.Pending };
        var scores = new List<double> { 75, 80, 0, 0, 0 };

        _manager.CalculateLiveScore(perf, scores);

        Assert.Equal(MatchStatus.InProgress, perf.Status);
        Assert.Null(perf.DroppedLowestScore);
        Assert.Null(perf.DroppedHighestScore);
        Assert.Equal(0, perf.FinalScore);
        Assert.Equal(5, perf.JudgeScores.Count);
        Assert.Equal(75, perf.JudgeScores[0]);
        Assert.Equal(80, perf.JudgeScores[1]);
        Assert.Equal(0, perf.JudgeScores[2]);
    }

    [Fact]
    public void CalculateLiveScore_With5Judges_ShouldCalculateMinMaxAndFinal_WhileRemainingInProgress()
    {
        var perf = new KataPerformance { Status = MatchStatus.Pending };
        var scores = new List<double> { 70, 75, 72, 80, 74 };

        _manager.CalculateLiveScore(perf, scores);

        Assert.Equal(MatchStatus.InProgress, perf.Status);
        Assert.Equal(70, perf.DroppedLowestScore);
        Assert.Equal(80, perf.DroppedHighestScore);
        Assert.Equal(221, perf.FinalScore);
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
    public void InitializeRound2_ShouldStartWithLowestScoreCompetitor()
    {
        var cat = new Category
        {
            Id = 1,
            Name = "Senior Hommes Kata",
            KataRound2MandatoryKata = "Saifa"
        };

        var r1 = new List<KataPerformance>
        {
            new() { CompetitorId = 1, FinalScore = 230, RankInRound = 1, IsQualifiedForTour2 = true, Competitor = new Competitor { Id = 1 } },
            new() { CompetitorId = 2, FinalScore = 225, RankInRound = 2, IsQualifiedForTour2 = true, Competitor = new Competitor { Id = 2 } },
            new() { CompetitorId = 3, FinalScore = 220, RankInRound = 3, IsQualifiedForTour2 = true, Competitor = new Competitor { Id = 3 } },
            new() { CompetitorId = 4, FinalScore = 215, RankInRound = 4, IsQualifiedForTour2 = true, Competitor = new Competitor { Id = 4 } }
        };

        var r2 = _manager.InitializeRound2(cat, r1);

        Assert.Equal(4, r2.Count);
        // Le 4e (215 pts - moins de points) passe en 1er, le 1er (230 pts) passe en dernier
        Assert.Equal(4, r2[0].CompetitorId);
        Assert.Equal(3, r2[1].CompetitorId);
        Assert.Equal(2, r2[2].CompetitorId);
        Assert.Equal(1, r2[3].CompetitorId);
        Assert.Equal("Saifa", r2[0].KataName);
    }

    [Fact]
    public void AssignRound2Qualifiers_WithTieInFinalScore_LowestJudgeNotePassesFirst()
    {
        var cat = new Category
        {
            Id = 1,
            Name = "Senior Kata",
            KataQualifiersCount = 2
        };

        // Deux compétiteurs avec le même FinalScore = 220
        // Compétiteur 1 : notes [72, 73, 75] (min = 72)
        // Compétiteur 2 : notes [70, 75, 75] (min = 70) -> a la note la plus basse (70), donc doit passer en PREMIER au tour 2
        var p1 = new KataPerformance
        {
            Id = 1,
            CompetitorId = 1,
            FinalScore = 220,
            JudgeScores = new List<double> { 72, 73, 75 },
            IsQualifiedForTour2 = true,
            Competitor = new Competitor { Id = 1, LastName = "Dupont" }
        };
        var p2 = new KataPerformance
        {
            Id = 2,
            CompetitorId = 2,
            FinalScore = 220,
            JudgeScores = new List<double> { 70, 75, 75 },
            IsQualifiedForTour2 = true,
            Competitor = new Competitor { Id = 2, LastName = "Martin" }
        };

        var round2Slots = new List<KataPerformance>
        {
            new() { Round = KataRound.Turn2, PerformanceOrder = 1 },
            new() { Round = KataRound.Turn2, PerformanceOrder = 2 }
        };

        _manager.AssignRound2Qualifiers(new List<KataPerformance> { p1, p2 }, round2Slots);

        // Compétiteur 2 a la plus petite note (70 < 72), donc passe en premier
        Assert.Equal(2, round2Slots[0].CompetitorId);
        Assert.Equal(1, round2Slots[1].CompetitorId);
    }

    [Fact]
    public void FinalizeRound1AndQualify_WithTieInFinalScore_HighestMinimumNoteRanksBetter()
    {
        var cat = new Category
        {
            Id = 1,
            Name = "Senior Kata",
            KataQualifiersCount = 2
        };

        // Compétiteur 1 : notes [72, 73, 75] (min = 72)
        // Compétiteur 2 : notes [70, 75, 75] (min = 70)
        // Compétiteur 1 a de meilleures notes individuelles minimales -> Rank 1
        // Compétiteur 2 -> Rank 2
        var p1 = new KataPerformance
        {
            Id = 1,
            CompetitorId = 1,
            FinalScore = 220,
            JudgeScores = new List<double> { 72, 73, 75 },
            Competitor = new Competitor { Id = 1, LastName = "Dupont" }
        };
        var p2 = new KataPerformance
        {
            Id = 2,
            CompetitorId = 2,
            FinalScore = 220,
            JudgeScores = new List<double> { 70, 75, 75 },
            Competitor = new Competitor { Id = 2, LastName = "Martin" }
        };

        var ranked = _manager.FinalizeRound1AndQualify(cat, new List<KataPerformance> { p2, p1 });

        Assert.Equal(1, ranked.First(p => p.CompetitorId == 1).RankInRound);
        Assert.Equal(2, ranked.First(p => p.CompetitorId == 2).RankInRound);
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

    [Fact]
    public void FinalizeRound1AndQualify_WhenOnlyFirstCompetitorHasScore_OnlyFirstCompetitorIsQualified()
    {
        var cat = new Category
        {
            Id = 1,
            Name = "Senior Kata",
            KataQualifiersCount = 4
        };

        var p1 = new KataPerformance { Id = 1, CompetitorId = 1, FinalScore = 222, Competitor = new Competitor { Id = 1, LastName = "A" } };
        var p2 = new KataPerformance { Id = 2, CompetitorId = 2, FinalScore = 0, Competitor = new Competitor { Id = 2, LastName = "B" } };
        var p3 = new KataPerformance { Id = 3, CompetitorId = 3, FinalScore = 0, Competitor = new Competitor { Id = 3, LastName = "C" } };
        var p4 = new KataPerformance { Id = 4, CompetitorId = 4, FinalScore = 0, Competitor = new Competitor { Id = 4, LastName = "D" } };

        var ranked = _manager.FinalizeRound1AndQualify(cat, new List<KataPerformance> { p1, p2, p3, p4 });

        // Seul le compétiteur avec un score > 0 est qualifié pour le Tour 2
        Assert.Single(ranked, p => p.IsQualifiedForTour2);
        Assert.True(ranked.First(p => p.CompetitorId == 1).IsQualifiedForTour2);
        Assert.False(ranked.First(p => p.CompetitorId == 2).IsQualifiedForTour2);
        Assert.False(ranked.First(p => p.CompetitorId == 3).IsQualifiedForTour2);
        Assert.False(ranked.First(p => p.CompetitorId == 4).IsQualifiedForTour2);
    }

    [Fact]
    public void AssignRound2Qualifiers_WhenOnlyFirstCompetitorHasScore_AssignsToTour2SlotAndOthersStayEmpty()
    {
        var cat = new Category
        {
            Id = 1,
            Name = "Senior Kata",
            KataQualifiersCount = 4
        };

        var p1 = new KataPerformance { Id = 1, CompetitorId = 1, FinalScore = 222, Competitor = new Competitor { Id = 1, LastName = "A" } };
        var p2 = new KataPerformance { Id = 2, CompetitorId = 2, FinalScore = 0, Competitor = new Competitor { Id = 2, LastName = "B" } };
        var p3 = new KataPerformance { Id = 3, CompetitorId = 3, FinalScore = 0, Competitor = new Competitor { Id = 3, LastName = "C" } };
        var p4 = new KataPerformance { Id = 4, CompetitorId = 4, FinalScore = 0, Competitor = new Competitor { Id = 4, LastName = "D" } };

        var round1 = _manager.FinalizeRound1AndQualify(cat, new List<KataPerformance> { p1, p2, p3, p4 });

        var round2Slots = new List<KataPerformance>
        {
            new() { Id = 101, Round = KataRound.Turn2, PerformanceOrder = 1 },
            new() { Id = 102, Round = KataRound.Turn2, PerformanceOrder = 2 },
            new() { Id = 103, Round = KataRound.Turn2, PerformanceOrder = 3 },
            new() { Id = 104, Round = KataRound.Turn2, PerformanceOrder = 4 }
        };

        _manager.AssignRound2Qualifiers(round1, round2Slots);

        // Le premier créneau du Tour 2 doit contenir le compétiteur 1
        Assert.Equal(1, round2Slots[0].CompetitorId);
        Assert.Equal("A", round2Slots[0].Competitor?.LastName);

        // Les 3 autres créneaux restent vides
        Assert.Null(round2Slots[1].CompetitorId);
        Assert.Null(round2Slots[2].CompetitorId);
        Assert.Null(round2Slots[3].CompetitorId);
    }

    [Fact]
    public void AssignRound2Qualifiers_ProgressiveGrading_PlacesCompetitorsInLowestScoreFirstOrder()
    {
        var cat = new Category
        {
            Id = 1,
            Name = "Senior Kata",
            KataQualifiersCount = 2
        };

        var round2Slots = new List<KataPerformance>
        {
            new() { Id = 101, Round = KataRound.Turn2, PerformanceOrder = 1 },
            new() { Id = 102, Round = KataRound.Turn2, PerformanceOrder = 2 }
        };

        // Étape 1 : Seul le compétiteur 1 passe avec 230 pts
        var p1 = new KataPerformance { Id = 1, CompetitorId = 1, FinalScore = 230, Competitor = new Competitor { Id = 1, LastName = "A" } };
        var p2 = new KataPerformance { Id = 2, CompetitorId = 2, FinalScore = 0, Competitor = new Competitor { Id = 2, LastName = "B" } };
        var r1 = _manager.FinalizeRound1AndQualify(cat, new List<KataPerformance> { p1, p2 });
        _manager.AssignRound2Qualifiers(r1, round2Slots);

        Assert.Equal(1, round2Slots[0].CompetitorId);
        Assert.Null(round2Slots[1].CompetitorId);

        // Étape 2 : Le compétiteur 2 passe avec 215 pts (moins de points que p1)
        p2.FinalScore = 215;
        r1 = _manager.FinalizeRound1AndQualify(cat, new List<KataPerformance> { p1, p2 });
        _manager.AssignRound2Qualifiers(r1, round2Slots);

        // Le compétiteur 2 (215 pts) a moins de points -> commence le tour 2 (Slot 0)
        // Le compétiteur 1 (230 pts) a plus de points -> passe ensuite (Slot 1)
        Assert.Equal(2, round2Slots[0].CompetitorId);
        Assert.Equal(1, round2Slots[1].CompetitorId);
    }
}
