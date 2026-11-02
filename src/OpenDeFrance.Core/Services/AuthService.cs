using OpenDeFrance.Core.Models;

namespace OpenDeFrance.Core.Services;

public interface IAuthService
{
    UserSession? Authenticate(string username, string password);
}

public class AuthService : IAuthService
{
    public UserSession? Authenticate(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        string u = username.Trim().ToLowerInvariant();
        string p = (password ?? string.Empty).Trim();

        // Admin : tous les droits (import excel, catégories, tirages, chrono, TV)
        if (u == "admin" && (p == "admin123" || p == "admin" || p == "kyokushin"))
        {
            return new UserSession
            {
                Username = "admin",
                Role = "Admin",
                DisplayName = "Administrateur Général"
            };
        }

        // Table : chronomètre, scores, prolongations, validation katas
        if (u == "table" && (p == "table123" || p == "table" || p == "tatami"))
        {
            return new UserSession
            {
                Username = "table",
                Role = "Table",
                DisplayName = "Table de Marque"
            };
        }

        // TV : affichage grand écran des tatamis
        if (u == "tv" && (p == "tv123" || p == "tv" || p == "ecran"))
        {
            return new UserSession
            {
                Username = "tv",
                Role = "Tv",
                DisplayName = "Écran Tatami TV"
            };
        }

        // Streamer : consultation en direct des combats et katas à venir (lecture seule régie / commentateurs)
        if (u == "streamer" && (p == "streamer123" || p == "streamer" || p == "stream" || p == "live"))
        {
            return new UserSession
            {
                Username = "streamer",
                Role = "Streamer",
                DisplayName = "Régie Streamer (Live)"
            };
        }

        // Collector : chambre d'appel et mise en place des compétiteurs
        if (u == "collector" && (p == "collector123" || p == "collector" || p == "appel" || p == "chambre"))
        {
            return new UserSession
            {
                Username = "collector",
                Role = "Collector",
                DisplayName = "Chambre d'Appel (Collector)"
            };
        }

        return null;
    }
}
