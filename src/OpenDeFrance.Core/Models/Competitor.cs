using OpenDeFrance.Core.Enums;

namespace OpenDeFrance.Core.Models;

public class Competitor
{
    public int Id { get; set; }
    public int? BibNumber { get; set; } // Numéro de dossard
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Country { get; set; } = "France";
    public string Club { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public int Age { get; set; }
    public double Weight { get; set; } // En kg
    public BeltGrade Grade { get; set; } = BeltGrade.White;
    public Gender Gender { get; set; } = Gender.Male;
    public bool IsKumiteRegistered { get; set; } = true;
    public bool IsKataRegistered { get; set; } = false;
    public CompetitorStatus Status { get; set; } = CompetitorStatus.Absent;

    public List<Category> Categories { get; set; } = new();

    public string FullName => $"{LastName.ToUpperInvariant()} {FirstName}";
    public string CountryCode => GetCountryCode(Country);
    public string CountryFlag => GetCountryFlag(Country);
    public string CountryWithFlag => $"{CountryFlag} {Country}";
    public string FlagImageUrl => $"/flags/{CountryCode}.png";
    public string BibDisplay => BibNumber.HasValue ? $"#{BibNumber.Value}" : "";
    public string DisplayInfo => $"{(BibNumber.HasValue ? $"[N°{BibNumber.Value}] " : "")}{FullName} ({CountryWithFlag} - {Club}) - {Grade} - {Weight:F1}kg";
    public string GradeName => GetGradeName(Grade);

    public string StatusBadgeClass => Status switch
    {
        CompetitorStatus.Present => "bg-success text-white",
        CompetitorStatus.Absent => "bg-danger text-white",
        CompetitorStatus.Eliminated => "bg-secondary text-white",
        _ => "bg-secondary text-white"
    };

    public string StatusName => Status switch
    {
        CompetitorStatus.Present => "Présent",
        CompetitorStatus.Absent => "Absent",
        CompetitorStatus.Eliminated => "Éliminé",
        _ => Status.ToString()
    };

    public static string GetGradeName(BeltGrade grade) 
    {
        switch(grade) {
            case BeltGrade.White:
                return "11eme Kyu";
            case BeltGrade.Orange:
                return "10eme Kyu";
            case BeltGrade.Orange_2:
                return "9eme Kyu";
            case BeltGrade.Blue:
                return "8eme Kyu";
            case BeltGrade.Blue_2:
                return "7eme Kyu";
            case BeltGrade.Yellow:
                return "6eme Kyu";
            case BeltGrade.Yellow_2:
                return "5eme Kyu";
            case BeltGrade.Green:
                return "4eme Kyu";
            case BeltGrade.Green_2:
                return "3eme Kyu";
            case BeltGrade.Brown:
                return "2eme Kyu";
            case BeltGrade.Brown_2:
                return "1er Kyu";
            case BeltGrade.Shodan:
                return "Shodan";
            case BeltGrade.Nidan:
                return "Nidan";
            case BeltGrade.Sandan:
                return "Sandan";
            case BeltGrade.Yondan:
                return "Yondan";
            case BeltGrade.Godan:
                return "Godan";
            default:
                return "";
        };
    }

    public static string GetCountryCode(string? country)
    {
        if (string.IsNullOrWhiteSpace(country)) return "fr";
        return country.Trim().ToLowerInvariant() switch
        {
            "france" or "fra" or "fr" => "fr",
            "japon" or "japan" or "jp" or "jpn" => "jp",
            "espagne" or "spain" or "es" or "esp" => "es",
            "pologne" or "poland" or "pl" or "pol" => "pl",
            "belgique" or "belgium" or "be" or "bel" => "be",
            "suisse" or "switzerland" or "ch" or "sui" => "ch",
            "italie" or "italy" or "it" or "ita" => "it",
            "allemagne" or "germany" or "de" or "ger" => "de",
            "royaume-uni" or "uk" or "great britain" or "england" or "angleterre" or "gb" => "gb",
            "pays-bas" or "netherlands" or "hollande" or "nl" or "ned" => "nl",
            "brésil" or "bresil" or "brazil" or "br" or "bra" => "br",
            "états-unis" or "etats-unis" or "usa" or "us" or "united states" => "us",
            "canada" or "ca" or "can" => "ca",
            "portugal" or "pt" or "por" => "pt",
            "suède" or "suede" or "sweden" or "se" or "swe" => "se",
            "norvège" or "norvege" or "norway" or "no" or "nor" => "no",
            "danemark" or "denmark" or "dk" or "den" => "dk",
            "irlande" or "ireland" or "ie" or "irl" => "ie",
            "roumanie" or "romania" or "ro" or "rou" => "ro",
            "bulgarie" or "bulgaria" or "bg" or "bul" => "bg",
            "hongrie" or "hungary" or "hu" or "hun" => "hu",
            "grèce" or "grece" or "greece" or "gr" or "gre" => "gr",
            "turquie" or "turkey" or "tr" or "tur" => "tr",
            "kazakhstan" or "kz" or "kaz" => "kz",
            "géorgie" or "georgie" or "georgia" or "ge" or "geo" => "ge",
            "lituanie" or "lithuania" or "lt" or "ltu" => "lt",
            "lettonie" or "latvia" or "lv" or "lat" => "lv",
            "estonie" or "estonia" or "ee" or "est" => "ee",
            _ => country.Trim().Length == 2 ? country.Trim().ToLowerInvariant() : "fr"
        };
    }

    public static string GetCountryFlag(string? country)
    {
        if (string.IsNullOrWhiteSpace(country)) return "🏳️";
        return country.Trim().ToLowerInvariant() switch
        {
            "france" or "fra" or "fr" => "🇫🇷",
            "japon" or "japan" or "jp" or "jpn" => "🇯🇵",
            "espagne" or "spain" or "es" or "esp" => "🇪🇸",
            "pologne" or "poland" or "pl" or "pol" => "🇵🇱",
            "belgique" or "belgium" or "be" or "bel" => "🇧🇪",
            "suisse" or "switzerland" or "ch" or "sui" => "🇨🇭",
            "italie" or "italy" or "it" or "ita" => "🇮🇹",
            "allemagne" or "germany" or "de" or "ger" => "🇩🇪",
            "royaume-uni" or "uk" or "great britain" or "england" or "angleterre" or "gb" => "🇬🇧",
            "pays-bas" or "netherlands" or "hollande" or "nl" or "ned" => "🇳🇱",
            "brésil" or "bresil" or "brazil" or "br" or "bra" => "🇧🇷",
            "états-unis" or "etats-unis" or "usa" or "us" or "united states" => "🇺🇸",
            "canada" or "ca" or "can" => "🇨🇦",
            "portugal" or "pt" or "por" => "🇵🇹",
            "suède" or "suede" or "sweden" or "se" or "swe" => "🇸🇪",
            "norvège" or "norvege" or "norway" or "no" or "nor" => "🇳🇴",
            "danemark" or "denmark" or "dk" or "den" => "🇩🇰",
            "irlande" or "ireland" or "ie" or "irl" => "🇮🇪",
            "roumanie" or "romania" or "ro" or "rou" => "🇷🇴",
            "bulgarie" or "bulgaria" or "bg" or "bul" => "🇧🇬",
            "hongrie" or "hungary" or "hu" or "hun" => "🇭🇺",
            "grèce" or "grece" or "greece" or "gr" or "gre" => "🇬🇷",
            "turquie" or "turkey" or "tr" or "tur" => "🇹🇷",
            "kazakhstan" or "kz" or "kaz" => "🇰🇿",
            "géorgie" or "georgie" or "georgia" or "ge" or "geo" => "🇬🇪",
            "lituanie" or "lithuania" or "lt" or "ltu" => "🇱🇹",
            "lettonie" or "latvia" or "lv" or "lat" => "🇱🇻",
            "estonie" or "estonia" or "ee" or "est" => "🇪🇪",
            _ => "🌍"
        };
    }
}
