using Microsoft.Extensions.DependencyInjection;
using OpenDeFrance.Core.Enums;
using OpenDeFrance.Core.Models;
using OpenDeFrance.Core.Services;
using OpenDeFrance.Infrastructure.Services;

namespace OpenDeFrance.Tests;

public class MultiDayAndTatamiTests
{
    private TournamentService CreateTournamentService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IKumiteBracketGenerator, KumiteBracketGenerator>();
        services.AddSingleton<IKataTournamentManager, KataTournamentManager>();
        services.AddSingleton<IExcelTournamentService, ExcelTournamentService>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return new TournamentService(
            scopeFactory,
            provider.GetRequiredService<IKumiteBracketGenerator>(),
            provider.GetRequiredService<IKataTournamentManager>(),
            provider.GetRequiredService<IExcelTournamentService>());
    }

    [Fact]
    public void SetCategoryDay_ShouldUpdateCategoryAndAllItsMatches()
    {
        var service = CreateTournamentService();

        var catKumite = new Category { Id = 1, Name = "Kumite -75kg", Type = CompetitionType.Kumite, CompetitionDay = 1 };
        for (int i = 1; i <= 4; i++)
        {
            catKumite.Competitors.Add(new Competitor { Id = i, LastName = $"Fighter{i}", IsKumiteRegistered = true });
        }
        service.GetCategories().Add(catKumite);
        service.GenerateCategoryBracket(1);

        // Vérifier initialisation Jour 1
        var matches = service.GetMatches().Where(m => m.CategoryId == 1).ToList();
        Assert.NotEmpty(matches);
        Assert.All(matches, m => Assert.Equal(1, m.CompetitionDay));

        // Déplacer sur le Jour 2
        service.SetCategoryDay(1, 2);

        var updatedCat = service.GetCategory(1);
        Assert.NotNull(updatedCat);
        Assert.Equal(2, updatedCat.CompetitionDay);

        var updatedMatches = service.GetMatches().Where(m => m.CategoryId == 1).ToList();
        Assert.All(updatedMatches, m => Assert.Equal(2, m.CompetitionDay));
    }

    [Fact]
    public void ChronologicalNumbering_ShouldNumberDay1MatchesBeforeDay2()
    {
        var service = CreateTournamentService();

        // Catégorie 1 sur Jour 1
        var catDay1 = new Category { Id = 10, Name = "Kumite J1", Type = CompetitionType.Kumite, CompetitionDay = 1 };
        for (int i = 1; i <= 4; i++)
        {
            catDay1.Competitors.Add(new Competitor { Id = 10 + i, LastName = $"C1_{i}", IsKumiteRegistered = true });
        }

        // Catégorie 2 sur Jour 2
        var catDay2 = new Category { Id = 20, Name = "Kumite J2", Type = CompetitionType.Kumite, CompetitionDay = 2 };
        for (int i = 1; i <= 4; i++)
        {
            catDay2.Competitors.Add(new Competitor { Id = 20 + i, LastName = $"C2_{i}", IsKumiteRegistered = true });
        }

        service.GetCategories().Add(catDay1);
        service.GetCategories().Add(catDay2);
        service.GenerateAllBrackets();

        var matchesDay1 = service.GetMatches().Where(m => m.CategoryId == 10 && !m.IsBye).ToList();
        var matchesDay2 = service.GetMatches().Where(m => m.CategoryId == 20 && !m.IsBye).ToList();

        Assert.NotEmpty(matchesDay1);
        Assert.NotEmpty(matchesDay2);

        // Tous les MatchNumber du Jour 1 doivent être strictement inférieurs aux MatchNumber du Jour 2
        int maxDay1Number = matchesDay1.Max(m => m.MatchNumber);
        int minDay2Number = matchesDay2.Min(m => m.MatchNumber);

        Assert.True(maxDay1Number < minDay2Number, $"Max J1 ({maxDay1Number}) doit être < Min J2 ({minDay2Number})");
    }

    [Fact]
    public void AssignCategoryMatchesToTatami_ShouldAssignTatamiToCategoryAndMatches()
    {
        var service = CreateTournamentService();

        var cat = new Category { Id = 5, Name = "Senior Femmes -60kg", Type = CompetitionType.Kumite };
        for (int i = 1; i <= 4; i++)
        {
            cat.Competitors.Add(new Competitor { Id = 50 + i, LastName = $"Femme{i}", IsKumiteRegistered = true });
        }
        service.GetCategories().Add(cat);
        service.GenerateCategoryBracket(5);

        // Assigner au Tatami 2
        service.AssignCategoryMatchesToTatami(5, 2);

        var updatedCat = service.GetCategory(5);
        Assert.NotNull(updatedCat);
        Assert.Equal(2, updatedCat.AssignedTatamiId);

        var catMatches = service.GetMatches().Where(m => m.CategoryId == 5 && !m.IsBye).ToList();
        Assert.All(catMatches, m => Assert.Equal(2, m.TatamiId));
    }

    [Fact]
    public void ExcelImport_WithDayAndTatami_ShouldAssignDayAndTatamiToCategories()
    {
        var excelService = new ExcelTournamentService();

        // Le template d'exemple inclut désormais la feuille 'Catégories' avec Jour et Tatami par catégorie
        var templateBytes = excelService.GenerateSampleExcelTemplate();
        using var ms = new MemoryStream(templateBytes);
        var result = excelService.ImportTournamentData(ms);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Categories);
        Assert.Equal(2, result.DaysCount);

        // Vérifier la présence de catégories sur Jour 1 et Jour 2
        Assert.Contains(result.Categories, c => c.CompetitionDay == 1);
        Assert.Contains(result.Categories, c => c.CompetitionDay == 2);

        // Vérifier que Kumite et Kata d'un même compétiteur ont bien leurs propres Tatamis
        var catKumiteH75 = result.Categories.FirstOrDefault(c => c.Name == "Senior Hommes -75kg");
        var catKataH = result.Categories.FirstOrDefault(c => c.Name == "Senior Hommes Kata");
        Assert.NotNull(catKumiteH75);
        Assert.NotNull(catKataH);

        Assert.Equal(1, catKumiteH75.CompetitionDay);
        Assert.Equal(1, catKumiteH75.AssignedTatamiId);

        Assert.Equal(1, catKataH.CompetitionDay);
        Assert.Equal(3, catKataH.AssignedTatamiId);
    }

    [Fact]
    public void BalanceCategoriesAcrossTatamis_ShouldDistributeKumiteAndKataCategoriesEvenly()
    {
        var service = CreateTournamentService();
        service.SeedSampleData();

        // Configurer des tatamis dédiés : Tatami 1 (Kumite), Tatami 2 (Kumite), Tatami 3 (Kata)
        service.UpdateTatami(1, "Tatami 1", CompetitionType.Kumite);
        service.UpdateTatami(2, "Tatami 2", CompetitionType.Kumite);
        service.UpdateTatami(3, "Tatami 3", CompetitionType.Kata);

        var tatamis = service.GetTatamis();
        Assert.Equal(3, tatamis.Count);

        // Rééquilibrer
        service.BalanceCategoriesAcrossTatamis(resetManualAssignments: true);

        var t1 = tatamis.First(t => t.Id == 1);
        var t2 = tatamis.First(t => t.Id == 2);
        var t3 = tatamis.First(t => t.Id == 3);

        // Kumite réparti entre Tatami 1 et Tatami 2
        Assert.NotEmpty(t1.AssignedKumiteMatches);
        Assert.NotEmpty(t2.AssignedKumiteMatches);
        Assert.Empty(t1.AssignedKataPerformances);
        Assert.Empty(t2.AssignedKataPerformances);

        // Kata sur Tatami 3
        Assert.NotEmpty(t3.AssignedKataPerformances);
        Assert.Empty(t3.AssignedKumiteMatches);

        // Toutes les catégories ont leur AssignedTatamiId assigné
        var categories = service.GetCategories();
        Assert.All(categories, c => Assert.NotNull(c.AssignedTatamiId));

        // Test SetCategoryTatami avec auto
        var firstCat = categories.First();
        service.SetCategoryTatami(firstCat.Id, null);
        Assert.NotNull(firstCat.AssignedTatamiId);
    }

    [Fact]
    public void TatamiWithBothDiscipline_ShouldAcceptBothKumiteAndKataCategories()
    {
        var service = CreateTournamentService();
        service.SeedSampleData();

        // Configurer Tatami 1 = Kumite, Tatami 2 = Both, Tatami 3 = Kata
        service.UpdateTatami(1, "Tatami 1", CompetitionType.Kumite);
        service.UpdateTatami(2, "Tatami 2", CompetitionType.Both);
        service.UpdateTatami(3, "Tatami 3", CompetitionType.Kata);

        service.BalanceCategoriesAcrossTatamis(resetManualAssignments: true);

        var t2 = service.GetTatami(2);
        Assert.NotNull(t2);
        Assert.Equal(CompetitionType.Both, t2.Discipline);

        // Tatami 2 étant 'Both', il est éligible pour Kumité et Kata et doit recevoir les deux
        Assert.NotEmpty(t2.AssignedKumiteMatches);
        Assert.NotEmpty(t2.AssignedKataPerformances);
    }

    [Fact]
    public void SwitchTatamiMode_ShouldToggleCurrentDisciplineWithoutModifyingConfiguredDiscipline()
    {
        var service = CreateTournamentService();
        service.UpdateTatami(2, "Tatami 2", CompetitionType.Both);
        var t = service.GetTatami(2);
        Assert.NotNull(t);

        service.SwitchTatamiMode(2, CompetitionType.Kata);
        Assert.Equal(CompetitionType.Both, t.Discipline);
        Assert.Equal(CompetitionType.Kata, t.CurrentDiscipline);

        service.SwitchTatamiMode(2, CompetitionType.Kumite);
        Assert.Equal(CompetitionType.Both, t.Discipline);
        Assert.Equal(CompetitionType.Kumite, t.CurrentDiscipline);
    }

    [Fact]
    public void SetMatchDay_And_SetKataDay_ShouldUpdateDayAndRenumber()
    {
        var service = CreateTournamentService();
        service.SeedSampleData();

        var match = service.GetMatches().First(m => !m.IsBye);
        int initialDay = match.CompetitionDay;
        int targetDay = initialDay == 1 ? 2 : 1;

        service.SetMatchDay(match.Id, targetDay);
        var updatedMatch = service.GetMatch(match.Id);
        Assert.NotNull(updatedMatch);
        Assert.Equal(targetDay, updatedMatch.CompetitionDay);

        var kata = service.GetKataPerformances().First();
        int kataInitialDay = kata.CompetitionDay;
        int kataTargetDay = kataInitialDay == 1 ? 2 : 1;

        service.SetKataDay(kata.Id, kataTargetDay);
        var updatedKata = service.GetKataPerformances().First(p => p.Id == kata.Id);
        Assert.Equal(kataTargetDay, updatedKata.CompetitionDay);
    }

    [Fact]
    public void SetCategoryOrderIndex_ShouldUpdateOrderAndRenumberMatchesChronologically()
    {
        var service = CreateTournamentService();

        // Créer 2 catégories Kumité le Jour 1 avec 2 compétiteurs chacune (1 combat par catégorie)
        var catA = new Category { Id = 101, Name = "Catégorie A", Type = CompetitionType.Kumite, CompetitionDay = 1, OrderIndex = 1 };
        catA.Competitors.Add(new Competitor { Id = 1, LastName = "A1", IsKumiteRegistered = true });
        catA.Competitors.Add(new Competitor { Id = 2, LastName = "A2", IsKumiteRegistered = true });

        var catB = new Category { Id = 102, Name = "Catégorie B", Type = CompetitionType.Kumite, CompetitionDay = 1, OrderIndex = 2 };
        catB.Competitors.Add(new Competitor { Id = 3, LastName = "B1", IsKumiteRegistered = true });
        catB.Competitors.Add(new Competitor { Id = 4, LastName = "B2", IsKumiteRegistered = true });

        service.GetCategories().Add(catA);
        service.GetCategories().Add(catB);
        service.GenerateAllBrackets();

        var matchA = service.GetMatches().First(m => m.CategoryId == 101 && !m.IsBye);
        var matchB = service.GetMatches().First(m => m.CategoryId == 102 && !m.IsBye);

        // Cat A (OrderIndex 1) doit passer avant Cat B (OrderIndex 2)
        Assert.True(matchA.MatchNumber < matchB.MatchNumber, $"Match A (#{matchA.MatchNumber}) doit être avant Match B (#{matchB.MatchNumber})");

        // Inverser l'ordre : Cat A devient 2, Cat B devient 1
        service.SetCategoryOrderIndex(101, 2);
        service.SetCategoryOrderIndex(102, 1);

        matchA = service.GetMatch(matchA.Id)!;
        matchB = service.GetMatch(matchB.Id)!;

        // Maintenant Cat B doit passer avant Cat A
        Assert.True(matchB.MatchNumber < matchA.MatchNumber, $"Après inversion, Match B (#{matchB.MatchNumber}) doit être avant Match A (#{matchA.MatchNumber})");
    }

    [Fact]
    public void MoveCategoryOrder_ShouldSwapOrderIndexWithAdjacentCategory()
    {
        var service = CreateTournamentService();

        var cat1 = new Category { Id = 201, Name = "Cat 1", Type = CompetitionType.Kumite, CompetitionDay = 1, OrderIndex = 1 };
        var cat2 = new Category { Id = 202, Name = "Cat 2", Type = CompetitionType.Kumite, CompetitionDay = 1, OrderIndex = 2 };
        var cat3 = new Category { Id = 203, Name = "Cat 3", Type = CompetitionType.Kumite, CompetitionDay = 1, OrderIndex = 3 };

        service.GetCategories().Add(cat1);
        service.GetCategories().Add(cat2);
        service.GetCategories().Add(cat3);

        // Déplacer Cat 1 vers le bas (+1) -> doit échanger avec Cat 2
        service.MoveCategoryOrder(201, 1);

        Assert.Equal(2, service.GetCategory(201)!.OrderIndex);
        Assert.Equal(1, service.GetCategory(202)!.OrderIndex);

        // Déplacer Cat 1 encore vers le bas (+1) -> doit échanger avec Cat 3
        service.MoveCategoryOrder(201, 1);

        Assert.Equal(3, service.GetCategory(201)!.OrderIndex);
        Assert.Equal(2, service.GetCategory(203)!.OrderIndex);
    }

    [Fact]
    public void ExcelImport_WithOrderColumn_ShouldAssignOrderIndexToCategories()
    {
        var excelService = new ExcelTournamentService();
        var templateBytes = excelService.GenerateSampleExcelTemplate();
        using var ms = new MemoryStream(templateBytes);
        var result = excelService.ImportTournamentData(ms);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Categories);

        // Vérifier que chaque catégorie a bien son OrderIndex configuré
        Assert.All(result.Categories, c => Assert.True(c.OrderIndex > 0));

        var catH75 = result.Categories.First(c => c.Name == "Senior Hommes -75kg");
        Assert.Equal(1, catH75.OrderIndex);
    }
}
