namespace OpenDeFrance.Core.Enums;

public enum CompetitionType
{
    Kumite = 0,
    Kata = 1,
    Both = 2
}

public enum Gender
{
    Male,
    Female,
    Mixed
}

public enum BeltGrade
{
    White       = 0,
    Orange      = 1,
    Orange_2    = 2,
    Blue        = 3,
    Blue_2      = 4,
    Yellow      = 5,
    Yellow_2    = 6,
    Green       = 7,
    Green_2     = 8,
    Brown       = 9,
    Brown_2     = 10,
    Shodan      = 11,
    Nidan       = 12,
    Sandan      = 13,
    Yondan      = 14,
    Godan       = 15
}

public enum MatchStatus
{
    Pending,
    Ready,
    InProgress,
    Paused,
    Finished
}

public enum CornerColor
{
    Aka,   // Rouge
    Shiro  // Blanc
}

public enum MatchWinnerReason
{
    None,
    Ippon,
    AwaseIppon,          // Deux Waza-ari
    WazaAriDecision,     // Avantage Waza-ari
    Hantei,              // Décision des arbitres (drapeaux)
    GentenDisqualification, // Hansoku (fautes répétées / disqualification)
    Kiken,               // Abandon / Blessure / Forfait
    Shikkaku             // Disqualification disciplinaire
}

public enum KataRound
{
    Turn1 = 1,
    Turn2 = 2
}

public enum CompetitorStatus
{
    Absent = 0,
    Present = 1,
    Eliminated = 2
}
