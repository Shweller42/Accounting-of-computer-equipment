using System;
using System.Windows;
using System.Windows.Input;
using WpfApp1.Controllers;
using WpfApp1.Models;
using static WpfApp1.Windows.MessageDialog;

namespace WpfApp1.Windows;

public partial class WorkLogEntryWindow : Window
{
    private readonly HardwareController _controller;
    private readonly int _hardwareId;

    private static readonly string[] EventTypes = ["Профилактика", "Ремонт", "Замена комплектующего", "Диагностика", "Списание"];

    public WorkLogEntryWindow(HardwareController controller, int hardwareId)
    {
        InitializeComponent();
        CbType.ItemsSource = EventTypes;
        CbType.SelectedIndex = 0;
        _controller = controller;
        _hardwareId = hardwareId;
        DpDate.SelectedDate = DateTime.Today;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var entry = new WorkLogEntry
        {
            HardwareId = _hardwareId,
            EventDate = DpDate.SelectedDate ?? DateTime.Today,
            EventType = CbType.Text,
            Description = TbDescription.Text.Trim(),
            Cost = decimal.TryParse(TbCost.Text.Trim(), out var c) ? c : 0
        };

        var (success, msg) = _controller.AddWorkLogEntry(entry);
        if (success) ShowSuccess(msg); else ShowError(msg);

        if (success)
            DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Title_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}
