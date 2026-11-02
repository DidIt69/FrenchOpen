using OpenDeFrance.Core.Enums;

namespace OpenDeFrance.Core.Models;

public class KumiteMatch
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public int? TatamiId { get; set; }
    public Tatami? Tatami { get; set; }

    public int CompetitionDay { get; set; } = 1; // Jour de la compétition (1, 2, etc.)
    public int MatchNumber { get; set; }
    public int BracketRound { get; set; } // 1 = 1er tour, 2 = Quarts, 3 = Demis, 4 = Finale, etc.
    public int RoundFromFinal { get; set; } // 0 = Finale, 1 = Demi-Finale, 2 = Quart, 3 = 8e, 4 = 16e, etc.
    public string RoundName { get; set; } = string.Empty;

    // Aka (Rouge)
    public int? AkaCompetitorId { get; set; }
    public Competitor? AkaCompetitor { get; set; }
    public int AkaWazaAri { get; set; } = 0;
    public int AkaIppon { get; set; } = 0;
    public int AkaChui { get; set; } = 0;   // Avertissements
    public int AkaGenten { get; set; } = 0; // Pénalités (2 Genten = Disqualification Hansoku)

    // Shiro (Blanc)
    public int? ShiroCompetitorId { get; set; }
    public Competitor? ShiroCompetitor { get; set; }
    public int ShiroWazaAri { get; set; } = 0;
    public int ShiroIppon { get; set; } = 0;
    public int ShiroChui { get; set; } = 0;
    public int ShiroGenten { get; set; } = 0;

    // Statut & Vainqueur
    public MatchStatus Status { get; set; } = MatchStatus.Pending;
    public int? WinnerCompetitorId { get; set; }
    public Competitor? WinnerCompetitor { get; set; }
    public MatchWinnerReason WinnerReason { get; set; } = MatchWinnerReason.None;

    // Chronomètre & Prolongations (Encho-sen)
    public int TotalDurationSeconds { get; set; } = 120;
    public int RemainingSeconds { get; set; } = 120;
    public bool IsTimerRunning { get; set; } = false;
    public int ExtensionsCount { get; set; } = 0; // 0 = Temps réglementaire, 1 = 1ère prolongation (Encho-sen), 2 = Sai-encho-sen

    // Arbre éliminatoire
    public int? NextMatchId { get; set; }
    public KumiteMatch? NextMatch { get; set; }
    public CornerColor? NextMatchCorner { get; set; }

    // Petite Finale (3e place) pour les perdants des demi-finales
    public bool IsBronzeMatch { get; set; } = false;
    public int? BronzeMatchId { get; set; }
    public KumiteMatch? BronzeMatch { get; set; }
    public CornerColor? BronzeMatchCorner { get; set; }

    public bool IsBye => (AkaCompetitorId.HasValue && !ShiroCompetitorId.HasValue) || (!AkaCompetitorId.HasValue && ShiroCompetitorId.HasValue);
    public string AkaDisplayName => AkaCompetitor != null 
        ? $"{(AkaCompetitor.BibNumber.HasValue ? $"#{AkaCompetitor.BibNumber.Value} " : "")}{AkaCompetitor.FullName}"
        : (IsBye ? (AkaCompetitorId.HasValue ? "-" : "(Exempt)") : "En attente...");
    public string ShiroDisplayName => ShiroCompetitor != null 
        ? $"{(ShiroCompetitor.BibNumber.HasValue ? $"#{ShiroCompetitor.BibNumber.Value} " : "")}{ShiroCompetitor.FullName}"
        : (IsBye ? (ShiroCompetitorId.HasValue ? "-" : "(Exempt)") : "En attente...");
    public string CurrentPeriodName => ExtensionsCount switch
    {
        0 => "Temps Réglementaire",
        1 => "1ère Prolongation (Encho-sen)",
        2 => "2ème Prolongation (Sai-encho-sen)",
        _ => $"Prolongation #{ExtensionsCount}"
    };
}
