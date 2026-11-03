using Microsoft.Extensions.DependencyInjection;
using OpenDeFrance.Core.Enums;
using OpenDeFrance.Core.Models;
using OpenDeFrance.Core.Services;
using OpenDeFrance.Infrastructure.Services;

namespace OpenDeFrance.Tests;

public class CompetitorManagementTests
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
    public void UpdateCompetitor_ShouldModifyAllFieldsAndAssociatedCategories()
    {
        var service = CreateTournamentService();

        var cat1 = new Category { Id = 1, Name = "Kumite Hommes -75kg", Type = CompetitionType.Kumite };
        var cat2 = new Category { Id = 2, Name = "Kata Hommes", Type = CompetitionType.Kata };
        service.GetCategories().Add(cat1);
        service.GetCategories().Add(cat2);

        var competitor = new Competitor
        {
            Id = 1,
            BibNumber = 10,
            FirstName = "Jean",
            LastName = "Dupont",
            Country = "France",
            Club = "Dojo Paris",
            BirthDate = new DateTime(1995, 5, 20),
            Age = 31,
            Weight = 74.5,
            Grade = BeltGrade.Shodan,
            Gender = Gender.Male,
            IsKumiteRegistered = true,
            IsKataRegistered = false
        };
        service.GetCompetitors().Add(competitor);
        cat1.Competitors.Add(competitor);
        competitor.Categories.Add(cat1);

        // Modifier l'ensemble des informations
        var updated = new Competitor
        {
            Id = 1,
            BibNumber = 42,
            FirstName = "Jean-Pierre",
            LastName = "Durand",
            Country = "Espagne",
            Club = "IKO Madrid",
            BirthDate = new DateTime(1990, 10, 15),
            Age = 36,
            Weight = 78.2,
            Grade = BeltGrade.Nidan,
            Gender = Gender.Male,
            IsKumiteRegistered = true,
            IsKataRegistered = true
        };

        // Affecter aux deux catégories (cat1 et cat2)
        service.UpdateCompetitor(updated, new List<int> { 1, 2 });

        var compInService = service.GetCompetitors().First(c => c.Id == 1);
        Assert.Equal(42, compInService.BibNumber);
        Assert.Equal("Jean-Pierre", compInService.FirstName);
        Assert.Equal("Durand", compInService.LastName);
        Assert.Equal("Espagne", compInService.Country);
        Assert.Equal("IKO Madrid", compInService.Club);
        Assert.Equal(36, compInService.Age);
        Assert.Equal(78.2, compInService.Weight);
        Assert.Equal(BeltGrade.Nidan, compInService.Grade);
        Assert.True(compInService.IsKumiteRegistered);
        Assert.True(compInService.IsKataRegistered);

        // Vérifier l'affectation aux catégories
        Assert.Contains(cat1.Competitors, c => c.Id == 1);
        Assert.Contains(cat2.Competitors, c => c.Id == 1);
        Assert.Contains(compInService.Categories, c => c.Id == 1);
        Assert.Contains(compInService.Categories, c => c.Id == 2);
    }

    [Fact]
    public void AddCompetitor_And_DeleteCompetitor_ShouldManageCompetitorCollection()
    {
        var service = CreateTournamentService();

        var cat = new Category { Id = 10, Name = "Open Kumite", Type = CompetitionType.Kumite };
        service.GetCategories().Add(cat);

        var newComp = new Competitor
        {
            BibNumber = 99,
            FirstName = "Taro",
            LastName = "Yamada",
            Country = "Japon",
            Club = "Honbu Tokyo",
            Age = 25,
            Weight = 80.0,
            Grade = BeltGrade.Sandan,
            Gender = Gender.Male,
            IsKumiteRegistered = true
        };

        var added = service.AddCompetitor(newComp, new List<int> { 10 });
        Assert.True(added.Id > 0);
        Assert.Contains(service.GetCompetitors(), c => c.Id == added.Id);
        Assert.Contains(cat.Competitors, c => c.Id == added.Id);

        // Supprimer le compétiteur
        bool deleted = service.DeleteCompetitor(added.Id);
        Assert.True(deleted);
        Assert.DoesNotContain(service.GetCompetitors(), c => c.Id == added.Id);
        Assert.DoesNotContain(cat.Competitors, c => c.Id == added.Id);
    }
}
