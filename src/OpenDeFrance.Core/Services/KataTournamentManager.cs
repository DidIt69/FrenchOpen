using OpenDeFrance.Core.Enums;
using OpenDeFrance.Core.Models;

namespace OpenDeFrance.Core.Services;

public interface IKataTournamentManager
{
    List<KataPerformance> InitializeRound1(Category category, List<Competitor> competitors);
    List<KataPerformance> InitializeRound2Placeholders(Category category, int qualifiersCount);
    void AssignRound2Qualifiers(List<KataPerformance> qualifiedRound1, List<KataPerformance> round2Slots);
    void CalculateScore(KataPerformance performance, List<double> judgeScores);
    void CalculateLiveScore(KataPerformance performance, List<double> judgeScores);
    List<KataPerformance> FinalizeRound1AndQualify(Category category, List<KataPerformance> round1Performances);
    List<KataPerformance> InitializeRound2(Category category, List<KataPerformance> qualifiedRound1Performances);
    List<KataPerformance> FinalizeRound2(List<KataPerformance> round2Performances);
}

public class KataTournamentManager : IKataTournamentManager
{
    public List<KataPerformance> InitializeRound1(Category category, List<Competitor> competitors)
    {
        if (competitors == null || competitors.Count == 0)
            return new List<KataPerformance>();

        // Tirage de l'ordre de passage du tour 1 :
        // Éloigner les compétiteurs du même pays et du même club pour ne pas qu'ils passent consécutivement
        var countryQueues = competitors
            .DistinctBy(c => c.Id)
            .GroupBy(c => (c.Country ?? "France").Trim().ToUpperInvariant())
            .OrderByDescending(g => g.Count())
            .Select(g => new Queue<Competitor>(g.OrderBy(c => c.Club).ThenBy(c => c.LastName)))
            .ToList();

        var orderedCompetitors = new List<Competitor>();
        while (countryQueues.Any(q => q.Count > 0))
        {
            foreach (var q in countryQueues)
            {
                if (q.Count > 0)
                {
                    orderedCompetitors.Add(q.Dequeue());
                }
            }
        }

        var performances = new List<KataPerformance>();
        int order = 1;

        foreach (var comp in orderedCompetitors)
        {
            performances.Add(new KataPerformance
            {
                CategoryId = category.Id,
                Category = category,
                CompetitorId = comp.Id,
                Competitor = comp,
                Round = KataRound.Turn1,
                KataName = category.KataRound1MandatoryKata,
                PerformanceOrder = order++,
                Status = MatchStatus.Pending
            });
        }

        return performances;
    }

    public List<KataPerformance> InitializeRound2Placeholders(Category category, int count)
    {
        var list = new List<KataPerformance>();
        for (int i = 1; i <= count; i++)
        {
            list.Add(new KataPerformance
            {
                CategoryId = category.Id,
                Category = category,
                Round = KataRound.Turn2,
                KataName = category.KataRound2MandatoryKata,
                PerformanceOrder = i,
                Status = MatchStatus.Pending
            });
        }
        return list;
    }

    /// <summary>
    /// Compare deux performances pour l'ordre de passage du Tour 2 :
    /// Ordre croissant (le compétiteur avec le moins de points passe en premier).
    /// En cas d'égalité sur FinalScore : celui qui a la plus petite note dans les différents juges passe en premier.
    /// Si encore égalité : comparaison note par note en ordre croissant des notes de juges.
    /// </summary>
    public static int CompareForRound2Order(KataPerformance a, KataPerformance b)
    {
        // 1. Note finale (FinalScore) croissant : le plus petit score passe en premier
        int scoreComp = a.FinalScore.CompareTo(b.FinalScore);
        if (scoreComp != 0) return scoreComp;

        // 2. En cas d'ex-aequo : comparer les notes individuelles des juges triées par ordre croissant
        // Celui qui a la plus petite note passe en premier
        var aScores = a.JudgeScores != null && a.JudgeScores.Count > 0 ? a.JudgeScores.OrderBy(s => s).ToList() : new List<double>();
        var bScores = b.JudgeScores != null && b.JudgeScores.Count > 0 ? b.JudgeScores.OrderBy(s => s).ToList() : new List<double>();

        int count = Math.Min(aScores.Count, bScores.Count);
        for (int i = 0; i < count; i++)
        {
            int noteComp = aScores[i].CompareTo(bScores[i]);
            if (noteComp != 0) return noteComp; // plus petite note passe en premier
        }

        int countComp = aScores.Count.CompareTo(bScores.Count);
        if (countComp != 0) return countComp;

        // 3. Dernier recours : Grade (le plus petit grade passe en premier) ou Id
        int aGrade = a.Competitor != null ? (int)a.Competitor.Grade : 0;
        int bGrade = b.Competitor != null ? (int)b.Competitor.Grade : 0;
        int gradeComp = aGrade.CompareTo(bGrade);
        if (gradeComp != 0) return gradeComp;

        return a.Id.CompareTo(b.Id);
    }

    public void AssignRound2Qualifiers(List<KataPerformance> qualifiedRound1, List<KataPerformance> round2Slots)
    {
        var finalists = qualifiedRound1
            .Where(p => p.IsQualifiedForTour2 && (p.Competitor != null || p.CompetitorId.HasValue))
            .OrderBy(p => p, Comparer<KataPerformance>.Create(CompareForRound2Order))
            .ToList();

        for (int i = 0; i < round2Slots.Count; i++)
        {
            if (round2Slots[i].Status == MatchStatus.Finished || round2Slots[i].Status == MatchStatus.InProgress)
            {
                continue;
            }

            if (i < finalists.Count)
            {
                round2Slots[i].CompetitorId = finalists[i].CompetitorId;
                round2Slots[i].Competitor = finalists[i].Competitor;
                round2Slots[i].Status = MatchStatus.Pending;
            }
            else
            {
                round2Slots[i].CompetitorId = null;
                round2Slots[i].Competitor = null;
                round2Slots[i].Status = MatchStatus.Pending;
            }
        }
    }

    /// <summary>
    /// Calcule la note finale selon les règles Kyokushinkai :
    /// Pour 5 juges : notes entières de 55 à 100. La note la plus haute et la plus basse sont supprimées, la note finale est la somme des 3 notes médianes.
    /// Pour 3 juges : somme des 3 notes.
    /// </summary>
    public void CalculateScore(KataPerformance performance, List<double> judgeScores)
    {
        if (judgeScores == null || judgeScores.Count == 0)
        {
            performance.JudgeScores.Clear();
            performance.DroppedLowestScore = null;
            performance.DroppedHighestScore = null;
            performance.FinalScore = 0;
            return;
        }

        // Arrondir chaque note en nombre entier
        performance.JudgeScores = judgeScores.Select(s => Math.Round(s)).ToList();
        var sorted = performance.JudgeScores.OrderBy(s => s).ToList();

        if (sorted.Count >= 5)
        {
            performance.DroppedLowestScore = sorted.First();
            performance.DroppedHighestScore = sorted.Last();

            // Retirer une note min et une note max
            var middleScores = sorted.Skip(1).Take(sorted.Count - 2).ToList();
            performance.FinalScore = Math.Round(middleScores.Sum());
        }
        else if (sorted.Count >= 3)
        {
            performance.DroppedLowestScore = null;
            performance.DroppedHighestScore = null;
            performance.FinalScore = Math.Round(sorted.Sum());
        }
        else
        {
            performance.FinalScore = Math.Round(sorted.Average());
        }

        performance.Status = MatchStatus.Finished;
    }

    /// <summary>
    /// Calcule les notes en temps réel pendant la saisie sans marquer le passage comme terminé.
    /// Les notes nulles ou 0 sont préservées dans la liste pour permettre l'affichage individuel en direct.
    /// </summary>
    public void CalculateLiveScore(KataPerformance performance, List<double> judgeScores)
    {
        if (judgeScores == null || judgeScores.Count == 0)
        {
            performance.JudgeScores.Clear();
            performance.DroppedLowestScore = null;
            performance.DroppedHighestScore = null;
            performance.FinalScore = 0;
            return;
        }

        // Arrondir les notes saisies, conserver 0 pour les juges non saisis
        performance.JudgeScores = judgeScores.Select(s => s > 0 ? Math.Round(s) : 0).ToList();
        var validScores = performance.JudgeScores.Where(s => s >= 50).OrderBy(s => s).ToList();

        if (validScores.Count >= 5)
        {
            performance.DroppedLowestScore = validScores.First();
            performance.DroppedHighestScore = validScores.Last();
            var middleScores = validScores.Skip(1).Take(validScores.Count - 2).ToList();
            performance.FinalScore = Math.Round(middleScores.Sum());
        }
        else if (validScores.Count >= 3)
        {
            performance.DroppedLowestScore = null;
            performance.DroppedHighestScore = null;
            performance.FinalScore = Math.Round(validScores.Sum());
        }
        else
        {
            performance.DroppedLowestScore = null;
            performance.DroppedHighestScore = null;
            performance.FinalScore = 0;
        }

        if (performance.Status == MatchStatus.Pending)
        {
            performance.Status = MatchStatus.InProgress;
        }
    }

    /// <summary>
    /// Classe les compétiteurs du tour 1 et qualifie les N meilleurs pour le tour 2.
    /// Classement du 1er au dernier selon le score et départage par les notes individuelles.
    /// </summary>
    public List<KataPerformance> FinalizeRound1AndQualify(Category category, List<KataPerformance> round1Performances)
    {
        var ranked = round1Performances
            .OrderBy(p => p, Comparer<KataPerformance>.Create((a, b) => -CompareForRound2Order(a, b)))
            .ToList();

        int rank = 1;
        int qualifiersCount = category.KataQualifiersCount > 0 ? category.KataQualifiersCount : 4;

        foreach (var perf in ranked)
        {
            perf.RankInRound = rank;
            perf.IsQualifiedForTour2 = (rank <= qualifiersCount && perf.FinalScore > 0);
            rank++;
        }

        return ranked;
    }

    /// <summary>
    /// Génère le Tour 2 (Finale) avec le Kata imposé du tour 2.
    /// Ordre de passage : le qualifié avec le moins de points (ou plus petite note individuelle) passe en premier.
    /// </summary>
    public List<KataPerformance> InitializeRound2(Category category, List<KataPerformance> qualifiedRound1Performances)
    {
        var finalists = qualifiedRound1Performances
            .Where(p => p.IsQualifiedForTour2 && p.Competitor != null)
            .OrderBy(p => p, Comparer<KataPerformance>.Create(CompareForRound2Order))
            .ToList();

        var round2List = new List<KataPerformance>();
        int order = 1;

        foreach (var r1 in finalists)
        {
            round2List.Add(new KataPerformance
            {
                CategoryId = category.Id,
                Category = category,
                CompetitorId = r1.CompetitorId,
                Competitor = r1.Competitor,
                Round = KataRound.Turn2,
                KataName = category.KataRound2MandatoryKata,
                PerformanceOrder = order++,
                Status = MatchStatus.Pending
            });
        }

        return round2List;
    }

    /// <summary>
    /// Classement final du Tour 2 (Podium)
    /// </summary>
    public List<KataPerformance> FinalizeRound2(List<KataPerformance> round2Performances)
    {
        var ranked = round2Performances
            .OrderBy(p => p, Comparer<KataPerformance>.Create((a, b) => -CompareForRound2Order(a, b)))
            .ToList();

        int rank = 1;
        foreach (var perf in ranked)
        {
            perf.RankInRound = rank++;
        }

        return ranked;
    }
}
