using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenDeFrance.Core.Enums;
using OpenDeFrance.Core.Models;
using OpenDeFrance.Core.Services;
using OpenDeFrance.Infrastructure.Data;
using OpenDeFrance.Infrastructure.Services;

namespace OpenDeFrance.Tests;

public class BronzeMatchAndTatamiManagementTests
{
    private (TournamentService service, string dbPath) CreateTestEnvironment()
    {
        string testDbPath = Path.Combine(Path.GetTempPath(), $"kyokushin_bronze_test_{Guid.NewGuid():N}.db");
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
        return (tournamentService, testDbPath);
    }

    [Fact]
    public void CategoryWithBronzeMatch_ShouldGenerateBronzeMatchAndPropagateLosers()
    {
        var (service, dbPath) = CreateTestEnvironment();
        try
        {
            var kumiteCat = service.GetCategories().First(c => c.Type == CompetitionType.Kumite);
            
            // Enable bronze match
            service.SetCategoryHasBronzeMatch(kumiteCat.Id, true);

            var catMatches = service.GetMatches().Where(m => m.CategoryId == kumiteCat.Id).ToList();
            var bronzeMatch = catMatches.FirstOrDefault(m => m.IsBronzeMatch);

            Assert.NotNull(bronzeMatch);
            Assert.Contains("Petite Finale", bronzeMatch.RoundName);

            // Find the semi-final matches
            var semiFinals = catMatches.Where(m => m.BronzeMatchId == bronzeMatch.Id || m.BronzeMatch == bronzeMatch).ToList();
            Assert.Equal(2, semiFinals.Count);

            // Declare winner on semi-final 1
            var semi1 = semiFinals[0];
            if (semi1.AkaCompetitorId.HasValue && semi1.ShiroCompetitorId.HasValue)
            {
                var loserId = semi1.ShiroCompetitorId;
                service.DeclareWinner(semi1.Id, CornerColor.Aka, MatchWinnerReason.Ippon);

                var updatedBronze = service.GetMatch(bronzeMatch.Id)!;
                if (semi1.BronzeMatchCorner == CornerColor.Aka)
                {
                    Assert.Equal(loserId, updatedBronze.AkaCompetitorId);
                }
                else
                {
                    Assert.Equal(loserId, updatedBronze.ShiroCompetitorId);
                }
            }
        }
        finally
        {
            try
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath)) File.Delete(dbPath);
            }
            catch { }
        }
    }

    [Fact]
    public void CategoryWithoutBronzeMatch_ShouldNotGenerateBronzeMatch()
    {
        var (service, dbPath) = CreateTestEnvironment();
        try
        {
            var kumiteCat = service.GetCategories().First(c => c.Type == CompetitionType.Kumite);
            
            // Disable bronze match
            service.SetCategoryHasBronzeMatch(kumiteCat.Id, false);

            var catMatches = service.GetMatches().Where(m => m.CategoryId == kumiteCat.Id).ToList();
            var bronzeMatch = catMatches.FirstOrDefault(m => m.IsBronzeMatch);

            Assert.Null(bronzeMatch);
        }
        finally
        {
            try
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath)) File.Delete(dbPath);
            }
            catch { }
        }
    }

    [Fact]
    public void TatamiManagement_ShouldAddAndDeleteTatami()
    {
        var (service, dbPath) = CreateTestEnvironment();
        try
        {
            int initialCount = service.GetTatamis().Count;
            var added = service.AddTatami("Tatami 4 - Test", CompetitionType.Kumite);

            Assert.Equal(initialCount + 1, service.GetTatamis().Count);
            Assert.Equal("Tatami 4 - Test", added.Name);

            bool deleted = service.DeleteTatami(added.Id);
            Assert.True(deleted);
            Assert.Equal(initialCount, service.GetTatamis().Count);
        }
        finally
        {
            try
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath)) File.Delete(dbPath);
            }
            catch { }
        }
    }

    [Fact]
    public void BalanceCategoriesAcrossTatamis_ShouldKeepCategoryMatchesOnSameTatami()
    {
        var (service, dbPath) = CreateTestEnvironment();
        try
        {
            service.BalanceCategoriesAcrossTatamis();

            var kumiteCategories = service.GetCategories().Where(c => c.Type == CompetitionType.Kumite).ToList();

            foreach (var cat in kumiteCategories)
            {
                var matches = service.GetMatches().Where(m => m.CategoryId == cat.Id && !m.IsBye).ToList();
                if (matches.Count > 0)
                {
                    var distinctTatamis = matches.Select(m => m.TatamiId).Distinct().ToList();
                    Assert.Single(distinctTatamis); // All matches of this category MUST be on the exact same Tatami
                }
            }
        }
        finally
        {
            try
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath)) File.Delete(dbPath);
            }
            catch { }
        }
    }

    [Fact]
    public void LanguageService_ShouldToggleLanguageAndReturnTranslations()
    {
        var lang = new LanguageService();
        Assert.Equal("fr", lang.CurrentLanguage);
        Assert.Equal("Tableaux & Tirages au Sort", lang["BracketsTitle"]);

        lang.ToggleLanguage();
        Assert.Equal("en", lang.CurrentLanguage);
        Assert.Equal("Brackets & Draws", lang["BracketsTitle"]);
        Assert.Equal("Bronze Match (3rd Place)", lang["BronzeMatch"]);

        lang.ToggleLanguage();
        Assert.Equal("fr", lang.CurrentLanguage);
    }
}
