using System;

namespace WpfApp1.Models;

public class WorkLogEntry
{
    public int Id { get; set; }
    public int HardwareId { get; set; }
    public DateTime EventDate { get; set; } = DateTime.Today;
    public string EventType { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Cost { get; set; }
    public string Status { get; set; } = "В работе";
    public string Notes { get; set; } = "";
    public decimal AdditionalCost { get; set; }

    public decimal TotalCost => Cost + AdditionalCost;
}
