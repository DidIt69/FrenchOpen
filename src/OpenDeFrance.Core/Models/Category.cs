using OpenDeFrance.Core.Enums;

namespace OpenDeFrance.Core.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CompetitionType Type { get; set; } = CompetitionType.Kumite;
    public Gender Gender { get; set; } = Gender.Male;
    public int MinAge { get; set; } = 18;
    public int MaxAge { get; set; } = 35;
    public double? MinWeight { get; set; }
    public double? MaxWeight { get; set; }

    // Multi-jours & Planification Tatami
    public int CompetitionDay { get; set; } = 1; // Jour 1, Jour 2, etc.
    public int OrderIndex { get; set; } = 0; // Ordre de passage de la catégorie (1, 2, 3...)
    public int? AssignedTatamiId { get; set; }
    public Tatami? AssignedTatami { get; set; }

    // Paramètres Kumite (dépendant de la catégorie d'âge)
    public int KumiteRegularDurationSeconds { get; set; } = 120; // 2 minutes standard Kyokushin (ou 1m30 pour jeunes, 3m pour élite)
    public int KumiteExtensionDurationSeconds { get; set; } = 120; // 2 min ou 1 min de prolongation (Encho-sen)
    public int MaxExtensionsAllowed { get; set; } = 2; // Prolongation standard + Sai-encho-sen (poids/décision)
    public bool HasBronzeMatch { get; set; } = false; // Petite finale pour la 3e place (les perdants des demi-finales se rencontrent)

    // Paramètres Kata (katas imposés suivant les âges et les tours)
    public string KataRound1MandatoryKata { get; set; } = "Pinan Sono Ichi";
    public string KataRound2MandatoryKata { get; set; } = "Gekisai Dai";
    public int KataQualifiersCount { get; set; } = 4; // 4 ou 5 qualifiés pour le tour 2
    public int KataJudgesCount { get; set; } = 5; // Généralement 5 juges (élimination note haute + note basse)

    public List<Competitor> Competitors { get; set; } = new();
    public List<KumiteMatch> Matches { get; set; } = new();
    public List<KataPerformance> KataPerformances { get; set; } = new();

    public string DisplayDetails => Type == CompetitionType.Kumite
        ? $"{Name} - {KumiteRegularDurationSeconds}s (Prolong: {KumiteExtensionDurationSeconds}s)"
        : $"{Name} - T1: {KataRound1MandatoryKata} | T2: {KataRound2MandatoryKata} (Top {KataQualifiersCount})";
}
