using OpenDeFrance.Core.Enums;
using OpenDeFrance.Core.Models;

namespace OpenDeFrance.Core.Services;

public interface ITournamentService
{
    event Action? OnStateChanged;
    event Action<int>? OnTatamiUpdated;
    event Action<int>? OnTimerZero; // tatamiId

    Tournament GetTournament();
    List<Tatami> GetTatamis();
    Tatami? GetTatami(int tatamiId);
    List<Category> GetCategories();
    Category? GetCategory(int categoryId);
    List<Competitor> GetCompetitors();
    List<KumiteMatch> GetMatches();
    KumiteMatch? GetMatch(int matchId);
    List<KataPerformance> GetKataPerformances();

    // Gestion des Compétiteurs
    Competitor AddCompetitor(Competitor competitor, List<int>? categoryIds = null);
    void UpdateCompetitor(Competitor competitor, List<int>? categoryIds = null);
    void SetCompetitorStatus(int competitorId, CompetitorStatus status);
    bool DeleteCompetitor(int competitorId);

    // Setup & Tirages
    void SeedSampleData();
    void ResetTournament();
    void ApplyExcelImport(ExcelImportResult importResult);
    void GenerateAllBrackets();
    void GenerateCategoryBracket(int categoryId);
    void AssignMatchToTatami(int matchId, int tatamiId);
    void AssignKataToTatami(int performanceId, int tatamiId);
    void AssignCategoryMatchesToTatami(int categoryId, int tatamiId);
    void SetCategoryTatami(int categoryId, int? tatamiId);
    void SwapKumiteCompetitors(int match1Id, CornerColor corner1, int match2Id, CornerColor corner2);
    void SwapKataPerformances(int perf1Id, int perf2Id);
    void SetCategoryHasBronzeMatch(int categoryId, bool hasBronzeMatch);
    void SetCategoryKataQualifiersCount(int categoryId, int qualifiersCount);
    void SetCategoryDay(int categoryId, int dayNumber);
    void SetCategoryOrderIndex(int categoryId, int orderIndex);
    void MoveCategoryOrder(int categoryId, int direction);
    void SetTournamentDaysCount(int daysCount);
    void SetActiveDay(int? day);
    void SetCategoryDayAndTatami(int categoryId, int dayNumber, int tatamiId);
    void SetMatchDay(int matchId, int dayNumber);
    void SetKataDay(int performanceId, int dayNumber);

    // Gestion des Tatamis & Équilibrage
    Tatami AddTatami(string name, CompetitionType discipline);
    bool DeleteTatami(int tatamiId);
    void UpdateTatami(int tatamiId, string name, CompetitionType discipline);
    void SwitchTatamiMode(int tatamiId, CompetitionType mode);
    void BalanceCategoriesAcrossTatamis(bool resetManualAssignments = true);

    // Contrôle Tatami Kumite
    void SetTatamiCurrentMatch(int tatamiId, int matchId);
    void ToggleTimer(int tatamiId);
    void ResetTimer(int tatamiId);
    void AdjustTimer(int tatamiId, int deltaSeconds);
    void AddExtension(int tatamiId); // Encho-sen / prolongation dynamique
    void AddScore(int matchId, CornerColor corner, string type); // wazaari, ippon, chui, genten
    void DeclareWinner(int matchId, CornerColor winnerCorner, MatchWinnerReason reason);

    // Contrôle Tatami Kata
    void SetTatamiCurrentKata(int tatamiId, int performanceId);
    void UpdateLiveKataScores(int performanceId, List<double> scores);
    void ResetKataScores(int performanceId);
    void SubmitKataScores(int performanceId, List<double> scores);
    void QualifyKataRound1(int categoryId);
    void FinalizeKataRound2(int categoryId);

    // Statut Compétition Kata vs Kumite (Katas en premier)
    bool IsKataCompetitionFinished(int? competitionDay = null, int? tatamiId = null);
    int GetPendingKataCount(int? competitionDay = null, int? tatamiId = null);
    int GetTotalKataCount(int? competitionDay = null, int? tatamiId = null);

    // Pause & Publicités / Sponsors
    void SetTatamiPause(int tatamiId, bool isPaused, string? pauseMessage = null);
    void SetAllTatamisPause(bool isPaused, string? pauseMessage = null);
    List<Advertisement> GetAdvertisements(bool onlyActive = false);
    Advertisement? GetAdvertisement(int id);
    Advertisement AddAdvertisement(Advertisement ad);
    void UpdateAdvertisement(Advertisement ad);
    bool DeleteAdvertisement(int id);
}
