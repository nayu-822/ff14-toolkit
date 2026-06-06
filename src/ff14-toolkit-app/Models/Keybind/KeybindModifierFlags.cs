using System;

namespace FF14Toolkit.App.Models.Keybind;

[Flags]
public enum KeybindModifierFlags : byte
{
    None = 0,
    Shift = 1,
    Ctrl = 2,
    Alt = 4
}
