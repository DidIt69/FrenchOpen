using OpenDeFrance.Core.Enums;
using OpenDeFrance.Core.Models;

namespace OpenDeFrance.Core.Services;

public interface IKataTournamentManager
{
    List<KataPerformance> InitializeRound1(Category category, List<Competitor> competitors);
    List<KataPerformance> InitializeRound2Placeholders(Category category, int qualifiersCount);
    void AssignRound2Qualifiers(List<KataPerformance> qualifiedRound1, List<KataPerformance> round2Slots);
    void CalculateScore(KataPerformance performance, List<double> judgeScores);
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
                Round = KataRound.Tour1,
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
                Round = KataRound.Tour2,
                KataName = category.KataRound2MandatoryKata,
                PerformanceOrder = i,
                Status = MatchStatus.Pending
            });
        }
        return list;
    }

    public void AssignRound2Qualifiers(List<KataPerformance> qualifiedRound1, List<KataPerformance> round2Slots)
    {
        var finalists = qualifiedRound1
            .Where(p => p.IsQualifiedForTour2 && p.Competitor != null)
            .OrderBy(p => p.RankInRound)
            .Reverse()
            .ToList();

        for (int i = 0; i < round2Slots.Count && i < finalists.Count; i++)
        {
            round2Slots[i].CompetitorId = finalists[i].CompetitorId;
            round2Slots[i].Competitor = finalists[i].Competitor;
            round2Slots[i].Status = MatchStatus.Pending;
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
    /// Classe les compétiteurs du tour 1 et qualifie les 4 ou 5 meilleurs pour le tour 2
    /// </summary>
    public List<KataPerformance> FinalizeRound1AndQualify(Category category, List<KataPerformance> round1Performances)
    {
        var ranked = round1Performances
            .OrderByDescending(p => p.FinalScore)
            .ThenByDescending(p => p.Competitor != null ? (int)p.Competitor.Grade : 0)
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
    /// Ordre de passage : inverse du classement du tour 1 (le 4e passe en 1er, le 1er passe en dernier).
    /// </summary>
    public List<KataPerformance> InitializeRound2(Category category, List<KataPerformance> qualifiedRound1Performances)
    {
        var finalists = qualifiedRound1Performances
            .Where(p => p.IsQualifiedForTour2 && p.Competitor != null)
            .OrderBy(p => p.RankInRound) // 1er, 2e, 3e, 4e
            .Reverse() // Inverse: 4e, 3e, 2e, 1er
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
                Round = KataRound.Tour2,
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
            .OrderByDescending(p => p.FinalScore)
            .ThenByDescending(p => p.Competitor != null ? (int)p.Competitor.Grade : 0)
            .ToList();

        int rank = 1;
        foreach (var perf in ranked)
        {
            perf.RankInRound = rank++;
        }

        return ranked;
    }
}
