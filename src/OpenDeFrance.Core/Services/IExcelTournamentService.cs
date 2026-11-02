using OpenDeFrance.Core.Models;

namespace OpenDeFrance.Core.Services;

public class ExcelImportResult
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public List<Competitor> Competitors { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public int DaysCount { get; set; } = 1;
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public interface IExcelTournamentService
{
    byte[] GenerateSampleExcelTemplate();
    ExcelImportResult ImportTournamentData(Stream excelStream);
}
