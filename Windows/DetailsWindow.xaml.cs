using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfApp1.Controllers;
using WpfApp1.Models;
using static WpfApp1.Windows.MessageDialog;

namespace WpfApp1.Windows;

public partial class DetailsWindow : Window
{
    private readonly HardwareController _controller;
    private readonly int _hardwareId;
    private HardwareItem? _item;

    public DetailsWindow(HardwareController controller, int hardwareId)
    {
        InitializeComponent();
        _controller = controller;
        _hardwareId = hardwareId;
        LoadData();
    }

    private void LoadData()
    {
        _item = _controller.GetById(_hardwareId);
        if (_item == null)
        {
            ShowError("Оборудование не найдено");
            DialogResult = false;
            return;
        }

        TitleText.Text = $"Карточка: {_item.InventoryNumber}";
        BuildInfoPanel();

        var log = _controller.GetWorkLog(_hardwareId);
        WorkLogGrid.ItemsSource = log;
    }

    private void BuildInfoPanel()
    {
        if (_item == null) return;
        InfoPanel.Children.Clear();

        AddField("Инвентарный номер", _item.InventoryNumber);
        AddField("Тип устройства", _item.Type);
        AddField("Производитель", _item.Manufacturer);
        AddField("Модель", _item.Model);
        AddField("Серийный номер", _item.SerialNumber);
        AddField("Дата ввода в эксплуатацию", _item.CommissionDate.ToString("dd.MM.yyyy"));
        AddField("Стоимость", _item.Cost.ToString("N2"));
        AddField("Местоположение", _item.Location);
        AddField("Ответственный пользователь", _item.ResponsibleUser);
        AddField("Состояние", _item.Condition);
    }

    private void AddField(string label, string value)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = FindResource("TextSecondaryBrush") as Brush
        });
        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = value,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindResource("TextBrush") as Brush
        });
        InfoPanel.Children.Add(panel);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_item == null) return;
        var dialog = new AddEditWindow(_controller, _item);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
            LoadData();
    }

    private void AddWorkLog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WorkLogEntryWindow(_controller, _hardwareId);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            var log = _controller.GetWorkLog(_hardwareId);
            WorkLogGrid.ItemsSource = null;
            WorkLogGrid.ItemsSource = log;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Title_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}
