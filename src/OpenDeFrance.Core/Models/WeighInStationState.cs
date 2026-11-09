using OpenDeFrance.Core.Enums;

namespace OpenDeFrance.Core.Models;

public class WeighInStationState
{
    public int StationId { get; set; } = 1;
    public string StationName { get; set; } = "Poste 1";
    public Competitor? Competitor { get; set; }
    public double? WeighedWeight { get; set; }
    public WeighInStatus Status { get; set; } = WeighInStatus.Idle;
    public string? StatusMessage { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.Now;
    public bool IsWeightValid { get; set; } = true;
    public double? WeightDifference { get; set; }
    public Category? KumiteCategory { get; set; }
    public Category? KataCategory { get; set; }

    public bool IsIdle => Status == WeighInStatus.Idle;
    public bool IsChecking => Status == WeighInStatus.Checking;
    public bool IsValidated => Status == WeighInStatus.Validated;
    public bool IsRejected => Status == WeighInStatus.Rejected;
}
