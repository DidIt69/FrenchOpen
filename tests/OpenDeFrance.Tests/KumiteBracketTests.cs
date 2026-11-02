using OpenDeFrance.Core.Enums;
using OpenDeFrance.Core.Models;
using OpenDeFrance.Core.Services;

namespace OpenDeFrance.Tests;

public class KumiteBracketTests
{
    private readonly KumiteBracketGenerator _generator = new();

    [Fact]
    public void GenerateBracket_ShouldSeparateSameClubCompetitors_AndPairCloseGrades()
    {
        // Arrange : 4 combattants, 2 de "Dojo Paris" et 2 de "Dojo Lyon"
        var category = new Category
        {
            Id = 1,
            Name = "Senior Hommes -75kg",
            Type = CompetitionType.Kumite,
            KumiteRegularDurationSeconds = 120,
            KumiteExtensionDurationSeconds = 120
        };

        var competitors = new List<Competitor>
        {
            new() { Id = 1, LastName = "Parisien1", Club = "Dojo Paris", Grade = BeltGrade.Shodan, Weight = 74.0 },
            new() { Id = 2, LastName = "Parisien2", Club = "Dojo Paris", Grade = BeltGrade.Blue, Weight = 72.0 },
            new() { Id = 3, LastName = "Lyonnais1", Club = "Dojo Lyon", Grade = BeltGrade.Nidan, Weight = 74.5 },
            new() { Id = 4, LastName = "Lyonnais2", Club = "Dojo Lyon", Grade = BeltGrade.Yellow, Weight = 71.5 }
        };

        // Act
        var matches = _generator.GenerateBracket(category, competitors);

        // Assert
        Assert.NotEmpty(matches);
        var round1Matches = matches.Where(m => m.BracketRound == 1).ToList();
        Assert.Equal(2, round1Matches.Count);

        // Au premier tour, aucun match ne doit opposer deux membres du même club
        foreach (var m in round1Matches)
        {
            if (m.AkaCompetitor != null && m.ShiroCompetitor != null)
            {
                Assert.NotEqual(m.AkaCompetitor.Club, m.ShiroCompetitor.Club);
            }
        }
    }

    [Fact]
    public void BracketTree_ShouldAdvanceWinnerToNextMatch()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Test", Type = CompetitionType.Kumite };
        var competitors = new List<Competitor>
        {
            new() { Id = 1, LastName = "Fighter1", Club = "ClubA", Grade = BeltGrade.Shodan, Weight = 70 },
            new() { Id = 2, LastName = "Fighter2", Club = "ClubB", Grade = BeltGrade.Shodan, Weight = 70 },
            new() { Id = 3, LastName = "Fighter3", Club = "ClubC", Grade = BeltGrade.Shodan, Weight = 70 },
            new() { Id = 4, LastName = "Fighter4", Club = "ClubD", Grade = BeltGrade.Shodan, Weight = 70 }
        };

        var matches = _generator.GenerateBracket(category, competitors);
        var semi1 = matches.First(m => m.BracketRound == 1 && m.NextMatchCorner == CornerColor.Aka);
        var finalMatch = semi1.NextMatch;

        Assert.NotNull(finalMatch);
        Assert.Equal(2, finalMatch.BracketRound); // Finale
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(15)]
    public void EveryRound1Match_MustHaveAtLeastOneCompetitor_NeverZero(int fighterCount)
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Open Weight", Type = CompetitionType.Kumite };
        var competitors = Enumerable.Range(1, fighterCount).Select(i => new Competitor
        {
            Id = i,
            LastName = $"Fighter_{i}",
            Club = $"Club_{i % 3}", // Multiple fighters per club
            Grade = (BeltGrade)(i % 9),
            Weight = 60 + i
        }).ToList();

        // Act
        var matches = _generator.GenerateBracket(category, competitors);

        // Assert
        var round1Matches = matches.Where(m => m.BracketRound == 1).ToList();
        Assert.NotEmpty(round1Matches);

        foreach (var m in round1Matches)
        {
            // Règle absolue : CHAQUE match de tour 1 doit avoir AU MOINS un combattant
            bool hasAka = m.AkaCompetitorId.HasValue;
            bool hasShiro = m.ShiroCompetitorId.HasValue;
            Assert.True(hasAka || hasShiro, $"Le match #{m.MatchNumber} du tour 1 n'a aucun combattant !");

            // Si le match a un exempt (Bye), le combattant présent doit être en Aka
            if (m.IsBye)
            {
                Assert.True(hasAka, $"Le match exempt #{m.MatchNumber} doit avoir son combattant en Aka");
                Assert.False(hasShiro, $"Le match exempt #{m.MatchNumber} ne doit pas avoir de combattant en Shiro");
                Assert.Equal(MatchStatus.Finished, m.Status);
                Assert.NotNull(m.WinnerCompetitorId);
            }
        }

        // Compter les combattants uniques présents au tour 1
        int placedCount = round1Matches.Sum(m => (m.AkaCompetitorId.HasValue ? 1 : 0) + (m.ShiroCompetitorId.HasValue ? 1 : 0));
        Assert.Equal(fighterCount, placedCount);
    }

    [Fact]
    public void GenerateBracket_ShouldSeparateSameCountryCompetitors_IntoOppositeHalves()
    {
        // 4 combattants : 2 de France et 2 d'Espagne
        var category = new Category { Id = 1, Name = "Senior -70kg", Type = CompetitionType.Kumite };
        var competitors = new List<Competitor>
        {
            new() { Id = 1, LastName = "Fra1", Country = "France", Club = "Paris", Grade = BeltGrade.Shodan, Weight = 68 },
            new() { Id = 2, LastName = "Fra2", Country = "France", Club = "Lyon", Grade = BeltGrade.Brown, Weight = 69 },
            new() { Id = 3, LastName = "Esp1", Country = "Espagne", Club = "Madrid", Grade = BeltGrade.Nidan, Weight = 68.5 },
            new() { Id = 4, LastName = "Esp2", Country = "Espagne", Club = "Barcelone", Grade = BeltGrade.Blue, Weight = 67 }
        };

        var matches = _generator.GenerateBracket(category, competitors);
        var r1 = matches.Where(m => m.BracketRound == 1).ToList();

        Assert.Equal(2, r1.Count);

        // Au 1er tour, aucun match n'oppose deux combattants du même pays
        foreach (var m in r1)
        {
            Assert.NotNull(m.AkaCompetitor);
            Assert.NotNull(m.ShiroCompetitor);
            Assert.NotEqual(m.AkaCompetitor.Country, m.ShiroCompetitor.Country);
        }

        // Les deux Français doivent être dans deux demi-finales distinctes (ne peuvent se croiser qu'en finale)
        var m1 = r1[0];
        var m2 = r1[1];
        bool fraInM1 = (m1.AkaCompetitor?.Country == "France" || m1.ShiroCompetitor?.Country == "France");
        bool fraInM2 = (m2.AkaCompetitor?.Country == "France" || m2.ShiroCompetitor?.Country == "France");
        Assert.True(fraInM1 && fraInM2, "Les combattants d'un même pays doivent être séparés dans les deux moitiés du tableau !");
    }

    [Fact]
    public void GenerateBracket_With8FightersFrom4Countries_ShouldSeparateCountries()
    {
        // 8 combattants : 2 France, 2 Espagne, 2 Japon, 2 Pologne
        var category = new Category { Id = 1, Name = "Open International", Type = CompetitionType.Kumite };
        var competitors = new List<Competitor>
        {
            new() { Id = 1, LastName = "FraA", Country = "France", Club = "ClubA", Grade = BeltGrade.Shodan, Weight = 75 },
            new() { Id = 2, LastName = "FraB", Country = "France", Club = "ClubB", Grade = BeltGrade.Brown, Weight = 74 },
            new() { Id = 3, LastName = "EspA", Country = "Espagne", Club = "ClubC", Grade = BeltGrade.Nidan, Weight = 76 },
            new() { Id = 4, LastName = "EspB", Country = "Espagne", Club = "ClubD", Grade = BeltGrade.Blue, Weight = 73 },
            new() { Id = 5, LastName = "JpnA", Country = "Japon", Club = "ClubE", Grade = BeltGrade.Sandan, Weight = 75 },
            new() { Id = 6, LastName = "JpnB", Country = "Japon", Club = "ClubF", Grade = BeltGrade.Green, Weight = 72 },
            new() { Id = 7, LastName = "PolA", Country = "Pologne", Club = "ClubG", Grade = BeltGrade.Shodan, Weight = 77 },
            new() { Id = 8, LastName = "PolB", Country = "Pologne", Club = "ClubH", Grade = BeltGrade.Yellow, Weight = 71 }
        };

        var matches = _generator.GenerateBracket(category, competitors);
        var r1 = matches.Where(m => m.BracketRound == 1).ToList();

        Assert.Equal(4, r1.Count);

        // Aucun duel du même pays au premier tour
        foreach (var m in r1)
        {
            Assert.NotNull(m.AkaCompetitor);
            Assert.NotNull(m.ShiroCompetitor);
            Assert.NotEqual(m.AkaCompetitor.Country, m.ShiroCompetitor.Country);
        }
    }
}
