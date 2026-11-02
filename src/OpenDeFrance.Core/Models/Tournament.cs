namespace OpenDeFrance.Core.Models;

public class Tournament
{
    public int Id { get; set; }
    public string Name { get; set; } = "Open de France de Karaté Kyokushinkai";
    public DateTime Date { get; set; } = DateTime.Today;
    public int DaysCount { get; set; } = 2; // Nombre de jours de la compétition (ex: Samedi et Dimanche)
    public string Location { get; set; } = "Paris, France";
    public int TatamisCount { get; set; } = 3;

    public List<Tatami> Tatamis { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
}
