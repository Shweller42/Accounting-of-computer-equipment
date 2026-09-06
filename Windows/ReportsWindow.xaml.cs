using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using WpfApp1.Controllers;
using WpfApp1.Models;

namespace WpfApp1.Windows;

public partial class ReportsWindow : Window
{
    private readonly HardwareController _controller;
    private List<HardwareItem>? _filteredCache;

    private static readonly string[] FilterTypes = ["Все", "Компьютер", "Ноутбук", "Монитор", "Принтер", "МФУ", "Сетевое", "Сервер", "ИБП", "Другое"];
    private static readonly string[] FilterConditions = ["Все", "Отличное", "Хорошее", "Рабочее", "Неисправен", "Неудовлетворительное", "Ремонт", "Списано"];

    public ReportsWindow(HardwareController controller)
    {
        InitializeComponent();
        _controller = controller;

        FilterType.ItemsSource = FilterTypes;
        FilterType.SelectedIndex = 0;
        FilterCondition.ItemsSource = FilterConditions;
        FilterCondition.SelectedIndex = 0;

        var locations = _controller.GetLocations();
        var locList = new List<string> { "Все" };
        locList.AddRange(locations);
        FilterLocation.ItemsSource = locList;
        FilterLocation.SelectedIndex = 0;

        TbYear.Text = DateTime.Today.Year.ToString();

        LoadStats();
        ReportsScrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
    }

    private static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is System.Windows.Controls.ScrollViewer sv)
        {
            e.Handled = true;
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 120 * 24);
        }
    }

    private void LoadStats()
    {
        var (totalCount, totalCost, byType, byCondition) = _controller.GetStats();

        TotalCountText.Text = totalCount.ToString();
        TotalCostText.Text = totalCost.ToString("N2");

        ByTypeList.ItemsSource = byType.Select(kv => $"{kv.Key}: {kv.Value} шт.").ToList();
        ByConditionList.ItemsSource = byCondition.Select(kv => $"{kv.Key}: {kv.Value} шт.").ToList();
    }

    private void ShowRepairCost_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TbYear.Text, out int year) && year > 1900 && year < 2200)
        {
            var cost = _controller.GetRepairCostByYear(year);
            RepairCostResult.Text = $"{cost:N2} Br";
        }
        else
        {
            RepairCostResult.Text = "—";
        }
    }

    private void TbYear_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        foreach (char c in e.Text)
            if (!char.IsDigit(c)) { e.Handled = true; return; }
    }

    private void Filter_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        foreach (char c in e.Text)
            if (!char.IsDigit(c)) { e.Handled = true; return; }
    }

    private void GenerateFiltered_Click(object sender, RoutedEventArgs e)
    {
        var type = FilterType.SelectedItem?.ToString();
        var condition = FilterCondition.SelectedItem?.ToString();
        var location = FilterLocation.SelectedItem?.ToString();

        var items = _controller.Search(null, type, condition, location);

        if (FilterAgeCheck.IsChecked == true && int.TryParse(FilterAgeValue.Text, out int years) && years > 0)
        {
            var threshold = DateTime.Today.AddYears(-years);
            items = items.Where(i => i.CommissionDate <= threshold).ToList();
        }

        _filteredCache = items;

        FilteredList.ItemsSource = _filteredCache.Select(i =>
            $"• {i.InventoryNumber} — {i.Type} {i.Manufacturer} {i.Model} — {i.Location} ({i.CommissionDate:dd.MM.yyyy})").ToList();

        ExportBtn.IsEnabled = _filteredCache.Count > 0;
    }

    private void ExportFiltered_Click(object sender, RoutedEventArgs e)
    {
        if (_filteredCache == null || _filteredCache.Count == 0) return;

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var filePath = Path.Combine(desktop, $"Отчет_по_фильтру_{DateTime.Today:yyyyMMdd}.csv");

        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));

        writer.WriteLine("Инв.номер;Тип;Производитель;Модель;Серийный номер;Дата ввода;Стоимость;Местоположение;Ответственный;Состояние");
        foreach (var item in _filteredCache)
        {
            writer.WriteLine(
                $"{EscapeCsv(item.InventoryNumber)};{EscapeCsv(item.Type)};{EscapeCsv(item.Manufacturer)};{EscapeCsv(item.Model)};" +
                $"{EscapeCsv(item.SerialNumber)};\"{item.CommissionDate:dd.MM.yyyy}\";{item.Cost:F2};" +
                $"{EscapeCsv(item.Location)};{EscapeCsv(item.ResponsibleUser)};{EscapeCsv(item.Condition)}");
        }

        writer.Flush();

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            MessageBox.Show($"Файл сохранён:\n{filePath}", "Excel", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private static string EscapeCsv(string value) =>
        value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Title_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}
