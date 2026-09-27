using UnityEngine;
using UnityEngine.InputSystem;

namespace EnhancedStorageBackpack;

internal sealed class BackpackInput
{
    private KeyCode last = KeyCode.None;
    private Key mapped = Key.None;
    public bool Pressed(KeyCode code)
    {
        if (code != last)
        {
            last = code;
            string name = code.ToString();
            if (name.StartsWith("Alpha", StringComparison.Ordinal)) name = "Digit" + name.Substring(5);
            if (name.StartsWith("Keypad", StringComparison.Ordinal)) name = "Numpad" + name.Substring(6);
            name = name switch
            {
                "Return" => "Enter", "LeftControl" => "LeftCtrl", "RightControl" => "RightCtrl",
                "BackQuote" => "Backquote", "Numlock" => "NumLock", "Print" => "PrintScreen",
                _ => name
            };
            mapped = Enum.TryParse<Key>(name, true, out var key) ? key : Key.None;
        }
        if (code >= KeyCode.Mouse0 && code <= KeyCode.Mouse4 && Mouse.current != null)
            return code switch
            {
                KeyCode.Mouse0 => Mouse.current.leftButton.wasPressedThisFrame,
                KeyCode.Mouse1 => Mouse.current.rightButton.wasPressedThisFrame,
                KeyCode.Mouse2 => Mouse.current.middleButton.wasPressedThisFrame,
                KeyCode.Mouse3 => Mouse.current.backButton.wasPressedThisFrame,
                _ => Mouse.current.forwardButton.wasPressedThisFrame
            };
        return mapped != Key.None && Keyboard.current != null && Keyboard.current[mapped].wasPressedThisFrame;
    }
}
