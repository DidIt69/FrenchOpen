using OpenDeFrance.Core.Enums;
using OpenDeFrance.Core.Models;

namespace OpenDeFrance.Core.Services;

public interface IKumiteBracketGenerator
{
    List<KumiteMatch> GenerateBracket(Category category, List<Competitor> competitors, int startingMatchNumber = 1);
}

public class KumiteBracketGenerator : IKumiteBracketGenerator
{
    public List<KumiteMatch> GenerateBracket(Category category, List<Competitor> competitors, int startingMatchNumber = 1)
    {
        if (competitors == null || competitors.Count == 0)
            return new List<KumiteMatch>();

        var list = competitors.DistinctBy(c => c.Id).ToList();
        int n = list.Count;

        // Si 1 seul combattant, pas de combat possible
        if (n == 1)
        {
            return new List<KumiteMatch>();
        }

        // Trouver la puissance de 2 immédiatement supérieure ou égale (ex: 2, 4, 8, 16, 32, 64)
        int bracketSize = 2;
        while (bracketSize < n)
        {
            bracketSize *= 2;
        }

        // Algorithme d'ordonnancement avec contraintes :
        // 1. Éloigner les combattants du même club (dans des moitiés/quarts différents)
        // 2. Rapprocher les combattants proches en terme de poids et de grade
        var placedSlots = PlaceCompetitors(list, bracketSize);

        // Construction de l'arbre éliminatoire
        var matches = new List<KumiteMatch>();
        int totalRounds = (int)Math.Log2(bracketSize);
        int currentMatchNum = startingMatchNumber;

        // Dictionnaire pour relier le match d'un tour à son match suivant
        // Clé: (round, indexInRound), Valeur: KumiteMatch
        var roundMatches = new Dictionary<(int round, int index), KumiteMatch>();

        // Générer de la finale vers le 1er tour pour lier facilement NextMatch
        for (int r = totalRounds; r >= 1; r--)
        {
            int matchesInRound = 1 << (totalRounds - r); // r=totalRounds -> 1 (finale), r=totalRounds-1 -> 2 (demis)...
            for (int i = 0; i < matchesInRound; i++)
            {
                var match = new KumiteMatch
                {
                    CategoryId = category.Id,
                    Category = category,
                    BracketRound = r,
                    RoundFromFinal = totalRounds - r,
                    RoundName = GetRoundName(r, totalRounds),
                    TotalDurationSeconds = category.KumiteRegularDurationSeconds,
                    RemainingSeconds = category.KumiteRegularDurationSeconds,
                    Status = MatchStatus.Pending,
                    MatchNumber = 0 // sera numéroté dans l'ordre chronologique
                };

                // Lier au tour suivant si pas finale
                if (r < totalRounds)
                {
                    int parentIndex = i / 2;
                    var parentMatch = roundMatches[(r + 1, parentIndex)];
                    match.NextMatch = parentMatch;
                    match.NextMatchCorner = (i % 2 == 0) ? CornerColor.Aka : CornerColor.Shiro;
                }

                roundMatches[(r, i)] = match;
            }
        }

        // Petite Finale (3e place) si demandée pour la catégorie
        KumiteMatch? bronzeMatch = null;
        if (category.HasBronzeMatch && totalRounds >= 2)
        {
            bronzeMatch = new KumiteMatch
            {
                CategoryId = category.Id,
                Category = category,
                BracketRound = totalRounds,
                RoundFromFinal = 0,
                RoundName = "Petite Finale",
                IsBronzeMatch = true,
                TotalDurationSeconds = category.KumiteRegularDurationSeconds,
                RemainingSeconds = category.KumiteRegularDurationSeconds,
                Status = MatchStatus.Pending,
                MatchNumber = 0
            };

            var semi1 = roundMatches[(totalRounds - 1, 0)];
            var semi2 = roundMatches[(totalRounds - 1, 1)];

            semi1.BronzeMatch = bronzeMatch;
            semi1.BronzeMatchCorner = CornerColor.Aka;

            semi2.BronzeMatch = bronzeMatch;
            semi2.BronzeMatchCorner = CornerColor.Shiro;
        }

        // Assigner les combattants placés au 1er tour (Round 1)
        int round1MatchesCount = bracketSize / 2;
        for (int i = 0; i < round1MatchesCount; i++)
        {
            var match = roundMatches[(1, i)];
            var aka = placedSlots[i * 2];
            var shiro = placedSlots[i * 2 + 1];

            if (aka != null)
            {
                match.AkaCompetitorId = aka.Id;
                match.AkaCompetitor = aka;
            }

            if (shiro != null)
            {
                match.ShiroCompetitorId = shiro.Id;
                match.ShiroCompetitor = shiro;
            }

            // Normalisation : si un seul combattant est présent et se trouve en Shiro, on le place en Aka
            if (!match.AkaCompetitorId.HasValue && match.ShiroCompetitorId.HasValue)
            {
                match.AkaCompetitorId = match.ShiroCompetitorId;
                match.AkaCompetitor = match.ShiroCompetitor;
                match.ShiroCompetitorId = null;
                match.ShiroCompetitor = null;
            }

            // Sécurité absolue : un combat du 1er tour ne doit JAMAIS se retrouver sans combattant
            if (!match.AkaCompetitorId.HasValue && !match.ShiroCompetitorId.HasValue)
            {
                throw new InvalidOperationException($"Erreur d'intégrité du tirage : le combat {i + 1} du 1er tour n'a aucun combattant.");
            }

            // Gestion de l'exempt (Bye) : si un seul combattant, il est qualifié directement
            if (match.IsBye)
            {
                match.WinnerCompetitorId = match.AkaCompetitorId;
                match.WinnerCompetitor = match.AkaCompetitor;
                match.Status = MatchStatus.Finished;
                match.WinnerReason = MatchWinnerReason.None;

                // Propager immédiatement au tour suivant
                if (match.NextMatch != null)
                {
                    if (match.NextMatchCorner == CornerColor.Aka)
                    {
                        match.NextMatch.AkaCompetitorId = match.WinnerCompetitorId;
                        match.NextMatch.AkaCompetitor = match.WinnerCompetitor;
                    }
                    else
                    {
                        match.NextMatch.ShiroCompetitorId = match.WinnerCompetitorId;
                        match.NextMatch.ShiroCompetitor = match.WinnerCompetitor;
                    }
                }
            }
        }

        // Numérotation chronologique des matchs (Round 1 en premier, puis Round 2, etc.)
        for (int r = 1; r <= totalRounds; r++)
        {
            // Petite Finale (3e place) juste avant la Finale (Round final)
            if (r == totalRounds && bronzeMatch != null)
            {
                bronzeMatch.MatchNumber = currentMatchNum++;
                matches.Add(bronzeMatch);
            }

            int matchesInRound = 1 << (totalRounds - r);
            for (int i = 0; i < matchesInRound; i++)
            {
                var match = roundMatches[(r, i)];
                match.MatchNumber = currentMatchNum++;
                matches.Add(match);
            }
        }

        return matches;
    }

    /// <summary>
    /// Placement respectant la séparation maximale des pays et des clubs, le rapprochement par poids/grade,
    /// et garantissant qu'AUCUN combat du 1er tour ne se retrouve sans combattant (au moins 1 combattant par match).
    /// </summary>
    private List<Competitor?> PlaceCompetitors(List<Competitor> competitors, int bracketSize)
    {
        var slots = new List<Competitor?>(new Competitor?[bracketSize]);
        int n = competitors.Count;
        int round1MatchesCount = bracketSize / 2;
        int byeCount = bracketSize - n; // Nombre d'exempts au premier tour

        // 1. Déterminer l'ordre des matchs par priorité des têtes de série
        var seedMatchOrder = GetSeedMatchOrder(bracketSize);

        // Les premiers `byeCount` matchs selon l'ordre des têtes de série reçoivent un exempt (1 seul combattant en Aka, Shiro=null)
        var byeMatches = seedMatchOrder.Take(byeCount).ToHashSet();

        // Les autres matchs ont 2 combattants (Aka et Shiro)
        var twoFighterMatches = seedMatchOrder.Skip(byeCount).ToList();

        // 2. Déterminer les slots actifs autorisés à recevoir un combattant :
        // Pour les matchs d'exempt : SEUL le slot Aka (2*m) est actif. Le slot Shiro (2*m+1) reste null (Exempt).
        // Pour les matchs à 2 combattants : les deux slots Aka (2*m) et Shiro (2*m+1) sont actifs.
        // Total de slots actifs = byeCount * 1 + (round1MatchesCount - byeCount) * 2 = bracketSize - byeCount = n !
        var byeSlots = byeMatches.Select(m => m * 2).ToList();
        var twoFighterSlots = twoFighterMatches.SelectMany(m => new[] { m * 2, m * 2 + 1 }).ToList();

        // 3. Trier les compétiteurs par grade et poids décroissants pour identifier les têtes de série naturelles
        var sorted = competitors
            .OrderByDescending(c => (int)c.Grade)
            .ThenByDescending(c => c.Weight)
            .ToList();

        // 4. Les top `byeCount` combattants (les têtes de série) reçoivent les Byes (exempts)
        // On les place dans les `byeSlots` en maximisant l'éloignement des pays et des clubs entre eux
        var topSeeds = sorted.Take(byeCount).ToList();
        var availableByeSlots = new List<int>(byeSlots);

        var topFightersOrdered = OrderFightersForSeparation(topSeeds);

        foreach (var fighter in topFightersOrdered)
        {
            int bestSlot = -1;
            double bestScore = double.NegativeInfinity;

            foreach (var slot in availableByeSlots)
            {
                double score = EvaluateSlotPlacement(fighter, slot, slots, bracketSize);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlot = slot;
                }
            }

            if (bestSlot >= 0)
            {
                slots[bestSlot] = fighter;
                availableByeSlots.Remove(bestSlot);
            }
        }

        // 5. Placer les combattants restants dans les matchs à 2 combattants
        var remainingFighters = sorted.Skip(byeCount).ToList();
        var availableTwoFighterSlots = new List<int>(twoFighterSlots);

        var remainingFightersOrdered = OrderFightersForSeparation(remainingFighters);

        foreach (var fighter in remainingFightersOrdered)
        {
            int bestSlot = -1;
            double bestScore = double.NegativeInfinity;

            foreach (var slot in availableTwoFighterSlots)
            {
                double score = EvaluateSlotPlacement(fighter, slot, slots, bracketSize);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlot = slot;
                }
            }

            if (bestSlot >= 0)
            {
                slots[bestSlot] = fighter;
                availableTwoFighterSlots.Remove(bestSlot);
            }
        }

        return slots;
    }

    /// <summary>
    /// Ordonne les combattants pour optimiser la séparation géographique :
    /// Regroupe par pays (pays le plus représenté en premier),
    /// puis au sein de chaque pays par club (club le plus représenté en premier).
    /// </summary>
    private List<Competitor> OrderFightersForSeparation(List<Competitor> fighters)
    {
        return fighters
            .GroupBy(c => (c.Country ?? "France").Trim().ToUpperInvariant())
            .OrderByDescending(g => g.Count())
            .SelectMany(countryGroup => countryGroup
                .GroupBy(c => (c.Club ?? string.Empty).Trim().ToUpperInvariant())
                .OrderByDescending(clubGroup => clubGroup.Count())
                .SelectMany(clubGroup => clubGroup.OrderByDescending(c => (int)c.Grade).ThenByDescending(c => c.Weight))
            )
            .ToList();
    }

    /// <summary>
    /// Calcule un score pour placer un combattant dans un slot donné.
    /// Pénalise au maximum la proximité avec un combattant du même pays ou du même club.
    /// Favorise la proximité de poids et de grade pour l'adversaire direct du 1er tour.
    /// </summary>
    private double EvaluateSlotPlacement(Competitor candidate, int targetSlot, List<Competitor?> currentSlots, int bracketSize)
    {
        double score = 0;
        int directOpponentSlot = (targetSlot % 2 == 0) ? targetSlot + 1 : targetSlot - 1;
        var directOpponent = currentSlots[directOpponentSlot];

        string candCountry = (candidate.Country ?? "France").Trim().ToUpperInvariant();
        string candClub = (candidate.Club ?? string.Empty).Trim().ToUpperInvariant();

        // 1. Contraintes avec tous les combattants déjà placés dans l'arbre
        for (int i = 0; i < bracketSize; i++)
        {
            var placed = currentSlots[i];
            if (placed == null) continue;

            string placedCountry = (placed.Country ?? "France").Trim().ToUpperInvariant();
            string placedClub = (placed.Club ?? string.Empty).Trim().ToUpperInvariant();

            bool sameCountry = !string.IsNullOrWhiteSpace(candCountry) && candCountry == placedCountry;
            bool sameClub = !string.IsNullOrWhiteSpace(candClub) && candClub == placedClub;

            int treeDistance = GetTreeDistance(targetSlot, i, bracketSize);

            // Contrainte PAYS :
            if (sameCountry)
            {
                if (treeDistance == 1)
                {
                    // Jamais le même pays au premier tour !
                    score -= 500000;
                }
                else
                {
                    // Plus ils sont éloignés dans l'arbre (ex: demi-tableaux opposés), plus le score est élevé
                    score += treeDistance * 10000;
                }
            }

            // Contrainte CLUB :
            if (sameClub)
            {
                if (treeDistance == 1)
                {
                    // Jamais le même club au 1er tour !
                    score -= 200000;
                }
                else
                {
                    // Plus ils sont éloignés dans l'arbre, meilleur est le score
                    score += treeDistance * 2000;
                }
            }
        }

        // 2. Rapprochement par poids et grade pour l'adversaire direct du 1er tour s'il existe
        if (directOpponent != null)
        {
            string oppCountry = (directOpponent.Country ?? "France").Trim().ToUpperInvariant();
            string oppClub = (directOpponent.Club ?? string.Empty).Trim().ToUpperInvariant();

            bool sameCountry = !string.IsNullOrWhiteSpace(candCountry) && candCountry == oppCountry;
            bool sameClub = !string.IsNullOrWhiteSpace(candClub) && candClub == oppClub;

            if (sameCountry)
            {
                score -= 500000; // Pas de duel du même pays au premier tour
            }
            if (sameClub)
            {
                score -= 200000; // Pas de duel du même club au premier tour
            }

            if (!sameCountry && !sameClub)
            {
                // Plus la différence de grade et de poids est petite, plus le score est élevé
                double gradeDiff = Math.Abs((int)candidate.Grade - (int)directOpponent.Grade);
                double weightDiff = Math.Abs(candidate.Weight - directOpponent.Weight);

                // Normalisation : grade max diff = 8, weight max diff typique = 20kg
                double closenessScore = 500 - (gradeDiff * 25 + weightDiff * 10);
                score += closenessScore;
            }
        }

        return score;
    }

    /// <summary>
    /// Calcule la distance dans l'arbre binaire entre 2 feuilles.
    /// 1 = même match de 1er tour
    /// 2 = même quart de tableau
    /// 3 = même demi-tableau
    /// 4 = demi-tableaux opposés (ne peuvent se rencontrer qu'en finale)
    /// </summary>
    private int GetTreeDistance(int slotA, int slotB, int bracketSize)
    {
        if (slotA == slotB) return 0;
        int diff = slotA ^ slotB;
        int distance = 0;
        while (diff > 0)
        {
            distance++;
            diff >>= 1;
        }
        return distance;
    }

    /// <summary>
    /// Retourne l'ordre des indices de matchs de Round 1 ordonnés par priorité des têtes de série.
    /// </summary>
    private List<int> GetSeedMatchOrder(int bracketSize)
    {
        var seedSlots = GenerateStandardSeedPositions(bracketSize);
        int m = bracketSize / 2;
        var matchOrder = new List<int>();
        var seen = new HashSet<int>();
        foreach (var slot in seedSlots)
        {
            int matchIndex = slot / 2;
            if (seen.Add(matchIndex))
            {
                matchOrder.Add(matchIndex);
                if (matchOrder.Count == m) break;
            }
        }
        return matchOrder;
    }

    private List<int> GenerateStandardSeedPositions(int n)
    {
        var seeds = new List<int> { 0, 1 };
        while (seeds.Count < n)
        {
            var next = new List<int>();
            int m = seeds.Count * 2 - 1;
            foreach (var s in seeds)
            {
                next.Add(s);
                next.Add(m - s);
            }
            seeds = next;
        }
        return seeds;
    }

    private string GetRoundName(int round, int totalRounds)
    {
        int fromFinal = totalRounds - round;
        return fromFinal switch
        {
            0 => "Final",
            1 => "Semi-Final",
            2 => "Quarter Final",
            3 => "Round of 16",
            4 => "Round of 32",
            _ => $"Turn {round}"
        };
    }
}
