using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfApp1.Controllers;
using WpfApp1.Database;
using WpfApp1.Models;
using WpfApp1.Windows;
using static WpfApp1.Windows.MessageDialog;

namespace WpfApp1;

public partial class MainWindow : Window
{
    private readonly HardwareController _controller = null!;
    private HardwareItem? _selected;
    private HardwareItem? _detailItem;
    private bool _isRefreshing;
    private int _editingWorkLogId = -1;

    private static readonly string[] Types = ["Все", "Компьютер", "Ноутбук", "Монитор", "Принтер", "МФУ", "Сетевое", "Сервер", "ИБП", "Другое"];
    private static readonly string[] Conditions = ["Все", "Отличное", "Хорошее", "Рабочее", "Неисправен", "Неудовлетворительное", "Ремонт", "Списано"];
    private static readonly string[] EventTypes = ["Профилактика", "Ремонт", "Замена комплектующего", "Диагностика", "Списание"];

    private bool _isResizing;
    private int _resizeEdge;
    private Point _resizeStartScreen;
    private Rect _resizeStartRect;
    private Rect _savedWindowRect;
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

    public MainWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            CreateResizeGrips();
            DlType.ItemsSource = EventTypes;
            DlType.SelectedIndex = 0;
            DlStatus.ItemsSource = new[] { "В работе", "Выполнено" };
            DlStatus.SelectedIndex = 0;
            _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); LoadData(); };
            DetailView.PreviewMouseDown += (s, me) =>
            {
                if (me.ChangedButton == MouseButton.XButton1)
                {
                    me.Handled = true;
                    ShowList();
                }
            };
            DetailView.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
            DetailView.Unloaded += (_, _) => DetailView.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
            HardwareGrid.PreviewMouseWheel += HardwareGrid_PreviewMouseWheel;
            UpdateMaximizeIcon();
        };

        try
        {
            var dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inventory.db");
            var db = new HardwareDatabase(dbPath);
            _controller = new HardwareController(db);
        }
        catch (Exception ex)
        {
            ShowError(
                $"Не удалось открыть базу данных:\n{ex.Message}\n\n" +
                "Убедитесь, что у программы есть права на запись в текущую папку.",
                "Ошибка инициализации");
            Application.Current.Shutdown();
            return;
        }

        FilterType.ItemsSource = Types;
        FilterType.SelectedIndex = 0;
        FilterCondition.ItemsSource = Conditions;
        FilterCondition.SelectedIndex = 0;

        PopulateLocationFilter();
        LoadData();
    }

    private void CreateResizeGrips()
    {
        var t = 6;
        void Grip(HorizontalAlignment ha, VerticalAlignment va, Cursor cursor, int edge, double? w = null, double? h = null)
        {
            var b = new Border
            {
                Width = w ?? t, Height = h ?? t,
                HorizontalAlignment = ha, VerticalAlignment = va,
                Background = System.Windows.Media.Brushes.Transparent,
                Cursor = cursor,
            };
            b.PreviewMouseLeftButtonDown += (_, e) =>
            {
                _resizeEdge = edge;
                _resizeStartScreen = PointToScreen(e.GetPosition(this));
                _resizeStartRect = new Rect(Left, Top, Width, Height);
                _isResizing = true;
                Mouse.Capture(b);
                e.Handled = true;
            };
            b.PreviewMouseMove += (_, e) =>
            {
                if (!_isResizing) return;
                var cur = PointToScreen(e.GetPosition(this));
                var dx = cur.X - _resizeStartScreen.X;
                var dy = cur.Y - _resizeStartScreen.Y;
                var r = _resizeStartRect;
                var nl = r.Left; var nt = r.Top; var nw = r.Width; var nh = r.Height;
                if ((edge & 1) != 0) { nl += dx; nw -= dx; }
                if ((edge & 2) != 0) { nw += dx; }
                if ((edge & 4) != 0) { nt += dy; nh -= dy; }
                if ((edge & 8) != 0) { nh += dy; }
                if (nw >= MinWidth) { Left = nl; Width = nw; }
                if (nh >= MinHeight) { Top = nt; Height = nh; }
                e.Handled = true;
            };
            b.PreviewMouseLeftButtonUp += (_, e) =>
            {
                _isResizing = false;
                Mouse.Capture(null);
                e.Handled = true;
            };
            ContentGrid.Children.Add(b);
        }
        Grip(HorizontalAlignment.Left, VerticalAlignment.Stretch, Cursors.SizeWE, 1);
        Grip(HorizontalAlignment.Right, VerticalAlignment.Stretch, Cursors.SizeWE, 2);
        Grip(HorizontalAlignment.Center, VerticalAlignment.Top, Cursors.SizeNS, 4);
        Grip(HorizontalAlignment.Center, VerticalAlignment.Bottom, Cursors.SizeNS, 8);
        Grip(HorizontalAlignment.Left, VerticalAlignment.Top, Cursors.SizeNWSE, 5);
        Grip(HorizontalAlignment.Right, VerticalAlignment.Top, Cursors.SizeNESW, 6);
        Grip(HorizontalAlignment.Left, VerticalAlignment.Bottom, Cursors.SizeNESW, 9);
        Grip(HorizontalAlignment.Right, VerticalAlignment.Bottom, Cursors.SizeNWSE, 10);
    }

    private void PopulateLocationFilter()
    {
        var locations = _controller.GetLocations();
        var list = new System.Collections.ObjectModel.ObservableCollection<string> { "Все" };
        foreach (var loc in locations)
            list.Add(loc);
        FilterLocation.ItemsSource = list;
        FilterLocation.SelectedIndex = 0;
    }

    private static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            e.Handled = true;
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 120 * 24);
        }
    }

    private void HardwareGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var sv = FindChild<ScrollViewer>(HardwareGrid);
        if (sv != null)
        {
            e.Handled = true;
            int rows = Math.Max(Math.Abs(e.Delta) / 60, 1);
            if (e.Delta > 0)
                for (int i = 0; i < rows; i++) sv.LineUp();
            else
                for (int i = 0; i < rows; i++) sv.LineDown();
        }
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var found = FindChild<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    private void LoadData()
    {
        try
        {
            var query = SearchBox.Text.Trim();
            var type = FilterType.SelectedItem?.ToString();
            var condition = FilterCondition.SelectedItem?.ToString();
            var location = FilterLocation.SelectedItem?.ToString();
            var items = _controller.Search(query, type, condition, location);
            HardwareGrid.ItemsSource = items;
            StatusText.Text = $"📋 Записей: {items.Count}";

            if (items.Count > 0)
            {
                var totalCost = items.Sum(i => i.Cost);
                StatusCost.Text = $"💰 {totalCost:N2} Br";
                StatusCost.Visibility = Visibility.Visible;

                var typeCount = items.Select(i => i.Type).Distinct().Count();
                StatusTypes.Text = $"📦 Типов: {typeCount}";
                StatusTypes.Visibility = Visibility.Visible;
            }
            else
            {
                StatusCost.Visibility = Visibility.Collapsed;
                StatusTypes.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка загрузки данных: {ex.Message}");
        }
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }
    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => LoadData();
    private void Filter_PreviewMouseWheel(object sender, MouseWheelEventArgs e) => e.Handled = true;

    private void Cost_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        foreach (char c in e.Text)
            if (!char.IsDigit(c) && c != ',' && c != '.') { e.Handled = true; return; }
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = HardwareGrid.SelectedItem as HardwareItem;
        EditBtn.IsEnabled = _selected != null;
        DeleteBtn.IsEnabled = _selected != null;
    }

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_selected != null)
            ShowDetail(_selected);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddEditWindow(_controller, null);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
            LoadData();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var dialog = new AddEditWindow(_controller, _selected);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
            LoadData();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var item = _selected;
        ShowConfirmAction(
            $"Вы уверены, что хотите удалить технику '{item.InventoryNumber}'?\nВся история ремонтов также будет удалена.",
            "Подтверждение удаления",
            () =>
            {
                var result = _controller.Delete(item.Id);
                if (result.success) { _selected = null; LoadData(); }
                return result;
            });
    }

    private void Reports_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ReportsWindow(_controller);
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        try
        {
            RefreshStatus.BeginAnimation(OpacityProperty, null);
            RefreshStatus.Foreground = FindResource("PrimaryBrush") as System.Windows.Media.Brush;
            RefreshStatus.Opacity = 1;
            RefreshStatus.Text = "Обновление...";
            LoadingSpinner.Visibility = Visibility.Visible;

            var spin = new DoubleAnimation
            {
                From = 0, To = 360, Duration = TimeSpan.FromSeconds(0.8),
                RepeatBehavior = RepeatBehavior.Forever
            };
            SpinnerRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, spin);

            await System.Threading.Tasks.Task.Delay(10);

            LoadData();

            LoadingSpinner.Visibility = Visibility.Collapsed;
            SpinnerRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);

            RefreshStatus.Text = "✅ Обновлено!";
            RefreshStatus.Foreground = FindResource("SuccessBrush") as System.Windows.Media.Brush;

            await System.Threading.Tasks.Task.Delay(1500);

            RefreshStatus.Visibility = Visibility.Collapsed;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    // ----- Detail view -----

    private void ShowDetail(HardwareItem item)
    {
        _detailItem = _controller.GetById(item.Id);
        if (_detailItem == null)
        {
            ShowError("Оборудование не найдено");
            return;
        }

        DetailTitle.Text = $"Карточка: {_detailItem.InventoryNumber}";
        BuildDetailInfoPanel();

        var log = _controller.GetWorkLog(_detailItem.Id);
        DetailWorkLogGrid.ItemsSource = log;
        DetailWorkLogGrid.Visibility = log.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        DetailWorkLogForm.Visibility = Visibility.Collapsed;
        ListView.Visibility = Visibility.Collapsed;
        DetailView.Visibility = Visibility.Visible;
        DetailView.ScrollToTop();
    }

    private void ShowList()
    {
        DetailView.Visibility = Visibility.Collapsed;
        ListView.Visibility = Visibility.Visible;
        _detailItem = null;
        LoadData();
    }

    private void BuildDetailInfoPanel()
    {
        if (_detailItem == null) return;
        DetailInfoPanel.Children.Clear();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var leftFields = new (string label, string value)[]
        {
            ("Инвентарный номер", _detailItem.InventoryNumber),
            ("Тип устройства", _detailItem.Type),
            ("Производитель", _detailItem.Manufacturer),
            ("Модель", _detailItem.Model),
            ("Серийный номер", _detailItem.SerialNumber),
        };

        var rightFields = new (string label, string value)[]
        {
            ("Дата ввода в эксплуатацию", _detailItem.CommissionDate.ToString("dd.MM.yyyy")),
            ("Стоимость", $"{_detailItem.Cost:N2} Br"),
            ("Местоположение", _detailItem.Location),
            ("Ответственный пользователь", _detailItem.ResponsibleUser),
            ("Состояние", _detailItem.Condition),
        };

        for (int i = 0; i < 5; i++)
        {
            AddDetailFieldToGrid(grid, leftFields[i].label, leftFields[i].value, 0, i);
            AddDetailFieldToGrid(grid, rightFields[i].label, rightFields[i].value, 2, i);
        }

        DetailInfoPanel.Children.Add(grid);
    }

    private static void AddDetailFieldToGrid(Grid grid, string label, string value, int col, int row)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = System.Windows.Application.Current.FindResource("TextSecondaryBrush") as System.Windows.Media.Brush
        });
        panel.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = System.Windows.Application.Current.FindResource("TextBrush") as System.Windows.Media.Brush
        });
        Grid.SetColumn(panel, col);
        Grid.SetRow(panel, row);
        grid.Children.Add(panel);
    }

    private void DetailBack_Click(object sender, RoutedEventArgs e) => ShowList();

    private void DetailEdit_Click(object sender, RoutedEventArgs e)
    {
        if (_detailItem == null) return;
        var dialog = new AddEditWindow(_controller, _detailItem);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
            ShowDetail(_detailItem);
    }

    private void DetailDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_detailItem == null) return;
        var item = _detailItem;
        ShowConfirmAction(
            $"Вы уверены, что хотите удалить технику '{item.InventoryNumber}'?\nВся история ремонтов также будет удалена.",
            "Подтверждение удаления",
            () =>
            {
                var result = _controller.Delete(item.Id);
                if (result.success) ShowList();
                return result;
            });
    }

    private void DetailAddWorkLog_Click(object sender, RoutedEventArgs e)
    {
        _editingWorkLogId = -1;
        DetailSaveWorkLogBtn.Content = "Сохранить";
        DlDate.SelectedDate = DateTime.Today;
        DlType.SelectedIndex = 0;
        DlStatus.SelectedIndex = 0;
        DlDescription.Text = "";
        DlCost.Text = "";
        DlAdditionalCost.Text = "";
        DlNotes.Text = "";
        DetailWorkLogForm.Visibility = DetailWorkLogForm.Visibility == Visibility.Visible
            ? Visibility.Collapsed : Visibility.Visible;
        if (DetailWorkLogForm.Visibility == Visibility.Visible)
        {
            Dispatcher.BeginInvoke(new Action(() => DetailView.ScrollToBottom()),
                System.Windows.Threading.DispatcherPriority.Render);
        }
    }

    private void DetailCancelWorkLog_Click(object sender, RoutedEventArgs e)
    {
        DetailWorkLogForm.Visibility = Visibility.Collapsed;
    }

    private void DetailEditWorkLog_Click(object sender, RoutedEventArgs e)
    {
        if (_detailItem == null) return;
        if (sender is not Button btn || btn.Tag is not WorkLogEntry entry) return;

        _editingWorkLogId = entry.Id;
        DetailSaveWorkLogBtn.Content = "Обновить";
        DlDate.SelectedDate = entry.EventDate;
        DlType.Text = entry.EventType;
        DlStatus.Text = entry.Status;
        DlDescription.Text = entry.Description;
        DlCost.Text = entry.Cost.ToString("F2");
        DlAdditionalCost.Text = entry.AdditionalCost > 0 ? entry.AdditionalCost.ToString("F2") : "";
        DlNotes.Text = entry.Notes;
        DetailWorkLogForm.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(new Action(() => DetailView.ScrollToBottom()),
            System.Windows.Threading.DispatcherPriority.Render);
    }

    private void DetailSaveWorkLog_Click(object sender, RoutedEventArgs e)
    {
        if (_detailItem == null) return;

        var entry = new WorkLogEntry
        {
            Id = _editingWorkLogId > 0 ? _editingWorkLogId : 0,
            HardwareId = _detailItem.Id,
            EventDate = DlDate.SelectedDate ?? DateTime.Today,
            EventType = DlType.Text,
            Description = DlDescription.Text.Trim(),
            Cost = decimal.TryParse(DlCost.Text.Trim(), out var c) ? c : 0,
            Status = DlStatus.Text,
            AdditionalCost = decimal.TryParse(DlAdditionalCost.Text.Trim(), out var ac) ? ac : 0,
            Notes = DlNotes.Text.Trim()
        };

        var (success, msg) = _editingWorkLogId > 0
            ? _controller.UpdateWorkLogEntry(entry)
            : _controller.AddWorkLogEntry(entry);

        if (success)
        {
            if (_editingWorkLogId < 0 && entry.EventType is "Ремонт" or "Замена комплектующего" or "Диагностика")
            {
                _controller.UpdateCondition(_detailItem.Id, "Ремонт");
                _detailItem.Condition = "Ремонт";
                BuildDetailInfoPanel();
            }
            ShowSuccess(msg);
            DetailWorkLogForm.Visibility = Visibility.Collapsed;
            _editingWorkLogId = -1;
            var log = _controller.GetWorkLog(_detailItem.Id);
            DetailWorkLogGrid.ItemsSource = null;
            DetailWorkLogGrid.ItemsSource = log;
            DetailWorkLogGrid.Visibility = log.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            ShowError(msg);
        }
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else if (WindowState == WindowState.Maximized)
            {
                var pt = e.GetPosition(sender as UIElement);
                var ratio = pt.X / ActualWidth;
                WindowState = WindowState.Normal;
                Left = Math.Max(0, Mouse.GetPosition(this).X - ratio * Width);
                Top = 0;
                DragMove();
            }
            else
            {
                DragMove();
            }
        }
    }

    private void MaximizeBtn_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            Left = _savedWindowRect.Left;
            Top = _savedWindowRect.Top;
            Width = _savedWindowRect.Width;
            Height = _savedWindowRect.Height;
        }
        else
        {
            _savedWindowRect = new Rect(Left, Top, Width, Height);
            WindowState = WindowState.Maximized;
        }
    }

    private void UpdateMaximizeIcon()
    {
        if (WindowState == WindowState.Maximized)
        {
            var canvas = new Canvas { Width = 14, Height = 14 };
            var back = new Rectangle { Width = 9, Height = 9, Stroke = Brushes.White, StrokeThickness = 1.5 };
            Canvas.SetLeft(back, 5); Canvas.SetTop(back, 0);
            canvas.Children.Add(back);
            var front = new Rectangle { Width = 9, Height = 9, Stroke = Brushes.White, StrokeThickness = 1.5 };
            Canvas.SetLeft(front, 0); Canvas.SetTop(front, 5);
            canvas.Children.Add(front);
            MaximizeBtn.Content = canvas;
        }
        else
        {
            var rect = new Rectangle { Width = 12, Height = 12, Stroke = Brushes.White, StrokeThickness = 1.5 };
            MaximizeBtn.Content = rect;
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Maximized)
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Left;
            Top = workArea.Top;
            Width = workArea.Width;
            Height = workArea.Height;
        }
        UpdateMaximizeIcon();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void RootGrid_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (SearchBox.IsFocused && !(e.OriginalSource is TextBox))
            Keyboard.ClearFocus();
    }
}
