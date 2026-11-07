using OpenDeFrance.Core.Enums;

namespace OpenDeFrance.Core.Models;

public class KataPerformance
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public int? TatamiId { get; set; }
    public Tatami? Tatami { get; set; }

    public int CompetitionDay { get; set; } = 1; // Jour de la compétition (1, 2, etc.)
    public int? CompetitorId { get; set; }
    public Competitor? Competitor { get; set; }

    public KataRound Round { get; set; } = KataRound.Turn1;
    public string KataName { get; set; } = string.Empty;
    public int PerformanceOrder { get; set; } = 1;
    public int GlobalMatchNumber { get; set; } = 0;

    public string CompetitorDisplayName => Competitor != null 
        ? $"{(Competitor.BibNumber.HasValue ? $"#{Competitor.BibNumber.Value} " : "")}{Competitor.FullName}"
        : "En attente qualification Tour 1";

    // Notes des juges (5 juges entre 55 et 100 en nombres entiers)
    public List<double> JudgeScores { get; set; } = new();

    // Notes exclues (la plus haute et la plus basse)
    public double? DroppedLowestScore { get; set; }
    public double? DroppedHighestScore { get; set; }

    // Note finale calculée (somme des notes médianes en nombres entiers)
    public double FinalScore { get; set; } = 0;
    public int? RankInRound { get; set; }
    public bool IsQualifiedForTour2 { get; set; } = false;

    public MatchStatus Status { get; set; } = MatchStatus.Pending;

    public string ScoresDisplay => GetScoresDisplay();

    public string GetScoresDisplay(OpenDeFrance.Core.Services.ILanguageService? lang = null)
    {
        if (JudgeScores.Any(s => s > 0))
            return string.Join(" | ", JudgeScores.Select(s => s > 0 ? s.ToString("0") : "-"));

        return lang?["NoScore"] ?? "-";
    }
}
