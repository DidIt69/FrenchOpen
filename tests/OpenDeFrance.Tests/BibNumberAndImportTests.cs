using OpenDeFrance.Core.Enums;
using OpenDeFrance.Core.Models;
using OpenDeFrance.Infrastructure.Services;

namespace OpenDeFrance.Tests;

public class BibNumberAndImportTests
{
    [Fact]
    public void Competitor_BibDisplayAndDisplayInfo_ShouldFormatCorrectly()
    {
        var compWithBib = new Competitor
        {
            Id = 1,
            BibNumber = 101,
            LastName = "Dupont",
            FirstName = "Jean",
            Country = "France",
            Club = "Dojo Paris",
            Grade = BeltGrade.Shodan,
            Weight = 75.0
        };

        Assert.Equal("#101", compWithBib.BibDisplay);
        Assert.Contains("[N°101]", compWithBib.DisplayInfo);
        Assert.Contains("DUPONT Jean", compWithBib.DisplayInfo);
        Assert.Contains("🇫🇷 France", compWithBib.DisplayInfo);

        var compWithoutBib = new Competitor
        {
            Id = 2,
            LastName = "Martin",
            FirstName = "Paul",
            Country = "France",
            Club = "Dojo Lyon",
            Grade = BeltGrade.Brown_2,
            Weight = 70.0
        };

        Assert.Equal("", compWithoutBib.BibDisplay);
        Assert.DoesNotContain("[N°", compWithoutBib.DisplayInfo);
        Assert.Contains("MARTIN Paul", compWithoutBib.DisplayInfo);
    }

    [Fact]
    public void KumiteMatch_DisplayName_ShouldIncludeBibNumber()
    {
        var match = new KumiteMatch
        {
            Id = 1,
            AkaCompetitor = new Competitor { Id = 1, BibNumber = 101, LastName = "Dupont", FirstName = "Jean" },
            ShiroCompetitor = new Competitor { Id = 2, BibNumber = 102, LastName = "Sanchez", FirstName = "Carlos" }
        };

        Assert.Equal("#101 DUPONT Jean", match.AkaDisplayName);
        Assert.Equal("#102 SANCHEZ Carlos", match.ShiroDisplayName);
    }

    [Fact]
    public void KataPerformance_DisplayName_ShouldIncludeBibNumber()
    {
        var perf = new KataPerformance
        {
            Id = 1,
            Competitor = new Competitor { Id = 3, BibNumber = 205, LastName = "Tani", FirstName = "Kenji" }
        };

        Assert.Equal("#205 TANI Kenji", perf.CompetitorDisplayName);
    }

    [Fact]
    public void ExcelService_GenerateAndImport_ShouldPreserveBibNumbers()
    {
        var excelService = new ExcelTournamentService();

        // Génère le template avec dossards (101, 102...)
        var templateBytes = excelService.GenerateSampleExcelTemplate();
        Assert.NotNull(templateBytes);
        Assert.True(templateBytes.Length > 0);

        // Importe les données
        using var ms = new MemoryStream(templateBytes);
        var result = excelService.ImportTournamentData(ms);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Competitors);

        // Vérifier que tous les compétiteurs ont bien un numéro de dossard
        foreach (var c in result.Competitors)
        {
            Assert.True(c.BibNumber.HasValue, $"Le compétiteur {c.FullName} n'a pas de dossard");
            Assert.True(c.BibNumber > 0);
        }

        var first = result.Competitors.First(c => c.LastName == "DUPONT");
        Assert.Equal(101, first.BibNumber);
    }
}
