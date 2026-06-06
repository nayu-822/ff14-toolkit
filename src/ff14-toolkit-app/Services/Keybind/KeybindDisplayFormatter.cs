using FF14Toolkit.App.Models.Keybind;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace FF14Toolkit.App.Services.Keybind;

public static class KeybindDisplayFormatter
{
    private static readonly Dictionary<string, string> SpecialKeyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1B"] = "Esc",
        ["20"] = "Space",
        ["21"] = "PageUp",
        ["22"] = "PageDown",
        ["24"] = "Home",
        ["26"] = "Up",
        ["28"] = "Down",
        ["30"] = "0",
        ["31"] = "1",
        ["32"] = "2",
        ["33"] = "3",
        ["34"] = "4",
        ["35"] = "5",
        ["36"] = "6",
        ["37"] = "7",
        ["38"] = "8",
        ["39"] = "9",
        ["45"] = "E",
        ["46"] = "F",
        ["49"] = "I",
        ["51"] = "Q",
        ["52"] = "R",
        ["54"] = "T",
        ["55"] = "U",
        ["57"] = "W",
        ["59"] = "Y",
        ["60"] = "Num0",
        ["62"] = "Num2",
        ["64"] = "Num4",
        ["66"] = "Num6",
        ["67"] = "Num7",
        ["68"] = "Num8",
        ["69"] = "Num9",
        ["6A"] = "Num*",
        ["6E"] = "Num.",
        ["70"] = "F1",
        ["71"] = "F2",
        ["72"] = "F3",
        ["73"] = "F4",
        ["74"] = "F5",
        ["75"] = "F6",
        ["76"] = "F7",
        ["77"] = "F8",
        ["78"] = "F9",
        ["79"] = "F10",
        ["7A"] = "F11",
        ["7B"] = "F12",
        ["81"] = "ScrollLock",
        ["84"] = "-",
        ["8C"] = "#"
    };

    public static string Format(KeybindAssignment assignment)
    {
        if (assignment is null || !assignment.IsAssigned)
        {
            return string.Empty;
        }

        List<string> parts = [];
        KeybindModifierFlags modifierFlags = ParseModifierFlags(assignment.ModifierCode);

        if (modifierFlags.HasFlag(KeybindModifierFlags.Ctrl))
        {
            parts.Add("Ctrl");
        }

        if (modifierFlags.HasFlag(KeybindModifierFlags.Alt))
        {
            parts.Add("Alt");
        }

        if (modifierFlags.HasFlag(KeybindModifierFlags.Shift))
        {
            parts.Add("Shift");
        }

        parts.Add(GetKeyName(assignment.KeyCode));
        return string.Join("+", parts);
    }

    private static KeybindModifierFlags ParseModifierFlags(string modifierCode)
    {
        if (!byte.TryParse(modifierCode, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value))
        {
            return KeybindModifierFlags.None;
        }

        return (KeybindModifierFlags)value;
    }

    private static string GetKeyName(string keyCode)
    {
        string normalized = keyCode.ToUpperInvariant();
        return SpecialKeyNames.TryGetValue(normalized, out string? displayName)
            ? displayName
            : normalized;
    }
}
