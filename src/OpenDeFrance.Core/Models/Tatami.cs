using OpenDeFrance.Core.Enums;

namespace OpenDeFrance.Core.Models;

public class Tatami
{
    public int Id { get; set; }
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public CompetitionType Discipline { get; set; } = CompetitionType.Both; // Kumite, Kata ou Both (les deux)
    public CompetitionType CurrentDiscipline { get; set; } = CompetitionType.Kumite; // Mode actuellement actif sur le tatami

    public int? CurrentKumiteMatchId { get; set; }
    public KumiteMatch? CurrentKumiteMatch { get; set; }

    public int? CurrentKataPerformanceId { get; set; }
    public KataPerformance? CurrentKataPerformance { get; set; }

    public List<KumiteMatch> AssignedKumiteMatches { get; set; } = new();
    public List<KataPerformance> AssignedKataPerformances { get; set; } = new();

    public bool IsPaused { get; set; } = false;
    public string? PauseMessage { get; set; }
}
