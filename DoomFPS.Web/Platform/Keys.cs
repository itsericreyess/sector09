namespace DoomFPS.Web.Platform;

// Mismos nombres que System.Windows.Forms.Keys para que la lógica de la
// ventana se conserve. Los rangos D0..D9 y NumPad0..NumPad9 son contiguos.
public enum Keys
{
    None,
    Enter, Escape, Space, Tab, Back, Delete,
    Up, Down, Left, Right, PageUp, PageDown, Home,
    ShiftKey, ControlKey,
    A, D, E, F, M, Q, R, S, W,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    NumPad0, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
    F1, F2, F3, F4, F5, F6, F7
}

public static class KeyMap
{
    // KeyboardEvent.code → Keys. Se usa "code" (posición física) para que
    // WASD funcione igual con teclado español, inglés o AZERTY.
    public static Keys FromCode(string code)
    {
        switch (code)
        {
            case "Enter": case "NumpadEnter": return Keys.Enter;
            case "Escape": return Keys.Escape;
            case "Space": return Keys.Space;
            case "Tab": return Keys.Tab;
            case "Backspace": return Keys.Back;
            case "Delete": return Keys.Delete;
            case "ArrowUp": return Keys.Up;
            case "ArrowDown": return Keys.Down;
            case "ArrowLeft": return Keys.Left;
            case "ArrowRight": return Keys.Right;
            case "PageUp": return Keys.PageUp;
            case "PageDown": return Keys.PageDown;
            case "Home": return Keys.Home;
            case "ShiftLeft": case "ShiftRight": return Keys.ShiftKey;
            case "ControlLeft": case "ControlRight": return Keys.ControlKey;
            case "KeyA": return Keys.A;
            case "KeyD": return Keys.D;
            case "KeyE": return Keys.E;
            case "KeyF": return Keys.F;
            case "KeyM": return Keys.M;
            case "KeyQ": return Keys.Q;
            case "KeyR": return Keys.R;
            case "KeyS": return Keys.S;
            case "KeyW": return Keys.W;
        }
        if (code.Length == 6 && code.StartsWith("Digit") && char.IsDigit(code[5])) return Keys.D0 + (code[5] - '0');
        if (code.Length == 7 && code.StartsWith("Numpad") && char.IsDigit(code[6])) return Keys.NumPad0 + (code[6] - '0');
        if (code.Length == 2 && code[0] == 'F' && code[1] is >= '1' and <= '7') return Keys.F1 + (code[1] - '1');
        return Keys.None;
    }
}
