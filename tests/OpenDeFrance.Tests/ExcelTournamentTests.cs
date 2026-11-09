using OpenDeFrance.Core.Enums;
using OpenDeFrance.Infrastructure.Services;

namespace OpenDeFrance.Tests;

public class ExcelTournamentTests
{
    private readonly ExcelTournamentService _service = new();

    [Fact]
    public void GenerateSampleExcelTemplate_AndImport_ShouldSucceed()
    {
        // Act : Générer le template
        var bytes = _service.GenerateSampleExcelTemplate();
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        // Act : Ré-importer le template généré
        using var ms = new MemoryStream(bytes);
        var result = _service.ImportTournamentData(ms);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Competitors.Count >= 10);
        Assert.True(result.Categories.Count >= 2);

        // Vérifier les catégories Kumite et Kata
        Assert.Contains(result.Categories, c => c.Type == CompetitionType.Kumite);
        Assert.Contains(result.Categories, c => c.Type == CompetitionType.Kata);

        // Vérifier le parsing des ceintures Kyokushin
        Assert.Contains(result.Competitors, c => c.Grade == BeltGrade.Shodan || c.Grade == BeltGrade.Nidan);

        // Vérifier l'importation de l'âge min/max et du poids min/max
        var senior75 = result.Categories.FirstOrDefault(c => c.Name == "Senior Hommes -75kg");
        Assert.NotNull(senior75);
        Assert.Equal(18, senior75.MinAge);
        Assert.Equal(35, senior75.MaxAge);
        Assert.Equal(75.0, senior75.EffectiveMaxWeight);

        var senior80Plus = result.Categories.FirstOrDefault(c => c.Name == "Senior Hommes +80kg");
        Assert.NotNull(senior80Plus);
        Assert.Equal(18, senior80Plus.MinAge);
        Assert.Equal(35, senior80Plus.MaxAge);
        Assert.Equal(80.0, senior80Plus.EffectiveMinWeight);
    }

    [Fact]
    public void ImportTournamentData_WithExplicitAgeAndWeightLimits_ShouldApplyToCategories()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        
        var wsComp = workbook.Worksheets.Add("Compétiteurs");
        wsComp.Cell(1, 1).Value = "Dossard";
        wsComp.Cell(1, 2).Value = "Nom";
        wsComp.Cell(1, 3).Value = "Prénom";
        wsComp.Cell(1, 4).Value = "Catégorie Kumite";
        wsComp.Cell(2, 1).Value = 1;
        wsComp.Cell(2, 2).Value = "TEST";
        wsComp.Cell(2, 3).Value = "Jean";
        wsComp.Cell(2, 4).Value = "Cadets -55kg";

        var wsCat = workbook.Worksheets.Add("Catégories");
        wsCat.Cell(1, 1).Value = "Catégorie";
        wsCat.Cell(1, 2).Value = "Discipline";
        wsCat.Cell(1, 3).Value = "Jour";
        wsCat.Cell(1, 4).Value = "Tatami";
        wsCat.Cell(1, 5).Value = "Ordre";
        wsCat.Cell(1, 6).Value = "Age Min";
        wsCat.Cell(1, 7).Value = "Age Max";
        wsCat.Cell(1, 8).Value = "Poids Min (kg)";
        wsCat.Cell(1, 9).Value = "Poids Max (kg)";

        wsCat.Cell(2, 1).Value = "Cadets -55kg";
        wsCat.Cell(2, 2).Value = "Kumite";
        wsCat.Cell(2, 3).Value = 1;
        wsCat.Cell(2, 4).Value = 1;
        wsCat.Cell(2, 5).Value = 1;
        wsCat.Cell(2, 6).Value = 14;
        wsCat.Cell(2, 7).Value = 15;
        wsCat.Cell(2, 8).Value = 45.0;
        wsCat.Cell(2, 9).Value = 55.0;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var result = _service.ImportTournamentData(ms);

        Assert.True(result.Success);
        var cat = Assert.Single(result.Categories);
        Assert.Equal("Cadets -55kg", cat.Name);
        Assert.Equal(14, cat.MinAge);
        Assert.Equal(15, cat.MaxAge);
        Assert.Equal(45.0, cat.MinWeight);
        Assert.Equal(55.0, cat.MaxWeight);
        Assert.Equal(45.0, cat.EffectiveMinWeight);
        Assert.Equal(55.0, cat.EffectiveMaxWeight);
        Assert.Equal("45 à 55 kg", cat.WeightLimitDisplay);
        Assert.Equal("14 à 15 ans", cat.AgeLimitDisplay);
    }
}
