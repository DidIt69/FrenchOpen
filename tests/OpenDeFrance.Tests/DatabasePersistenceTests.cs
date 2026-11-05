using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenDeFrance.Core.Enums;
using OpenDeFrance.Core.Services;
using OpenDeFrance.Infrastructure.Data;
using OpenDeFrance.Infrastructure.Services;

namespace OpenDeFrance.Tests;

// Ce test écrit en base SQLite : on le force à s'exécuter seul pour éviter la contention
[Collection("DatabaseTests")]
public class DatabasePersistenceTests
{
    [Fact]
    public void DatabasePersistence_ShouldSaveAndReloadTournamentState()
    {
        // Arrange
        string testDbPath = Path.Combine(Path.GetTempPath(), $"kyokushin_test_{Guid.NewGuid():N}.db");
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

        try
        {
            // Act 1 : Démarrer et persister les données d'exemple
            tournamentService.SeedSampleData();

            // Assert 1 : Vérifier que les tables SQLite sont bien remplies
            using (var scope = scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
                Assert.True(db.Competitors.Count() >= 10, "Les compétiteurs doivent être enregistrés en base");
                Assert.True(db.Categories.Count() >= 2, "Les catégories doivent être enregistrées en base");
                Assert.True(db.KumiteMatches.Any(), "Les combats Kumité doivent être enregistrés en base");
                Assert.True(db.KataPerformances.Any(), "Les passages Kata doivent être enregistrés en base");
                Assert.Equal(3, db.Tatamis.Count());
            }

            // Act 2 : Simuler une modification de score
            var firstMatch = tournamentService.GetMatches().First(m => !m.IsBye);
            tournamentService.AddScore(firstMatch.Id, CornerColor.Aka, "wazaari");

            // Attendre la sauvegarde asynchrone avec une attente active (max 3s)
            var deadline = DateTime.UtcNow.AddSeconds(3);
            int savedValue = 0;
            while (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(100);
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
                savedValue = db.KumiteMatches.AsNoTracking()
                                             .First(m => m.Id == firstMatch.Id)
                                             .AkaWazaAri;
                if (savedValue == 1) break;
            }

            // Assert 2 : Vérifier que la base SQLite contient le score à jour
            Assert.Equal(1, savedValue);
        }
        finally
        {
            if (File.Exists(testDbPath))
            {
                try { File.Delete(testDbPath); } catch { }
            }
        }
    }
}
