namespace PijacaUDzepu.API.Models;

public class DeliveryScheduleEntry
{
    public int Day { get; set; }
    public string From { get; set; } = "08:00";
    public string To { get; set; } = "18:00";
}
