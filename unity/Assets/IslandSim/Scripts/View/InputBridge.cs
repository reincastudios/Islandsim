using UnityEngine;
#if ENABLE_INPUT_SYSTEM && ISIM_INPUTSYS
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace IslandSim.View
{
    /// <summary>
    /// Keyboard, mouse and touch, read through Unity's built-in Input Manager by default, or the Input System package when
    /// the project defines ISIM_INPUTSYS. Positions are in pixels from the top-left corner (as in the browser), and the
    /// wheel reads like the browser's deltaY (positive = toward you = zoom out).
    /// </summary>
    public static class InputBridge
    {
        public enum Sp { Esc, Tab, F1, Up, Down, Left, Right, Shift, Ctrl, Alt, Enter, Backspace }

#if ENABLE_INPUT_SYSTEM && ISIM_INPUTSYS
        static KeyControl K(char c)
        {
            var kb = Keyboard.current; if (kb == null) return null;
            if (c >= 'a' && c <= 'z') return kb[(Key)System.Enum.Parse(typeof(Key), char.ToUpperInvariant(c).ToString())];
            if (c >= '0' && c <= '9') return kb[(Key)System.Enum.Parse(typeof(Key), "Digit" + c)];
            return c == '[' ? kb[Key.LeftBracket] : c == ']' ? kb[Key.RightBracket] : c == ' ' ? kb[Key.Space] : null;
        }
        static KeyControl K(Sp s)
        {
            var kb = Keyboard.current; if (kb == null) return null;
            switch (s)
            {
                case Sp.Esc: return kb[Key.Escape]; case Sp.Tab: return kb[Key.Tab]; case Sp.F1: return kb[Key.F1];
                case Sp.Up: return kb[Key.UpArrow]; case Sp.Down: return kb[Key.DownArrow]; case Sp.Left: return kb[Key.LeftArrow]; case Sp.Right: return kb[Key.RightArrow];
                case Sp.Shift: return kb[Key.LeftShift]; case Sp.Ctrl: return kb[Key.LeftCtrl]; case Sp.Alt: return kb[Key.LeftAlt];
                case Sp.Enter: return kb[Key.Enter]; default: return kb[Key.Backspace];
            }
        }
        public static bool Held(char c) { var k = K(c); return k != null && k.isPressed; }
        public static bool Pressed(char c) { var k = K(c); return k != null && k.wasPressedThisFrame; }
        public static bool Held(Sp s)
        {
            var kb = Keyboard.current; if (kb == null) return false;
            if (s == Sp.Shift) return kb[Key.LeftShift].isPressed || kb[Key.RightShift].isPressed;
            if (s == Sp.Ctrl) return kb[Key.LeftCtrl].isPressed || kb[Key.RightCtrl].isPressed || kb[Key.LeftCommand].isPressed || kb[Key.RightCommand].isPressed;
            if (s == Sp.Alt) return kb[Key.LeftAlt].isPressed || kb[Key.RightAlt].isPressed;
            var k = K(s); return k != null && k.isPressed;
        }
        public static bool Pressed(Sp s) { var k = K(s); return k != null && k.wasPressedThisFrame; }
        static ButtonControl B(int b) { var m = Mouse.current; return m == null ? null : b == 0 ? m.leftButton : b == 1 ? m.rightButton : m.middleButton; }
        public static Vector2 MousePos { get { var m = Mouse.current; var p = m != null ? m.position.ReadValue() : Vector2.zero; return new Vector2(p.x, Screen.height - p.y); } }
        public static bool MouseHeld(int b) { var c = B(b); return c != null && c.isPressed; }
        public static bool MouseDown(int b) { var c = B(b); return c != null && c.wasPressedThisFrame; }
        public static bool MouseUp(int b) { var c = B(b); return c != null && c.wasReleasedThisFrame; }
        public static float Scroll { get { var m = Mouse.current; return m != null ? -m.scroll.ReadValue().y / 120f * 100f : 0; } }
        public static int TouchCount => 0;
        public static Vector2 TouchPos(int i) => Vector2.zero;
#else
        static KeyCode C(char c)
        {
            if (c >= 'a' && c <= 'z') return KeyCode.A + (c - 'a');
            if (c >= '0' && c <= '9') return KeyCode.Alpha0 + (c - '0');
            return c == '[' ? KeyCode.LeftBracket : c == ']' ? KeyCode.RightBracket : c == ' ' ? KeyCode.Space : KeyCode.None;
        }
        static KeyCode C(Sp s)
        {
            switch (s)
            {
                case Sp.Esc: return KeyCode.Escape; case Sp.Tab: return KeyCode.Tab; case Sp.F1: return KeyCode.F1;
                case Sp.Up: return KeyCode.UpArrow; case Sp.Down: return KeyCode.DownArrow; case Sp.Left: return KeyCode.LeftArrow; case Sp.Right: return KeyCode.RightArrow;
                case Sp.Shift: return KeyCode.LeftShift; case Sp.Ctrl: return KeyCode.LeftControl; case Sp.Alt: return KeyCode.LeftAlt;
                case Sp.Enter: return KeyCode.Return; default: return KeyCode.Backspace;
            }
        }
        public static bool Held(char c) => Input.GetKey(C(c));
        public static bool Pressed(char c) => Input.GetKeyDown(C(c));
        public static bool Held(Sp s)
        {
            if (s == Sp.Shift) return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (s == Sp.Ctrl) return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            if (s == Sp.Alt) return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            return Input.GetKey(C(s));
        }
        public static bool Pressed(Sp s) => Input.GetKeyDown(C(s));
        public static Vector2 MousePos { get { var p = Input.mousePosition; return new Vector2(p.x, Screen.height - p.y); } }
        public static bool MouseHeld(int b) => Input.GetMouseButton(b == 1 ? 1 : b == 2 ? 2 : 0);
        public static bool MouseDown(int b) => Input.GetMouseButtonDown(b == 1 ? 1 : b == 2 ? 2 : 0);
        public static bool MouseUp(int b) => Input.GetMouseButtonUp(b == 1 ? 1 : b == 2 ? 2 : 0);
        public static float Scroll => -Input.mouseScrollDelta.y * 100f;
        public static int TouchCount => Input.touchSupported ? Input.touchCount : 0;
        public static Vector2 TouchPos(int i) { var p = Input.GetTouch(i).position; return new Vector2(p.x, Screen.height - p.y); }
#endif
    }
}
