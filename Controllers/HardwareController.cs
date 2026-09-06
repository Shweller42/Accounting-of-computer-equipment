using System;
using System.Collections.Generic;
using System.Linq;
using WpfApp1.Database;
using WpfApp1.Models;

namespace WpfApp1.Controllers;

public class HardwareController
{
    private readonly HardwareDatabase _db;

    public HardwareController(HardwareDatabase db)
    {
        _db = db;
    }

    public List<HardwareItem> LoadAll() => _db.GetAll();

    public List<string> GetLocations() => _db.GetLocations();

    public List<HardwareItem> Search(string? query, string? type, string? condition, string? location = null) =>
        _db.Search(query, type, condition, location);

    public HardwareItem? GetById(int id) => _db.GetById(id);

    public (bool success, string message) Add(HardwareItem item)
    {
        var error = Validate(item);
        if (error != null)
            return (false, error);

        try
        {
            var id = _db.Add(item);
            item.Id = id;
            return (true, "Оборудование добавлено");
        }
        catch (Exception ex)
        {
            return (false, ex.Message.Contains("UNIQUE")
                ? "Ошибка: техника с таким инвентарным номером уже существует"
                : $"Ошибка БД: {ex.Message}");
        }
    }

    public (bool success, string message) Update(HardwareItem item)
    {
        var error = Validate(item);
        if (error != null)
            return (false, error);

        try
        {
            _db.Update(item);
            return (true, "Оборудование обновлено");
        }
        catch (Exception ex)
        {
            return (false, $"Ошибка БД: {ex.Message}");
        }
    }

    public (bool success, string message) Delete(int id)
    {
        try
        {
            _db.Delete(id);
            return (true, "Оборудование удалено");
        }
        catch (Exception ex)
        {
            return (false, $"Ошибка при удалении: {ex.Message}");
        }
    }

    public List<WorkLogEntry> GetWorkLog(int hardwareId) => _db.GetWorkLog(hardwareId);

    public (bool success, string message) AddWorkLogEntry(WorkLogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.EventType))
            return (false, "Выберите тип события");
        if (string.IsNullOrWhiteSpace(entry.Description))
            return (false, "Введите описание события");
        if (entry.Cost < 0)
            return (false, "Стоимость не может быть отрицательной");

        try
        {
            _db.AddWorkLogEntry(entry);
            return (true, "Запись добавлена");
        }
        catch (Exception ex)
        {
            return (false, $"Ошибка БД: {ex.Message}");
        }
    }

    public (bool success, string message) UpdateWorkLogEntry(WorkLogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.EventType))
            return (false, "Выберите тип события");
        if (string.IsNullOrWhiteSpace(entry.Description))
            return (false, "Введите описание события");

        try
        {
            _db.UpdateWorkLogEntry(entry);
            return (true, "Запись обновлена");
        }
        catch (Exception ex)
        {
            return (false, $"Ошибка БД: {ex.Message}");
        }
    }

    public (int totalCount, decimal totalCost, Dictionary<string, int> byType, Dictionary<string, int> byCondition) GetStats() =>
        _db.GetStats();

    public decimal GetRepairCostByYear(int year) => _db.GetRepairCostByYear(year);

    public List<HardwareItem> GetOldEquipment(int years) => _db.GetOldEquipment(years);

    public void UpdateCondition(int hardwareId, string condition) => _db.UpdateCondition(hardwareId, condition);

    private static string? Validate(HardwareItem item)
    {
        if (string.IsNullOrWhiteSpace(item.InventoryNumber))
            return "Поле 'Инвентарный номер' обязательно для заполнения";
        if (string.IsNullOrWhiteSpace(item.Type))
            return "Поле 'Тип устройства' обязательно для заполнения";
        if (item.CommissionDate > DateTime.Today)
            return "Дата ввода в эксплуатацию не может быть в будущем";
        if (item.Cost < 0)
            return "Стоимость не может быть отрицательной";
        return null;
    }
}
