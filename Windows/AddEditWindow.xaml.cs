using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp1.Controllers;
using WpfApp1.Models;
using static WpfApp1.Windows.MessageDialog;

namespace WpfApp1.Windows;

public partial class AddEditWindow : Window
{
    private readonly HardwareController _controller;
    private readonly HardwareItem? _existing;
private static readonly string[] Types = ["Компьютер", "Ноутбук", "Монитор", "Принтер", "МФУ", "Сетевое", "Сервер", "ИБП", "Другое"];

    private static readonly string[] Conditions = ["Отличное", "Хорошее", "Рабочее", "Неисправен", "Неудовлетворительное", "Ремонт", "Списано"];

    private static readonly Dictionary<string, string[]> ManufacturersByType = new()
    {
        ["Компьютер"] = ["HP", "Dell", "Lenovo", "Acer", "Asus", "MSI", "Apple", "Custom"],
        ["Ноутбук"] = ["HP", "Dell", "Lenovo", "Acer", "Asus", "MSI", "Apple", "Samsung", "Huawei"],
        ["Монитор"] = ["Samsung", "LG", "Dell", "HP", "BenQ", "AOC", "Philips", "ViewSonic"],
        ["Принтер"] = ["HP", "Canon", "Epson", "Brother", "Kyocera", "Xerox"],
        ["МФУ"] = ["HP", "Canon", "Epson", "Brother", "Kyocera", "Xerox", "Pantum"],
        ["Сетевое"] = ["Cisco", "MikroTik", "TP-Link", "D-Link", "Ubiquiti", "Huawei"],
        ["Сервер"] = ["Dell", "HP", "Lenovo", "Supermicro", "Fujitsu"],
        ["ИБП"] = ["APC", "Eaton", "Powercom", "CyberPower", "Ippon"],
        ["Другое"] = []
    };

    public AddEditWindow(HardwareController controller, HardwareItem? existing)
    {
        InitializeComponent();
        CbType.ItemsSource = Types;
        CbType.SelectedIndex = 0;
        CbCondition.ItemsSource = Conditions;
        CbCondition.SelectedIndex = 0;
        CbCondition.IsEditable = true;
        _controller = controller;
        _existing = existing;

        UpdateManufacturerList(0);

        if (existing != null)
        {
            TitleText.Text = "Редактирование техники";
            TbInventory.Text = existing.InventoryNumber;
            CbType.Text = existing.Type;
            CbManufacturer.Text = existing.Manufacturer;
            TbModel.Text = existing.Model;
            TbSerial.Text = existing.SerialNumber;
            DpDate.SelectedDate = existing.CommissionDate;
            TbCost.Text = existing.Cost.ToString("F2");
            TbLocation.Text = existing.Location;
            TbResponsible.Text = existing.ResponsibleUser;
            CbCondition.Text = existing.Condition;
        }
        else
        {
            DpDate.SelectedDate = DateTime.Today;
        }

        AddEditScrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
    }

    private static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            e.Handled = true;
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 120 * 24);
        }
    }

    private void Type_Changed(object sender, SelectionChangedEventArgs e)
    {
        var idx = CbType.SelectedIndex;
        if (idx < 0) return;

        UpdateManufacturerList(idx);
    }

    private void UpdateManufacturerList(int typeIndex)
    {
        if (typeIndex < 0 || typeIndex >= Types.Length) return;
        var typeName = Types[typeIndex];
        if (ManufacturersByType.TryGetValue(typeName, out var list))
        {
            CbManufacturer.ItemsSource = list;
            if (list.Length > 0 && string.IsNullOrEmpty(CbManufacturer.Text))
                CbManufacturer.SelectedIndex = 0;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(TbCost.Text.Trim(), out var cost) || cost < 0)
        {
            ShowWarning("Стоимость должна быть положительным числом");
            return;
        }

        if (string.IsNullOrWhiteSpace(CbType.Text))
        {
            ShowWarning("Укажите тип устройства");
            return;
        }

        var item = new HardwareItem
        {
            Id = _existing?.Id ?? 0,
            InventoryNumber = TbInventory.Text.Trim(),
            Type = CbType.Text.Trim(),
            Manufacturer = CbManufacturer.Text.Trim(),
            Model = TbModel.Text.Trim(),
            SerialNumber = TbSerial.Text.Trim(),
            CommissionDate = DpDate.SelectedDate ?? DateTime.Today,
            Cost = cost,
            Location = TbLocation.Text.Trim(),
            ResponsibleUser = TbResponsible.Text.Trim(),
            Condition = CbCondition.Text.Trim()
        };

        var (success, msg) = _existing == null
            ? _controller.Add(item)
            : _controller.Update(item);

        if (success) ShowSuccess(msg); else ShowError(msg);

        if (success)
            DialogResult = true;
    }

    private void ComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
    }

    private void TbCost_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var textBox = (TextBox)sender;
        var newText = textBox.Text.Substring(0, textBox.SelectionStart) + e.Text +
                      textBox.Text.Substring(textBox.SelectionStart + textBox.SelectionLength);
        e.Handled = !decimal.TryParse(newText, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out _);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Title_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}
