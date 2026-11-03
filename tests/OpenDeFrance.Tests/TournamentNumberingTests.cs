using Microsoft.Extensions.DependencyInjection;
using OpenDeFrance.Core.Enums;
using OpenDeFrance.Core.Models;
using OpenDeFrance.Core.Services;
using OpenDeFrance.Infrastructure.Services;

namespace OpenDeFrance.Tests;

public class TournamentNumberingTests
{
    [Fact]
    public void GenerateAllBrackets_ShouldNumberKatasFirst_ThenKumiteRoundByRound()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IKumiteBracketGenerator, KumiteBracketGenerator>();
        services.AddSingleton<IKataTournamentManager, KataTournamentManager>();
        services.AddSingleton<IExcelTournamentService, ExcelTournamentService>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var tournamentService = new TournamentService(
            scopeFactory,
            provider.GetRequiredService<IKumiteBracketGenerator>(),
            provider.GetRequiredService<IKataTournamentManager>(),
            provider.GetRequiredService<IExcelTournamentService>());

        // Créer 2 catégories Kata et 2 catégories Kumité (une avec 16 combattants = 8e de finale, une avec 32 = 16e de finale)
        var catKata1 = new Category { Id = 1, Name = "Kata Hommes", Type = CompetitionType.Kata, KataQualifiersCount = 4 };
        var catKata2 = new Category { Id = 2, Name = "Kata Femmes", Type = CompetitionType.Kata, KataQualifiersCount = 4 };
        var catKumite1 = new Category { Id = 3, Name = "Kumite -75kg", Type = CompetitionType.Kumite };
        var catKumite2 = new Category { Id = 4, Name = "Kumite +80kg", Type = CompetitionType.Kumite };

        // 6 compétiteurs en Kata 1, 5 en Kata 2
        for (int i = 1; i <= 6; i++) catKata1.Competitors.Add(new Competitor { Id = 100 + i, LastName = $"KataM{i}", IsKataRegistered = true, Club = "ClubA" });
        for (int i = 1; i <= 5; i++) catKata2.Competitors.Add(new Competitor { Id = 200 + i, LastName = $"KataF{i}", IsKataRegistered = true, Club = "ClubB" });

        // 8 compétiteurs en Kumite 1 (Quarts de finale -> Demis -> Finale)
        for (int i = 1; i <= 8; i++) catKumite1.Competitors.Add(new Competitor { Id = 300 + i, LastName = $"KumiteA{i}", IsKumiteRegistered = true, Club = $"Club{i % 3}", Grade = BeltGrade.Shodan, Weight = 70 });

        // 16 compétiteurs en Kumite 2 (8e de finale -> Quarts -> Demis -> Finale)
        for (int i = 1; i <= 16; i++) catKumite2.Competitors.Add(new Competitor { Id = 400 + i, LastName = $"KumiteB{i}", IsKumiteRegistered = true, Club = $"Club{i % 4}", Grade = BeltGrade.Nidan, Weight = 85 });

        var categories = tournamentService.GetCategories();
        categories.Add(catKata1);
        categories.Add(catKata2);
        categories.Add(catKumite1);
        categories.Add(catKumite2);

        // Act
        tournamentService.GenerateAllBrackets();

        var katas = tournamentService.GetKataPerformances();
        var matches = tournamentService.GetMatches();

        // Assert 1 : En Kata, chaque catégorie termine ses 2 tours avant de passer à la suivante
        var cat1Katas = katas.Where(p => p.CategoryId == 1).OrderBy(p => p.PerformanceOrder).ToList();
        var cat1T1 = cat1Katas.Where(p => p.Round == KataRound.Turn1).ToList();
        var cat1T2 = cat1Katas.Where(p => p.Round == KataRound.Turn2).ToList();
        Assert.Equal(6, cat1T1.Count);
        Assert.Equal(4, cat1T2.Count);
        Assert.Equal(1, cat1T1.First().PerformanceOrder);
        Assert.Equal(6, cat1T1.Last().PerformanceOrder);
        Assert.Equal(7, cat1T2.First().PerformanceOrder);
        Assert.Equal(10, cat1T2.Last().PerformanceOrder);

        // Catégorie 2 enchaîne immédiatement après que Catégorie 1 a terminé ses 2 tours
        var cat2Katas = katas.Where(p => p.CategoryId == 2).OrderBy(p => p.PerformanceOrder).ToList();
        var cat2T1 = cat2Katas.Where(p => p.Round == KataRound.Turn1).ToList();
        var cat2T2 = cat2Katas.Where(p => p.Round == KataRound.Turn2).ToList();
        Assert.Equal(5, cat2T1.Count);
        Assert.Equal(4, cat2T2.Count);
        Assert.Equal(11, cat2T1.First().PerformanceOrder);
        Assert.Equal(15, cat2T1.Last().PerformanceOrder);
        Assert.Equal(16, cat2T2.First().PerformanceOrder);
        Assert.Equal(19, cat2T2.Last().PerformanceOrder);

        // Assert 2 : Les combats Kumité commencent après les Katas (ici au #20)
        Assert.True(matches.Min(m => m.MatchNumber) >= 20);

        // Assert 4 : Vérifier l'ordre des combats : 8e de finale (fromFinal=3) < Quarts (fromFinal=2) < Demis (fromFinal=1) < Finales (fromFinal=0)
        int min8e = matches.Where(m => m.RoundFromFinal == 3).Min(m => m.MatchNumber);
        int max8e = matches.Where(m => m.RoundFromFinal == 3).Max(m => m.MatchNumber);

        int minQuarts = matches.Where(m => m.RoundFromFinal == 2).Min(m => m.MatchNumber);
        int maxQuarts = matches.Where(m => m.RoundFromFinal == 2).Max(m => m.MatchNumber);

        int minDemis = matches.Where(m => m.RoundFromFinal == 1).Min(m => m.MatchNumber);
        int maxDemis = matches.Where(m => m.RoundFromFinal == 1).Max(m => m.MatchNumber);

        int minFinales = matches.Where(m => m.RoundFromFinal == 0).Min(m => m.MatchNumber);

        // Les 8e doivent être avant les Quarts
        Assert.True(max8e < minQuarts, $"Max 8e ({max8e}) doit être inférieur à Min Quarts ({minQuarts})");

        // Les Quarts doivent être avant les Demis
        Assert.True(maxQuarts < minDemis, $"Max Quarts ({maxQuarts}) doit être inférieur à Min Demis ({minDemis})");

        // Les Demis doivent être avant les Finales
        Assert.True(maxDemis < minFinales, $"Max Demis ({maxDemis}) doit être inférieur à Min Finales ({minFinales})");
    }

    [Fact]
    public void ChronologicalNumbering_ShouldNumberIndependentlyPerTatami()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IKumiteBracketGenerator, KumiteBracketGenerator>();
        services.AddSingleton<IKataTournamentManager, KataTournamentManager>();
        services.AddSingleton<IExcelTournamentService, ExcelTournamentService>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var tournamentService = new TournamentService(
            scopeFactory,
            provider.GetRequiredService<IKumiteBracketGenerator>(),
            provider.GetRequiredService<IKataTournamentManager>(),
            provider.GetRequiredService<IExcelTournamentService>());

        // Tatami 1 : Kumite uniquement
        // Tatami 2 : Kata + Kumite
        // Tatami 3 : Kata uniquement
        var catKumiteT1 = new Category { Id = 10, Name = "Kumite T1", Type = CompetitionType.Kumite, AssignedTatamiId = 1 };
        for (int i = 1; i <= 4; i++) catKumiteT1.Competitors.Add(new Competitor { Id = 100 + i, LastName = $"T1_{i}", IsKumiteRegistered = true });

        var catKataT2 = new Category { Id = 20, Name = "Kata T2", Type = CompetitionType.Kata, AssignedTatamiId = 2, KataQualifiersCount = 2 };
        for (int i = 1; i <= 3; i++) catKataT2.Competitors.Add(new Competitor { Id = 200 + i, LastName = $"T2K_{i}", IsKataRegistered = true });

        var catKumiteT2 = new Category { Id = 21, Name = "Kumite T2", Type = CompetitionType.Kumite, AssignedTatamiId = 2 };
        for (int i = 1; i <= 4; i++) catKumiteT2.Competitors.Add(new Competitor { Id = 210 + i, LastName = $"T2M_{i}", IsKumiteRegistered = true });

        var catKataT3 = new Category { Id = 30, Name = "Kata T3", Type = CompetitionType.Kata, AssignedTatamiId = 3, KataQualifiersCount = 2 };
        for (int i = 1; i <= 4; i++) catKataT3.Competitors.Add(new Competitor { Id = 300 + i, LastName = $"T3K_{i}", IsKataRegistered = true });

        var categories = tournamentService.GetCategories();
        categories.Add(catKumiteT1);
        categories.Add(catKataT2);
        categories.Add(catKumiteT2);
        categories.Add(catKataT3);

        // Act
        tournamentService.GenerateAllBrackets();

        var matchesT1 = tournamentService.GetMatches().Where(m => m.TatamiId == 1 && !m.IsBye).OrderBy(m => m.MatchNumber).ToList();
        var katasT2 = tournamentService.GetKataPerformances().Where(p => p.TatamiId == 2).OrderBy(p => p.PerformanceOrder).ToList();
        var matchesT2 = tournamentService.GetMatches().Where(m => m.TatamiId == 2 && !m.IsBye).OrderBy(m => m.MatchNumber).ToList();
        var katasT3 = tournamentService.GetKataPerformances().Where(p => p.TatamiId == 3).OrderBy(p => p.PerformanceOrder).ToList();

        // Tatami 1 : Demarre au #1 pour ses combats
        Assert.NotEmpty(matchesT1);
        Assert.Equal(1, matchesT1.First().MatchNumber);

        // Tatami 2 : Demarre au #1 pour ses katas, puis ses combats enchainent apres les katas
        Assert.NotEmpty(katasT2);
        Assert.Equal(1, katasT2.First().PerformanceOrder);
        int maxKataT2 = katasT2.Max(p => p.PerformanceOrder);
        Assert.NotEmpty(matchesT2);
        Assert.Equal(maxKataT2 + 1, matchesT2.First().MatchNumber);

        // Tatami 3 : Demarre au #1 pour ses katas
        Assert.NotEmpty(katasT3);
        Assert.Equal(1, katasT3.First().PerformanceOrder);
    }

    [Fact]
    public void ChronologicalNumbering_KataCategories_ShouldFinishBothRoundsBeforeNextCategory()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IKumiteBracketGenerator, KumiteBracketGenerator>();
        services.AddSingleton<IKataTournamentManager, KataTournamentManager>();
        services.AddSingleton<IExcelTournamentService, ExcelTournamentService>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var tournamentService = new TournamentService(
            scopeFactory,
            provider.GetRequiredService<IKumiteBracketGenerator>(),
            provider.GetRequiredService<IKataTournamentManager>(),
            provider.GetRequiredService<IExcelTournamentService>());

        // 2 catégories Kata sur le même Tatami 1
        var catKataA = new Category { Id = 1, Name = "Kata Juniors", Type = CompetitionType.Kata, AssignedTatamiId = 1, OrderIndex = 1, KataQualifiersCount = 2 };
        for (int i = 1; i <= 3; i++) catKataA.Competitors.Add(new Competitor { Id = 10 + i, LastName = $"Jun_{i}", IsKataRegistered = true });

        var catKataB = new Category { Id = 2, Name = "Kata Seniors", Type = CompetitionType.Kata, AssignedTatamiId = 1, OrderIndex = 2, KataQualifiersCount = 2 };
        for (int i = 1; i <= 4; i++) catKataB.Competitors.Add(new Competitor { Id = 20 + i, LastName = $"Sen_{i}", IsKataRegistered = true });

        var catKumite = new Category { Id = 3, Name = "Kumite Open", Type = CompetitionType.Kumite, AssignedTatamiId = 1, OrderIndex = 3 };
        for (int i = 1; i <= 2; i++) catKumite.Competitors.Add(new Competitor { Id = 30 + i, LastName = $"Kum_{i}", IsKumiteRegistered = true });

        var categories = tournamentService.GetCategories();
        categories.Add(catKataA);
        categories.Add(catKataB);
        categories.Add(catKumite);

        tournamentService.GenerateAllBrackets();

        var katas = tournamentService.GetKataPerformances().Where(p => p.TatamiId == 1).ToList();
        var matches = tournamentService.GetMatches().Where(m => m.TatamiId == 1 && !m.IsBye).ToList();

        // Catégorie A : Tour 1 (#1..#3) puis Tour 2 (#4..#5)
        var aT1 = katas.Where(p => p.CategoryId == 1 && p.Round == KataRound.Turn1).OrderBy(p => p.PerformanceOrder).ToList();
        var aT2 = katas.Where(p => p.CategoryId == 1 && p.Round == KataRound.Turn2).OrderBy(p => p.PerformanceOrder).ToList();
        Assert.Equal(3, aT1.Count);
        Assert.Equal(2, aT2.Count);
        Assert.Equal(1, aT1.First().PerformanceOrder);
        Assert.Equal(3, aT1.Last().PerformanceOrder);
        Assert.Equal(4, aT2.First().PerformanceOrder);
        Assert.Equal(5, aT2.Last().PerformanceOrder);

        // Catégorie B : Tour 1 (#6..#9) puis Tour 2 (#10..#11)
        var bT1 = katas.Where(p => p.CategoryId == 2 && p.Round == KataRound.Turn1).OrderBy(p => p.PerformanceOrder).ToList();
        var bT2 = katas.Where(p => p.CategoryId == 2 && p.Round == KataRound.Turn2).OrderBy(p => p.PerformanceOrder).ToList();
        Assert.Equal(4, bT1.Count);
        Assert.Equal(2, bT2.Count);
        Assert.Equal(6, bT1.First().PerformanceOrder);
        Assert.Equal(9, bT1.Last().PerformanceOrder);
        Assert.Equal(10, bT2.First().PerformanceOrder);
        Assert.Equal(11, bT2.Last().PerformanceOrder);

        // Kumité démarre après tous les katas (au #12)
        Assert.Single(matches);
        Assert.Equal(12, matches.First().MatchNumber);
    }
}
