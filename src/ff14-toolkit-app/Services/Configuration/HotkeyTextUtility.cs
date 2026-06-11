using System.Text;
using System.Windows.Input;

namespace FF14Toolkit.App.Services.Configuration;

public static class HotkeyTextUtility
{
    public static string FormatHotKeyText(ModifierKeys modifiers, Key key)
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

    public static bool IsModifierKey(Key key)
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

    public static bool TryParseHotKey(string hotKeyText, out uint modifiers, out uint virtualKey)
    {
        const uint modifierAlt = 0x0001;
        const uint modifierControl = 0x0002;
        const uint modifierShift = 0x0004;
        const uint modifierWin = 0x0008;

        modifiers = 0;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(hotKeyText))
        {
            return false;
        }

        string[] parts = hotKeyText
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < parts.Length - 1; index++)
        {
            switch (parts[index].ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= modifierControl;
                    break;
                case "shift":
                    modifiers |= modifierShift;
                    break;
                case "alt":
                    modifiers |= modifierAlt;
                    break;
                case "win":
                case "windows":
                    modifiers |= modifierWin;
                    break;
                default:
                    return false;
            }
        }

        string keyToken = NormalizeKeyToken(parts[^1]);
        if (!Enum.TryParse(keyToken, true, out Key key))
        {
            return false;
        }

        int keyCode = KeyInterop.VirtualKeyFromKey(key);
        if (keyCode <= 0)
        {
            return false;
        }

        virtualKey = (uint)keyCode;
        return true;
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

    private static string NormalizeKeyToken(string keyToken)
    {
        string normalized = keyToken.Trim().ToUpperInvariant();
        return normalized switch
        {
            "0" => nameof(Key.D0),
            "1" => nameof(Key.D1),
            "2" => nameof(Key.D2),
            "3" => nameof(Key.D3),
            "4" => nameof(Key.D4),
            "5" => nameof(Key.D5),
            "6" => nameof(Key.D6),
            "7" => nameof(Key.D7),
            "8" => nameof(Key.D8),
            "9" => nameof(Key.D9),
            "ESC" => nameof(Key.Escape),
            "ENTER" => nameof(Key.Return),
            "DEL" => nameof(Key.Delete),
            "INS" => nameof(Key.Insert),
            "PGUP" => nameof(Key.Prior),
            "PAGEUP" => nameof(Key.Prior),
            "PGDN" => nameof(Key.Next),
            "PAGEDOWN" => nameof(Key.Next),
            "LEFT" => nameof(Key.Left),
            "RIGHT" => nameof(Key.Right),
            "UP" => nameof(Key.Up),
            "DOWN" => nameof(Key.Down),
            "SPACE" => nameof(Key.Space),
            _ => keyToken.Trim()
        };
    }
}
