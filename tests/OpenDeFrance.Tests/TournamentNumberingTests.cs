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

        // Assert 1 : Tous les Katas Tour 1 doivent avoir les premiers numéros
        var kataTour1 = katas.Where(p => p.Round == KataRound.Tour1).OrderBy(p => p.PerformanceOrder).ToList();
        Assert.Equal(11, kataTour1.Count); // 6 + 5 = 11
        Assert.Equal(1, kataTour1.First().PerformanceOrder);
        Assert.Equal(11, kataTour1.Last().PerformanceOrder);

        // Assert 2 : Tous les Katas Tour 2 (Finales) viennent immédiatement après Tour 1
        var kataTour2 = katas.Where(p => p.Round == KataRound.Tour2).OrderBy(p => p.PerformanceOrder).ToList();
        Assert.Equal(8, kataTour2.Count); // 4 + 4 = 8
        Assert.Equal(12, kataTour2.First().PerformanceOrder);
        Assert.Equal(19, kataTour2.Last().PerformanceOrder);

        // Assert 3 : Les combats Kumité commencent après les Katas (ici au #20)
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
}
