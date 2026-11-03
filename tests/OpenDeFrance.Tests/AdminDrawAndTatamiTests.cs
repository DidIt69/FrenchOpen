using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenDeFrance.Core.Enums;
using OpenDeFrance.Core.Services;
using OpenDeFrance.Infrastructure.Data;
using OpenDeFrance.Infrastructure.Services;

namespace OpenDeFrance.Tests;

public class AdminDrawAndTatamiTests
{
    private (TournamentService service, IServiceScopeFactory scopeFactory, string dbPath) CreateTestEnvironment()
    {
        string testDbPath = Path.Combine(Path.GetTempPath(), $"kyokushin_draw_test_{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={testDbPath}";

        var services = new ServiceCollection();
        services.AddDbContext<TournamentDbContext>(opt => opt.UseSqlite(connectionString));
        services.AddSingleton<IKumiteBracketGenerator, KumiteBracketGenerator>();
        services.AddSingleton<IKataTournamentManager, KataTournamentManager>();
        services.AddSingleton<IExcelTournamentService, ExcelTournamentService>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var tournamentService = new TournamentService(
            scopeFactory,
            provider.GetRequiredService<IKumiteBracketGenerator>(),
            provider.GetRequiredService<IKataTournamentManager>(),
            provider.GetRequiredService<IExcelTournamentService>());

        tournamentService.SeedSampleData();
        return (tournamentService, scopeFactory, testDbPath);
    }

    [Fact]
    public void SwapKumiteCompetitors_ShouldSwapCompetitorsInRound1()
    {
        var (service, _, dbPath) = CreateTestEnvironment();
        try
        {
            var matches = service.GetMatches().Where(m => m.BracketRound == 1 && m.AkaCompetitorId.HasValue && m.ShiroCompetitorId.HasValue).ToList();
            Assert.True(matches.Count >= 2);

            var m1 = matches[0];
            var m2 = matches[1];

            int? initialM1Aka = m1.AkaCompetitorId;
            int? initialM2Shiro = m2.ShiroCompetitorId;

            // Swap m1 Aka with m2 Shiro
            service.SwapKumiteCompetitors(m1.Id, CornerColor.Aka, m2.Id, CornerColor.Shiro);

            var updatedM1 = service.GetMatch(m1.Id)!;
            var updatedM2 = service.GetMatch(m2.Id)!;

            Assert.Equal(initialM2Shiro, updatedM1.AkaCompetitorId);
            Assert.Equal(initialM1Aka, updatedM2.ShiroCompetitorId);
        }
        finally
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath)) File.Delete(dbPath);
            }
            catch { }
        }
    }

    [Fact]
    public void AssignMatchToTatami_ShouldMoveMatchBetweenTatamis()
    {
        var (service, _, dbPath) = CreateTestEnvironment();
        try
        {
            var match = service.GetMatches().First(m => !m.IsBye);
            int originalTatamiId = match.TatamiId ?? 1;
            int targetTatamiId = (originalTatamiId == 1) ? 2 : 1;

            service.AssignMatchToTatami(match.Id, targetTatamiId);

            var updatedMatch = service.GetMatch(match.Id)!;
            Assert.Equal(targetTatamiId, updatedMatch.TatamiId);

            var targetTatami = service.GetTatami(targetTatamiId)!;
            Assert.Contains(targetTatami.AssignedKumiteMatches, m => m.Id == match.Id);

            var oldTatami = service.GetTatami(originalTatamiId)!;
            Assert.DoesNotContain(oldTatami.AssignedKumiteMatches, m => m.Id == match.Id);
        }
        finally
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath)) File.Delete(dbPath);
            }
            catch { }
        }
    }

    [Fact]
    public void AssignCategoryMatchesToTatami_ShouldAssignAllMatchesOfCategory()
    {
        var (service, _, dbPath) = CreateTestEnvironment();
        try
        {
            var kumiteCat = service.GetCategories().First(c => c.Type == CompetitionType.Kumite);
            int targetTatamiId = 2;

            service.AssignCategoryMatchesToTatami(kumiteCat.Id, targetTatamiId);

            var categoryMatches = service.GetMatches().Where(m => m.CategoryId == kumiteCat.Id).ToList();
            Assert.All(categoryMatches, m => Assert.Equal(targetTatamiId, m.TatamiId));
        }
        finally
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath)) File.Delete(dbPath);
            }
            catch { }
        }
    }

    [Fact]
    public void SwapKumiteCompetitors_ShouldRefuseSwap_IfItWouldLeaveARound1MatchWithZeroCompetitors()
    {
        var (service, _, dbPath) = CreateTestEnvironment();
        try
        {
            var byeMatch = service.GetMatches().FirstOrDefault(m => m.BracketRound == 1 && m.IsBye);
            if (byeMatch == null)
            {
                var r1Matches = service.GetMatches().Where(m => m.BracketRound == 1).Take(2).ToList();
                Assert.Equal(2, r1Matches.Count);
                r1Matches[0].AkaCompetitorId = 1;
                r1Matches[0].ShiroCompetitorId = null;
                byeMatch = r1Matches[0];
            }

            var otherMatch = service.GetMatches().FirstOrDefault(m => m.BracketRound == 1 && m.Id != byeMatch.Id && m.CategoryId == byeMatch.CategoryId);
            if (otherMatch != null)
            {
                // Tenter d'échanger le seul combattant de byeMatch (Aka) avec le coin Shiro (null) de otherMatch
                service.SwapKumiteCompetitors(byeMatch.Id, CornerColor.Aka, otherMatch.Id, CornerColor.Shiro);

                var afterM1 = service.GetMatch(byeMatch.Id)!;
                // byeMatch doit TOUJOURS conserver au moins un combattant !
                Assert.True(afterM1.AkaCompetitorId.HasValue || afterM1.ShiroCompetitorId.HasValue);
            }
        }
        finally
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath)) File.Delete(dbPath);
            }
            catch { }
        }
    }

    [Fact]
    public void SwapKataPerformances_ShouldSwapCompetitors()
    {
        var (service, _, dbPath) = CreateTestEnvironment();
        try
        {
            var perfs = service.GetKataPerformances().Where(p => p.Round == KataRound.Turn1).ToList();
            Assert.True(perfs.Count >= 2);

            var p1 = perfs[0];
            var p2 = perfs[1];

            int? initialC1 = p1.CompetitorId;
            int? initialC2 = p2.CompetitorId;

            service.SwapKataPerformances(p1.Id, p2.Id);

            var updatedP1 = service.GetKataPerformances().First(p => p.Id == p1.Id);
            var updatedP2 = service.GetKataPerformances().First(p => p.Id == p2.Id);

            Assert.Equal(initialC2, updatedP1.CompetitorId);
            Assert.Equal(initialC1, updatedP2.CompetitorId);
        }
        finally
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath)) File.Delete(dbPath);
            }
            catch { }
        }
    }

    [Fact]
    public void DeclareWinner_ShouldAutomaticallyAdvanceToNextMatchOnTatami()
    {
        var (service, _, dbPath) = CreateTestEnvironment();
        try
        {
            var tatami = service.GetTatami(1)!;
            var assigned = tatami.AssignedKumiteMatches
                .Where(m => !m.IsBye && m.AkaCompetitorId.HasValue && m.ShiroCompetitorId.HasValue && m.Status != MatchStatus.Finished)
                .OrderBy(m => m.MatchNumber)
                .ToList();

            Assert.True(assigned.Count >= 2, "Le tatami 1 doit avoir au moins 2 combats prêts pour le test.");

            var m1 = assigned[0];
            var m2 = assigned[1];

            // Mettre m1 en combat actuel sur le tatami 1
            service.SetTatamiCurrentMatch(tatami.Id, m1.Id);
            Assert.Equal(m1.Id, service.GetTatami(tatami.Id)!.CurrentKumiteMatchId);

            // Déclarer un vainqueur sur m1
            service.DeclareWinner(m1.Id, CornerColor.Aka, MatchWinnerReason.Ippon);

            // Vérifier que m1 est terminé
            var finishedM1 = service.GetMatch(m1.Id)!;
            Assert.Equal(MatchStatus.Finished, finishedM1.Status);
            Assert.Equal(finishedM1.AkaCompetitorId, finishedM1.WinnerCompetitorId);

            // Vérifier que le tatami est passé AUTOMATIQUEMENT au combat suivant m2
            var updatedTatami = service.GetTatami(tatami.Id)!;
            Assert.Equal(m2.Id, updatedTatami.CurrentKumiteMatchId);
            Assert.Equal(MatchStatus.Ready, service.GetMatch(m2.Id)!.Status);
        }
        finally
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath)) File.Delete(dbPath);
            }
            catch { }
        }
    }

    [Fact]
    public void SubmitKataScores_ShouldAutomaticallyAdvanceToNextKataOnTatami()
    {
        var (service, _, dbPath) = CreateTestEnvironment();
        try
        {
            var tatami = service.GetTatami(2)!; // Tatami Kata par défaut
            var perfs = service.GetKataPerformances()
                .Where(p => p.TatamiId == tatami.Id && p.Status != MatchStatus.Finished)
                .OrderBy(p => p.PerformanceOrder)
                .ToList();

            if (perfs.Count >= 2)
            {
                var p1 = perfs[0];
                var p2 = perfs[1];

                service.SetTatamiCurrentKata(tatami.Id, p1.Id);
                Assert.Equal(p1.Id, service.GetTatami(tatami.Id)!.CurrentKataPerformanceId);

                // Soumettre les 5 notes (de 55 à 100)
                service.SubmitKataScores(p1.Id, new List<double> { 75, 78, 76, 80, 74 });

                var finishedP1 = service.GetKataPerformances().First(p => p.Id == p1.Id);
                Assert.Equal(MatchStatus.Finished, finishedP1.Status);

                // Vérifier que le tatami est passé AUTOMATIQUEMENT au kata suivant p2
                var updatedTatami = service.GetTatami(tatami.Id)!;
                Assert.Equal(p2.Id, updatedTatami.CurrentKataPerformanceId);
            }
        }
        finally
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath)) File.Delete(dbPath);
            }
            catch { }
        }
    }

    [Fact]
    public void SetCategoryKataQualifiersCount_ShouldUpdateCategoryAndTour2Placeholders()
    {
        var (service, _, dbPath) = CreateTestEnvironment();
        try
        {
            var kataCat = service.GetCategories().First(c => c.Type == CompetitionType.Kata);
            int catId = kataCat.Id;

            // Par défaut, vérifions le nombre de qualifiés (généralement 4)
            var initialT2Slots = service.GetKataPerformances()
                .Where(p => p.CategoryId == catId && p.Round == KataRound.Turn2)
                .ToList();
            int initialExpected = Math.Min(kataCat.KataQualifiersCount, kataCat.Competitors.Count);
            Assert.Equal(initialExpected, initialT2Slots.Count);

            // Changer le nombre de qualifiés à 2
            service.SetCategoryKataQualifiersCount(catId, 2);

            var updatedCat = service.GetCategory(catId)!;
            Assert.Equal(2, updatedCat.KataQualifiersCount);

            var updatedT2Slots = service.GetKataPerformances()
                .Where(p => p.CategoryId == catId && p.Round == KataRound.Turn2)
                .ToList();
            Assert.Equal(2, updatedT2Slots.Count);
        }
        finally
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath)) File.Delete(dbPath);
            }
            catch { }
        }
    }
}
