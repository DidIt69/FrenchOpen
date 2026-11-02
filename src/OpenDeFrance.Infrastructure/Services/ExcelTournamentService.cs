using ClosedXML.Excel;
using OpenDeFrance.Core.Enums;
using OpenDeFrance.Core.Models;
using OpenDeFrance.Core.Services;

namespace OpenDeFrance.Infrastructure.Services;

public class ExcelTournamentService : IExcelTournamentService
{
    public byte[] GenerateSampleExcelTemplate()
    {
        using var workbook = new XLWorkbook();
        
        // ==========================================
        // FEUILLE 1 : COMPÉTITEURS (Sans jour ni tatami)
        // ==========================================
        var wsComp = workbook.Worksheets.Add("Compétiteurs");

        string[] compHeaders = {
            "Dossard", "Nom", "Prénom", "Pays", "Sexe (M/F)", "Age", "Date Naissance",
            "Club", "Grade", "Poids (kg)", "Kumite (O/N)", "Kata (O/N)",
            "Catégorie Kumite", "Catégorie Kata"
        };

        for (int i = 0; i < compHeaders.Length; i++)
        {
            var cell = wsComp.Cell(1, i + 1);
            cell.Value = compHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.Navy;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var sampleCompRows = new List<object[]>
        {
            // Senior Hommes -75kg & Katas
            new object[] { 101, "DUPONT", "Jean", "France", "M", 24, "2002-05-14", "Dojo Paris 11", "Noire1Dan", 73.5, "O", "O", "Senior Hommes -75kg", "Senior Hommes Kata" },
            new object[] { 102, "SANCHEZ", "Carlos", "Espagne", "M", 26, "2000-08-20", "Dojo Madrid", "Marron", 74.0, "O", "N", "Senior Hommes -75kg", "" },
            new object[] { 103, "TANI", "Kenji", "Japon", "M", 22, "2004-03-10", "Honbu Tokyo", "Noire2Dan", 72.8, "O", "O", "Senior Hommes -75kg", "Senior Hommes Kata" },
            new object[] { 104, "KOWALSKI", "Jan", "Pologne", "M", 25, "2001-11-03", "Kyokushin Varsovie", "Vert", 71.5, "O", "N", "Senior Hommes -75kg", "" },
            new object[] { 105, "LECLERC", "Maxime", "France", "M", 28, "1998-01-22", "Karaté Club Marseille", "Bleu", 74.8, "O", "O", "Senior Hommes -75kg", "Senior Hommes Kata" },
            new object[] { 106, "VANDENBERGHE", "Lucas", "Belgique", "M", 23, "2003-09-17", "Dojo Bruxelles", "Jaune", 73.0, "O", "N", "Senior Hommes -75kg", "" },
            new object[] { 107, "ROUX", "Julien", "France", "M", 29, "1997-04-12", "Dojo Bordeaux", "Noire1Dan", 72.0, "O", "O", "Senior Hommes -75kg", "Senior Hommes Kata" },
            new object[] { 108, "GARCIA", "Alejandro", "Espagne", "M", 27, "1999-07-29", "Dojo Barcelone", "Marron", 74.2, "O", "N", "Senior Hommes -75kg", "" },
            
            // Poids Lourds +80kg
            new object[] { 201, "GARCIA", "Thomas", "Espagne", "M", 25, "2001-02-18", "Dojo Barcelone", "Noire2Dan", 88.0, "O", "N", "Senior Hommes +80kg", "" },
            new object[] { 202, "DUBOIS", "Romain", "France", "M", 27, "1999-10-05", "Kyokushin Lille", "Marron", 86.5, "O", "N", "Senior Hommes +80kg", "" },
            new object[] { 203, "NOWAK", "Piotr", "Pologne", "M", 30, "1996-06-12", "Kyokushin Cracovie", "Noire1Dan", 91.0, "O", "N", "Senior Hommes +80kg", "" },
            new object[] { 204, "FONTAINE", "Christophe", "France", "M", 24, "2002-12-01", "Karaté Club Marseille", "Vert", 84.5, "O", "N", "Senior Hommes +80kg", "" },

            // Féminines
            new object[] { 301, "PETIT", "Camille", "France", "F", 23, "2003-04-05", "Kyokushin Lyon", "Marron", 58.0, "O", "O", "Senior Femmes -60kg", "Senior Femmes Kata" },
            new object[] { 302, "MARTINEZ", "Elena", "Espagne", "F", 25, "2001-07-14", "Dojo Madrid", "Noire1Dan", 59.2, "O", "O", "Senior Femmes -60kg", "Senior Femmes Kata" },
            new object[] { 303, "SUZUKI", "Yuki", "Japon", "F", 22, "2004-11-21", "Honbu Tokyo", "Vert", 57.5, "O", "O", "Senior Femmes -60kg", "Senior Femmes Kata" },
            new object[] { 304, "GIRARD", "Sarah", "France", "F", 26, "2000-03-30", "Karaté Club Marseille", "Bleu", 58.8, "O", "O", "Senior Femmes -60kg", "Senior Femmes Kata" }
        };

        for (int r = 0; r < sampleCompRows.Count; r++)
        {
            var rowData = sampleCompRows[r];
            for (int c = 0; c < rowData.Length; c++)
            {
                wsComp.Cell(r + 2, c + 1).Value = rowData[c]?.ToString() ?? "";
            }
        }
        wsComp.Columns().AdjustToContents();

        // ==========================================
        // FEUILLE 2 : CATÉGORIES (Jour & Tatami par Catégorie)
        // ==========================================
        var wsCat = workbook.Worksheets.Add("Catégories");

        string[] catHeaders = { "Catégorie", "Discipline", "Jour", "Tatami", "Ordre" };

        for (int i = 0; i < catHeaders.Length; i++)
        {
            var cell = wsCat.Cell(1, i + 1);
            cell.Value = catHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.Navy;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var sampleCatRows = new List<object[]>
        {
            // Jour 1 : Senior Hommes -75kg sur Tatami 1 (Kumite) et Katas sur Tatami 3 (Kata)
            new object[] { "Senior Hommes -75kg", "Kumite", 1, 1, 1 },
            new object[] { "Senior Hommes Kata", "Kata", 1, 3, 2 },
            
            // Jour 2 : Poids Lourds sur Tatami 2 (Kumite)
            new object[] { "Senior Hommes +80kg", "Kumite", 2, 2, 1 },

            // Jour 2 : Féminines sur Tatami 1 (Kumite) et Katas sur Tatami 3 (Kata)
            new object[] { "Senior Femmes -60kg", "Kumite", 2, 1, 2 },
            new object[] { "Senior Femmes Kata", "Kata", 2, 3, 3 }
        };

        for (int r = 0; r < sampleCatRows.Count; r++)
        {
            var rowData = sampleCatRows[r];
            for (int c = 0; c < rowData.Length; c++)
            {
                wsCat.Cell(r + 2, c + 1).Value = rowData[c]?.ToString() ?? "";
            }
        }
        wsCat.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public ExcelImportResult ImportTournamentData(Stream excelStream)
    {
        var result = new ExcelImportResult();

        try
        {
            using var workbook = new XLWorkbook(excelStream);
            if (workbook.Worksheets.Count == 0)
            {
                result.Success = false;
                result.Errors.Add("Aucune feuille trouvée dans le fichier Excel.");
                return result;
            }

            // Détection de la feuille Compétiteurs
            IXLWorksheet wsCompetitors = workbook.Worksheets.FirstOrDefault(w =>
                w.Name.Contains("comp", StringComparison.OrdinalIgnoreCase) ||
                w.Name.Contains("part", StringComparison.OrdinalIgnoreCase) ||
                w.Name.Contains("athle", StringComparison.OrdinalIgnoreCase) ||
                w.Name.Contains("inscrit", StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.First();

            // Détection de la feuille Catégories (si présente)
            IXLWorksheet? wsCategories = workbook.Worksheets.FirstOrDefault(w =>
                w != wsCompetitors && (
                    w.Name.Contains("cat", StringComparison.OrdinalIgnoreCase) ||
                    w.Name.Contains("plan", StringComparison.OrdinalIgnoreCase) ||
                    w.Name.Contains("prog", StringComparison.OrdinalIgnoreCase) ||
                    w.Name.Contains("tatami", StringComparison.OrdinalIgnoreCase) ||
                    w.Name.Contains("jour", StringComparison.OrdinalIgnoreCase)
                ));

            int rowCount = wsCompetitors.LastRowUsed()?.RowNumber() ?? 0;
            if (rowCount <= 1)
            {
                result.Success = false;
                result.Errors.Add("La feuille des compétiteurs ne contient aucune ligne de données.");
                return result;
            }

            var categoryDict = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
            var competitorList = new List<Competitor>();

            int currentId = 1;
            int nextCatId = 1;

            // Détection dynamique des colonnes de la feuille Compétiteurs
            int colBibNumber = -1, colLastName = 1, colFirstName = 2, colCountry = -1, colGender = 3, colAge = 4, colBirthDate = 5;
            int colClub = 6, colGrade = 7, colWeight = 8, colKumite = 9, colKata = 10, colCustomKumite = 11, colCustomKata = 12;
            int colKumiteDay = -1, colKumiteTatami = -1, colKataDay = -1, colKataTatami = -1;
            int colLegacyDay = -1, colLegacyTatami = -1;

            int lastCol = wsCompetitors.LastColumnUsed()?.ColumnNumber() ?? 16;
            for (int c = 1; c <= lastCol; c++)
            {
                string h = wsCompetitors.Cell(1, c).GetString().Trim().ToLowerInvariant();
                if (h.Contains("dossard") || h.Contains("bib") || (h.Contains("num") && (h.Contains("dossard") || h.Contains("comp")))) colBibNumber = c;
                else if (h.Contains("nom") && !h.Contains("prénom") && !h.Contains("prenom")) colLastName = c;
                else if (h.Contains("prénom") || h.Contains("prenom")) colFirstName = c;
                else if (h.Contains("pays") || h.Contains("country") || h.Contains("nation")) colCountry = c;
                else if (h.Contains("sexe") || h.Contains("gender")) colGender = c;
                else if (h.Contains("age") || h.Contains("âge")) colAge = c;
                else if (h.Contains("naissance") || h.Contains("birth")) colBirthDate = c;
                else if (h.Contains("club") || h.Contains("dojo")) colClub = c;
                else if (h.Contains("grade") || h.Contains("ceinture") || h.Contains("belt")) colGrade = c;
                else if (h.Contains("poids") || h.Contains("weight")) colWeight = c;
                else if (h.Contains("kumite") && (h.Contains("jour") || h.Contains("day"))) colKumiteDay = c;
                else if (h.Contains("kumite") && h.Contains("tatami")) colKumiteTatami = c;
                else if (h.Contains("kata") && (h.Contains("jour") || h.Contains("day"))) colKataDay = c;
                else if (h.Contains("kata") && h.Contains("tatami")) colKataTatami = c;
                else if (h.Contains("kumite") && (h.Contains("o/n") || h.Contains("oui") || h.Contains("inscrit") || h == "kumite")) colKumite = c;
                else if (h.Contains("kata") && (h.Contains("o/n") || h.Contains("oui") || h.Contains("inscrit") || h == "kata")) colKata = c;
                else if (h.Contains("cat") && h.Contains("kumite")) colCustomKumite = c;
                else if (h.Contains("cat") && h.Contains("kata")) colCustomKata = c;
                else if (h.Contains("jour") || h.Contains("day")) colLegacyDay = c;
                else if (h.Contains("tatami") && !h.Contains("assign")) colLegacyTatami = c;
            }

            for (int r = 2; r <= rowCount; r++)
            {
                var row = wsCompetitors.Row(r);
                string lastName = row.Cell(colLastName).GetString().Trim();
                string firstName = row.Cell(colFirstName).GetString().Trim();

                if (string.IsNullOrWhiteSpace(lastName) && string.IsNullOrWhiteSpace(firstName))
                    continue;

                int? bibNumber = null;
                if (colBibNumber > 0)
                {
                    string bibStr = row.Cell(colBibNumber).GetString().Trim().Replace("#", "");
                    if (int.TryParse(bibStr, out int parsedBib))
                    {
                        bibNumber = parsedBib;
                    }
                }

                if (!bibNumber.HasValue)
                {
                    bibNumber = currentId;
                }

                string country = colCountry > 0 ? row.Cell(colCountry).GetString().Trim() : "France";
                if (string.IsNullOrWhiteSpace(country)) country = "France";

                string genderStr = row.Cell(colGender).GetString().Trim().ToUpperInvariant();
                Gender gender = (genderStr == "F" || genderStr == "FEMME" || genderStr == "FEMININ")
                    ? Gender.Female
                    : Gender.Male;

                int age = 20;
                if (int.TryParse(row.Cell(colAge).GetString(), out int parsedAge))
                {
                    age = parsedAge;
                }

                DateTime? birthDate = null;
                if (DateTime.TryParse(row.Cell(colBirthDate).GetString(), out DateTime parsedDate))
                {
                    birthDate = parsedDate;
                    if (age == 20)
                    {
                        age = DateTime.Today.Year - birthDate.Value.Year;
                    }
                }

                string club = row.Cell(colClub).GetString().Trim();
                string gradeStr = row.Cell(colGrade).GetString().Trim();
                BeltGrade grade = ParseGrade(gradeStr);

                double weight = 70.0;
                if (double.TryParse(row.Cell(colWeight).GetString().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedWeight))
                {
                    weight = parsedWeight;
                }

                string kumiteRegStr = row.Cell(colKumite).GetString().Trim().ToUpperInvariant();
                bool isKumite = kumiteRegStr == "O" || kumiteRegStr == "OUI" || kumiteRegStr == "YES" || kumiteRegStr == "1" || kumiteRegStr == "TRUE";

                string kataRegStr = row.Cell(colKata).GetString().Trim().ToUpperInvariant();
                bool isKata = kataRegStr == "O" || kataRegStr == "OUI" || kataRegStr == "YES" || kataRegStr == "1" || kataRegStr == "TRUE";

                string customKumiteCat = colCustomKumite > 0 ? row.Cell(colCustomKumite).GetString().Trim() : "";
                string customKataCat = colCustomKata > 0 ? row.Cell(colCustomKata).GetString().Trim() : "";

                if (!isKumite && !isKata)
                {
                    isKumite = true;
                }

                var competitor = new Competitor
                {
                    Id = currentId++,
                    BibNumber = bibNumber,
                    LastName = lastName,
                    FirstName = firstName,
                    Country = country,
                    Gender = gender,
                    Age = age,
                    BirthDate = birthDate,
                    Club = string.IsNullOrWhiteSpace(club) ? "Indépendant" : club,
                    Grade = grade,
                    Weight = weight,
                    IsKumiteRegistered = isKumite,
                    IsKataRegistered = isKata
                };

                competitorList.Add(competitor);

                // Associer / créer catégorie Kumite
                if (isKumite)
                {
                    string catKumiteName = !string.IsNullOrWhiteSpace(customKumiteCat)
                        ? customKumiteCat
                        : DetectDefaultCategoryName(competitor, CompetitionType.Kumite);

                    if (!categoryDict.TryGetValue(catKumiteName, out var kumiteCat))
                    {
                        kumiteCat = CreateCategoryWithKyokushinDefaults(catKumiteName, CompetitionType.Kumite, gender, age, weight);
                        kumiteCat.Id = nextCatId++;

                        // Si jour / tatami défini sur la ligne compétiteur (fallback 1 seule feuille)
                        int kDay = 1;
                        if (colKumiteDay > 0 && int.TryParse(row.Cell(colKumiteDay).GetString().Trim(), out int kd)) kDay = Math.Max(1, kd);
                        else if (colLegacyDay > 0 && wsCategories == null && int.TryParse(row.Cell(colLegacyDay).GetString().Trim(), out int ld)) kDay = Math.Max(1, ld);
                        kumiteCat.CompetitionDay = kDay;

                        int? kTatami = null;
                        if (colKumiteTatami > 0) kTatami = ParseTatamiId(row.Cell(colKumiteTatami).GetString().Trim());
                        else if (colLegacyTatami > 0 && wsCategories == null) kTatami = ParseTatamiId(row.Cell(colLegacyTatami).GetString().Trim());
                        if (kTatami.HasValue) kumiteCat.AssignedTatamiId = kTatami.Value;

                        categoryDict[catKumiteName] = kumiteCat;
                    }
                    kumiteCat.Competitors.Add(competitor);
                }

                // Associer / créer catégorie Kata
                if (isKata)
                {
                    string catKataName = !string.IsNullOrWhiteSpace(customKataCat)
                        ? customKataCat
                        : DetectDefaultCategoryName(competitor, CompetitionType.Kata);

                    if (!categoryDict.TryGetValue(catKataName, out var kataCat))
                    {
                        kataCat = CreateCategoryWithKyokushinDefaults(catKataName, CompetitionType.Kata, gender, age, weight);
                        kataCat.Id = nextCatId++;

                        int kDay = 1;
                        if (colKataDay > 0 && int.TryParse(row.Cell(colKataDay).GetString().Trim(), out int kd)) kDay = Math.Max(1, kd);
                        else if (colLegacyDay > 0 && wsCategories == null && int.TryParse(row.Cell(colLegacyDay).GetString().Trim(), out int ld)) kDay = Math.Max(1, ld);
                        kataCat.CompetitionDay = kDay;

                        int? kTatami = null;
                        if (colKataTatami > 0) kTatami = ParseTatamiId(row.Cell(colKataTatami).GetString().Trim());
                        else if (colLegacyTatami > 0 && wsCategories == null) kTatami = ParseTatamiId(row.Cell(colLegacyTatami).GetString().Trim());
                        if (kTatami.HasValue) kataCat.AssignedTatamiId = kTatami.Value;

                        categoryDict[catKataName] = kataCat;
                    }
                    kataCat.Competitors.Add(competitor);
                }
            }

            // Lecture de la feuille dédiée aux Catégories (prioritaire pour la planification)
            if (wsCategories != null)
            {
                int catRowCount = wsCategories.LastRowUsed()?.RowNumber() ?? 0;
                if (catRowCount >= 2)
                {
                    int colCatName = 1, colDiscipline = -1, colCatDay = -1, colCatTatami = -1, colCatOrder = -1;
                    int lastCatCol = wsCategories.LastColumnUsed()?.ColumnNumber() ?? 5;
                    for (int c = 1; c <= lastCatCol; c++)
                    {
                        string h = wsCategories.Cell(1, c).GetString().Trim().ToLowerInvariant();
                        if (h.Contains("cat") || h.Contains("nom") || h.Contains("name") || h.Contains("intitul")) colCatName = c;
                        else if (h.Contains("disc") || h.Contains("type")) colDiscipline = c;
                        else if (h.Contains("jour") || h.Contains("day")) colCatDay = c;
                        else if (h.Contains("tatami")) colCatTatami = c;
                        else if (h.Contains("ordre") || h.Contains("order") || h.Contains("rang") || h.Contains("sequence")) colCatOrder = c;
                    }

                    for (int r = 2; r <= catRowCount; r++)
                    {
                        var row = wsCategories.Row(r);
                        string catName = row.Cell(colCatName).GetString().Trim();
                        if (string.IsNullOrWhiteSpace(catName)) continue;

                        int day = 1;
                        if (colCatDay > 0 && int.TryParse(row.Cell(colCatDay).GetString().Trim(), out int parsedDay))
                        {
                            day = Math.Max(1, parsedDay);
                        }

                        int? tatamiId = null;
                        if (colCatTatami > 0)
                        {
                            tatamiId = ParseTatamiId(row.Cell(colCatTatami).GetString().Trim());
                        }

                        int orderIndex = r - 1;
                        if (colCatOrder > 0 && int.TryParse(row.Cell(colCatOrder).GetString().Trim(), out int parsedOrder) && parsedOrder > 0)
                        {
                            orderIndex = parsedOrder;
                        }

                        CompetitionType disc = CompetitionType.Kumite;
                        if (colDiscipline > 0)
                        {
                            string discStr = row.Cell(colDiscipline).GetString().Trim().ToLowerInvariant();
                            if (discStr.Contains("kata")) disc = CompetitionType.Kata;
                        }
                        else if (catName.Contains("kata", StringComparison.OrdinalIgnoreCase))
                        {
                            disc = CompetitionType.Kata;
                        }

                        if (categoryDict.TryGetValue(catName, out var existingCat))
                        {
                            existingCat.CompetitionDay = day;
                            existingCat.OrderIndex = orderIndex;
                            if (tatamiId.HasValue) existingCat.AssignedTatamiId = tatamiId.Value;
                        }
                        else
                        {
                            var newCat = CreateCategoryWithKyokushinDefaults(catName, disc, Gender.Male, 20, 70.0);
                            newCat.Id = nextCatId++;
                            newCat.CompetitionDay = day;
                            newCat.OrderIndex = orderIndex;
                            if (tatamiId.HasValue) newCat.AssignedTatamiId = tatamiId.Value;
                            categoryDict[catName] = newCat;
                        }
                    }
                }
            }

            result.Competitors = competitorList;
            result.Categories = categoryDict.Values.ToList();
            result.DaysCount = categoryDict.Values.Select(c => c.CompetitionDay).DefaultIfEmpty(1).Max();
            result.Message = $"{competitorList.Count} compétiteur(s) importé(s) dans {result.Categories.Count} catégorie(s) sur {result.DaysCount} jour(s).";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Erreur lors de la lecture du fichier Excel : {ex.Message}");
        }

        return result;
    }

    private static int? ParseTatamiId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string cleaned = raw.Replace("Tatami", "", StringComparison.OrdinalIgnoreCase).Replace("#", "").Trim();
        return int.TryParse(cleaned, out int t) && t > 0 ? t : null;
    }

    private BeltGrade ParseGrade(string gradeStr)
    {
        if (string.IsNullOrWhiteSpace(gradeStr)) return BeltGrade.White;

        string clean = gradeStr.ToLowerInvariant().Replace(" ", "").Replace("-", "");

        if (clean.Contains("godan") || clean.Contains("5dan")) return BeltGrade.Godan;
        if (clean.Contains("yondan") || clean.Contains("4dan")) return BeltGrade.Yondan;
        if (clean.Contains("sandan") || clean.Contains("3dan")) return BeltGrade.Sandan;
        if (clean.Contains("nidan") || clean.Contains("2dan")) return BeltGrade.Nidan;
        if (clean.Contains("shodan") || clean.Contains("1dan") || clean.Contains("noire") || clean.Contains("black") || clean.Contains("dan")) return BeltGrade.Shodan;

        if (clean.Contains("marron") || clean.Contains("brown") || clean.Contains("1erkyu") || clean.Contains("1kyu")) return BeltGrade.Brown_2;
        if (clean.Contains("2emekyu") || clean.Contains("2kyu")) return BeltGrade.Brown;
        if (clean.Contains("vert") || clean.Contains("green") || clean.Contains("3emekyu") || clean.Contains("3kyu")) return BeltGrade.Green_2;
        if (clean.Contains("4emekyu") || clean.Contains("4kyu")) return BeltGrade.Green;
        if (clean.Contains("jaune") || clean.Contains("yellow") || clean.Contains("5emekyu") || clean.Contains("5kyu")) return BeltGrade.Yellow_2;
        if (clean.Contains("6emekyu") || clean.Contains("6kyu")) return BeltGrade.Yellow;
        if (clean.Contains("bleu") || clean.Contains("blue") || clean.Contains("7emekyu") || clean.Contains("7kyu")) return BeltGrade.Blue_2;
        if (clean.Contains("8emekyu") || clean.Contains("8kyu")) return BeltGrade.Blue;
        if (clean.Contains("orange") || clean.Contains("9emekyu") || clean.Contains("9kyu")) return BeltGrade.Orange_2;
        if (clean.Contains("10emekyu") || clean.Contains("10kyu")) return BeltGrade.Orange;
        if (clean.Contains("blanc") || clean.Contains("white") || clean.Contains("11emekyu") || clean.Contains("11kyu")) return BeltGrade.White;

        return BeltGrade.White;
    }

    private string DetectDefaultCategoryName(Competitor c, CompetitionType type)
    {
        string genderStr = c.Gender == Gender.Female ? "Femmes" : "Hommes";
        string ageBand = c.Age switch
        {
            < 8 => "Poussins",
            < 10 => "Pupilles",
            < 12 => "Benjamins",
            < 14 => "Minimes",
            < 16 => "Cadets",
            < 18 => "Juniors",
            < 35 => "Seniors",
            _ => "Vétérans"
        };

        if (type == CompetitionType.Kata)
        {
            return $"{ageBand} {genderStr} Kata";
        }

        // Kumite : avec tranche de poids
        string weightBand = (c.Gender == Gender.Female)
            ? (c.Weight < 55 ? "-55kg" : (c.Weight < 65 ? "-65kg" : "+65kg"))
            : (c.Weight < 70 ? "-70kg" : (c.Weight < 80 ? "-80kg" : "+80kg"));

        return $"{ageBand} {genderStr} {weightBand}";
    }

    private Category CreateCategoryWithKyokushinDefaults(string name, CompetitionType type, Gender gender, int age, double weight)
    {
        var cat = new Category
        {
            Name = name,
            Type = type,
            Gender = gender,
            MinAge = Math.Max(6, age - 2),
            MaxAge = age + 2,
            KataQualifiersCount = 4
        };

        // Règles Kyokushin de durée suivant l'âge
        if (age < 10)
        {
            cat.KumiteRegularDurationSeconds = 60; // 1 min
            cat.KumiteExtensionDurationSeconds = 60;
            cat.KataRound1MandatoryKata = "Taikyoku Sono Ichi";
            cat.KataRound2MandatoryKata = "Taikyoku Sono San";
        }
        else if (age < 14)
        {
            cat.KumiteRegularDurationSeconds = 90; // 1m30
            cat.KumiteExtensionDurationSeconds = 60;
            cat.KataRound1MandatoryKata = "Pinan Sono Ichi";
            cat.KataRound2MandatoryKata = "Pinan Sono Ni";
        }
        else if (age < 18)
        {
            cat.KumiteRegularDurationSeconds = 120; // 2 min
            cat.KumiteExtensionDurationSeconds = 90;
            cat.KataRound1MandatoryKata = "Pinan Sono San";
            cat.KataRound2MandatoryKata = "Gekisai Dai";
        }
        else if (age >= 35)
        {
            cat.KumiteRegularDurationSeconds = 120; // 2 min vétéran
            cat.KumiteExtensionDurationSeconds = 90;
            cat.KataRound1MandatoryKata = "Pinan Sono Go";
            cat.KataRound2MandatoryKata = "Saifa";
        }
        else
        {
            // Seniors
            cat.KumiteRegularDurationSeconds = 120; // 2 min (ou 3 min elite)
            cat.KumiteExtensionDurationSeconds = 120; // 2 min encho-sen
            cat.HasBronzeMatch = true; // Petite finale pour la 3e place en seniors
            cat.KataRound1MandatoryKata = "Gekisai Dai";
            cat.KataRound2MandatoryKata = "Saifa";
        }

        return cat;
    }
}
