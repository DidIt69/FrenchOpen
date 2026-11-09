using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenDeFrance.Core.Enums;
using OpenDeFrance.Core.Models;
using OpenDeFrance.Core.Services;
using OpenDeFrance.Infrastructure.Data;

namespace OpenDeFrance.Infrastructure.Services;

public class TournamentService : ITournamentService, IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IKumiteBracketGenerator _bracketGenerator;
    private readonly IKataTournamentManager _kataManager;
    private readonly IExcelTournamentService _excelService;
    private readonly object _lock = new();

    private Tournament _tournament = new();
    private List<Tatami> _tatamis = new()
    {
        new() { Id = 1, Number = 1, Name = "Tatami 1", Discipline = CompetitionType.Kumite, CurrentDiscipline = CompetitionType.Kumite },
        new() { Id = 2, Number = 2, Name = "Tatami 2", Discipline = CompetitionType.Both, CurrentDiscipline = CompetitionType.Kata },
        new() { Id = 3, Number = 3, Name = "Tatami 3", Discipline = CompetitionType.Kata, CurrentDiscipline = CompetitionType.Kata }
    };
    private List<Category> _categories = new();
    private List<Competitor> _competitors = new();
    private List<KumiteMatch> _matches = new();
    private List<KataPerformance> _kataPerformances = new();
    private List<Advertisement> _advertisements = new();

    private System.Threading.Timer? _clockTimer;

    public event Action? OnStateChanged;
    public event Action<int>? OnTatamiUpdated;
    public event Action<int>? OnTimerZero;
    public event Action<int, WeighInStationState>? OnWeighInStationUpdated;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, WeighInStationState> _weighInStations = new();

    public TournamentService(
        IServiceScopeFactory scopeFactory,
        IKumiteBracketGenerator bracketGenerator,
        IKataTournamentManager kataManager,
        IExcelTournamentService excelService)
    {
        _scopeFactory = scopeFactory;
        _bracketGenerator = bracketGenerator;
        _kataManager = kataManager;
        _excelService = excelService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        LoadFromDatabaseOrCreate();
        _clockTimer = new System.Threading.Timer(TimerTick, null, 1000, 1000);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _clockTimer?.Dispose();
        return Task.CompletedTask;
    }

    private void TimerTick(object? state)
    {
        bool anyChanged = false;
        var zeroTatamis = new List<int>();

        lock (_lock)
        {
            foreach (var tatami in _tatamis)
            {
                if (tatami.CurrentKumiteMatch != null && tatami.CurrentKumiteMatch.IsTimerRunning)
                {
                    if (tatami.CurrentKumiteMatch.RemainingSeconds > 0)
                    {
                        tatami.CurrentKumiteMatch.RemainingSeconds--;
                        anyChanged = true;

                        if (tatami.CurrentKumiteMatch.RemainingSeconds == 0)
                        {
                            tatami.CurrentKumiteMatch.IsTimerRunning = false;
                            tatami.CurrentKumiteMatch.Status = MatchStatus.Paused;
                            zeroTatamis.Add(tatami.Id);
                        }
                    }
                }
            }
        }

        if (anyChanged)
        {
            OnStateChanged?.Invoke();
            foreach (var t in _tatamis)
            {
                OnTatamiUpdated?.Invoke(t.Id);
            }
        }

        foreach (var tId in zeroTatamis)
        {
            OnTimerZero?.Invoke(tId);
        }
    }

    public Tournament GetTournament() => _tournament;
    public List<Tatami> GetTatamis() => _tatamis;
    public Tatami? GetTatami(int tatamiId) => _tatamis.FirstOrDefault(t => t.Id == tatamiId);
    public List<Category> GetCategories() => _categories;
    public Category? GetCategory(int categoryId) => _categories.FirstOrDefault(c => c.Id == categoryId);
    public List<Competitor> GetCompetitors() => _competitors;
    public List<KumiteMatch> GetMatches() => _matches;
    public KumiteMatch? GetMatch(int matchId) => _matches.FirstOrDefault(m => m.Id == matchId);
    public List<KataPerformance> GetKataPerformances() => _kataPerformances;

    public Competitor AddCompetitor(Competitor competitor, List<int>? categoryIds = null)
    {
        lock (_lock)
        {
            int nextId = _competitors.Count > 0 ? _competitors.Max(c => c.Id) + 1 : 1;
            competitor.Id = nextId;
            competitor.FirstName = competitor.FirstName?.Trim() ?? "";
            competitor.LastName = competitor.LastName?.Trim() ?? "";
            competitor.Country = string.IsNullOrWhiteSpace(competitor.Country) ? "France" : competitor.Country.Trim();
            competitor.Club = competitor.Club?.Trim() ?? "";

            _competitors.Add(competitor);

            if (categoryIds != null && categoryIds.Count > 0)
            {
                foreach (var catId in categoryIds)
                {
                    var cat = _categories.FirstOrDefault(c => c.Id == catId);
                    if (cat != null)
                    {
                        if (!cat.Competitors.Any(c => c.Id == competitor.Id)) cat.Competitors.Add(competitor);
                        if (!competitor.Categories.Any(c => c.Id == cat.Id)) competitor.Categories.Add(cat);
                    }
                }
            }

            SaveAllToDatabase();
            NotifyChange();
            return competitor;
        }
    }

    public void UpdateCompetitor(Competitor updated, List<int>? categoryIds = null)
    {
        lock (_lock)
        {
            var comp = _competitors.FirstOrDefault(c => c.Id == updated.Id);
            if (comp == null) return;

            comp.BibNumber = updated.BibNumber;
            comp.FirstName = updated.FirstName?.Trim() ?? "";
            comp.LastName = updated.LastName?.Trim() ?? "";
            comp.Country = string.IsNullOrWhiteSpace(updated.Country) ? "France" : updated.Country.Trim();
            comp.Club = updated.Club?.Trim() ?? "";
            comp.BirthDate = updated.BirthDate;
            comp.Age = updated.Age;
            comp.Weight = updated.Weight;
            comp.Grade = updated.Grade;
            comp.Gender = updated.Gender;
            comp.IsKumiteRegistered = updated.IsKumiteRegistered;
            comp.IsKataRegistered = updated.IsKataRegistered;
            comp.Status = updated.Status;

            if (categoryIds != null)
            {
                foreach (var cat in _categories)
                {
                    bool shouldBeInCat = categoryIds.Contains(cat.Id);
                    bool isInCat = cat.Competitors.Any(c => c.Id == comp.Id);

                    if (shouldBeInCat && !isInCat)
                    {
                        cat.Competitors.Add(comp);
                    }
                    else if (!shouldBeInCat && isInCat)
                    {
                        cat.Competitors.RemoveAll(c => c.Id == comp.Id);
                    }

                    bool inCompCat = comp.Categories.Any(c => c.Id == cat.Id);
                    if (shouldBeInCat && !inCompCat)
                    {
                        comp.Categories.Add(cat);
                    }
                    else if (!shouldBeInCat && inCompCat)
                    {
                        comp.Categories.RemoveAll(c => c.Id == cat.Id);
                    }
                }
            }

            SaveAllToDatabase();
            NotifyChange();
        }
    }

    public void SetCompetitorStatus(int competitorId, CompetitorStatus status)
    {
        lock (_lock)
        {
            var comp = _competitors.FirstOrDefault(c => c.Id == competitorId);
            if (comp == null) return;

            comp.Status = status;
            SaveCompetitorStateToDatabase(comp.Id);
            NotifyChange();
        }
    }

    public bool DeleteCompetitor(int competitorId)
    {
        lock (_lock)
        {
            var comp = _competitors.FirstOrDefault(c => c.Id == competitorId);
            if (comp == null) return false;

            _competitors.Remove(comp);
            foreach (var cat in _categories)
            {
                cat.Competitors.RemoveAll(c => c.Id == competitorId);
            }

            SaveAllToDatabase();
            NotifyChange();
            return true;
        }
    }


    public void SeedSampleData()
    {
        lock (_lock)
        {
            var templateBytes = _excelService.GenerateSampleExcelTemplate();
            using var ms = new MemoryStream(templateBytes);
            var result = _excelService.ImportTournamentData(ms);
            ApplyExcelImport(result);

            if (_advertisements.Count == 0)
            {
                _advertisements = GetDefaultSampleAdvertisements();
            }
        }
    }

    public void ResetTournament()
    {
        lock (_lock)
        {
            _competitors.Clear();
            _categories.Clear();
            _matches.Clear();
            _kataPerformances.Clear();

            foreach (var t in _tatamis)
            {
                t.CurrentKumiteMatch = null;
                t.CurrentKumiteMatchId = null;
                t.CurrentKataPerformance = null;
                t.CurrentKataPerformanceId = null;
                t.AssignedKumiteMatches.Clear();
                t.AssignedKataPerformances.Clear();
                t.IsPaused = false;
                t.PauseMessage = null;
            }

            if (_advertisements.Count == 0)
            {
                _advertisements = GetDefaultSampleAdvertisements();
            }

            SaveAllToDatabase();
            NotifyChange();
        }
    }

    public void ApplyExcelImport(ExcelImportResult importResult)
    {
        lock (_lock)
        {
            _competitors = importResult.Competitors;
            _categories = importResult.Categories;

            var importCompDict = _competitors.ToDictionary(c => c.Id);
            foreach (var cat in _categories)
            {
                for (int i = 0; i < cat.Competitors.Count; i++)
                {
                    if (importCompDict.TryGetValue(cat.Competitors[i].Id, out var sharedComp))
                    {
                        cat.Competitors[i] = sharedComp;
                        if (!sharedComp.Categories.Any(c => c.Id == cat.Id))
                        {
                            sharedComp.Categories.Add(cat);
                        }
                    }
                }
            }
            _matches.Clear();
            _kataPerformances.Clear();

            int catId = 1;
            int autoOrder = 1;
            foreach (var cat in _categories)
            {
                if (cat.Id <= 0) cat.Id = catId++;
                if (cat.OrderIndex <= 0) cat.OrderIndex = autoOrder++;
            }

            if (importResult.DaysCount > 0)
            {
                _tournament.DaysCount = importResult.DaysCount;
            }

            // S'assurer d'avoir au moins 3 tatamis par défaut
            if (_tatamis.Count == 0)
            {
                _tatamis = new List<Tatami>
                {
                    new() { Id = 1, Number = 1, Name = "Tatami 1 - Principal", Discipline = CompetitionType.Kumite, CurrentDiscipline = CompetitionType.Kumite },
                    new() { Id = 2, Number = 2, Name = "Tatami 2", Discipline = CompetitionType.Both, CurrentDiscipline = CompetitionType.Kata },
                    new() { Id = 3, Number = 3, Name = "Tatami 3 - Kata & Kumité", Discipline = CompetitionType.Kata, CurrentDiscipline = CompetitionType.Kata }
                };
            }

            // Générer immédiatement les tirages et tableaux
            GenerateAllBrackets();

            NotifyChange();
        }
    }

    public void GenerateAllBrackets()
    {
        lock (_lock)
        {
            _matches.Clear();
            _kataPerformances.Clear();

            int nextMatchId = 1;
            int nextKataPerfId = 1;

            // 1. Initialiser les arbres Kumite
            foreach (var category in _categories.Where(c => c.Type == CompetitionType.Kumite).OrderBy(c => c.CompetitionDay).ThenBy(c => c.OrderIndex).ThenBy(c => c.Id))
            {
                var catCompetitors = category.Competitors.Where(c => c.IsKumiteRegistered).ToList();
                var bracket = _bracketGenerator.GenerateBracket(category, catCompetitors, 1);
                foreach (var m in bracket)
                {
                    m.Id = nextMatchId++;
                    m.CompetitionDay = category.CompetitionDay;
                    if (category.AssignedTatamiId.HasValue) m.TatamiId = category.AssignedTatamiId.Value;
                    _matches.Add(m);
                }
                category.Matches = bracket;
            }

            // 2. Initialiser les Katas Tour 1 et pré-créer les créneaux pour Tour 2
            foreach (var category in _categories.Where(c => c.Type == CompetitionType.Kata).OrderBy(c => c.CompetitionDay).ThenBy(c => c.OrderIndex).ThenBy(c => c.Id))
            {
                var catCompetitors = category.Competitors.Where(c => c.IsKataRegistered).ToList();
                var performances = _kataManager.InitializeRound1(category, catCompetitors);
                foreach (var p in performances)
                {
                    p.Id = nextKataPerfId++;
                    p.CompetitionDay = category.CompetitionDay;
                    if (category.AssignedTatamiId.HasValue) p.TatamiId = category.AssignedTatamiId.Value;
                    _kataPerformances.Add(p);
                }

                // Créneaux pour le 2e tour (Finale)
                int qualifiersCount = Math.Min(category.KataQualifiersCount > 0 ? category.KataQualifiersCount : 4, catCompetitors.Count);
                var t2Placeholders = _kataManager.InitializeRound2Placeholders(category, qualifiersCount);
                foreach (var p in t2Placeholders)
                {
                    p.Id = nextKataPerfId++;
                    p.CompetitionDay = category.CompetitionDay;
                    if (category.AssignedTatamiId.HasValue) p.TatamiId = category.AssignedTatamiId.Value;
                    _kataPerformances.Add(p);
                }

                category.KataPerformances = _kataPerformances.Where(p => p.CategoryId == category.Id).ToList();
            }

            // 3. Appliquer la numérotation chronologique globale par jour :
            ApplyChronologicalNumbering();

            // 4. Dispatch initial sur les tatamis
            DistributeToTatamis();
            SaveAllToDatabase();
            NotifyChange();
        }
    }

    public void GenerateCategoryBracket(int categoryId)
    {
        lock (_lock)
        {
            var category = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (category == null) return;

            if (category.Type == CompetitionType.Kumite)
            {
                _matches.RemoveAll(m => m.CategoryId == categoryId);
                var catCompetitors = category.Competitors.Where(c => c.IsKumiteRegistered).ToList();
                int startId = _matches.Count > 0 ? _matches.Max(m => m.Id) + 1 : 1;

                var bracket = _bracketGenerator.GenerateBracket(category, catCompetitors, 1);
                foreach (var m in bracket)
                {
                    m.Id = startId++;
                    m.CompetitionDay = category.CompetitionDay;
                    if (category.AssignedTatamiId.HasValue) m.TatamiId = category.AssignedTatamiId.Value;
                    _matches.Add(m);
                }
                category.Matches = bracket;
            }
            else
            {
                _kataPerformances.RemoveAll(p => p.CategoryId == categoryId);
                var catCompetitors = category.Competitors.Where(c => c.IsKataRegistered).ToList();
                var perfs = _kataManager.InitializeRound1(category, catCompetitors);
                int startId = _kataPerformances.Count > 0 ? _kataPerformances.Max(p => p.Id) + 1 : 1;
                foreach (var p in perfs)
                {
                    p.Id = startId++;
                    p.CompetitionDay = category.CompetitionDay;
                    if (category.AssignedTatamiId.HasValue) p.TatamiId = category.AssignedTatamiId.Value;
                    _kataPerformances.Add(p);
                }

                int qualifiersCount = Math.Min(category.KataQualifiersCount > 0 ? category.KataQualifiersCount : 4, catCompetitors.Count);
                var t2 = _kataManager.InitializeRound2Placeholders(category, qualifiersCount);
                foreach (var p in t2)
                {
                    p.Id = startId++;
                    p.CompetitionDay = category.CompetitionDay;
                    if (category.AssignedTatamiId.HasValue) p.TatamiId = category.AssignedTatamiId.Value;
                    _kataPerformances.Add(p);
                }

                category.KataPerformances = _kataPerformances.Where(p => p.CategoryId == category.Id).ToList();
            }

            ApplyChronologicalNumbering();
            DistributeToTatamis();
            SaveAllToDatabase();
            NotifyChange();
        }
    }

    private int GetCategoryOrderIndex(int categoryId)
    {
        var cat = _categories.FirstOrDefault(c => c.Id == categoryId);
        return cat?.OrderIndex > 0 ? cat.OrderIndex : categoryId;
    }

    /// <summary>
    /// Numérote l'ensemble des tirages chronologiquement par tatami selon la règle :
    /// - Indépendamment sur chaque tatami (les numéros démarrent à 1 sur chaque tatami)
    /// - Par jour de compétition (Jour 1, Jour 2, etc.)
    /// - Pour les katas : chaque catégorie termine ses 2 tours (Tour 1 puis Finale) avant de passer à la suivante
    /// - Puis les combats du tatami du tour le plus éloigné vers les finales, ordonnés par ordre des catégories
    /// - Les rencontres non assignées à un tatami (ou tatami null) sont également numérotées séquentiellement
    /// </summary>
    private void ApplyChronologicalNumbering()
    {
        // 0. Synchroniser TatamiId depuis les catégories si non renseigné
        foreach (var m in _matches)
        {
            if (!m.TatamiId.HasValue)
            {
                var cat = _categories.FirstOrDefault(c => c.Id == m.CategoryId);
                if (cat?.AssignedTatamiId.HasValue == true)
                {
                    m.TatamiId = cat.AssignedTatamiId.Value;
                    m.Tatami = cat.AssignedTatami ?? _tatamis.FirstOrDefault(t => t.Id == m.TatamiId.Value);
                }
            }
        }
        foreach (var p in _kataPerformances)
        {
            if (!p.TatamiId.HasValue)
            {
                var cat = _categories.FirstOrDefault(c => c.Id == p.CategoryId);
                if (cat?.AssignedTatamiId.HasValue == true)
                {
                    p.TatamiId = cat.AssignedTatamiId.Value;
                    p.Tatami = cat.AssignedTatami ?? _tatamis.FirstOrDefault(t => t.Id == p.TatamiId.Value);
                }
            }
        }

        var allDays = _categories.Select(c => c.CompetitionDay)
            .Concat(_matches.Select(m => m.CompetitionDay))
            .Concat(_kataPerformances.Select(p => p.CompetitionDay))
            .DefaultIfEmpty(1)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        // Tatamis ordonnés par numéro/Id
        var tatamiIds = _tatamis.OrderBy(t => t.Number).ThenBy(t => t.Id).Select(t => (int?)t.Id).ToList();

        // S'il existe des rencontres sans tatami assigné (ou aucun tatami configuré)
        bool hasUnassigned = _matches.Any(m => !m.TatamiId.HasValue) || _kataPerformances.Any(p => !p.TatamiId.HasValue);
        if (hasUnassigned || tatamiIds.Count == 0)
        {
            tatamiIds.Add(null);
        }

        foreach (var tatamiId in tatamiIds)
        {
            int tatamiNumber = 1;

            foreach (var day in allDays)
            {
                // 1. Katas : Pour chaque catégorie Kata, on termine ses 2 tours (Tour 1 puis Finale) avant de passer à la suivante
                var dayKataCategoryIds = _kataPerformances
                    .Where(p => p.TatamiId == tatamiId && p.CompetitionDay == day)
                    .Select(p => p.CategoryId)
                    .Distinct()
                    .OrderBy(catId => GetCategoryOrderIndex(catId))
                    .ThenBy(catId => catId)
                    .ToList();

                foreach (var catId in dayKataCategoryIds)
                {
                    // Tour 1 de la catégorie
                    var catTour1 = _kataPerformances
                        .Where(p => p.TatamiId == tatamiId && p.CompetitionDay == day && p.CategoryId == catId && p.Round == KataRound.Turn1)
                        .OrderBy(p => p.PerformanceOrder)
                        .ThenBy(p => p.Id)
                        .ToList();

                    foreach (var p in catTour1)
                    {
                        p.PerformanceOrder = tatamiNumber;
                        p.GlobalMatchNumber = tatamiNumber;
                        tatamiNumber++;
                    }

                    // Tour 2 (Finale) de la catégorie
                    var catTour2 = _kataPerformances
                        .Where(p => p.TatamiId == tatamiId && p.CompetitionDay == day && p.CategoryId == catId && p.Round == KataRound.Turn2)
                        .OrderBy(p => p.PerformanceOrder)
                        .ThenBy(p => p.Id)
                        .ToList();

                    foreach (var p in catTour2)
                    {
                        p.PerformanceOrder = tatamiNumber;
                        p.GlobalMatchNumber = tatamiNumber;
                        tatamiNumber++;
                    }
                }

                // 2. Combats Kumité du jour sur ce tatami : du tour le plus éloigné vers la finale
                var dayMatches = _matches
                    .Where(m => m.TatamiId == tatamiId && m.CompetitionDay == day)
                    .ToList();

                if (dayMatches.Count > 0)
                {
                    int maxRoundFromFinal = dayMatches.Max(m => m.RoundFromFinal);

                    for (int r = maxRoundFromFinal; r >= 0; r--)
                    {
                        var matchesInThisRound = dayMatches
                            .Where(m => m.RoundFromFinal == r)
                            .OrderBy(m => GetCategoryOrderIndex(m.CategoryId))
                            .ThenBy(m => m.Id)
                            .ToList();

                        foreach (var match in matchesInThisRound)
                        {
                            match.MatchNumber = tatamiNumber++;
                        }
                    }
                }
            }
        }
    }

    public void AssignMatchToTatami(int matchId, int tatamiId)
    {
        lock (_lock)
        {
            var match = _matches.FirstOrDefault(m => m.Id == matchId);
            var targetTatami = _tatamis.FirstOrDefault(t => t.Id == tatamiId);
            if (match == null || targetTatami == null) return;

            int oldTatamiId = match.TatamiId ?? 0;
            if (oldTatamiId != 0 && oldTatamiId != tatamiId)
            {
                var oldTatami = _tatamis.FirstOrDefault(t => t.Id == oldTatamiId);
                if (oldTatami != null)
                {
                    oldTatami.AssignedKumiteMatches.Remove(match);
                    if (oldTatami.CurrentKumiteMatchId == matchId)
                    {
                        var nextM = oldTatami.AssignedKumiteMatches.FirstOrDefault(m => m.Status != MatchStatus.Finished);
                        oldTatami.CurrentKumiteMatchId = nextM?.Id;
                        oldTatami.CurrentKumiteMatch = nextM;
                    }
                }
            }

            match.TatamiId = tatamiId;
            match.Tatami = targetTatami;

            if (!targetTatami.AssignedKumiteMatches.Any(m => m.Id == matchId))
            {
                targetTatami.AssignedKumiteMatches.Add(match);
            }
            targetTatami.AssignedKumiteMatches = targetTatami.AssignedKumiteMatches.OrderBy(m => m.MatchNumber).ToList();

            if (targetTatami.CurrentKumiteMatchId == null)
            {
                SetTatamiCurrentMatch(tatamiId, matchId);
            }

            SaveMatchStateToDatabase(match.Id);
            SaveTatamiStateToDatabase(tatamiId);
            if (oldTatamiId > 0 && oldTatamiId != tatamiId)
            {
                SaveTatamiStateToDatabase(oldTatamiId);
                NotifyChange(oldTatamiId);
            }
            NotifyChange(tatamiId);
        }
    }

    public void AssignKataToTatami(int performanceId, int tatamiId)
    {
        lock (_lock)
        {
            var perf = _kataPerformances.FirstOrDefault(p => p.Id == performanceId);
            var targetTatami = _tatamis.FirstOrDefault(t => t.Id == tatamiId);
            if (perf == null || targetTatami == null) return;

            int oldTatamiId = perf.TatamiId ?? 0;
            if (oldTatamiId != 0 && oldTatamiId != tatamiId)
            {
                var oldTatami = _tatamis.FirstOrDefault(t => t.Id == oldTatamiId);
                if (oldTatami != null)
                {
                    oldTatami.AssignedKataPerformances.Remove(perf);
                    if (oldTatami.CurrentKataPerformanceId == performanceId)
                    {
                        var nextP = oldTatami.AssignedKataPerformances.FirstOrDefault(p => p.Status != MatchStatus.Finished);
                        oldTatami.CurrentKataPerformanceId = nextP?.Id;
                        oldTatami.CurrentKataPerformance = nextP;
                    }
                }
            }

            perf.TatamiId = tatamiId;
            perf.Tatami = targetTatami;

            if (!targetTatami.AssignedKataPerformances.Any(p => p.Id == performanceId))
            {
                targetTatami.AssignedKataPerformances.Add(perf);
            }
            targetTatami.AssignedKataPerformances = targetTatami.AssignedKataPerformances.OrderBy(p => p.PerformanceOrder).ToList();

            if (targetTatami.CurrentKataPerformanceId == null)
            {
                SetTatamiCurrentKata(tatamiId, performanceId);
            }

            SaveKataStateToDatabase(perf.Id);
            SaveTatamiStateToDatabase(tatamiId);
            if (oldTatamiId > 0 && oldTatamiId != tatamiId)
            {
                SaveTatamiStateToDatabase(oldTatamiId);
                NotifyChange(oldTatamiId);
            }
            NotifyChange(tatamiId);
        }
    }

    public void AssignCategoryMatchesToTatami(int categoryId, int tatamiId)
    {
        lock (_lock)
        {
            var cat = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (cat == null) return;

            if (tatamiId <= 0)
            {
                SetCategoryTatami(categoryId, null);
                return;
            }

            cat.AssignedTatamiId = tatamiId;
            cat.AssignedTatami = _tatamis.FirstOrDefault(t => t.Id == tatamiId);

            if (cat.Type == CompetitionType.Kumite)
            {
                var matches = _matches.Where(m => m.CategoryId == categoryId).ToList();
                foreach (var m in matches)
                {
                    AssignMatchToTatami(m.Id, tatamiId);
                }
            }
            else
            {
                var perfs = _kataPerformances.Where(p => p.CategoryId == categoryId).ToList();
                foreach (var p in perfs)
                {
                    AssignKataToTatami(p.Id, tatamiId);
                }
            }

            ApplyChronologicalNumbering();
            DistributeToTatamis();
            SaveAllToDatabase();
            NotifyChange();
        }
    }

    public void SetCategoryTatami(int categoryId, int? tatamiId)
    {
        lock (_lock)
        {
            var cat = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (cat == null) return;

            if (tatamiId.HasValue && tatamiId.Value > 0)
            {
                AssignCategoryMatchesToTatami(categoryId, tatamiId.Value);
            }
            else
            {
                // Affecter à aucun tatami (catégorie et rencontres non affichées sur les tatamis)
                cat.AssignedTatamiId = null;
                cat.AssignedTatami = null;

                var matches = _matches.Where(m => m.CategoryId == categoryId).ToList();
                foreach (var m in matches)
                {
                    int oldTatamiId = m.TatamiId ?? 0;
                    m.TatamiId = null;
                    m.Tatami = null;
                    if (oldTatamiId > 0)
                    {
                        var oldT = _tatamis.FirstOrDefault(t => t.Id == oldTatamiId);
                        if (oldT != null)
                        {
                            oldT.AssignedKumiteMatches.Remove(m);
                            if (oldT.CurrentKumiteMatchId == m.Id)
                            {
                                var nextM = oldT.AssignedKumiteMatches.FirstOrDefault(x => x.Status != MatchStatus.Finished);
                                oldT.CurrentKumiteMatchId = nextM?.Id;
                                oldT.CurrentKumiteMatch = nextM;
                            }
                            SaveTatamiStateToDatabase(oldTatamiId);
                        }
                    }
                    SaveMatchStateToDatabase(m.Id);
                }

                var perfs = _kataPerformances.Where(p => p.CategoryId == categoryId).ToList();
                foreach (var p in perfs)
                {
                    int oldTatamiId = p.TatamiId ?? 0;
                    p.TatamiId = null;
                    p.Tatami = null;
                    if (oldTatamiId > 0)
                    {
                        var oldT = _tatamis.FirstOrDefault(t => t.Id == oldTatamiId);
                        if (oldT != null)
                        {
                            oldT.AssignedKataPerformances.Remove(p);
                            if (oldT.CurrentKataPerformanceId == p.Id)
                            {
                                var nextP = oldT.AssignedKataPerformances.FirstOrDefault(x => x.Status != MatchStatus.Finished);
                                oldT.CurrentKataPerformanceId = nextP?.Id;
                                oldT.CurrentKataPerformance = nextP;
                            }
                            SaveTatamiStateToDatabase(oldTatamiId);
                        }
                    }
                    SaveKataStateToDatabase(p.Id);
                }

                ApplyChronologicalNumbering();
                SaveAllToDatabase();
                NotifyChange();
            }
        }
    }

    public void SetCategoryDay(int categoryId, int dayNumber)
    {
        lock (_lock)
        {
            var cat = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (cat == null) return;

            cat.CompetitionDay = Math.Max(1, dayNumber);
            foreach (var m in _matches.Where(m => m.CategoryId == categoryId))
            {
                m.CompetitionDay = cat.CompetitionDay;
            }
            foreach (var p in _kataPerformances.Where(p => p.CategoryId == categoryId))
            {
                p.CompetitionDay = cat.CompetitionDay;
            }

            ApplyChronologicalNumbering();
            SaveAllToDatabase();
            NotifyChange();
        }
    }

    public void SetCategoryOrderIndex(int categoryId, int orderIndex)
    {
        lock (_lock)
        {
            var cat = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (cat == null) return;

            cat.OrderIndex = Math.Max(1, orderIndex);

            ApplyChronologicalNumbering();
            SaveAllToDatabase();
            NotifyChange();
        }
    }

    public void MoveCategoryOrder(int categoryId, int direction)
    {
        lock (_lock)
        {
            var cat = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (cat == null) return;

            // Catégories du même jour triées selon leur ordre actuel
            var sameDayCats = _categories
                .Where(c => c.CompetitionDay == cat.CompetitionDay)
                .OrderBy(c => c.OrderIndex)
                .ThenBy(c => c.Id)
                .ToList();

            // Normaliser en 1, 2, 3...
            for (int i = 0; i < sameDayCats.Count; i++)
            {
                sameDayCats[i].OrderIndex = i + 1;
            }

            int currentIndex = sameDayCats.IndexOf(cat);
            int targetIndex = currentIndex + direction;

            if (targetIndex >= 0 && targetIndex < sameDayCats.Count)
            {
                var targetCat = sameDayCats[targetIndex];
                int temp = cat.OrderIndex;
                cat.OrderIndex = targetCat.OrderIndex;
                targetCat.OrderIndex = temp;

                ApplyChronologicalNumbering();
                SaveAllToDatabase();
                NotifyChange();
            }
        }
    }

    public void SetTournamentDaysCount(int daysCount)
    {
        lock (_lock)
        {
            _tournament.DaysCount = Math.Max(1, daysCount);
            // Si le jour actif dépasse le nouveau nombre de jours, le réinitialiser
            if (_tournament.ActiveDay.HasValue && _tournament.ActiveDay.Value > _tournament.DaysCount)
                _tournament.ActiveDay = null;
            SaveAllToDatabase();
            NotifyChange();
        }
    }

    public void SetActiveDay(int? day)
    {
        lock (_lock)
        {
            _tournament.ActiveDay = day;
            SaveAllToDatabase();
            NotifyChange();
        }
    }

    public void SetCategoryDayAndTatami(int categoryId, int dayNumber, int tatamiId)
    {
        lock (_lock)
        {
            var cat = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (cat == null) return;

            cat.CompetitionDay = Math.Max(1, dayNumber);
            foreach (var m in _matches.Where(m => m.CategoryId == categoryId))
            {
                m.CompetitionDay = cat.CompetitionDay;
            }
            foreach (var p in _kataPerformances.Where(p => p.CategoryId == categoryId))
            {
                p.CompetitionDay = cat.CompetitionDay;
            }

            if (tatamiId > 0)
            {
                AssignCategoryMatchesToTatami(categoryId, tatamiId);
            }
            else
            {
                SetCategoryTatami(categoryId, null);
            }
        }
    }

    public void SetMatchDay(int matchId, int dayNumber)
    {
        lock (_lock)
        {
            var match = _matches.FirstOrDefault(m => m.Id == matchId);
            if (match == null) return;

            match.CompetitionDay = Math.Max(1, dayNumber);
            ApplyChronologicalNumbering();
            SaveMatchStateToDatabase(matchId);
            NotifyChange(match.TatamiId ?? 0);
        }
    }

    public void SetKataDay(int performanceId, int dayNumber)
    {
        lock (_lock)
        {
            var perf = _kataPerformances.FirstOrDefault(p => p.Id == performanceId);
            if (perf == null) return;

            perf.CompetitionDay = Math.Max(1, dayNumber);
            ApplyChronologicalNumbering();
            SaveKataStateToDatabase(performanceId);
            NotifyChange(perf.TatamiId ?? 0);
        }
    }

    public void SwapKumiteCompetitors(int match1Id, CornerColor corner1, int match2Id, CornerColor corner2)
    {
        lock (_lock)
        {
            var m1 = _matches.FirstOrDefault(m => m.Id == match1Id);
            var m2 = _matches.FirstOrDefault(m => m.Id == match2Id);
            if (m1 == null || m2 == null) return;

            // Swapping permitted only within the same category
            if (m1.CategoryId != m2.CategoryId) return;

            // Swapping not permitted on matches already fought with a decisive reason
            if (m1.Status == MatchStatus.Finished && m1.WinnerReason != MatchWinnerReason.None) return;
            if (m2.Status == MatchStatus.Finished && m2.WinnerReason != MatchWinnerReason.None) return;

            int? c1Id = (corner1 == CornerColor.Aka) ? m1.AkaCompetitorId : m1.ShiroCompetitorId;
            var c1 = (corner1 == CornerColor.Aka) ? m1.AkaCompetitor : m1.ShiroCompetitor;

            int? c2Id = (corner2 == CornerColor.Aka) ? m2.AkaCompetitorId : m2.ShiroCompetitorId;
            var c2 = (corner2 == CornerColor.Aka) ? m2.AkaCompetitor : m2.ShiroCompetitor;

            // Règle d'or du tirage : chaque combat du 1er tour doit comporter au moins un combattant (jamais 0 combattant)
            int? simM1Aka = (corner1 == CornerColor.Aka) ? c2Id : m1.AkaCompetitorId;
            int? simM1Shiro = (corner1 == CornerColor.Shiro) ? c2Id : m1.ShiroCompetitorId;
            int? simM2Aka = (corner2 == CornerColor.Aka) ? c1Id : m2.AkaCompetitorId;
            int? simM2Shiro = (corner2 == CornerColor.Shiro) ? c1Id : m2.ShiroCompetitorId;

            if (m1.BracketRound == 1 && !simM1Aka.HasValue && !simM1Shiro.HasValue) return;
            if (m2.BracketRound == 1 && !simM2Aka.HasValue && !simM2Shiro.HasValue) return;

            if (corner1 == CornerColor.Aka)
            {
                m1.AkaCompetitorId = c2Id;
                m1.AkaCompetitor = c2;
            }
            else
            {
                m1.ShiroCompetitorId = c2Id;
                m1.ShiroCompetitor = c2;
            }

            if (corner2 == CornerColor.Aka)
            {
                m2.AkaCompetitorId = c1Id;
                m2.AkaCompetitor = c1;
            }
            else
            {
                m2.ShiroCompetitorId = c1Id;
                m2.ShiroCompetitor = c1;
            }

            UpdateMatchByeAndProgression(m1);
            if (m1.Id != m2.Id)
            {
                UpdateMatchByeAndProgression(m2);
            }

            SaveAllToDatabase();
            NotifyChange();
        }
    }

    private void UpdateMatchByeAndProgression(KumiteMatch match)
    {
        if (match.Status == MatchStatus.Finished && match.WinnerReason != MatchWinnerReason.None)
        {
            return;
        }

        // Normalize if only Shiro is present
        if (!match.AkaCompetitorId.HasValue && match.ShiroCompetitorId.HasValue)
        {
            match.AkaCompetitorId = match.ShiroCompetitorId;
            match.AkaCompetitor = match.ShiroCompetitor;
            match.ShiroCompetitorId = null;
            match.ShiroCompetitor = null;
        }

        if (match.AkaCompetitorId.HasValue && !match.ShiroCompetitorId.HasValue)
        {
            // BYE (Exempt)
            match.WinnerCompetitorId = match.AkaCompetitorId;
            match.WinnerCompetitor = match.AkaCompetitor;
            match.WinnerReason = MatchWinnerReason.None;
            match.Status = MatchStatus.Finished;

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
                UpdateMatchByeAndProgression(match.NextMatch);
            }
        }
        else if (match.AkaCompetitorId.HasValue && match.ShiroCompetitorId.HasValue)
        {
            // Two competitors -> Regular match
            match.WinnerCompetitorId = null;
            match.WinnerCompetitor = null;
            match.WinnerReason = MatchWinnerReason.None;
            match.Status = MatchStatus.Pending;

            if (match.NextMatch != null)
            {
                if (match.NextMatchCorner == CornerColor.Aka)
                {
                    if (match.NextMatch.AkaCompetitorId == match.AkaCompetitorId || match.NextMatch.AkaCompetitorId == match.ShiroCompetitorId)
                    {
                        match.NextMatch.AkaCompetitorId = null;
                        match.NextMatch.AkaCompetitor = null;
                    }
                }
                else
                {
                    if (match.NextMatch.ShiroCompetitorId == match.AkaCompetitorId || match.NextMatch.ShiroCompetitorId == match.ShiroCompetitorId)
                    {
                        match.NextMatch.ShiroCompetitorId = null;
                        match.NextMatch.ShiroCompetitor = null;
                    }
                }
                UpdateMatchByeAndProgression(match.NextMatch);
            }
        }
        else
        {
            // Empty
            match.WinnerCompetitorId = null;
            match.WinnerCompetitor = null;
            match.WinnerReason = MatchWinnerReason.None;
            match.Status = MatchStatus.Pending;

            if (match.NextMatch != null)
            {
                if (match.NextMatchCorner == CornerColor.Aka)
                {
                    match.NextMatch.AkaCompetitorId = null;
                    match.NextMatch.AkaCompetitor = null;
                }
                else
                {
                    match.NextMatch.ShiroCompetitorId = null;
                    match.NextMatch.ShiroCompetitor = null;
                }
                UpdateMatchByeAndProgression(match.NextMatch);
            }
        }
    }

    public void SwapKataPerformances(int perf1Id, int perf2Id)
    {
        lock (_lock)
        {
            var p1 = _kataPerformances.FirstOrDefault(p => p.Id == perf1Id);
            var p2 = _kataPerformances.FirstOrDefault(p => p.Id == perf2Id);
            if (p1 == null || p2 == null) return;
            if (p1.CategoryId != p2.CategoryId) return;
            if (p1.Round != p2.Round) return;
            if (p1.Status == MatchStatus.Finished || p2.Status == MatchStatus.Finished) return;

            var tempCompId = p1.CompetitorId;
            var tempComp = p1.Competitor;
            p1.CompetitorId = p2.CompetitorId;
            p1.Competitor = p2.Competitor;
            p2.CompetitorId = tempCompId;
            p2.Competitor = tempComp;

            SaveKataStateToDatabase(p1.Id);
            SaveKataStateToDatabase(p2.Id);
            NotifyChange(p1.TatamiId ?? 0);
        }
    }

    public void SetTatamiCurrentMatch(int tatamiId, int matchId)
    {
        lock (_lock)
        {
            var tatami = _tatamis.FirstOrDefault(t => t.Id == tatamiId);
            var match = _matches.FirstOrDefault(m => m.Id == matchId);
            if (tatami == null) return;

            tatami.CurrentKumiteMatchId = matchId;
            tatami.CurrentKumiteMatch = match;
            
            // Katas en premier : si tatami mixte et des katas sont encore en attente, conserver le mode Kata
            if (tatami.Discipline == CompetitionType.Both && !IsKataCompetitionFinished(tatamiId: tatami.Id))
            {
                tatami.CurrentDiscipline = CompetitionType.Kata;
            }
            else
            {
                tatami.CurrentDiscipline = CompetitionType.Kumite;
            }

            if (match != null && match.Status == MatchStatus.Pending)
            {
                match.Status = MatchStatus.Ready;
            }

            NotifyChange(tatamiId);
        }
    }

    public void ToggleTimer(int tatamiId)
    {
        lock (_lock)
        {
            var tatami = _tatamis.FirstOrDefault(t => t.Id == tatamiId);
            var match = tatami?.CurrentKumiteMatch;
            if (match == null) return;

            match.IsTimerRunning = !match.IsTimerRunning;
            match.Status = match.IsTimerRunning ? MatchStatus.InProgress : MatchStatus.Paused;

            NotifyChange(tatamiId);
        }
    }

    public void ResetTimer(int tatamiId)
    {
        lock (_lock)
        {
            var tatami = _tatamis.FirstOrDefault(t => t.Id == tatamiId);
            var match = tatami?.CurrentKumiteMatch;
            if (match == null) return;

            match.IsTimerRunning = false;
            match.RemainingSeconds = match.TotalDurationSeconds;
            match.Status = MatchStatus.Ready;

            NotifyChange(tatamiId);
        }
    }

    public void AdjustTimer(int tatamiId, int deltaSeconds)
    {
        lock (_lock)
        {
            var tatami = _tatamis.FirstOrDefault(t => t.Id == tatamiId);
            var match = tatami?.CurrentKumiteMatch;
            if (match == null) return;

            match.RemainingSeconds = Math.Max(0, match.RemainingSeconds + deltaSeconds);
            NotifyChange(tatamiId);
        }
    }

    /// <summary>
    /// Ajoute dynamiquement une prolongation (Encho-sen) en cas d'égalité en Kyokushin
    /// </summary>
    public void AddExtension(int tatamiId)
    {
        lock (_lock)
        {
            var tatami = _tatamis.FirstOrDefault(t => t.Id == tatamiId);
            var match = tatami?.CurrentKumiteMatch;
            if (match == null) return;

            int extensionSeconds = match.Category?.KumiteExtensionDurationSeconds ?? 120;
            match.ExtensionsCount++;
            match.TotalDurationSeconds = extensionSeconds;
            match.RemainingSeconds = extensionSeconds;
            match.IsTimerRunning = false;
            match.Status = MatchStatus.Ready;

            SaveMatchStateToDatabase(match.Id);
            NotifyChange(tatamiId);
        }
    }

    public void AddScore(int matchId, CornerColor corner, string type)
    {
        lock (_lock)
        {
            var match = _matches.FirstOrDefault(m => m.Id == matchId);
            if (match == null) return;

            switch (type.ToLowerInvariant())
            {
                case "wazaari":
                    if (corner == CornerColor.Aka)
                    {
                        match.AkaWazaAri++;
                        if (match.AkaWazaAri >= 2)
                        {
                            // Awase-Ippon !
                            match.AkaIppon = 1;
                            DeclareWinner(matchId, CornerColor.Aka, MatchWinnerReason.AwaseIppon);
                            return;
                        }
                    }
                    else
                    {
                        match.ShiroWazaAri++;
                        if (match.ShiroWazaAri >= 2)
                        {
                            match.ShiroIppon = 1;
                            DeclareWinner(matchId, CornerColor.Shiro, MatchWinnerReason.AwaseIppon);
                            return;
                        }
                    }
                    break;

                case "ippon":
                    if (corner == CornerColor.Aka)
                    {
                        match.AkaIppon = 1;
                        DeclareWinner(matchId, CornerColor.Aka, MatchWinnerReason.Ippon);
                        return;
                    }
                    else
                    {
                        match.ShiroIppon = 1;
                        DeclareWinner(matchId, CornerColor.Shiro, MatchWinnerReason.Ippon);
                        return;
                    }

                case "chui":
                    if (corner == CornerColor.Aka)
                    {
                        match.AkaChui++;
                        if (match.AkaChui >= 2)
                        {
                            // 2 Chui = Genten Ichi
                            match.AkaChui = 0;
                            match.AkaGenten++;
                        }
                    }
                    else
                    {
                        match.ShiroChui++;
                        if (match.ShiroChui >= 2)
                        {
                            match.ShiroChui = 0;
                            match.ShiroGenten++;
                        }
                    }
                    break;

                case "genten":
                    if (corner == CornerColor.Aka)
                    {
                        match.AkaGenten++;
                        if (match.AkaGenten >= 2)
                        {
                            // Disqualification Hansoku -> Shiro gagne
                            DeclareWinner(matchId, CornerColor.Shiro, MatchWinnerReason.GentenDisqualification);
                            return;
                        }
                    }
                    else
                    {
                        match.ShiroGenten++;
                        if (match.ShiroGenten >= 2)
                        {
                            // Disqualification Hansoku -> Aka gagne
                            DeclareWinner(matchId, CornerColor.Aka, MatchWinnerReason.GentenDisqualification);
                            return;
                        }
                    }
                    break;

                case "remove_wazaari":
                    if (corner == CornerColor.Aka) match.AkaWazaAri = Math.Max(0, match.AkaWazaAri - 1);
                    else match.ShiroWazaAri = Math.Max(0, match.ShiroWazaAri - 1);
                    break;

                case "remove_chui":
                    if (corner == CornerColor.Aka) match.AkaChui = Math.Max(0, match.AkaChui - 1);
                    else match.ShiroChui = Math.Max(0, match.ShiroChui - 1);
                    break;

                case "remove_genten":
                    if (corner == CornerColor.Aka) match.AkaGenten = Math.Max(0, match.AkaGenten - 1);
                    else match.ShiroGenten = Math.Max(0, match.ShiroGenten - 1);
                    break;
            }

            SaveMatchStateToDatabase(match.Id);
            NotifyChange(match.TatamiId ?? 0);
        }
    }

    public void DeclareWinner(int matchId, CornerColor winnerCorner, MatchWinnerReason reason)
    {
        lock (_lock)
        {
            var match = _matches.FirstOrDefault(m => m.Id == matchId);
            if (match == null) return;

            var winner = (winnerCorner == CornerColor.Aka) ? match.AkaCompetitor : match.ShiroCompetitor;
            if (winner == null) return;

            match.WinnerCompetitorId = winner.Id;
            match.WinnerCompetitor = winner;
            match.WinnerReason = reason;
            match.Status = MatchStatus.Finished;
            match.IsTimerRunning = false;

            // Propagation vers le tour suivant dans l'arbre !
            if (match.NextMatch != null)
            {
                if (match.NextMatchCorner == CornerColor.Aka)
                {
                    match.NextMatch.AkaCompetitorId = winner.Id;
                    match.NextMatch.AkaCompetitor = winner;
                }
                else
                {
                    match.NextMatch.ShiroCompetitorId = winner.Id;
                    match.NextMatch.ShiroCompetitor = winner;
                }

                // Si les deux combattants du tour suivant sont maintenant prêts
                if (match.NextMatch.AkaCompetitorId.HasValue && match.NextMatch.ShiroCompetitorId.HasValue)
                {
                    match.NextMatch.Status = MatchStatus.Ready;
                }

                SaveMatchStateToDatabase(match.NextMatch.Id);
            }

            // Propagation du perdant vers la Petite Finale si applicable
            if (match.BronzeMatch != null)
            {
                var loser = (winnerCorner == CornerColor.Aka) ? match.ShiroCompetitor : match.AkaCompetitor;
                if (loser != null)
                {
                    if (match.BronzeMatchCorner == CornerColor.Aka)
                    {
                        match.BronzeMatch.AkaCompetitorId = loser.Id;
                        match.BronzeMatch.AkaCompetitor = loser;
                    }
                    else
                    {
                        match.BronzeMatch.ShiroCompetitorId = loser.Id;
                        match.BronzeMatch.ShiroCompetitor = loser;
                    }

                    if (match.BronzeMatch.AkaCompetitorId.HasValue && match.BronzeMatch.ShiroCompetitorId.HasValue)
                    {
                        match.BronzeMatch.Status = MatchStatus.Ready;
                    }

                    SaveMatchStateToDatabase(match.BronzeMatch.Id);
                }
            }

            // Passer automatiquement au combat suivant disponible sur ce tatami
            if (match.TatamiId.HasValue)
            {
                var tatami = _tatamis.FirstOrDefault(t => t.Id == match.TatamiId.Value);
                if (tatami != null)
                {
                    // Liste des combats restants non terminés assignés à ce tatami (hors combat actuel et exempts)
                    var remainingMatches = _matches
                        .Where(m => m.TatamiId == tatami.Id && m.Id != matchId && m.Status != MatchStatus.Finished && !m.IsBye)
                        .OrderBy(m => m.MatchNumber)
                        .ToList();

                    // Priorité 1 : Prochain combat dans l'ordre chronologique dont les 2 combattants sont prêts
                    var nextMatch = remainingMatches
                        .FirstOrDefault(m => m.MatchNumber > match.MatchNumber && m.AkaCompetitorId.HasValue && m.ShiroCompetitorId.HasValue);

                    // Priorité 2 : Si aucun combat postérieur n'est prêt, chercher parmi les combats antérieurs du tatami
                    if (nextMatch == null)
                    {
                        nextMatch = remainingMatches
                            .FirstOrDefault(m => m.AkaCompetitorId.HasValue && m.ShiroCompetitorId.HasValue);
                    }

                    // Priorité 3 : S'il reste des combats mais qu'au moins un adversaire est en attente
                    if (nextMatch == null)
                    {
                        nextMatch = remainingMatches.FirstOrDefault(m => m.MatchNumber > match.MatchNumber)
                                 ?? remainingMatches.FirstOrDefault();
                    }

                    if (nextMatch != null)
                    {
                        tatami.CurrentKumiteMatchId = nextMatch.Id;
                        tatami.CurrentKumiteMatch = nextMatch;
                        tatami.CurrentDiscipline = CompetitionType.Kumite;

                        if (nextMatch.Status == MatchStatus.Pending)
                        {
                            nextMatch.Status = MatchStatus.Ready;
                        }

                        SaveMatchStateToDatabase(nextMatch.Id);
                    }
                    else
                    {
                        // Tous les combats du tatami sont terminés
                        tatami.CurrentKumiteMatchId = null;
                        tatami.CurrentKumiteMatch = null;
                    }

                    SaveTatamiStateToDatabase(tatami.Id);
                }
            }

            SaveMatchStateToDatabase(match.Id);
            if (match.NextMatchId.HasValue)
            {
                SaveMatchStateToDatabase(match.NextMatchId.Value);
            }

            NotifyChange(match.TatamiId ?? 0);
        }
    }

    public void SetTatamiCurrentKata(int tatamiId, int performanceId)
    {
        lock (_lock)
        {
            var tatami = _tatamis.FirstOrDefault(t => t.Id == tatamiId);
            var perf = _kataPerformances.FirstOrDefault(p => p.Id == performanceId);
            if (tatami == null) return;

            // Si l'ancien passage était InProgress sans être terminé, le repasser en Pending
            if (tatami.CurrentKataPerformance != null && tatami.CurrentKataPerformance.Id != performanceId && tatami.CurrentKataPerformance.Status == MatchStatus.InProgress)
            {
                tatami.CurrentKataPerformance.Status = MatchStatus.Pending;
                SaveKataStateToDatabase(tatami.CurrentKataPerformance.Id);
            }

            tatami.CurrentKataPerformanceId = performanceId;
            tatami.CurrentKataPerformance = perf;
            tatami.CurrentDiscipline = CompetitionType.Kata;

            if (perf != null && perf.Status == MatchStatus.Pending)
            {
                perf.Status = MatchStatus.InProgress;
                SaveKataStateToDatabase(perf.Id);
            }

            SaveTatamiStateToDatabase(tatami.Id);
            NotifyChange(tatamiId);
        }
    }

    public void UpdateLiveKataScores(int performanceId, List<double> scores)
    {
        lock (_lock)
        {
            var perf = _kataPerformances.FirstOrDefault(p => p.Id == performanceId);
            if (perf == null) return;

            _kataManager.CalculateLiveScore(perf, scores);
            SaveKataStateToDatabase(perf.Id);

            if (perf.TatamiId.HasValue)
            {
                var tatami = _tatamis.FirstOrDefault(t => t.Id == perf.TatamiId.Value);
                if (tatami != null && tatami.CurrentKataPerformanceId == perf.Id)
                {
                    tatami.CurrentKataPerformance = perf;
                }
                NotifyChange(perf.TatamiId.Value);
            }
            else
            {
                NotifyChange();
            }
        }
    }

    public void ResetKataScores(int performanceId)
    {
        lock (_lock)
        {
            var perf = _kataPerformances.FirstOrDefault(p => p.Id == performanceId);
            if (perf == null) return;

            perf.JudgeScores.Clear();
            perf.DroppedLowestScore = null;
            perf.DroppedHighestScore = null;
            perf.FinalScore = 0;
            if (perf.Status == MatchStatus.Finished)
            {
                perf.Status = MatchStatus.InProgress;
            }

            SaveKataStateToDatabase(perf.Id);

            if (perf.TatamiId.HasValue)
            {
                var tatami = _tatamis.FirstOrDefault(t => t.Id == perf.TatamiId.Value);
                if (tatami != null && tatami.CurrentKataPerformanceId == perf.Id)
                {
                    tatami.CurrentKataPerformance = perf;
                }
                NotifyChange(perf.TatamiId.Value);
            }
            else
            {
                NotifyChange();
            }
        }
    }

    public void SubmitKataScores(int performanceId, List<double> scores)
    {
        lock (_lock)
        {
            var perf = _kataPerformances.FirstOrDefault(p => p.Id == performanceId);
            if (perf == null) return;

            _kataManager.CalculateScore(perf, scores);
            SaveKataStateToDatabase(perf.Id);

            // Dès qu'une note est saisie au Tour 1, mettre à jour immédiatement les qualifiés et l'ordre de passage du Tour 2
            if (perf.Round == KataRound.Turn1)
            {
                QualifyKataRound1(perf.CategoryId);
            }
            else if (perf.Round == KataRound.Turn2)
            {
                var catT2 = _kataPerformances.Where(p => p.CategoryId == perf.CategoryId && p.Round == KataRound.Turn2).ToList();
                if (catT2.Count > 0 && catT2.All(p => p.Status == MatchStatus.Finished))
                {
                    FinalizeKataRound2(perf.CategoryId);
                }
            }

            // Passer automatiquement au passage Kata suivant disponible sur ce tatami
            if (perf.TatamiId.HasValue)
            {
                var tatami = _tatamis.FirstOrDefault(t => t.Id == perf.TatamiId.Value);
                if (tatami != null)
                {
                    var nextPerf = _kataPerformances
                        .Where(p => p.TatamiId == tatami.Id && p.Id != performanceId && p.Status != MatchStatus.Finished)
                        .OrderBy(p => p.PerformanceOrder)
                        .FirstOrDefault();

                    if (nextPerf != null)
                    {
                        tatami.CurrentKataPerformanceId = nextPerf.Id;
                        tatami.CurrentKataPerformance = nextPerf;
                        tatami.CurrentDiscipline = CompetitionType.Kata;
                        if (nextPerf.Status == MatchStatus.Pending)
                        {
                            nextPerf.Status = MatchStatus.InProgress;
                            SaveKataStateToDatabase(nextPerf.Id);
                        }
                    }
                    else
                    {
                        tatami.CurrentKataPerformanceId = null;
                        tatami.CurrentKataPerformance = null;

                        // Si plus de Katas en attente sur ce tatami, basculer vers les combats s'il y en a
                        if (tatami.Discipline == CompetitionType.Both || tatami.AssignedKumiteMatches.Count > 0)
                        {
                            tatami.CurrentDiscipline = CompetitionType.Kumite;
                            if (tatami.CurrentKumiteMatch == null && tatami.AssignedKumiteMatches.Count > 0)
                            {
                                tatami.CurrentKumiteMatch = tatami.AssignedKumiteMatches.FirstOrDefault(m => m.Status != MatchStatus.Finished) ?? tatami.AssignedKumiteMatches[0];
                                tatami.CurrentKumiteMatchId = tatami.CurrentKumiteMatch?.Id;
                            }
                        }
                    }

                    SaveTatamiStateToDatabase(tatami.Id);
                }
            }

            NotifyChange(perf.TatamiId ?? 0);
        }
    }

    public void QualifyKataRound1(int categoryId)
    {
        lock (_lock)
        {
            var category = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (category == null) return;

            var r1Perfs = _kataPerformances.Where(p => p.CategoryId == categoryId && p.Round == KataRound.Turn1).ToList();
            foreach (var r1 in r1Perfs)
            {
                if (r1.Competitor == null && r1.CompetitorId.HasValue)
                {
                    r1.Competitor = _competitors.FirstOrDefault(c => c.Id == r1.CompetitorId.Value);
                }
            }

            _kataManager.FinalizeRound1AndQualify(category, r1Perfs);

            foreach (var r1 in r1Perfs)
            {
                SaveKataStateToDatabase(r1.Id);
            }

            int qualifiersCount = Math.Min(category.KataQualifiersCount > 0 ? category.KataQualifiersCount : 4, r1Perfs.Count);

            // Utiliser les créneaux Tour 2 déjà prévus et numérotés
            var existingT2 = _kataPerformances
                .Where(p => p.CategoryId == categoryId && p.Round == KataRound.Turn2)
                .OrderBy(p => p.PerformanceOrder)
                .ToList();

            bool t2Started = existingT2.Any(p => p.Status == MatchStatus.Finished || p.Status == MatchStatus.InProgress);
            if (!t2Started && existingT2.Count != qualifiersCount)
            {
                _kataPerformances.RemoveAll(p => p.CategoryId == categoryId && p.Round == KataRound.Turn2);
                int nextId = _kataPerformances.Count > 0 ? _kataPerformances.Max(p => p.Id) + 1 : 1;
                existingT2 = _kataManager.InitializeRound2Placeholders(category, qualifiersCount);
                foreach (var p in existingT2)
                {
                    p.Id = nextId++;
                    _kataPerformances.Add(p);
                }
                ApplyChronologicalNumbering();
            }

            if (existingT2.Count > 0)
            {
                _kataManager.AssignRound2Qualifiers(r1Perfs, existingT2);
            }
            else
            {
                // Fallback si non pré-créés
                var r2Perfs = _kataManager.InitializeRound2(category, r1Perfs);
                int nextId = _kataPerformances.Count > 0 ? _kataPerformances.Max(p => p.Id) + 1 : 1;
                foreach (var p in r2Perfs)
                {
                    p.Id = nextId++;
                    _kataPerformances.Add(p);
                }
                ApplyChronologicalNumbering();
                existingT2 = r2Perfs;
            }

            foreach (var t2 in existingT2)
            {
                if (t2.Competitor == null && t2.CompetitorId.HasValue)
                {
                    t2.Competitor = _competitors.FirstOrDefault(c => c.Id == t2.CompetitorId.Value);
                }
                SaveKataStateToDatabase(t2.Id);
            }

            category.KataPerformances = _kataPerformances.Where(p => p.CategoryId == categoryId).ToList();

            // Attribuer au bon tatami (même tatami que le Tour 1 ou la catégorie)
            int? targetTatamiId = category.AssignedTatamiId 
                ?? r1Perfs.FirstOrDefault(p => p.TatamiId.HasValue)?.TatamiId 
                ?? _tatamis.FirstOrDefault(t => t.CurrentDiscipline == CompetitionType.Kata)?.Id 
                ?? _tatamis.LastOrDefault()?.Id;

            if (targetTatamiId.HasValue)
            {
                var targetTatami = _tatamis.FirstOrDefault(t => t.Id == targetTatamiId.Value);
                if (targetTatami != null)
                {
                    var catT2 = _kataPerformances.Where(p => p.CategoryId == categoryId && p.Round == KataRound.Turn2).ToList();
                    foreach (var p in catT2)
                    {
                        p.TatamiId = targetTatami.Id;
                        p.Tatami = targetTatami;
                        if (!targetTatami.AssignedKataPerformances.Any(x => x.Id == p.Id))
                        {
                            targetTatami.AssignedKataPerformances.Add(p);
                        }
                    }
                    targetTatami.AssignedKataPerformances = targetTatami.AssignedKataPerformances.OrderBy(p => p.PerformanceOrder).ToList();
                }
            }

            // Mettre à jour les AssignedKataPerformances de tous les tatamis
            foreach (var t in _tatamis)
            {
                t.AssignedKataPerformances = _kataPerformances
                    .Where(p => p.TatamiId == t.Id)
                    .OrderBy(p => p.PerformanceOrder)
                    .ToList();
            }

            SaveAllToDatabase();
            NotifyChange(targetTatamiId ?? 0);
        }
    }

    public void FinalizeKataRound2(int categoryId)
    {
        lock (_lock)
        {
            var r2Perfs = _kataPerformances.Where(p => p.CategoryId == categoryId && p.Round == KataRound.Turn2).ToList();
            _kataManager.FinalizeRound2(r2Perfs);
            SaveAllToDatabase();
            NotifyChange();
        }
    }

    private void DistributeToTatamis()
    {
        if (_tatamis.Count == 0) return;

        foreach (var t in _tatamis)
        {
            t.AssignedKumiteMatches = _matches.Where(m => m.TatamiId == t.Id && !m.IsBye).OrderBy(m => m.MatchNumber).ToList();
            t.AssignedKataPerformances = _kataPerformances.Where(p => p.TatamiId == t.Id).OrderBy(p => p.PerformanceOrder).ToList();

            if (t.CurrentKumiteMatch == null && t.AssignedKumiteMatches.Count > 0)
            {
                var firstReady = t.AssignedKumiteMatches.FirstOrDefault(m => m.Status != MatchStatus.Finished) ?? t.AssignedKumiteMatches[0];
                t.CurrentKumiteMatchId = firstReady.Id;
                t.CurrentKumiteMatch = firstReady;
                if (firstReady.Status == MatchStatus.Pending) firstReady.Status = MatchStatus.Ready;
            }

            if (t.CurrentKataPerformance == null && t.AssignedKataPerformances.Count > 0)
            {
                var firstReady = t.AssignedKataPerformances.FirstOrDefault(p => p.Status != MatchStatus.Finished) ?? t.AssignedKataPerformances[0];
                t.CurrentKataPerformanceId = firstReady.Id;
                t.CurrentKataPerformance = firstReady;
                if (firstReady.Status == MatchStatus.Pending)
                {
                    firstReady.Status = MatchStatus.InProgress;
                }
            }
            else if (t.CurrentKataPerformance != null && t.CurrentKataPerformance.Status == MatchStatus.Pending)
            {
                t.CurrentKataPerformance.Status = MatchStatus.InProgress;
            }

            // S'assurer qu'un seul match et un seul kata peuvent avoir le statut InProgress sur ce tatami
            foreach (var m in t.AssignedKumiteMatches)
            {
                if (m.Id != t.CurrentKumiteMatchId && m.Status == MatchStatus.InProgress)
                {
                    m.Status = MatchStatus.Pending;
                }
            }
            foreach (var p in t.AssignedKataPerformances)
            {
                if (p.Id != t.CurrentKataPerformanceId && p.Status == MatchStatus.InProgress)
                {
                    p.Status = MatchStatus.Pending;
                }
            }
        }
    }

    public void BalanceCategoriesAcrossTatamis(bool resetManualAssignments = true)
    {
        lock (_lock)
        {
            if (_tatamis.Count == 0) return;

            var kumiteTatamis = _tatamis.Where(t => t.Discipline == CompetitionType.Kumite || t.Discipline == CompetitionType.Both).ToList();
            if (kumiteTatamis.Count == 0) kumiteTatamis = _tatamis.ToList();

            var kataTatamis = _tatamis.Where(t => t.Discipline == CompetitionType.Kata || t.Discipline == CompetitionType.Both).ToList();
            if (kataTatamis.Count == 0) kataTatamis = _tatamis.ToList();

            // Si réinitialisation demandée (par ex. bouton 'Rééquilibrer les combats' ou ajout/suppression de tatami),
            // on efface les affectations précédentes pour permettre une répartition optimale
            if (resetManualAssignments)
            {
                foreach (var c in _categories)
                {
                    c.AssignedTatamiId = null;
                    c.AssignedTatami = null;
                }
            }

            foreach (var t in _tatamis)
            {
                t.AssignedKumiteMatches.Clear();
                t.AssignedKataPerformances.Clear();
            }

            // Rééquilibrage par jour de compétition afin que chaque jour soit équilibré entre les tatamis
            var allDays = _categories.Select(c => c.CompetitionDay)
                .Concat(_matches.Select(m => m.CompetitionDay))
                .Concat(_kataPerformances.Select(p => p.CompetitionDay))
                .DefaultIfEmpty(1)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            foreach (var day in allDays)
            {
                // 1. Kumité pour ce jour
                var dayKumiteCategories = _categories
                    .Where(c => c.Type == CompetitionType.Kumite && c.CompetitionDay == day)
                    .Select(c => new
                    {
                        Category = c,
                        Matches = _matches.Where(m => m.CategoryId == c.Id && !m.IsBye).ToList()
                    })
                    .OrderByDescending(x => x.Matches.Count) // Heuristique LPT (Longest Processing Time)
                    .ToList();

                var kumiteLoads = kumiteTatamis.ToDictionary(t => t.Id, t => 0);

                // Prendre en compte les catégories déjà assignées manuellement (si non réinitialisées)
                foreach (var item in dayKumiteCategories)
                {
                    if (item.Category.AssignedTatamiId.HasValue && kumiteTatamis.Any(t => t.Id == item.Category.AssignedTatamiId.Value))
                    {
                        kumiteLoads[item.Category.AssignedTatamiId.Value] += item.Matches.Count;
                    }
                }

                foreach (var item in dayKumiteCategories)
                {
                    if (!resetManualAssignments && !item.Category.AssignedTatamiId.HasValue)
                    {
                        // Catégorie affectée à aucun tatami : ne pas l'assigner
                        var unassignedMatches = _matches.Where(m => m.CategoryId == item.Category.Id).ToList();
                        foreach (var m in unassignedMatches)
                        {
                            m.TatamiId = null;
                            m.Tatami = null;
                        }
                        item.Category.AssignedTatami = null;
                        continue;
                    }

                    int chosenTatamiId;
                    if (item.Category.AssignedTatamiId.HasValue && kumiteTatamis.Any(t => t.Id == item.Category.AssignedTatamiId.Value))
                    {
                        chosenTatamiId = item.Category.AssignedTatamiId.Value;
                    }
                    else
                    {
                        chosenTatamiId = kumiteLoads.OrderBy(kv => kv.Value).First().Key;
                        kumiteLoads[chosenTatamiId] += item.Matches.Count;
                        item.Category.AssignedTatamiId = chosenTatamiId;
                    }

                    var chosenTatami = _tatamis.First(t => t.Id == chosenTatamiId);
                    item.Category.AssignedTatami = chosenTatami;

                    var allCatMatches = _matches.Where(m => m.CategoryId == item.Category.Id).ToList();
                    foreach (var m in allCatMatches)
                    {
                        m.TatamiId = chosenTatami.Id;
                        m.Tatami = chosenTatami;
                        if (!m.IsBye && !chosenTatami.AssignedKumiteMatches.Any(x => x.Id == m.Id))
                        {
                            chosenTatami.AssignedKumiteMatches.Add(m);
                        }
                    }
                    foreach (var m in item.Category.Matches)
                    {
                        m.TatamiId = chosenTatami.Id;
                        m.Tatami = chosenTatami;
                    }
                }

                // 2. Kata pour ce jour
                var dayKataCategories = _categories
                    .Where(c => c.Type == CompetitionType.Kata && c.CompetitionDay == day)
                    .Select(c => new
                    {
                        Category = c,
                        Performances = _kataPerformances.Where(p => p.CategoryId == c.Id).ToList()
                    })
                    .OrderByDescending(x => x.Performances.Count)
                    .ToList();

                var kataLoads = kataTatamis.ToDictionary(t => t.Id, t => 0);

                // Prendre en compte les catégories déjà assignées manuellement
                foreach (var item in dayKataCategories)
                {
                    if (item.Category.AssignedTatamiId.HasValue && kataTatamis.Any(t => t.Id == item.Category.AssignedTatamiId.Value))
                    {
                        kataLoads[item.Category.AssignedTatamiId.Value] += item.Performances.Count;
                    }
                }

                foreach (var item in dayKataCategories)
                {
                    if (!resetManualAssignments && !item.Category.AssignedTatamiId.HasValue)
                    {
                        // Catégorie affectée à aucun tatami : ne pas l'assigner
                        var unassignedPerfs = _kataPerformances.Where(p => p.CategoryId == item.Category.Id).ToList();
                        foreach (var p in unassignedPerfs)
                        {
                            p.TatamiId = null;
                            p.Tatami = null;
                        }
                        item.Category.AssignedTatami = null;
                        continue;
                    }

                    int chosenTatamiId;
                    if (item.Category.AssignedTatamiId.HasValue && kataTatamis.Any(t => t.Id == item.Category.AssignedTatamiId.Value))
                    {
                        chosenTatamiId = item.Category.AssignedTatamiId.Value;
                    }
                    else
                    {
                        chosenTatamiId = kataLoads.OrderBy(kv => kv.Value).First().Key;
                        kataLoads[chosenTatamiId] += item.Performances.Count;
                        item.Category.AssignedTatamiId = chosenTatamiId;
                    }

                    var chosenTatami = _tatamis.First(t => t.Id == chosenTatamiId);
                    item.Category.AssignedTatami = chosenTatami;

                    var allCatPerfs = _kataPerformances.Where(p => p.CategoryId == item.Category.Id).ToList();
                    foreach (var p in allCatPerfs)
                    {
                        p.TatamiId = chosenTatami.Id;
                        p.Tatami = chosenTatami;
                        if (!chosenTatami.AssignedKataPerformances.Any(x => x.Id == p.Id))
                        {
                            chosenTatami.AssignedKataPerformances.Add(p);
                        }
                    }
                    foreach (var p in item.Category.KataPerformances)
                    {
                        p.TatamiId = chosenTatami.Id;
                        p.Tatami = chosenTatami;
                    }
                }
            }

            ApplyChronologicalNumbering();

            // Trier les combats et katas sur chaque tatami par ordre chronologique
            foreach (var t in _tatamis)
            {
                t.AssignedKumiteMatches = t.AssignedKumiteMatches.OrderBy(m => m.MatchNumber).ToList();
                t.AssignedKataPerformances = t.AssignedKataPerformances.OrderBy(p => p.PerformanceOrder).ToList();

                if (t.CurrentKumiteMatch != null && !t.AssignedKumiteMatches.Any(m => m.Id == t.CurrentKumiteMatch.Id))
                {
                    t.CurrentKumiteMatchId = null;
                    t.CurrentKumiteMatch = null;
                }
                if (t.CurrentKataPerformance != null && !t.AssignedKataPerformances.Any(p => p.Id == t.CurrentKataPerformance.Id))
                {
                    t.CurrentKataPerformanceId = null;
                    t.CurrentKataPerformance = null;
                }

                if (t.CurrentKumiteMatch == null && t.AssignedKumiteMatches.Count > 0)
                {
                    var firstReady = t.AssignedKumiteMatches.FirstOrDefault(m => m.Status != MatchStatus.Finished) ?? t.AssignedKumiteMatches[0];
                    t.CurrentKumiteMatchId = firstReady.Id;
                    t.CurrentKumiteMatch = firstReady;
                    if (firstReady.Status == MatchStatus.Pending) firstReady.Status = MatchStatus.Ready;
                }

                if (t.CurrentKataPerformance == null && t.AssignedKataPerformances.Count > 0)
                {
                    var firstPerf = t.AssignedKataPerformances.FirstOrDefault(p => p.Status != MatchStatus.Finished) ?? t.AssignedKataPerformances[0];
                    t.CurrentKataPerformanceId = firstPerf.Id;
                    t.CurrentKataPerformance = firstPerf;
                    if (firstPerf.Status == MatchStatus.Pending)
                    {
                        firstPerf.Status = MatchStatus.InProgress;
                    }
                }
                else if (t.CurrentKataPerformance != null && t.CurrentKataPerformance.Status == MatchStatus.Pending)
                {
                    t.CurrentKataPerformance.Status = MatchStatus.InProgress;
                }

                // S'assurer qu'un seul match et un seul kata peuvent avoir le statut InProgress sur ce tatami
                foreach (var m in t.AssignedKumiteMatches)
                {
                    if (m.Id != t.CurrentKumiteMatchId && m.Status == MatchStatus.InProgress)
                    {
                        m.Status = MatchStatus.Pending;
                    }
                }
                foreach (var p in t.AssignedKataPerformances)
                {
                    if (p.Id != t.CurrentKataPerformanceId && p.Status == MatchStatus.InProgress)
                    {
                        p.Status = MatchStatus.Pending;
                    }
                }
            }

            SaveAllToDatabase();
            NotifyChange();
        }
    }

    public Tatami AddTatami(string name, CompetitionType discipline)
    {
        lock (_lock)
        {
            int newId = _tatamis.Count > 0 ? _tatamis.Max(t => t.Id) + 1 : 1;
            int newNumber = _tatamis.Count > 0 ? _tatamis.Max(t => t.Number) + 1 : 1;

            var tatami = new Tatami
            {
                Id = newId,
                Number = newNumber,
                Name = string.IsNullOrWhiteSpace(name) ? $"Tatami {newNumber}" : name.Trim(),
                Discipline = discipline,
                CurrentDiscipline = (discipline == CompetitionType.Both || discipline == CompetitionType.Kata) ? CompetitionType.Kata : CompetitionType.Kumite
            };

            _tatamis.Add(tatami);
            BalanceCategoriesAcrossTatamis(resetManualAssignments: true);
            return tatami;
        }
    }

    public bool DeleteTatami(int tatamiId)
    {
        lock (_lock)
        {
            if (_tatamis.Count <= 1) return false;

            var tatami = _tatamis.FirstOrDefault(t => t.Id == tatamiId);
            if (tatami == null) return false;

            _tatamis.Remove(tatami);
            BalanceCategoriesAcrossTatamis(resetManualAssignments: true);
            return true;
        }
    }

    public void UpdateTatami(int tatamiId, string name, CompetitionType discipline)
    {
        lock (_lock)
        {
            var tatami = _tatamis.FirstOrDefault(t => t.Id == tatamiId);
            if (tatami == null) return;

            var oldDiscipline = tatami.Discipline;
            tatami.Name = string.IsNullOrWhiteSpace(name) ? tatami.Name : name.Trim();
            tatami.Discipline = discipline;
            if (discipline == CompetitionType.Kumite) tatami.CurrentDiscipline = CompetitionType.Kumite;
            else tatami.CurrentDiscipline = CompetitionType.Kata;

            if (oldDiscipline != discipline)
            {
                BalanceCategoriesAcrossTatamis(resetManualAssignments: true);
            }
            else
            {
                SaveTatamiStateToDatabase(tatamiId);
                NotifyChange(tatamiId);
            }
        }
    }

    public void SwitchTatamiMode(int tatamiId, CompetitionType mode)
    {
        lock (_lock)
        {
            var tatami = _tatamis.FirstOrDefault(t => t.Id == tatamiId);
            if (tatami == null) return;

            if (mode == CompetitionType.Kumite || mode == CompetitionType.Kata)
            {
                tatami.CurrentDiscipline = mode;
                SaveTatamiStateToDatabase(tatamiId);
                NotifyChange(tatamiId);
            }
        }
    }

    public void SetCategoryHasBronzeMatch(int categoryId, bool hasBronzeMatch)
    {
        lock (_lock)
        {
            var category = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (category == null) return;

            category.HasBronzeMatch = hasBronzeMatch;

            var catMatches = _matches.Where(m => m.CategoryId == categoryId).ToList();
            bool hasStarted = catMatches.Any(m => m.Status == MatchStatus.Finished && !m.IsBye || m.Status == MatchStatus.InProgress);

            if (!hasStarted)
            {
                GenerateCategoryBracket(categoryId);
            }
            else
            {
                SaveAllToDatabase();
                NotifyChange();
            }
        }
    }

    public void SetCategoryKataQualifiersCount(int categoryId, int qualifiersCount)
    {
        lock (_lock)
        {
            var category = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (category == null || category.Type != CompetitionType.Kata) return;

            if (qualifiersCount < 1) qualifiersCount = 1;
            category.KataQualifiersCount = qualifiersCount;

            var catT2 = _kataPerformances.Where(p => p.CategoryId == categoryId && p.Round == KataRound.Turn2).ToList();
            bool t2Started = catT2.Any(p => p.Status == MatchStatus.Finished || p.Status == MatchStatus.InProgress);

            if (!t2Started)
            {
                var catT1 = _kataPerformances.Where(p => p.CategoryId == categoryId && p.Round == KataRound.Turn1).ToList();
                bool t1Started = catT1.Any(p => p.Status == MatchStatus.Finished || p.Status == MatchStatus.InProgress);

                if (!t1Started)
                {
                    GenerateCategoryBracket(categoryId);
                }
                else
                {
                    int catCompetitorsCount = category.Competitors.Count;
                    int actualQualifiersCount = Math.Min(qualifiersCount, catCompetitorsCount > 0 ? catCompetitorsCount : qualifiersCount);

                    _kataPerformances.RemoveAll(p => p.CategoryId == categoryId && p.Round == KataRound.Turn2);
                    int startId = _kataPerformances.Count > 0 ? _kataPerformances.Max(p => p.Id) + 1 : 1;
                    var newT2 = _kataManager.InitializeRound2Placeholders(category, actualQualifiersCount);
                    foreach (var p in newT2)
                    {
                        p.Id = startId++;
                        _kataPerformances.Add(p);
                    }

                    category.KataPerformances = _kataPerformances.Where(p => p.CategoryId == categoryId).ToList();

                    bool t1AllFinished = catT1.Count > 0 && catT1.All(p => p.Status == MatchStatus.Finished);
                    if (t1AllFinished)
                    {
                        _kataManager.FinalizeRound1AndQualify(category, catT1);
                        _kataManager.AssignRound2Qualifiers(catT1, newT2);
                    }

                    ApplyChronologicalNumbering();
                    DistributeToTatamis();
                    SaveAllToDatabase();
                    NotifyChange();
                }
            }
            else
            {
                SaveAllToDatabase();
                NotifyChange();
            }
        }
    }

    private void LoadFromDatabaseOrCreate()
    {
        lock (_lock)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<TournamentDbContext>();
                if (db != null)
                {
                    db.Database.EnsureCreated();

                    bool initIsSqlServer = db.Database.ProviderName?.Contains("SqlServer") == true;

                    if (initIsSqlServer)
                    {
                        // SQL Server : ADD COLUMN avec IF NOT EXISTS via sys.columns
                        try { db.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tatamis') AND name = 'IsPaused') ALTER TABLE [Tatamis] ADD [IsPaused] BIT NOT NULL DEFAULT 0;"); } catch { }
                        try { db.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tatamis') AND name = 'PauseMessage') ALTER TABLE [Tatamis] ADD [PauseMessage] NVARCHAR(MAX) NULL;"); } catch { }
                        try { db.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Advertisements') AND name = 'VideoUrl') ALTER TABLE [Advertisements] ADD [VideoUrl] NVARCHAR(MAX) NULL;"); } catch { }
                        try { db.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Competitors') AND name = 'Status') ALTER TABLE [Competitors] ADD [Status] INT NOT NULL DEFAULT 0;"); } catch { }
                        try { db.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Competitors') AND name = 'ActualWeight') ALTER TABLE [Competitors] ADD [ActualWeight] FLOAT NULL;"); } catch { }
                        try { db.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Competitors') AND name = 'IsWeighInValidated') ALTER TABLE [Competitors] ADD [IsWeighInValidated] BIT NOT NULL DEFAULT 0;"); } catch { }
                        try { db.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Competitors') AND name = 'WeighInDate') ALTER TABLE [Competitors] ADD [WeighInDate] DATETIME2 NULL;"); } catch { }
                        try { db.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Competitors') AND name = 'WeighInStation') ALTER TABLE [Competitors] ADD [WeighInStation] NVARCHAR(MAX) NULL;"); } catch { }

                        // Créer la table Advertisements si elle n'existe pas (SQL Server)
                        try
                        {
                            db.Database.ExecuteSqlRaw(@"
                                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Advertisements')
                                BEGIN
                                    CREATE TABLE [Advertisements] (
                                        [Id]                     INT            IDENTITY(1,1) PRIMARY KEY,
                                        [Title]                  NVARCHAR(MAX)  NOT NULL DEFAULT '',
                                        [Subtitle]               NVARCHAR(MAX)  NULL,
                                        [Description]            NVARCHAR(MAX)  NULL,
                                        [ImageUrl]               NVARCHAR(MAX)  NULL,
                                        [VideoUrl]               NVARCHAR(MAX)  NULL,
                                        [LogoUrl]                NVARCHAR(MAX)  NULL,
                                        [WebsiteUrl]             NVARCHAR(MAX)  NULL,
                                        [DisplayDurationSeconds] INT            NOT NULL DEFAULT 8,
                                        [OrderIndex]             INT            NOT NULL DEFAULT 0,
                                        [IsActive]               BIT            NOT NULL DEFAULT 1
                                    );
                                END");
                        }
                        catch { }
                    }
                    else
                    {
                        // SQLite : ALTER TABLE simple (échoue silencieusement si colonne existe déjà)
                        try { db.Database.ExecuteSqlRaw("ALTER TABLE Tatamis ADD COLUMN IsPaused INTEGER NOT NULL DEFAULT 0;"); } catch { }
                        try { db.Database.ExecuteSqlRaw("ALTER TABLE Tatamis ADD COLUMN PauseMessage TEXT NULL;"); } catch { }
                        try { db.Database.ExecuteSqlRaw("ALTER TABLE Advertisements ADD COLUMN VideoUrl TEXT NULL;"); } catch { }
                        try { db.Database.ExecuteSqlRaw("ALTER TABLE Competitors ADD COLUMN Status INTEGER NOT NULL DEFAULT 0;"); } catch { }
                        try { db.Database.ExecuteSqlRaw("ALTER TABLE Competitors ADD COLUMN ActualWeight REAL NULL;"); } catch { }
                        try { db.Database.ExecuteSqlRaw("ALTER TABLE Competitors ADD COLUMN IsWeighInValidated INTEGER NOT NULL DEFAULT 0;"); } catch { }
                        try { db.Database.ExecuteSqlRaw("ALTER TABLE Competitors ADD COLUMN WeighInDate TEXT NULL;"); } catch { }
                        try { db.Database.ExecuteSqlRaw("ALTER TABLE Competitors ADD COLUMN WeighInStation TEXT NULL;"); } catch { }

                        // Créer la table Advertisements si elle n'existe pas (SQLite)
                        try
                        {
                            db.Database.ExecuteSqlRaw(@"
                                CREATE TABLE IF NOT EXISTS Advertisements (
                                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                    Title TEXT NOT NULL DEFAULT '',
                                    Subtitle TEXT NULL,
                                    Description TEXT NULL,
                                    ImageUrl TEXT NULL,
                                    VideoUrl TEXT NULL,
                                    LogoUrl TEXT NULL,
                                    WebsiteUrl TEXT NULL,
                                    DisplayDurationSeconds INTEGER NOT NULL DEFAULT 8,
                                    OrderIndex INTEGER NOT NULL DEFAULT 0,
                                    IsActive INTEGER NOT NULL DEFAULT 1
                                );");
                        }
                        catch { }
                    }

                    try
                    {
                        _advertisements = db.Advertisements.AsNoTracking().OrderBy(a => a.OrderIndex).ToList();
                    }
                    catch { }

                    if (_advertisements.Count == 0)
                    {
                        _advertisements = GetDefaultSampleAdvertisements();
                    }

                    if (db.Competitors.Any() && db.Categories.Any())
                    {
                        // Charger les settings du Tournament (DaysCount, ActiveDay…)
                        var dbTournament = db.Tournaments.AsNoTracking().FirstOrDefault();
                        if (dbTournament != null)
                        {
                            _tournament.Id = dbTournament.Id;
                            _tournament.Name = dbTournament.Name;
                            _tournament.Date = dbTournament.Date;
                            _tournament.DaysCount = dbTournament.DaysCount;
                            _tournament.ActiveDay = dbTournament.ActiveDay;
                            _tournament.Location = dbTournament.Location;
                            _tournament.TatamisCount = dbTournament.TatamisCount;
                        }

                        _competitors = db.Competitors.AsNoTracking().ToList();
                        _categories = db.Categories.AsNoTracking().Include(c => c.Competitors).ToList();

                        var compDict = _competitors.ToDictionary(c => c.Id);
                        foreach (var cat in _categories)
                        {
                            for (int i = 0; i < cat.Competitors.Count; i++)
                            {
                                if (compDict.TryGetValue(cat.Competitors[i].Id, out var sharedComp))
                                {
                                    cat.Competitors[i] = sharedComp;
                                    if (!sharedComp.Categories.Any(c => c.Id == cat.Id))
                                    {
                                        sharedComp.Categories.Add(cat);
                                    }
                                }
                            }
                        }
                        _matches = db.KumiteMatches.AsNoTracking()
                            .Include(m => m.AkaCompetitor)
                            .Include(m => m.ShiroCompetitor)
                            .Include(m => m.WinnerCompetitor)
                            .Include(m => m.Category)
                            .ToList();

                        var matchMap = _matches.ToDictionary(m => m.Id);
                        foreach (var m in _matches)
                        {
                            if (m.NextMatchId.HasValue && matchMap.TryGetValue(m.NextMatchId.Value, out var parent))
                            {
                                m.NextMatch = parent;
                            }
                            if (m.BronzeMatchId.HasValue && matchMap.TryGetValue(m.BronzeMatchId.Value, out var bronze))
                            {
                                m.BronzeMatch = bronze;
                            }
                        }

                        _kataPerformances = db.KataPerformances.AsNoTracking()
                            .Include(p => p.Competitor)
                            .Include(p => p.Category)
                            .ToList();
                        _tatamis = db.Tatamis.AsNoTracking().ToList();

                        if (_tatamis.Count == 0)
                        {
                            _tatamis = new List<Tatami>
                            {
                                new() { Id = 1, Number = 1, Name = "Tatami 1", Discipline = CompetitionType.Kumite, CurrentDiscipline = CompetitionType.Kumite },
                                new() { Id = 2, Number = 2, Name = "Tatami 2", Discipline = CompetitionType.Both, CurrentDiscipline = CompetitionType.Kata },
                                new() { Id = 3, Number = 3, Name = "Tatami 3", Discipline = CompetitionType.Kata, CurrentDiscipline = CompetitionType.Kata }
                            };
                        }

                        // Mettre à jour les qualifiés Tour 2 pour les catégories Kata ayant déjà des scores
                        var kataCatsWithScores = _kataPerformances
                            .Where(p => p.Round == KataRound.Turn1 && p.FinalScore > 0)
                            .Select(p => p.CategoryId)
                            .Distinct()
                            .ToList();

                        foreach (var catId in kataCatsWithScores)
                        {
                            var cat = _categories.FirstOrDefault(c => c.Id == catId);
                            if (cat != null)
                            {
                                var r1 = _kataPerformances.Where(p => p.CategoryId == catId && p.Round == KataRound.Turn1).ToList();
                                _kataManager.FinalizeRound1AndQualify(cat, r1);
                                var t2 = _kataPerformances.Where(p => p.CategoryId == catId && p.Round == KataRound.Turn2).OrderBy(p => p.PerformanceOrder).ToList();
                                if (t2.Count > 0)
                                {
                                    _kataManager.AssignRound2Qualifiers(r1, t2);
                                    foreach (var p2 in t2)
                                    {
                                        if (p2.Competitor == null && p2.CompetitorId.HasValue)
                                        {
                                            p2.Competitor = _competitors.FirstOrDefault(c => c.Id == p2.CompetitorId.Value);
                                        }
                                    }
                                }
                            }
                        }

                        DistributeToTatamis();
                        foreach (var t in _tatamis)
                        {
                            if (t.CurrentKataPerformance != null && t.CurrentKataPerformance.Status == MatchStatus.Pending)
                            {
                                t.CurrentKataPerformance.Status = MatchStatus.InProgress;
                            }
                        }
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] Notice chargement base : {ex.Message}");
            }

            _tatamis = new List<Tatami>
            {
                new() { Id = 1, Number = 1, Name = "Tatami 1", Discipline = CompetitionType.Kumite, CurrentDiscipline = CompetitionType.Kumite },
                new() { Id = 2, Number = 2, Name = "Tatami 2", Discipline = CompetitionType.Both, CurrentDiscipline = CompetitionType.Kata },
                new() { Id = 3, Number = 3, Name = "Tatami 3", Discipline = CompetitionType.Kata, CurrentDiscipline = CompetitionType.Kata }
            };

            SeedSampleData();
        }
    }

    private void SaveAllToDatabase()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<TournamentDbContext>();
            if (db == null) return;

            // Créer les tables si elles n'existent pas encore (premier démarrage)
            db.Database.EnsureCreated();

            bool isSqlServer = db.Database.ProviderName?.Contains("SqlServer") == true;

            if (isSqlServer)
            {
                // SQL Server avec NoAction : nullifier d'abord les FK circulaires entre Tatamis et KumiteMatches/KataPerformances,
                // puis supprimer dans l'ordre FK correct
                db.Database.ExecuteSqlRaw("UPDATE [Tatamis] SET [CurrentKumiteMatchId] = NULL, [CurrentKataPerformanceId] = NULL");
                db.Database.ExecuteSqlRaw("UPDATE [KumiteMatches] SET [NextMatchId] = NULL, [BronzeMatchId] = NULL");
                db.Database.ExecuteSqlRaw("DELETE FROM [CategoryCompetitor]");
                db.Database.ExecuteSqlRaw("DELETE FROM [KataPerformances]");
                db.Database.ExecuteSqlRaw("DELETE FROM [KumiteMatches]");
                db.Database.ExecuteSqlRaw("DELETE FROM [Categories]");
                db.Database.ExecuteSqlRaw("DELETE FROM [Competitors]");
                db.Database.ExecuteSqlRaw("DELETE FROM [Tatamis]");
                db.Database.ExecuteSqlRaw("DELETE FROM [Tournaments]");
                try { db.Database.ExecuteSqlRaw("DELETE FROM [Advertisements]"); } catch { }
            }
            else
            {
                // SQLite : désactiver temporairement les FK pour vider les tables sans contraintes
                db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
                db.Database.ExecuteSqlRaw("DELETE FROM CategoryCompetitor");
                db.Database.ExecuteSqlRaw("DELETE FROM KataPerformances");
                db.Database.ExecuteSqlRaw("DELETE FROM KumiteMatches");
                db.Database.ExecuteSqlRaw("DELETE FROM Categories");
                db.Database.ExecuteSqlRaw("DELETE FROM Competitors");
                db.Database.ExecuteSqlRaw("DELETE FROM Tatamis");
                db.Database.ExecuteSqlRaw("DELETE FROM Tournaments");
                try { db.Database.ExecuteSqlRaw("DELETE FROM Advertisements"); } catch { }
                db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON");
            }

            // Helper local : encadre AddRange+SaveChanges avec IDENTITY_INSERT ON/OFF sur SQL Server.
            // On maintient la connexion ouverte explicitement (OpenConnection) pour que SET IDENTITY_INSERT
            // et SaveChanges() partagent la MÊME connexion SQL (IDENTITY_INSERT est session-scoped).
            void InsertWithIdentity(string tableName, Action insert)
            {
                if (isSqlServer)
                {
                    db.Database.OpenConnection();
                    try
                    {
                        db.Database.ExecuteSqlRaw($"SET IDENTITY_INSERT [{tableName}] ON");
                        insert();
                        db.Database.ExecuteSqlRaw($"SET IDENTITY_INSERT [{tableName}] OFF");
                    }
                    finally
                    {
                        db.Database.CloseConnection();
                    }
                }
                else
                {
                    insert();
                }
            }

            // Sauvegarder le Tournament (DaysCount, ActiveDay, etc.)
            InsertWithIdentity("Tournaments", () =>
            {
                db.Tournaments.Add(new Tournament
                {
                    Id = _tournament.Id > 0 ? _tournament.Id : 1,
                    Name = _tournament.Name,
                    Date = _tournament.Date,
                    DaysCount = _tournament.DaysCount,
                    ActiveDay = _tournament.ActiveDay,
                    Location = _tournament.Location,
                    TatamisCount = _tournament.TatamisCount
                });
                db.SaveChanges();
            });

            var dbCompetitors = _competitors.Select(c => new Competitor
            {
                Id = c.Id,
                BibNumber = c.BibNumber,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Country = c.Country,
                Club = c.Club,
                BirthDate = c.BirthDate,
                Age = c.Age,
                Weight = c.Weight,
                Grade = c.Grade,
                Gender = c.Gender,
                IsKumiteRegistered = c.IsKumiteRegistered,
                IsKataRegistered = c.IsKataRegistered,
                Status = c.Status,
                ActualWeight = c.ActualWeight,
                IsWeighInValidated = c.IsWeighInValidated,
                WeighInDate = c.WeighInDate,
                WeighInStation = c.WeighInStation
            }).ToList();
            InsertWithIdentity("Competitors", () =>
            {
                db.Competitors.AddRange(dbCompetitors);
                db.SaveChanges();
            });

            var compMap = dbCompetitors.ToDictionary(c => c.Id);

            var dbTatamis = _tatamis.Select(t => new Tatami
            {
                Id = t.Id,
                Number = t.Number,
                Name = t.Name,
                Discipline = t.Discipline,
                CurrentDiscipline = t.CurrentDiscipline,
                IsPaused = t.IsPaused,
                PauseMessage = t.PauseMessage
            }).ToList();
            InsertWithIdentity("Tatamis", () =>
            {
                db.Tatamis.AddRange(dbTatamis);
                db.SaveChanges();
            });

            var dbAds = _advertisements.Select(a => new Advertisement
            {
                Id = a.Id,
                Title = a.Title,
                Subtitle = a.Subtitle,
                Description = a.Description,
                ImageUrl = a.ImageUrl,
                LogoUrl = a.LogoUrl,
                WebsiteUrl = a.WebsiteUrl,
                DisplayDurationSeconds = a.DisplayDurationSeconds,
                OrderIndex = a.OrderIndex,
                IsActive = a.IsActive
            }).ToList();
            InsertWithIdentity("Advertisements", () =>
            {
                db.Advertisements.AddRange(dbAds);
                db.SaveChanges();
            });

            var dbCategories = _categories.Select(cat => new Category
            {
                Id = cat.Id,
                Name = cat.Name,
                Type = cat.Type,
                Gender = cat.Gender,
                MinAge = cat.MinAge,
                MaxAge = cat.MaxAge,
                MinWeight = cat.MinWeight,
                MaxWeight = cat.MaxWeight,
                CompetitionDay = cat.CompetitionDay,
                OrderIndex = cat.OrderIndex,
                AssignedTatamiId = cat.AssignedTatamiId,
                KumiteRegularDurationSeconds = cat.KumiteRegularDurationSeconds,
                KumiteExtensionDurationSeconds = cat.KumiteExtensionDurationSeconds,
                MaxExtensionsAllowed = cat.MaxExtensionsAllowed,
                HasBronzeMatch = cat.HasBronzeMatch,
                KataRound1MandatoryKata = cat.KataRound1MandatoryKata,
                KataRound2MandatoryKata = cat.KataRound2MandatoryKata,
                KataQualifiersCount = cat.KataQualifiersCount,
                KataJudgesCount = cat.KataJudgesCount,
                Competitors = cat.Competitors.Where(c => compMap.ContainsKey(c.Id)).Select(c => compMap[c.Id]).ToList()
            }).ToList();
            InsertWithIdentity("Categories", () =>
            {
                db.Categories.AddRange(dbCategories);
                db.SaveChanges();
            });

            // 1ère passe pour KumiteMatches : insérer sans NextMatchId / BronzeMatchId pour satisfaire les contraintes d'intégrité
            var dbMatches = _matches.Select(m => new KumiteMatch
            {
                Id = m.Id,
                CategoryId = m.CategoryId,
                TatamiId = m.TatamiId,
                CompetitionDay = m.CompetitionDay,
                MatchNumber = m.MatchNumber,
                BracketRound = m.BracketRound,
                RoundFromFinal = m.RoundFromFinal,
                RoundName = m.RoundName,
                AkaCompetitorId = m.AkaCompetitorId,
                AkaWazaAri = m.AkaWazaAri,
                AkaIppon = m.AkaIppon,
                AkaChui = m.AkaChui,
                AkaGenten = m.AkaGenten,
                ShiroCompetitorId = m.ShiroCompetitorId,
                ShiroWazaAri = m.ShiroWazaAri,
                ShiroIppon = m.ShiroIppon,
                ShiroChui = m.ShiroChui,
                ShiroGenten = m.ShiroGenten,
                Status = m.Status,
                WinnerCompetitorId = m.WinnerCompetitorId,
                WinnerReason = m.WinnerReason,
                TotalDurationSeconds = m.TotalDurationSeconds,
                RemainingSeconds = m.RemainingSeconds,
                IsTimerRunning = false,
                ExtensionsCount = m.ExtensionsCount,
                NextMatchId = null, // différé en 2e passe
                NextMatchCorner = m.NextMatchCorner,
                IsBronzeMatch = m.IsBronzeMatch,
                BronzeMatchId = null, // différé en 2e passe
                BronzeMatchCorner = m.BronzeMatchCorner
            }).ToList();
            InsertWithIdentity("KumiteMatches", () =>
            {
                db.KumiteMatches.AddRange(dbMatches);
                db.SaveChanges();
            });

            // 2ème passe : relier NextMatchId et BronzeMatchId maintenant que tous les matchs sont persistés
            for (int i = 0; i < dbMatches.Count; i++)
            {
                var targetParentId = _matches[i].NextMatch?.Id ?? _matches[i].NextMatchId;
                if (targetParentId.HasValue && dbMatches.Any(x => x.Id == targetParentId.Value))
                    dbMatches[i].NextMatchId = targetParentId.Value;

                var targetBronzeId = _matches[i].BronzeMatch?.Id ?? _matches[i].BronzeMatchId;
                if (targetBronzeId.HasValue && dbMatches.Any(x => x.Id == targetBronzeId.Value))
                    dbMatches[i].BronzeMatchId = targetBronzeId.Value;
            }
            db.SaveChanges();

            var dbKataPerfs = _kataPerformances.Select(p => new KataPerformance
            {
                Id = p.Id,
                CategoryId = p.CategoryId,
                TatamiId = p.TatamiId,
                CompetitionDay = p.CompetitionDay,
                CompetitorId = p.CompetitorId,
                Round = p.Round,
                KataName = p.KataName,
                PerformanceOrder = p.PerformanceOrder,
                GlobalMatchNumber = p.GlobalMatchNumber,
                JudgeScores = new List<double>(p.JudgeScores),
                DroppedLowestScore = p.DroppedLowestScore,
                DroppedHighestScore = p.DroppedHighestScore,
                FinalScore = p.FinalScore,
                RankInRound = p.RankInRound,
                IsQualifiedForTour2 = p.IsQualifiedForTour2,
                Status = p.Status
            }).ToList();
            InsertWithIdentity("KataPerformances", () =>
            {
                db.KataPerformances.AddRange(dbKataPerfs);
                db.SaveChanges();
            });

            // 3ème passe : relier CurrentKumiteMatchId et CurrentKataPerformanceId des Tatamis
            // (différé car les KumiteMatches et KataPerformances doivent exister en BD d'abord)
            for (int i = 0; i < dbTatamis.Count; i++)
            {
                var src = _tatamis[i];
                var currentMatchId = src.CurrentKumiteMatch?.Id ?? src.CurrentKumiteMatchId;
                if (currentMatchId.HasValue && dbMatches.Any(m => m.Id == currentMatchId.Value))
                    dbTatamis[i].CurrentKumiteMatchId = currentMatchId.Value;

                var currentKataId = src.CurrentKataPerformance?.Id ?? src.CurrentKataPerformanceId;
                if (currentKataId.HasValue && dbKataPerfs.Any(p => p.Id == currentKataId.Value))
                    dbTatamis[i].CurrentKataPerformanceId = currentKataId.Value;
            }
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Erreur lors de la sauvegarde complète : {ex}");
        }
    }

    private void SaveMatchStateToDatabase(int matchId)
    {
        Task.Run(() =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
                var local = _matches.FirstOrDefault(m => m.Id == matchId);
                if (local == null) return;

                var entity = db.KumiteMatches.FirstOrDefault(m => m.Id == matchId);
                if (entity != null)
                {
                    entity.AkaCompetitorId = local.AkaCompetitorId;
                    entity.ShiroCompetitorId = local.ShiroCompetitorId;
                    entity.TatamiId = local.TatamiId;
                    entity.AkaWazaAri = local.AkaWazaAri;
                    entity.AkaIppon = local.AkaIppon;
                    entity.AkaChui = local.AkaChui;
                    entity.AkaGenten = local.AkaGenten;
                    entity.ShiroWazaAri = local.ShiroWazaAri;
                    entity.ShiroIppon = local.ShiroIppon;
                    entity.ShiroChui = local.ShiroChui;
                    entity.ShiroGenten = local.ShiroGenten;
                    entity.RemainingSeconds = local.RemainingSeconds;
                    entity.TotalDurationSeconds = local.TotalDurationSeconds;
                    entity.ExtensionsCount = local.ExtensionsCount;
                    entity.Status = local.Status;
                    entity.WinnerCompetitorId = local.WinnerCompetitorId;
                    entity.WinnerReason = local.WinnerReason;
                    entity.NextMatchId = local.NextMatchId ?? local.NextMatch?.Id;
                    entity.NextMatchCorner = local.NextMatchCorner;
                    entity.IsBronzeMatch = local.IsBronzeMatch;
                    entity.BronzeMatchId = local.BronzeMatchId ?? local.BronzeMatch?.Id;
                    entity.BronzeMatchCorner = local.BronzeMatchCorner;
                    db.SaveChanges();
                }
            }
            catch { }
        });
    }

    private void SaveKataStateToDatabase(int perfId)
    {
        Task.Run(() =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
                var local = _kataPerformances.FirstOrDefault(p => p.Id == perfId);
                if (local == null) return;

                var entity = db.KataPerformances.FirstOrDefault(p => p.Id == perfId);
                if (entity != null)
                {
                    entity.CompetitorId = local.CompetitorId;
                    entity.TatamiId = local.TatamiId;
                    entity.PerformanceOrder = local.PerformanceOrder;
                    entity.JudgeScores = new List<double>(local.JudgeScores);
                    entity.DroppedLowestScore = local.DroppedLowestScore;
                    entity.DroppedHighestScore = local.DroppedHighestScore;
                    entity.FinalScore = local.FinalScore;
                    entity.RankInRound = local.RankInRound;
                    entity.IsQualifiedForTour2 = local.IsQualifiedForTour2;
                    entity.Status = local.Status;
                    db.SaveChanges();
                }
            }
            catch { }
        });
    }

    private void SaveCompetitorStateToDatabase(int compId)
    {
        Task.Run(() =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
                var local = _competitors.FirstOrDefault(c => c.Id == compId);
                if (local == null) return;

                var entity = db.Competitors.FirstOrDefault(c => c.Id == compId);
                if (entity != null)
                {
                    entity.Status = local.Status;
                    entity.BibNumber = local.BibNumber;
                    entity.FirstName = local.FirstName;
                    entity.LastName = local.LastName;
                    entity.Country = local.Country;
                    entity.Club = local.Club;
                    entity.BirthDate = local.BirthDate;
                    entity.Age = local.Age;
                    entity.Weight = local.Weight;
                    entity.Grade = local.Grade;
                    entity.Gender = local.Gender;
                    entity.IsKumiteRegistered = local.IsKumiteRegistered;
                    entity.IsKataRegistered = local.IsKataRegistered;
                    entity.ActualWeight = local.ActualWeight;
                    entity.IsWeighInValidated = local.IsWeighInValidated;
                    entity.WeighInDate = local.WeighInDate;
                    entity.WeighInStation = local.WeighInStation;
                    db.SaveChanges();
                }
            }
            catch { }
        });
    }

    private void SaveTatamiStateToDatabase(int tatamiId)
    {
        Task.Run(() =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
                var local = _tatamis.FirstOrDefault(t => t.Id == tatamiId);
                if (local == null) return;

                var entity = db.Tatamis.FirstOrDefault(t => t.Id == tatamiId);
                if (entity != null)
                {
                    entity.CurrentKumiteMatchId = local.CurrentKumiteMatchId;
                    entity.CurrentKataPerformanceId = local.CurrentKataPerformanceId;
                    entity.CurrentDiscipline = local.CurrentDiscipline;
                    entity.IsPaused = local.IsPaused;
                    entity.PauseMessage = local.PauseMessage;
                    db.SaveChanges();
                }
            }
            catch { }
        });
    }

    public bool IsKataCompetitionFinished(int? competitionDay = null, int? tatamiId = null)
    {
        lock (_lock)
        {
            var query = _kataPerformances.AsEnumerable();
            if (competitionDay.HasValue)
            {
                query = query.Where(p => p.CompetitionDay == competitionDay.Value);
            }
            if (tatamiId.HasValue)
            {
                query = query.Where(p => p.TatamiId == tatamiId.Value);
            }

            var list = query.ToList();
            if (list.Count == 0) return true; // S'il n'y a pas de passages Kata, rien ne bloque
            return list.All(p => p.Status == MatchStatus.Finished);
        }
    }

    public int GetPendingKataCount(int? competitionDay = null, int? tatamiId = null)
    {
        lock (_lock)
        {
            var query = _kataPerformances.AsEnumerable();
            if (competitionDay.HasValue)
            {
                query = query.Where(p => p.CompetitionDay == competitionDay.Value);
            }
            if (tatamiId.HasValue)
            {
                query = query.Where(p => p.TatamiId == tatamiId.Value);
            }

            return query.Count(p => p.Status != MatchStatus.Finished);
        }
    }

    public int GetTotalKataCount(int? competitionDay = null, int? tatamiId = null)
    {
        lock (_lock)
        {
            var query = _kataPerformances.AsEnumerable();
            if (competitionDay.HasValue)
            {
                query = query.Where(p => p.CompetitionDay == competitionDay.Value);
            }
            if (tatamiId.HasValue)
            {
                query = query.Where(p => p.TatamiId == tatamiId.Value);
            }

            return query.Count();
        }
    }

    public void SetTatamiPause(int tatamiId, bool isPaused, string? pauseMessage = null)
    {
        lock (_lock)
        {
            var tatami = _tatamis.FirstOrDefault(t => t.Id == tatamiId);
            if (tatami == null) return;

            tatami.IsPaused = isPaused;
            if (pauseMessage != null)
            {
                tatami.PauseMessage = pauseMessage;
            }
            else if (!isPaused)
            {
                tatami.PauseMessage = null;
            }

            NotifyChange(tatamiId);
            SaveTatamiStateToDatabase(tatamiId);
        }
    }

    public void SetAllTatamisPause(bool isPaused, string? pauseMessage = null)
    {
        lock (_lock)
        {
            foreach (var tatami in _tatamis)
            {
                tatami.IsPaused = isPaused;
                if (pauseMessage != null)
                {
                    tatami.PauseMessage = pauseMessage;
                }
                else if (!isPaused)
                {
                    tatami.PauseMessage = null;
                }
                SaveTatamiStateToDatabase(tatami.Id);
            }

            NotifyChange();
        }
    }

    public List<Advertisement> GetAdvertisements(bool onlyActive = false)
    {
        lock (_lock)
        {
            if (_advertisements.Count == 0)
            {
                _advertisements = GetDefaultSampleAdvertisements();
            }

            return onlyActive
                ? _advertisements.Where(a => a.IsActive).OrderBy(a => a.OrderIndex).ToList()
                : _advertisements.OrderBy(a => a.OrderIndex).ToList();
        }
    }

    public Advertisement? GetAdvertisement(int id)
    {
        lock (_lock)
        {
            return _advertisements.FirstOrDefault(a => a.Id == id);
        }
    }

    public Advertisement AddAdvertisement(Advertisement ad)
    {
        lock (_lock)
        {
            int nextId = _advertisements.Count > 0 ? _advertisements.Max(a => a.Id) + 1 : 1;
            ad.Id = nextId;
            if (ad.OrderIndex == 0)
            {
                ad.OrderIndex = nextId;
            }
            if (ad.DisplayDurationSeconds <= 0)
            {
                ad.DisplayDurationSeconds = 8;
            }

            _advertisements.Add(ad);
            SaveAdvertisementsToDatabase();
            NotifyChange();
            return ad;
        }
    }

    public void UpdateAdvertisement(Advertisement ad)
    {
        lock (_lock)
        {
            var existing = _advertisements.FirstOrDefault(a => a.Id == ad.Id);
            if (existing != null)
            {
                existing.Title = ad.Title;
                existing.Subtitle = ad.Subtitle;
                existing.Description = ad.Description;
                existing.ImageUrl = ad.ImageUrl;
                existing.VideoUrl = ad.VideoUrl;
                existing.LogoUrl = ad.LogoUrl;
                existing.WebsiteUrl = ad.WebsiteUrl;
                existing.DisplayDurationSeconds = ad.DisplayDurationSeconds > 0 ? ad.DisplayDurationSeconds : 8;
                existing.OrderIndex = ad.OrderIndex;
                existing.IsActive = ad.IsActive;

                SaveAdvertisementsToDatabase();
                NotifyChange();
            }
        }
    }

    public bool DeleteAdvertisement(int id)
    {
        lock (_lock)
        {
            int removed = _advertisements.RemoveAll(a => a.Id == id);
            if (removed > 0)
            {
                SaveAdvertisementsToDatabase();
                NotifyChange();
                return true;
            }
            return false;
        }
    }

    private void SaveAdvertisementsToDatabase()
    {
        Task.Run(() =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
                db.Database.EnsureCreated();
                try { db.Database.ExecuteSqlRaw("DELETE FROM Advertisements"); } catch { }

                var list = _advertisements.Select(a => new Advertisement
                {
                    Id = a.Id,
                    Title = a.Title,
                    Subtitle = a.Subtitle,
                    Description = a.Description,
                    ImageUrl = a.ImageUrl,
                    VideoUrl = a.VideoUrl,
                    LogoUrl = a.LogoUrl,
                    WebsiteUrl = a.WebsiteUrl,
                    DisplayDurationSeconds = a.DisplayDurationSeconds,
                    OrderIndex = a.OrderIndex,
                    IsActive = a.IsActive
                }).ToList();

                db.Advertisements.AddRange(list);
                db.SaveChanges();
            }
            catch { }
        });
    }

    private static List<Advertisement> GetDefaultSampleAdvertisements()
    {
        return new List<Advertisement>
        {
            new()
            {
                Id = 1,
                Title = "Kyokushin France Official Gear",
                Subtitle = "Équipementier Officiel du Tournoi",
                Description = "Kimonos de combat haute résistance, ceintures brodées et protections homologuées IKO. Profitez de 15% de remise avec le code OPEN2027.",
                WebsiteUrl = "https://www.kyokushin-france.com",
                DisplayDurationSeconds = 8,
                OrderIndex = 1,
                IsActive = true
            },
            new()
            {
                Id = 2,
                Title = "Dojo Kyokushinkai Paris",
                Subtitle = "Centre d'Entraînement National",
                Description = "Cours d'arts martiaux traditionnels pour tous niveaux. Stages internationaux et préparation intensive aux compétitions.",
                WebsiteUrl = "https://www.dojoparis.fr",
                DisplayDurationSeconds = 8,
                OrderIndex = 2,
                IsActive = true
            },
            new()
            {
                Id = 3,
                Title = "Budo Sport Santé & Nutrition",
                Subtitle = "Partenaire Performance & Récupération",
                Description = "Gamme complète de compléments nutritionnels certifiés et soins du combattant. L'énergie au service du guerrier Kyokushin.",
                WebsiteUrl = "https://www.budosport.fr",
                DisplayDurationSeconds = 8,
                OrderIndex = 3,
                IsActive = true
            },
            new()
            {
                Id = 4,
                Title = "Fédération Française de Karaté",
                Subtitle = "Soutien Institutionnel & Homologation",
                Description = "Fiers de soutenir l'esprit martial, le respect et l'excellence sportive lors de l'Open de France Kyokushinkai 2027.",
                WebsiteUrl = "https://www.ffkarate.fr",
                DisplayDurationSeconds = 8,
                OrderIndex = 4,
                IsActive = true
            }
        };
    }

    private void NotifyChange(int tatamiId = 0)
    {
        OnStateChanged?.Invoke();
        OnTatamiUpdated?.Invoke(tatamiId);
    }

    // ==========================================
    // CONTRÔLE & ÉCRANS DE PESÉE (WEIGH-IN)
    // ==========================================

    public WeighInStationState GetWeighInStation(int stationId)
    {
        return _weighInStations.GetOrAdd(stationId, id => new WeighInStationState
        {
            StationId = id,
            StationName = $"Poste {id}"
        });
    }

    public List<WeighInStationState> GetAllWeighInStations()
    {
        GetWeighInStation(1);
        GetWeighInStation(2);
        return _weighInStations.Values.OrderBy(s => s.StationId).ToList();
    }

    public Competitor? FindCompetitorByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var clean = code.Trim();

        // 1. URL pattern: ex. "https://.../weighin?bib=101" ou "?id=42"
        if (clean.Contains('?') || clean.Contains('&'))
        {
            var mBib = System.Text.RegularExpressions.Regex.Match(clean, @"[?&]bib=(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mBib.Success && int.TryParse(mBib.Groups[1].Value, out var bibVal))
            {
                var c = _competitors.FirstOrDefault(x => x.BibNumber == bibVal);
                if (c != null) return c;
            }
            var mId = System.Text.RegularExpressions.Regex.Match(clean, @"[?&]id=(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mId.Success && int.TryParse(mId.Groups[1].Value, out var idVal))
            {
                var c = _competitors.FirstOrDefault(x => x.Id == idVal);
                if (c != null) return c;
            }
        }

        // 2. Format custom "KYOKUSHIN:ID:BIB" ou "KYOKUSHIN:ID"
        if (clean.StartsWith("KYOKUSHIN:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = clean.Split(':');
            if (parts.Length >= 3 && int.TryParse(parts[2], out var bibNum))
            {
                var c = _competitors.FirstOrDefault(x => x.BibNumber == bibNum);
                if (c != null) return c;
            }
            if (parts.Length >= 2 && int.TryParse(parts[1], out var compId))
            {
                var c = _competitors.FirstOrDefault(x => x.Id == compId);
                if (c != null) return c;
            }
        }

        // 3. Préfixes type "BIB:101", "#101", "DOSSARD:101", "ID:42"
        if (clean.StartsWith("BIB:", StringComparison.OrdinalIgnoreCase) || clean.StartsWith("DOSSARD:", StringComparison.OrdinalIgnoreCase))
        {
            var numPart = clean.Split(':')[1].Trim();
            if (int.TryParse(numPart, out var bib))
            {
                var c = _competitors.FirstOrDefault(x => x.BibNumber == bib);
                if (c != null) return c;
            }
        }
        if (clean.StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
        {
            var numPart = clean.Split(':')[1].Trim();
            if (int.TryParse(numPart, out var id))
            {
                var c = _competitors.FirstOrDefault(x => x.Id == id);
                if (c != null) return c;
            }
        }
        if (clean.StartsWith('#') && int.TryParse(clean.TrimStart('#'), out var hashBib))
        {
            var c = _competitors.FirstOrDefault(x => x.BibNumber == hashBib);
            if (c != null) return c;
        }

        // 4. Nombre entier direct : tester d'abord par Dossard, puis par Id
        if (int.TryParse(clean, out var number))
        {
            var byBib = _competitors.FirstOrDefault(x => x.BibNumber == number);
            if (byBib != null) return byBib;

            var byId = _competitors.FirstOrDefault(x => x.Id == number);
            if (byId != null) return byId;
        }

        // 5. Recherche par nom / prénom
        var lower = clean.ToLowerInvariant();
        var byName = _competitors.FirstOrDefault(x =>
            x.FullName.ToLowerInvariant().Contains(lower) ||
            x.LastName.ToLowerInvariant() == lower);
        if (byName != null) return byName;

        return null;
    }

    public WeighInStationState ScanCompetitorForWeighIn(int stationId, string rawInput)
    {
        var station = GetWeighInStation(stationId);
        var comp = FindCompetitorByCode(rawInput);

        if (comp == null)
        {
            station.Status = WeighInStatus.Idle;
            station.Competitor = null;
            station.WeighedWeight = null;
            station.StatusMessage = $"Aucun compétiteur trouvé pour le code : '{rawInput}'";
            station.LastUpdated = DateTime.Now;
            OnWeighInStationUpdated?.Invoke(stationId, station);
            return station;
        }

        station.Competitor = comp;
        station.KumiteCategory = comp.Categories.FirstOrDefault(c => c.Type == CompetitionType.Kumite);
        station.KataCategory = comp.Categories.FirstOrDefault(c => c.Type == CompetitionType.Kata);
        station.WeighedWeight = comp.ActualWeight ?? comp.Weight;
        station.Status = comp.IsWeighInValidated ? WeighInStatus.Validated : WeighInStatus.Checking;
        station.StatusMessage = comp.IsWeighInValidated ? "Pesée déjà validée" : "Compétiteur identifié - Veuillez vérifier le poids";
        station.LastUpdated = DateTime.Now;

        CalculateWeightDifference(station);

        OnWeighInStationUpdated?.Invoke(stationId, station);
        return station;
    }

    public WeighInStationState UpdateWeighedWeight(int stationId, double? weight)
    {
        var station = GetWeighInStation(stationId);
        station.WeighedWeight = weight;
        station.LastUpdated = DateTime.Now;
        if (station.Status != WeighInStatus.Validated && station.Competitor != null)
        {
            station.Status = WeighInStatus.Checking;
        }
        CalculateWeightDifference(station);
        OnWeighInStationUpdated?.Invoke(stationId, station);
        return station;
    }

    public WeighInStationState ValidateWeighIn(int stationId, double weighedWeight)
    {
        var station = GetWeighInStation(stationId);
        if (station.Competitor == null) return station;

        var comp = station.Competitor;
        comp.ActualWeight = weighedWeight;
        comp.IsWeighInValidated = true;
        comp.WeighInDate = DateTime.Now;
        comp.WeighInStation = station.StationName;
        comp.Status = CompetitorStatus.Present;

        station.WeighedWeight = weighedWeight;
        station.Status = WeighInStatus.Validated;
        station.StatusMessage = $"Pesée validée : {comp.FullName} ({weighedWeight:F1} kg)";
        station.LastUpdated = DateTime.Now;

        CalculateWeightDifference(station);

        SaveCompetitorStateToDatabase(comp.Id);
        NotifyChange();
        OnWeighInStationUpdated?.Invoke(stationId, station);
        return station;
    }

    public WeighInStationState RejectWeighIn(int stationId, string? reason = null)
    {
        var station = GetWeighInStation(stationId);
        station.Status = WeighInStatus.Rejected;
        station.StatusMessage = reason ?? "Poids non conforme à la catégorie";
        station.LastUpdated = DateTime.Now;
        OnWeighInStationUpdated?.Invoke(stationId, station);
        return station;
    }

    public WeighInStationState ResetWeighInStation(int stationId)
    {
        var station = GetWeighInStation(stationId);
        station.Competitor = null;
        station.KumiteCategory = null;
        station.KataCategory = null;
        station.WeighedWeight = null;
        station.Status = WeighInStatus.Idle;
        station.StatusMessage = null;
        station.LastUpdated = DateTime.Now;
        station.IsWeightValid = true;
        station.WeightDifference = null;
        OnWeighInStationUpdated?.Invoke(stationId, station);
        return station;
    }

    public List<Competitor> GetRecentWeighedCompetitors(int count = 10, int? stationId = null)
    {
        var query = _competitors.Where(c => c.IsWeighInValidated);
        if (stationId.HasValue)
        {
            var stationName = $"Poste {stationId.Value}";
            query = query.Where(c => c.WeighInStation == stationName);
        }
        return query.OrderByDescending(c => c.WeighInDate ?? DateTime.MinValue).Take(count).ToList();
    }

    public void CancelWeighInValidation(int competitorId)
    {
        var comp = _competitors.FirstOrDefault(c => c.Id == competitorId);
        if (comp == null) return;

        comp.IsWeighInValidated = false;
        comp.ActualWeight = null;
        comp.WeighInDate = null;
        comp.WeighInStation = null;
        comp.Status = CompetitorStatus.Absent;

        SaveCompetitorStateToDatabase(comp.Id);
        NotifyChange();

        foreach (var station in _weighInStations.Values)
        {
            if (station.Competitor?.Id == competitorId)
            {
                station.Status = WeighInStatus.Checking;
                station.StatusMessage = "Validation de pesée annulée";
                OnWeighInStationUpdated?.Invoke(station.StationId, station);
            }
        }
    }

    private static void CalculateWeightDifference(WeighInStationState station)
    {
        if (station.Competitor == null || !station.WeighedWeight.HasValue)
        {
            station.IsWeightValid = true;
            station.WeightDifference = null;
            return;
        }

        var weight = station.WeighedWeight.Value;
        var cat = station.KumiteCategory;
        if (cat == null)
        {
            station.IsWeightValid = true;
            station.WeightDifference = 0;
            return;
        }

        var max = cat.EffectiveMaxWeight;
        var min = cat.EffectiveMinWeight;

        if  (max.HasValue && min.HasValue) 
        {
            station.IsWeightValid = (weight >= min.Value && weight <= max.Value);
            station.WeightDifference = Math.Round(weight - max.Value, 2);
        }
        else if (max.HasValue && weight > max.Value)
        {
            station.IsWeightValid = false;
            station.WeightDifference = Math.Round(weight - max.Value, 2);
        }
        else if (min.HasValue && weight < min.Value)
        {
            station.IsWeightValid = false;
            station.WeightDifference = Math.Round(weight - min.Value, 2);
        }
        else
        {
            station.IsWeightValid = true;
            station.WeightDifference = 0;
        }
    }
}
