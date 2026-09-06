using System;

namespace WpfApp1.Models;

public class HardwareItem
{
    public int Id { get; set; }
    public string InventoryNumber { get; set; } = "";
    public string Type { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public DateTime CommissionDate { get; set; } = DateTime.Today;
    public decimal Cost { get; set; }
    public string Location { get; set; } = "";
    public string ResponsibleUser { get; set; } = "";
    public string Condition { get; set; } = "Рабочее";
}
