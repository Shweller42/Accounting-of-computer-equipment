using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfApp1.Windows;

public partial class MessageDialog : Window
{
    public enum DialogType
    {
        Info,
        Warning,
        Error,
        Success,
        Question
    }

    public MessageDialog(string message, string title = "Сообщение",
        DialogType type = DialogType.Info, bool showCancel = false)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;

        CancelBtn.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
        CloseButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;

        _ = type switch
        {
            DialogType.Warning => TitleText.Text = "⚠️ " + title,
            DialogType.Error => TitleText.Text = "❌ " + title,
            DialogType.Success => TitleText.Text = "✅ " + title,
            DialogType.Question => TitleText.Text = "❓ " + title,
            _ => TitleText.Text = "ℹ️ " + title
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Title_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    public static bool? Show(string message, string title = "Сообщение",
        DialogType type = DialogType.Info, bool showCancel = false)
    {
        var dialog = new MessageDialog(message, title, type, showCancel);
        dialog.Owner = Application.Current.Windows.OfType<Window>()
            .LastOrDefault(w => w != dialog && w.IsVisible);
        return dialog.ShowDialog();
    }

    public static void ShowInfo(string message, string title = "Информация")
        => Show(message, title, DialogType.Info);

    public static void ShowWarning(string message, string title = "Предупреждение")
        => Show(message, title, DialogType.Warning);

    public static void ShowError(string message, string title = "Ошибка")
        => Show(message, title, DialogType.Error);

    public static void ShowSuccess(string message, string title = "Успех")
        => Show(message, title, DialogType.Success);

    public static bool? ShowQuestion(string message, string title = "Подтверждение")
        => Show(message, title, DialogType.Question, showCancel: true);

    public static bool? ShowConfirmAction(string message, string title,
        Func<(bool success, string message)> action)
    {
        var dialog = new MessageDialog(message, title, DialogType.Question, showCancel: true);

        dialog.OkBtn.Click -= dialog.Ok_Click;
        RoutedEventHandler firstClick = null!;
        firstClick = (s, e) =>
        {
            dialog.OkBtn.Click -= firstClick;
            dialog.OkBtn.IsEnabled = false;
            dialog.CancelBtn.Visibility = Visibility.Collapsed;
            dialog.CloseButton.Visibility = Visibility.Collapsed;
            dialog.MessageText.Text = "⏳ Выполнение...";

            var result = action();

            dialog.OkBtn.Content = "Закрыть";
            dialog.OkBtn.IsEnabled = true;
            dialog.MessageText.Text = result.message;
            dialog.TitleText.Text = (result.success ? "✅ " : "❌ ") + title;

            RoutedEventHandler closeHandler = null!;
            closeHandler = (_, _) =>
            {
                dialog.OkBtn.Click -= closeHandler;
                dialog.DialogResult = result.success;
            };
            dialog.OkBtn.Click += closeHandler;
        };
        dialog.OkBtn.Click += firstClick;

        dialog.Owner = Application.Current.Windows.OfType<Window>()
            .LastOrDefault(w => w != dialog && w.IsVisible);
        return dialog.ShowDialog();
    }
}
