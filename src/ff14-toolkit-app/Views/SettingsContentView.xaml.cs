using FF14Toolkit.App.Services.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace FF14Toolkit.App.Views;

public partial class SettingsContentView : UserControl
{
    private readonly HotkeyCaptureState? hotkeyCaptureState;

    public SettingsContentView()
    {
        InitializeComponent();
        hotkeyCaptureState = App.Services?.GetService<HotkeyCaptureState>();
    }

    private void OnHotKeyTextBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (!textBox.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            textBox.Focus();
        }
    }

    private void OnHotKeyTextBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        hotkeyCaptureState?.BeginCapture();
        UpdateCaptureIndicators(sender as TextBox);
    }

    private void OnHotKeyTextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            bool stillCapturing = ToggleOverlayHotKeyTextBox.IsKeyboardFocusWithin
                || ToggleOverlayEditHotKeyTextBox.IsKeyboardFocusWithin;

            if (stillCapturing)
            {
                return;
            }

            hotkeyCaptureState?.EndCapture();
            UpdateCaptureIndicators(null);
        }, DispatcherPriority.Input);
    }

    private void OnHotKeyTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Tab)
        {
            return;
        }

        e.Handled = true;

        if (key is Key.Back)
        {
            textBox.Text = string.Empty;
            return;
        }

        if (IsModifierKey(key))
        {
            return;
        }

        ModifierKeys modifiers = Keyboard.Modifiers;
        string hotKeyText = FormatHotKeyText(modifiers, key);
        if (string.IsNullOrWhiteSpace(hotKeyText))
        {
            return;
        }

        textBox.Text = hotKeyText;
        textBox.CaretIndex = textBox.Text.Length;
        Keyboard.ClearFocus();
    }

    private void OnHotKeyTextBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = true;
    }

    private static string FormatHotKeyText(ModifierKeys modifiers, Key key)
    {
        StringBuilder builder = new();

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            builder.Append("Ctrl+");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            builder.Append("Shift+");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            builder.Append("Alt+");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            builder.Append("Win+");
        }

        string keyText = GetKeyText(key);
        if (string.IsNullOrWhiteSpace(keyText))
        {
            return string.Empty;
        }

        builder.Append(keyText);
        return builder.ToString();
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl
            or Key.RightCtrl
            or Key.LeftShift
            or Key.RightShift
            or Key.LeftAlt
            or Key.RightAlt
            or Key.LWin
            or Key.RWin;
    }

    private static string GetKeyText(Key key)
    {
        return key switch
        {
            >= Key.D0 and <= Key.D9 => key.ToString()[1..],
            >= Key.NumPad0 and <= Key.NumPad9 => key.ToString(),
            >= Key.A and <= Key.Z => key.ToString(),
            >= Key.F1 and <= Key.F24 => key.ToString(),
            Key.Escape => "Escape",
            Key.Space => "Space",
            Key.Return => "Enter",
            Key.Prior => "PageUp",
            Key.Next => "PageDown",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            Key.Home => "Home",
            Key.End => "End",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.OemPlus => "OemPlus",
            Key.OemMinus => "OemMinus",
            Key.OemComma => "OemComma",
            Key.OemPeriod => "OemPeriod",
            Key.OemQuestion => "OemQuestion",
            Key.OemSemicolon => "OemSemicolon",
            Key.OemQuotes => "OemQuotes",
            Key.OemOpenBrackets => "OemOpenBrackets",
            Key.OemCloseBrackets => "OemCloseBrackets",
            Key.OemPipe => "OemPipe",
            Key.OemTilde => "OemTilde",
            Key.OemBackslash => "OemBackslash",
            Key.Multiply => "Multiply",
            Key.Add => "Add",
            Key.Subtract => "Subtract",
            Key.Decimal => "Decimal",
            Key.Divide => "Divide",
            _ => key.ToString()
        };
    }

    private void UpdateCaptureIndicators(TextBox? activeTextBox)
    {
        ToggleOverlayHotKeyCaptureStateTextBlock.Visibility =
            ReferenceEquals(activeTextBox, ToggleOverlayHotKeyTextBox)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Hidden;

        ToggleOverlayEditHotKeyCaptureStateTextBlock.Visibility =
            ReferenceEquals(activeTextBox, ToggleOverlayEditHotKeyTextBox)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Hidden;
    }
}
