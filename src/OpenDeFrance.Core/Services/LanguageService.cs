namespace OpenDeFrance.Core.Services;

public class LanguageService : ILanguageService
{
    private string _currentLanguage = "fr";

    public string CurrentLanguage => _currentLanguage;

    public event Action? OnLanguageChanged;

    public string this[string key] => Get(key);

    public void SetLanguage(string language)
    {
        string normalized = language.ToLowerInvariant();
        if (normalized != "fr" && normalized != "en")
        {
            normalized = "fr";
        }

        if (_currentLanguage != normalized)
        {
            _currentLanguage = normalized;
            OnLanguageChanged?.Invoke();
        }
    }

    public void ToggleLanguage()
    {
        SetLanguage(_currentLanguage == "fr" ? "en" : "fr");
    }

    public string Get(string key)
    {
        if (Translations.TryGetValue(key, out var dict))
        {
            if (dict.TryGetValue(_currentLanguage, out var translation))
            {
                return translation;
            }
        }

        return key;
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
    {
        ["FrenchOpen2027"] = new() { ["fr"] = "Open de France 2027", ["en"] = "French Open 2027" },
        ["KataInProgress"] = new() { ["fr"] = "Kata en cours", ["en"] = "Kata in progress" },
        ["Day"] = new() { ["fr"] = "J", ["en"] = "D" },
        ["Turn1"] = new() { ["fr"] = "Tour 1", ["en"] = "Turn 1" },
        ["Turn2Final"] = new() { ["fr"] = "Tour 2 (finale)", ["en"] = "Turn 2 (final)" },
        ["Grade"] = new() {["fr"] = "Grade", ["en"] = "Grade" },
        ["Kata"] = new() { ["fr"] = "Kata", ["en"] = "Kata" },
        ["Notes"] = new() { ["fr"] = "Notes", ["en"] = "Notes" },
        ["WaitingNotes"] = new() { ["fr"] = "En attente notes", ["en"] = "Pending notes" },
        ["NotNotes"] = new() { ["fr"] = "Pas de notes", ["en"] = "No notes" },
        ["NoKataOnTatami"] = new() { ["fr"] = "Aucun passage Kata en cours sur ce tatami", ["en"] = "No Kata sequence is currently in progress on this tatami" },
        ["NextKataPassages"] = new() { ["fr"] = "⏭️ Prochains passages Kata", ["en"] = "⏭️ Next Kata passages" },
        ["NoOtherPassagesPending"] = new() { ["fr"] = "Aucun autre passage en attente.", ["en"] = "No other passages pending." },

        // General / Nav
        ["AppTitle"] = new() { ["fr"] = "Kyokushin Open", ["en"] = "Kyokushin Open" },
        ["NavPublicLive"] = new() { ["fr"] = "🥋 Direct Public (Tatamis)", ["en"] = "🥋 Public Live (Tatamis)" },
        ["NavBrackets"] = new() { ["fr"] = "🏆 Tirages & Tableaux", ["en"] = "🏆 Brackets & Draws" },
        ["NavScoreboards"] = new() { ["fr"] = "Tables de Marque", ["en"] = "Scoreboards" },
        ["NavTvScreens"] = new() { ["fr"] = "Écrans Télé Tatamis", ["en"] = "Tatami TV Screens" },
        ["NavAdmin"] = new() { ["fr"] = "⚙️ Import Excel & Gestion", ["en"] = "⚙️ Admin & Settings" },
        ["Login"] = new() { ["fr"] = "Connexion", ["en"] = "Login" },
        ["Logout"] = new() { ["fr"] = "Déconnexion", ["en"] = "Logout" },
        ["ConnectedAs"] = new() { ["fr"] = "Connecté en tant que :", ["en"] = "Logged in as:" },
        ["SwitchLanguage"] = new() { ["fr"] = "Langue : Français 🇫🇷", ["en"] = "Language: English 🇬🇧" },

        // Brackets & Matches
        ["BracketsTitle"] = new() { ["fr"] = "Tableaux & Tirages au Sort", ["en"] = "Brackets & Draws" },
        ["BracketsSubtitle"] = new() { ["fr"] = "Arbres éliminatoires Kumite & Classements Katas", ["en"] = "Kumite elimination brackets & Kata rankings" },
        ["Category"] = new() { ["fr"] = "Catégorie", ["en"] = "Category" },
        ["Tatami"] = new() { ["fr"] = "Tatami", ["en"] = "Tatami" },
        ["Match"] = new() { ["fr"] = "Combat", ["en"] = "Match" },
        ["Round"] = new() { ["fr"] = "Tour", ["en"] = "Round" },
        ["Final"] = new() { ["fr"] = "Finale", ["en"] = "Final" },
        ["GrandFinal"] = new() { ["fr"] = "Grande Finale (1ère place)", ["en"] = "Grand Final (1st Place)" },
        ["SemiFinal"] = new() { ["fr"] = "Demi-Finale", ["en"] = "Semi-Final" },
        ["QuarterFinal"] = new() { ["fr"] = "Quart de finale", ["en"] = "Quarter-Final" },
        ["RoundOf16"] = new() { ["fr"] = "8e de finale", ["en"] = "Round of 16" },
        ["RoundOf32"] = new() { ["fr"] = "16e de finale", ["en"] = "Round of 32" },
        ["BronzeMatch"] = new() { ["fr"] = "Petite Finale (3e place)", ["en"] = "Bronze Match (3rd Place)" },
        ["HasBronzeMatch"] = new() { ["fr"] = "Petite finale (3e place)", ["en"] = "Bronze match (3rd place)" },
        ["Winner"] = new() { ["fr"] = "Vainqueur", ["en"] = "Winner" },
        ["Bye"] = new() { ["fr"] = "(Exempt)", ["en"] = "(Bye)" },
        ["Pending"] = new() { ["fr"] = "En attente", ["en"] = "Pending" },
        ["Ready"] = new() { ["fr"] = "Prêt", ["en"] = "Ready" },
        ["InProgress"] = new() { ["fr"] = "En cours", ["en"] = "In Progress" },
        ["Finished"] = new() { ["fr"] = "Terminé", ["en"] = "Finished" },
        ["RegularTime"] = new() { ["fr"] = "Temps Réglementaire", ["en"] = "Regular Time" },
        ["Extension"] = new() { ["fr"] = "Prolongation (Encho-sen)", ["en"] = "Extension (Encho-sen)" },

        // Admin
        ["AdminTitle"] = new() { ["fr"] = "⚙️ Administration & Tirages au Sort", ["en"] = "⚙️ Administration & Draws" },
        ["AdminSubtitle"] = new() { ["fr"] = "Modification des tirages par Drag & Drop, affectation des tatamis et gestion des inscrits", ["en"] = "Drag & Drop draw adjustments, tatami planning and competitor management" },
        ["TabDraws"] = new() { ["fr"] = "🎯 Tirages & Tableaux (Drag & Drop)", ["en"] = "🎯 Brackets & Draws (Drag & Drop)" },
        ["TabTatamis"] = new() { ["fr"] = "🥋 Planning Tatamis (Kanban)", ["en"] = "🥋 Tatami Planning (Kanban)" },
        ["TabCategories"] = new() { ["fr"] = "📁 Import Excel & Catégories", ["en"] = "📁 Excel Import & Categories" },
        ["TabCompetitors"] = new() { ["fr"] = "👥 Compétiteurs", ["en"] = "👥 Competitors" },
        ["AddTatami"] = new() { ["fr"] = "➕ Ajouter un Tatami", ["en"] = "➕ Add Tatami" },
        ["DeleteTatami"] = new() { ["fr"] = "Supprimer", ["en"] = "Delete" },
        ["RebalanceTatamis"] = new() { ["fr"] = "⚖️ Rééquilibrer les combats sur les tatamis", ["en"] = "⚖️ Balance matches across tatamis" },
        ["CategoryAllMatchesSameTatami"] = new() { ["fr"] = "Tous les combats d'une même catégorie sont regroupés sur le même tatami", ["en"] = "All matches of a category are grouped on the same tatami" },
        ["TatamiConfiguration"] = new() { ["fr"] = "Configuration des Tatamis", ["en"] = "Tatami Configuration" },
        ["Discipline"] = new() { ["fr"] = "Discipline", ["en"] = "Discipline" },
        ["CompetitorsCount"] = new() { ["fr"] = "Inscrits", ["en"] = "Competitors" },
        ["Duration"] = new() { ["fr"] = "Durée Match", ["en"] = "Match Duration" },
        ["Action"] = new() { ["fr"] = "Action", ["en"] = "Action" }
    };
}
