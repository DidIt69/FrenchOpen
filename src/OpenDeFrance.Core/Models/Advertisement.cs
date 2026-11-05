namespace OpenDeFrance.Core.Models;

public class Advertisement
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public int DisplayDurationSeconds { get; set; } = 8;
    public int OrderIndex { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}
