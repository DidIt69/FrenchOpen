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
        ["Espoir Femmes 18 - 20ans +75kg"] = new() { ["fr"] = "Espoir Femmes 18-20 ans +75kg", ["en"] = "Hope Woman 18-20 y.o. +75kg" },
        ["Espoir Femmes 18 - 20ans -65kg"] = new() { ["fr"] = "Espoir Femmes 18-20 ans -65kg", ["en"] = "Hope Woman 18-20 y.o. -65kg" },
        ["Espoir Femmes 18 - 20ans -75kg"] = new() { ["fr"] = "Espoir Femmes 18-20 ans -75kg", ["en"] = "Hope Woman 18-20 y.o. -75kg" },
        ["Espoir Hommes 18 - 20ans +80kg"] = new() { ["fr"] = "Espoir Femmes 18-20 ans +80kg", ["en"] = "Hope Woman 18-20 y.o. +80kg" },
        ["Espoir Hommes 18 - 20ans -70kg"] = new() { ["fr"] = "Espoir Femmes 18-20 ans -70kg", ["en"] = "Hope Woman 18-20 y.o. -70kg" },
        ["Espoir Hommes 18 - 20ans -80kg"] = new() { ["fr"] = "Espoir Femmes 18-20 ans -80kg", ["en"] = "Hope Woman 18-20 y.o. -80kg" },
        ["Femmes +18ans +65kg"] = new() { ["fr"] = "Femmes +18 ans +65kg", ["en"] = "Woman 18+ y.o. +65kg" },
        ["Femmes +18ans -55kg"] = new() { ["fr"] = "Femmes +18 ans -55kg", ["en"] = "Woman 18+ y.o. -55kg" },
        ["Femmes +18ans -65kg"] = new() { ["fr"] = "Femmes +18 ans -65kg", ["en"] = "Woman 18+ y.o. -65kg" },
        ["Filles 10 - 11ans +45kg"] = new() { ["fr"] = "Filles 10-11 ans +45kg", ["en"] = "Girls 10-11 y.o. +45kg" },
        ["Filles 10 - 11ans -30kg"] = new() { ["fr"] = "Filles 10-11 ans -30kg", ["en"] = "Girls 10-11 y.o. -30kg" },
        ["Filles 10 - 11ans -35kg"] = new() { ["fr"] = "Filles 10-11 ans -35kg", ["en"] = "Girls 10-11 y.o. -35kg" },
        ["Filles 10 - 11ans -40kg"] = new() { ["fr"] = "Filles 10-11 ans -40kg", ["en"] = "Girls 10-11 y.o. -40kg" },
        ["Filles 10 - 11ans -45kg"] = new() { ["fr"] = "Filles 10-11 ans -45kg", ["en"] = "Girls 10-11 y.o. -45kg" },
        ["Filles 12 - 13ans +50kg"] = new() { ["fr"] = "Filles 12-13 ans +50kg", ["en"] = "Girls 12-13 y.o. +50kg" },
        ["Filles 12 - 13ans -40kg"] = new() { ["fr"] = "Filles 12-13 ans -40kg", ["en"] = "Girls 12-13 y.o. -40kg" },
        ["Filles 12 - 13ans -45kg"] = new() { ["fr"] = "Filles 12-13 ans -45kg", ["en"] = "Girls 12-13 y.o. -45kg" },
        ["Filles 12 - 13ans -50kg"] = new() { ["fr"] = "Filles 12-13 ans -50kg", ["en"] = "Girls 12-13 y.o. -50kg" },
        ["Filles 14 - 15ans +50kg"] = new() { ["fr"] = "Filles 14-15 ans +50kg", ["en"] = "Girls 14-15 y.o. +50kg" },
        ["Filles 14 - 15ans +60kg"] = new() { ["fr"] = "Filles 14-15 ans +60kg", ["en"] = "Girls 14-15 y.o. +60kg" },
        ["Filles 14 - 15ans -50kg"] = new() { ["fr"] = "Filles 14-15 ans -50kg", ["en"] = "Girls 14-15 y.o. -50kg" },
        ["Filles 14 - 15ans -60kg"] = new() { ["fr"] = "Filles 14-15 ans -60kg", ["en"] = "Girls 14-15 y.o. -60kg" },
        ["Filles 16 - 17ans +55kg"] = new() { ["fr"] = "Filles 16-17 ans +55kg", ["en"] = "Girls 16-17 y.o. +55kg" },
        ["Filles 16 - 17ans +65kg"] = new() { ["fr"] = "Filles 16-17 ans +65kg", ["en"] = "Girls 16-17 y.o. +65kg" },
        ["Filles 16 - 17ans -50kg"] = new() { ["fr"] = "Filles 16-17 ans -50kg", ["en"] = "Girls 16-17 y.o. -50kg" },
        ["Filles 16 - 17ans -55kg"] = new() { ["fr"] = "Filles 16-17 ans -55kg", ["en"] = "Girls 16-17 y.o. -55kg" },
        ["Filles 16 - 17ans -60kg"] = new() { ["fr"] = "Filles 16-17 ans -60kg", ["en"] = "Girls 16-17 y.o. -60kg" },
        ["Filles 16 - 17ans -65kg"] = new() { ["fr"] = "Filles 16-17 ans -65kg", ["en"] = "Girls 16-17 y.o. -65kg" },
        ["Filles 8 - 9ans +30kg"] = new() { ["fr"] = "Filles 8-9 ans +30kg", ["en"] = "Girls 8-9 y.o. +30kg" },
        ["Filles 8 - 9ans -25kg"] = new() { ["fr"] = "Filles 8-9 ans -25kg", ["en"] = "Girls 8-9 y.o. -25kg" },
        ["Filles 8 - 9ans -30kg"] = new() { ["fr"] = "Filles 8-9 ans -30kg", ["en"] = "Girls 8-9 y.o. -30kg" },
        ["Garçons 10 - 11ans +35kg"] = new() { ["fr"] = "Garçons 10-11 ans +35kg", ["en"] = "Boys 10-11 y.o. +35kg" },
        ["Garçons 10 - 11ans +45kg"] = new() { ["fr"] = "Garçons 10-11 ans +45kg", ["en"] = "Boys 10-11 y.o. +45kg" },
        ["Garçons 10 - 11ans -30kg"] = new() { ["fr"] = "Garçons 10-11 ans -30kg", ["en"] = "Boys 10-11 y.o. -30kg" },
        ["Garçons 10 - 11ans -35kg"] = new() { ["fr"] = "Garçons 10-11 ans -35kg", ["en"] = "Boys 10-11 y.o. -35kg" },
        ["Garçons 10 - 11ans -40kg"] = new() { ["fr"] = "Garçons 10-11 ans -40kg", ["en"] = "Boys 10-11 y.o. -40kg" },
        ["Garçons 10 - 11ans -45kg"] = new() { ["fr"] = "Garçons 10-11 ans -45kg", ["en"] = "Boys 10-11 y.o. -45kg" },
        ["Garçons 12 - 13ans +55kg"] = new() { ["fr"] = "Garçons 12-13 ans +55kg", ["en"] = "Boys 12-13 y.o. +55kg" },
        ["Garçons 12 - 13ans -40kg"] = new() { ["fr"] = "Garçons 12-13 ans -40kg", ["en"] = "Boys 12-13 y.o. -40kg" },
        ["Garçons 12 - 13ans -45kg"] = new() { ["fr"] = "Garçons 12-13 ans -45kg", ["en"] = "Boys 12-13 y.o. -45kg" },
        ["Garçons 12 - 13ans -50kg"] = new() { ["fr"] = "Garçons 12-13 ans -50kg", ["en"] = "Boys 12-13 y.o. -50kg" },
        ["Garçons 12 - 13ans -55kg"] = new() { ["fr"] = "Garçons 12-13 ans -55kg", ["en"] = "Boys 12-13 y.o. -55kg" },
        ["Garçons 14 - 15ans +75kg"] = new() { ["fr"] = "Garçons 14-15 ans +75kg", ["en"] = "Boys 14-15 y.o. +75kg" },
        ["Garçons 14 - 15ans -50kg"] = new() { ["fr"] = "Garçons 14-15 ans -50kg", ["en"] = "Boys 14-15 y.o. -50kg" },
        ["Garçons 14 - 15ans -55kg"] = new() { ["fr"] = "Garçons 14-15 ans -55kg", ["en"] = "Boys 14-15 y.o. -55kg" },
        ["Garçons 14 - 15ans -60kg"] = new() { ["fr"] = "Garçons 14-15 ans -60kg", ["en"] = "Boys 14-15 y.o. -60kg" },
        ["Garçons 14 - 15ans -65kg"] = new() { ["fr"] = "Garçons 14-15 ans -65kg", ["en"] = "Boys 14-15 y.o. -65kg" },
        ["Garçons 14 - 15ans -70kg"] = new() { ["fr"] = "Garçons 14-15 ans -70kg", ["en"] = "Boys 14-15 y.o. -70kg" },
        ["Garçons 14 - 15ans -75kg"] = new() { ["fr"] = "Garçons 14-15 ans -75kg", ["en"] = "Boys 14-15 y.o. -75kg" },
        ["Garçons 16 - 17ans +80kg"] = new() { ["fr"] = "Garçons 16-17 ans +80kg", ["en"] = "Boys 16-17 y.o. +80kg" },
        ["Garçons 16 - 17ans -55kg"] = new() { ["fr"] = "Garçons 16-17 ans -55kg", ["en"] = "Boys 16-17 y.o. -55kg" },
        ["Garçons 16 - 17ans -60kg"] = new() { ["fr"] = "Garçons 16-17 ans -60kg", ["en"] = "Boys 16-17 y.o. -60kg" },
        ["Garçons 16 - 17ans -65kg"] = new() { ["fr"] = "Garçons 16-17 ans -65kg", ["en"] = "Boys 16-17 y.o. -65kg" },
        ["Garçons 16 - 17ans -70kg"] = new() { ["fr"] = "Garçons 16-17 ans -70kg", ["en"] = "Boys 16-17 y.o. -70kg" },
        ["Garçons 16 - 17ans -75kg"] = new() { ["fr"] = "Garçons 16-17 ans -75kg", ["en"] = "Boys 16-17 y.o. -75kg" },
        ["Garçons 16 - 17ans -80kg"] = new() { ["fr"] = "Garçons 16-17 ans -80kg", ["en"] = "Boys 16-17 y.o. -80kg" },
        ["Garçons 8 - 9ans +35kg"] = new() { ["fr"] = "Garçons 8-9 ans +35kg", ["en"] = "Boys 8-9 y.o. +35kg" },
        ["Garçons 8 - 9ans -25kg"] = new() { ["fr"] = "Garçons 8-9 ans -25kg", ["en"] = "Boys 8-9 y.o. -25kg" },
        ["Garçons 8 - 9ans -30kg"] = new() { ["fr"] = "Garçons 8-9 ans -30kg", ["en"] = "Boys 8-9 y.o. -30kg" },
        ["Garçons 8 - 9ans -35kg"] = new() { ["fr"] = "Garçons 8-9 ans -35kg", ["en"] = "Boys 8-9 y.o. -35kg" },
        ["Garçons 8 - 9ans -kg"] = new() { ["fr"] = "Garçons 8-9 ans -kg", ["en"] = "Boys 8-9 y.o. -kg" },
        ["Hommes +18ans +90kg"] = new() { ["fr"] = "Hommes +18 ans +90kg", ["en"] = "Men +18 y.o. +90kg" },
        ["Hommes +18ans -60kg"] = new() { ["fr"] = "Hommes +18 ans -60kg", ["en"] = "Men +18 y.o. -60kg" },
        ["Hommes +18ans -70kg"] = new() { ["fr"] = "Hommes +18 ans -70kg", ["en"] = "Men +18 y.o. -70kg" },
        ["Hommes +18ans -80kg"] = new() { ["fr"] = "Hommes +18 ans -80kg", ["en"] = "Men +18 y.o. -80kg" },
        ["Hommes +18ans -90kg"] = new() { ["fr"] = "Hommes +18 ans -90kg", ["en"] = "Men +18 y.o. -90kg" },
        ["Vétérans Femmes +35ans +60kg"] = new() { ["fr"] = "Vétérans Femmes +35 ans +60kg", ["en"] = "Veterans Women 35+ y.o. +60kg" },
        ["Vétérans Femmes +35ans +65kg"] = new() { ["fr"] = "Vétérans Femmes +35 ans +65kg", ["en"] = "Veterans Women 35+ y.o. +65kg" },
        ["Vétérans Femmes +35ans -55kg"] = new() { ["fr"] = "Vétérans Femmes +35 ans -55kg", ["en"] = "Veterans Women 35+ y.o. -55kg" },
        ["Vétérans Femmes +35ans -60kg"] = new() { ["fr"] = "Vétérans Femmes +35 ans -60kg", ["en"] = "Veterans Women 35+ y.o. -60kg" },
        ["Vétérans Femmes +35ans -65kg"] = new() { ["fr"] = "Vétérans Femmes +35 ans -65kg", ["en"] = "Veterans Women 35+ y.o. -65kg" },
        ["Vétérans Hommes +45ans +85kg"] = new() { ["fr"] = "Vétérans Hommes +45 ans +85kg", ["en"] = "Veterans Men 45+ y.o. +85kg" },
        ["Vétérans Hommes +45ans -75kg"] = new() { ["fr"] = "Vétérans Hommes +45 ans -75kg", ["en"] = "Veterans Men 45+ y.o. -75kg" },
        ["Vétérans Hommes +45ans -85kg"] = new() { ["fr"] = "Vétérans Hommes +45 ans -85kg", ["en"] = "Veterans Men 45+ y.o. -85kg" },
        ["Vétérans Hommes +85kg"] = new() { ["fr"] = "Vétérans Hommes +85kg", ["en"] = "Veterans Men +85kg" },
        ["Vétérans Hommes 35 - 45ans +85kg"] = new() { ["fr"] = "Vétérans Hommes 35-45 ans +85kg", ["en"] = "Veterans Men 35-45 y.o. +85kg" },
        ["Vétérans Hommes 35 - 45ans -75kg"] = new() { ["fr"] = "Vétérans Hommes 35-45 ans -75kg", ["en"] = "Veterans Men 35-45 y.o. -75kg" },
        ["Vétérans Hommes 35 - 45ans -85kg"] = new() { ["fr"] = "Vétérans Hommes 35-45 ans -85kg", ["en"] = "Veterans Men 35-45 y.o. -85kg" },
        ["Vétérans Hommes -85kg"] = new() { ["fr"] = "Vétérans Hommes -85kg", ["en"] = "Veterans Men -85kg" },
        ["Femmes +34ans"] = new() { ["fr"] = "Femmes +34 ans", ["en"] = "Women 34+ y.o." },
        ["Femmes 18 - 34ans"] = new() { ["fr"] = "Femmes 18-34 ans", ["en"] = "Women 18-34 y.o." },
        ["Filles 14 - 15ans"] = new() { ["fr"] = "Filles 14-15 ans", ["en"] = "Girls 14-15 y.o." },
        ["Filles 16 - 17ans"] = new() { ["fr"] = "Filles 16-17 ans", ["en"] = "Girls 16-17 y.o." },
        ["Garçons / Filles 10 - 11ans"] = new() { ["fr"] = "Garçons / Filles 10-11 ans", ["en"] = "Boys / Girls 10-11 y.o." },
        ["Garçons / Filles 12 - 13ans"] = new() { ["fr"] = "Garçons / Filles 12-13 ans", ["en"] = "Boys / Girls 12-13 y.o." },
        ["Garçons / Filles 8 - 9ans"] = new() { ["fr"] = "Garçons / Filles 8-9 ans", ["en"] = "Boys / Girls 8-9 y.o." },
        ["Garçons 14 - 15ans"] = new() { ["fr"] = "Garçons 14-15 ans", ["en"] = "Boys 14-15 y.o." },
        ["Garçons 16 - 17ans"] = new() { ["fr"] = "Garçons 16-17 ans", ["en"] = "Boys 16-17 y.o." },
        ["Hommes +34ans"] = new() { ["fr"] = "Hommes +34 ans", ["en"] = "Men 34+ y.o." },
        ["Hommes 18 - 34ans"] = new() { ["fr"] = "Hommes 18-34 ans", ["en"] = "Men 18-34 y.o." },


        ["FrenchOpen2027"] = new() { ["fr"] = "Open de France 2027", ["en"] = "French Open 2027" },
        ["KataInProgress"] = new() { ["fr"] = "Kata en cours", ["en"] = "Kata in progress" },
        ["Day"] = new() { ["fr"] = "J", ["en"] = "D" },
        ["DayFull"] = new() { ["fr"] = "Jour", ["en"] = "Day" },
        ["Turn1"] = new() { ["fr"] = "Tour 1", ["en"] = "Turn 1" },
        ["Turn2"] = new() { ["fr"] = "Tour 2", ["en"] = "Turn 2" },
        ["Turn2Final"] = new() { ["fr"] = "Tour 2 (finale)", ["en"] = "Turn 2 (final)" },
        ["Grade"] = new() {["fr"] = "Grade", ["en"] = "Grade" },
        ["Kata"] = new() { ["fr"] = "Kata", ["en"] = "Kata" },
        ["Kumite"] = new() { ["fr"] = "Kumité", ["en"] = "Kumite" },
        ["Katas"] = new() { ["fr"] = "Katas", ["en"] = "Katas" },
        ["Kumites"] = new() { ["fr"] = "Kumités", ["en"] = "Kumites" },
        ["Notes"] = new() { ["fr"] = "Notes", ["en"] = "Notes" },
        ["WaitingNotes"] = new() { ["fr"] = "En attente notes", ["en"] = "Pending notes" },
        ["NotNotes"] = new() { ["fr"] = "Pas de note", ["en"] = "No score" },
        ["NoScore"] = new() { ["fr"] = "Pas de note", ["en"] = "No score" },
        ["KataFirstNotice"] = new() { ["fr"] = "Les épreuves Kata se déroulent en premier. Les combats débuteront à l'issue des Katas.", ["en"] = "Kata events run first. Kumite matches will begin after Kata events finish." },
        ["KataInProgressFightsPending"] = new() { ["fr"] = "Épreuves Kata en cours", ["en"] = "Kata events in progress" },
        ["PendingKataRemaining"] = new() { ["fr"] = "passage(s) Kata restant(s)", ["en"] = "Kata performance(s) remaining" },
        ["NoKataOnTatami"] = new() { ["fr"] = "Aucun passage Kata en cours sur ce tatami", ["en"] = "No Kata sequence is currently in progress on this tatami" },
        ["NoFightOnTatami"] = new() { ["fr"] = "Aucun combat en cours sur ce tatami", ["en"] = "No match currently taking place on this tatami" },
        ["NextKataPassages"] = new() { ["fr"] = "⏭️ Prochains passages Kata", ["en"] = "⏭️ Next Kata passages" },
        ["NextFight"] = new() { ["fr"] = "⏭️ Prochains combats", ["en"] = "⏭️ Next fights" },
        ["NoOtherPassagesPending"] = new() { ["fr"] = "Aucun autre passage en attente.", ["en"] = "No other passages pending." },
        ["NoOtherFightPending"] = new() { ["fr"] = "Aucun autre combat programmé pour le moment.", ["en"] = "No other fight scheduled at the moment." },
        ["Program"] = new() { ["fr"] = "📋 Programme", ["en"] = "📋 Program"},
        ["All"] = new() { ["fr"] = "Tous", ["en"] = "All" },
        ["Search"] = new() { ["fr"] = "Rechercher...", ["en"] = "Search..." },
        ["FilterByTatami"] = new() { ["fr"] = "Filtrer par Tatami :", ["en"] = "Filter by Tatami:" },
        ["AllTatamis"] = new() { ["fr"] = "Tous les tatamis", ["en"] = "All tatamis" },
        ["FilterByCategory"] = new() { ["fr"] = "Filtrer par Catégorie :", ["en"] = "Filter by Category:" },
        ["AllCategories"] = new() { ["fr"] = "Toutes les catégories", ["en"] = "All categories" },
        ["AllDays"] = new() { ["fr"] = "Tous les jours", ["en"] = "All days" },
        ["Refresh"] = new() { ["fr"] = "Rafraîchir", ["en"] = "Refresh" },
        ["SelectAll"] = new() { ["fr"] = "Tout sélectionner", ["en"] = "Select all" },
        ["DeselectAll"] = new() { ["fr"] = "Tout désélectionner", ["en"] = "Deselect all" },
        ["CategoriesFiltered"] = new() { ["fr"] = "catégorie(s)", ["en"] = "category(ies)" },
        ["ResetFilter"] = new() { ["fr"] = "Réinitialiser", ["en"] = "Reset" },
        ["Order"] = new() { ["fr"] = "Ordre", ["en"] = "Order" },
        ["Type"] = new() { ["fr"] = "Type", ["en"] = "Type" },
        ["Tatami"] = new() { ["fr"] = "Tatami", ["en"] = "Tatami" },
        ["Category"] = new() { ["fr"] = "Catégorie", ["en"] = "Category" },
        ["Competitor(s)"] = new() { ["fr"] = "Compétiteur(s)", ["en"] = "Competitor(s)" },
        ["Details"] = new() { ["fr"] = "Détails", ["en"] = "Details" },
        ["Status"] = new() { ["fr"] = "Statut", ["en"] = "Status" },
        ["Coming"] = new() { ["fr"] = "À venir", ["en"] = "Coming" },
        ["ShowFinished"] = new() { ["fr"] = "Afficher terminés", ["en"] = "Show finished" },
        ["HideFinished"] = new() { ["fr"] = "Masquer terminés", ["en"] = "Hide finished" },
        ["ShowMenu"] = new() { ["fr"] = "Afficher le menu", ["en"] = "Show menu" },
        ["HideMenu"] = new() { ["fr"] = "Cacher le menu", ["en"] = "Hide menu" },
        ["HasLittleFinal"] = new() { ["fr"] = "🥉 Petite finale (3e place)", ["en"] = "🥉 Little final (3rd place)" },
        ["WithoutLittleFinal"] = new() { ["fr"] = "Sans petite finale", ["en"] = "Without little final" },
        ["FirstTurn"] = new() { ["fr"] = "1er Tour - Éliminatoire", ["en"] = "1st Turn - Qualifying Round" },
        ["QualifiedFinal"] = new() { ["fr"] = "✅ Qualifié Finale", ["en"] = "✅ Qualified for the final"},
        ["Eliminated"] = new() { ["fr"] = "Éliminé", ["en"] = "Eliminated" },
        ["OnlyNFirst"] = new() { ["fr"] = "premiers sont qualifiés pour la finale", ["en"] = "first ones qualify for the final" },
        ["Only"] = new() { ["fr"] = "Seulement les", ["en"] = "Only the" },
        ["SecondTurn"] = new() { ["fr"] = "2e Tour - Finale", ["en"] = "2nd Turn - Final" },
        ["Stopped"] = new() { ["fr"] = "Arrêté", ["en"] = "Stopped" },
        ["WithoutTatami"] = new() { ["fr"] = "(🚫 Sans tatami)", ["en"] = "(🚫 Without tatami)" },
        ["Final"] = new() { ["fr"] = "Finale", ["en"] = "Final" },
        ["Semi-Final"] = new() { ["fr"] = "Demi-Finale", ["en"] = "Semi-Final" },
        ["Quarter Final"] = new() { ["fr"] = "Quart de finale", ["en"] = "Quarter Final" },
        ["Round of 16"] = new() { ["fr"] = "8ème de finale", ["en"] = "Round of 16" },
        ["Round of 32"] = new() { ["fr"] = "16ème de finale", ["en"] = "Round of 32" },
        ["Turn 1"] = new() { ["fr"] = "Tour 1", ["en"] = "Turn 1" },
        ["Turn 2"] = new() { ["fr"] = "Tour 2", ["en"] = "Turn 2" },
        ["Turn 3"] = new() { ["fr"] = "Tour 3", ["en"] = "Turn 3" },
        ["Turn 4"] = new() { ["fr"] = "Tour 4", ["en"] = "Turn 4" },
        ["Turn 5"] = new() { ["fr"] = "Tour 5", ["en"] = "Turn 5" },
        ["Filter"] = new() { ["fr"] = "Filtrer", ["en"] = "Filter" },


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
