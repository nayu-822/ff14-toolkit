using FF14Toolkit.App.Services.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    }

    private void OnHotKeyTextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (sender is TextBox textBox && textBox.IsKeyboardFocusWithin)
            {
                return;
            }

            hotkeyCaptureState?.EndCapture();
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

        if (HotkeyTextUtility.IsModifierKey(key))
        {
            return;
        }

        ModifierKeys modifiers = Keyboard.Modifiers;
        string hotKeyText = HotkeyTextUtility.FormatHotKeyText(modifiers, key);
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

    private void OnRootPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dependencyObject && FindAncestor<TextBox>(dependencyObject) is not null)
        {
            return;
        }

        Keyboard.ClearFocus();
    }

    private static T? FindAncestor<T>(DependencyObject? dependencyObject)
        where T : DependencyObject
    {
        while (dependencyObject is not null)
        {
            if (dependencyObject is T typedObject)
            {
                return typedObject;
            }

            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        return null;
    }
}
