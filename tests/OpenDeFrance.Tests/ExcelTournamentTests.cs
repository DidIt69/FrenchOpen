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
    }
}
