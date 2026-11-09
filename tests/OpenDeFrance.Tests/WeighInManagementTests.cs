using Microsoft.Extensions.DependencyInjection;
using OpenDeFrance.Core.Enums;
using OpenDeFrance.Core.Models;
using OpenDeFrance.Core.Services;
using OpenDeFrance.Infrastructure.Services;

namespace OpenDeFrance.Tests;

public class WeighInManagementTests
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
    public void CategoryWeightLimitParsing_ShouldDetectLimitsAccurately()
    {
        var catMinus = new Category { Name = "Kumite Seniors Hommes -75kg", Type = CompetitionType.Kumite };
        Assert.Equal(75, catMinus.EffectiveMaxWeight);
        Assert.Null(catMinus.EffectiveMinWeight);
        Assert.Equal("-75 kg", catMinus.WeightLimitDisplay);

        var catPlus = new Category { Name = "Kumite Seniors Hommes +85kg", Type = CompetitionType.Kumite };
        Assert.Null(catPlus.EffectiveMaxWeight);
        Assert.Equal(85, catPlus.EffectiveMinWeight);
        Assert.Equal("+85 kg", catPlus.WeightLimitDisplay);

        var catRange = new Category { Name = "Kumite Juniors 65-70kg", Type = CompetitionType.Kumite };
        Assert.Equal(65, catRange.EffectiveMinWeight);
        Assert.Equal(70, catRange.EffectiveMaxWeight);
        Assert.Equal("65 à 70 kg", catRange.WeightLimitDisplay);

        var catKata = new Category { Name = "Kata Seniors Hommes", Type = CompetitionType.Kata };
        Assert.Equal("Sans limite (Kata)", catKata.WeightLimitDisplay);

        var catOpen = new Category { Name = "Kumite Toutes Catégories Open", Type = CompetitionType.Kumite };
        Assert.Equal("Open (Toutes catégories)", catOpen.WeightLimitDisplay);
    }

    [Fact]
    public void CompetitorQrCode_ShouldGenerateValidSvg()
    {
        var comp = new Competitor
        {
            Id = 42,
            BibNumber = 105,
            FirstName = "Kenji",
            LastName = "Sato"
        };

        Assert.Equal("105", comp.QrCodePayload);
        var svg = comp.GetQrCodeSvg(10);

        Assert.NotNull(svg);
        Assert.Contains("<svg", svg);
        Assert.Contains("</svg>", svg);
    }

    [Fact]
    public void FindCompetitorByCode_ShouldSupportMultipleFormats()
    {
        var service = CreateTournamentService();

        var comp1 = new Competitor { Id = 1, BibNumber = 101, FirstName = "Taro", LastName = "Yamada" };
        var comp2 = new Competitor { Id = 2, BibNumber = 102, FirstName = "Pierre", LastName = "Dupont" };
        service.GetCompetitors().Add(comp1);
        service.GetCompetitors().Add(comp2);

        // 1. Par numéro de dossard direct
        var found1 = service.FindCompetitorByCode("101");
        Assert.NotNull(found1);
        Assert.Equal(101, found1.BibNumber);

        // 2. Par préfixe BIB:
        var found2 = service.FindCompetitorByCode("BIB:102");
        Assert.NotNull(found2);
        Assert.Equal(102, found2.BibNumber);

        // 3. Par #
        var found3 = service.FindCompetitorByCode("#101");
        Assert.NotNull(found3);
        Assert.Equal(101, found3.BibNumber);

        // 4. Par format custom KYOKUSHIN:ID:BIB
        var found4 = service.FindCompetitorByCode("KYOKUSHIN:2:102");
        Assert.NotNull(found4);
        Assert.Equal(2, found4.Id);

        // 5. Par URL contenant bib=
        var found5 = service.FindCompetitorByCode("https://openkyokushin.fr/weighin?bib=101");
        Assert.NotNull(found5);
        Assert.Equal(101, found5.BibNumber);

        // 6. Par nom
        var found6 = service.FindCompetitorByCode("Yamada");
        Assert.NotNull(found6);
        Assert.Equal("Yamada", found6.LastName);

        // Inexistant
        var notFound = service.FindCompetitorByCode("9999");
        Assert.Null(notFound);
    }

    [Fact]
    public void ScanAndValidateWeighIn_ShouldUpdateCompetitorAndStationState()
    {
        var service = CreateTournamentService();

        var cat = new Category { Id = 1, Name = "Kumite Hommes -75kg", Type = CompetitionType.Kumite, MaxWeight = 75 };
        service.GetCategories().Add(cat);

        var comp = new Competitor
        {
            Id = 10,
            BibNumber = 201,
            FirstName = "Marc",
            LastName = "Dubois",
            Weight = 73.0,
            Status = CompetitorStatus.Absent
        };
        service.GetCompetitors().Add(comp);
        cat.Competitors.Add(comp);
        comp.Categories.Add(cat);

        // 1. Scanner le compétiteur sur le Poste 1
        var state = service.ScanCompetitorForWeighIn(1, "201");
        Assert.NotNull(state.Competitor);
        Assert.Equal(WeighInStatus.Checking, state.Status);
        Assert.True(state.IsWeightValid);
        Assert.Equal(73.0, state.WeighedWeight);

        // 2. Mettre à jour le poids mesuré à 74.2 kg (conforme car <= 75kg)
        service.UpdateWeighedWeight(1, 74.2);
        state = service.GetWeighInStation(1);
        Assert.Equal(74.2, state.WeighedWeight);
        Assert.True(state.IsWeightValid);

        // 3. Valider la pesée
        service.ValidateWeighIn(1, 74.2);
        state = service.GetWeighInStation(1);
        Assert.Equal(WeighInStatus.Validated, state.Status);

        // Vérifier l'athlète mis à jour
        Assert.True(comp.IsWeighInValidated);
        Assert.Equal(74.2, comp.ActualWeight);
        Assert.NotNull(comp.WeighInDate);
        Assert.Equal("Poste 1", comp.WeighInStation);
        Assert.Equal(CompetitorStatus.Present, comp.Status); // Devient Présent !

        // Vérifier liste des pesées récentes
        var recent = service.GetRecentWeighedCompetitors(5);
        Assert.Contains(recent, c => c.Id == 10);
    }

    [Fact]
    public void OverweightCompetitor_ShouldBeDetectedAsNonCompliant()
    {
        var service = CreateTournamentService();

        var cat = new Category { Id = 2, Name = "Kumite Seniors -70kg", Type = CompetitionType.Kumite, MaxWeight = 70 };
        service.GetCategories().Add(cat);

        var comp = new Competitor
        {
            Id = 20,
            BibNumber = 301,
            FirstName = "Luc",
            LastName = "Moreau",
            Weight = 69.5
        };
        service.GetCompetitors().Add(comp);
        comp.Categories.Add(cat);

        service.ScanCompetitorForWeighIn(1, "301");

        // Peser à 71.3 kg (surcharge de 1.3 kg)
        service.UpdateWeighedWeight(1, 71.3);
        var state = service.GetWeighInStation(1);

        Assert.False(state.IsWeightValid);
        Assert.Equal(1.3, state.WeightDifference);

        // Rejeter
        service.RejectWeighIn(1, "Hors poids de 1.3 kg");
        state = service.GetWeighInStation(1);
        Assert.Equal(WeighInStatus.Rejected, state.Status);
    }

    [Fact]
    public void MultiStation_ShouldOperateIndependently()
    {
        var service = CreateTournamentService();

        var comp1 = new Competitor { Id = 101, BibNumber = 501, FirstName = "Athlète", LastName = "Un", Weight = 70 };
        var comp2 = new Competitor { Id = 102, BibNumber = 502, FirstName = "Athlète", LastName = "Deux", Weight = 80 };
        service.GetCompetitors().Add(comp1);
        service.GetCompetitors().Add(comp2);

        // Poste 1 scanne comp1
        var state1 = service.ScanCompetitorForWeighIn(1, "501");
        // Poste 2 scanne comp2
        var state2 = service.ScanCompetitorForWeighIn(2, "502");

        Assert.Equal(101, state1.Competitor?.Id);
        Assert.Equal(102, state2.Competitor?.Id);

        // Valider Poste 1
        service.ValidateWeighIn(1, 70.0);
        Assert.Equal(WeighInStatus.Validated, service.GetWeighInStation(1).Status);
        Assert.Equal(WeighInStatus.Checking, service.GetWeighInStation(2).Status); // Poste 2 reste en Checking !

        // Réinitialiser Poste 1
        service.ResetWeighInStation(1);
        Assert.Equal(WeighInStatus.Idle, service.GetWeighInStation(1).Status);
        Assert.Equal(WeighInStatus.Checking, service.GetWeighInStation(2).Status); // Poste 2 toujours intact
    }

    [Fact]
    public void CancelWeighInValidation_ShouldResetCompetitorToAbsent()
    {
        var service = CreateTournamentService();

        var comp = new Competitor
        {
            Id = 300,
            BibNumber = 888,
            FirstName = "Jean",
            LastName = "Test",
            Status = CompetitorStatus.Absent
        };
        service.GetCompetitors().Add(comp);

        service.ScanCompetitorForWeighIn(1, "888");
        service.ValidateWeighIn(1, 75.0);

        Assert.True(comp.IsWeighInValidated);
        Assert.Equal(CompetitorStatus.Present, comp.Status);

        // Annuler
        service.CancelWeighInValidation(300);

        Assert.False(comp.IsWeighInValidated);
        Assert.Null(comp.ActualWeight);
        Assert.Equal(CompetitorStatus.Absent, comp.Status);
    }
}
