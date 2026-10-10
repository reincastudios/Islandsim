using UnityEngine;
#if ENABLE_INPUT_SYSTEM && ISIM_INPUTSYS
using UnityEngine.InputSystem;
#endif

namespace IslandSim.View
{
    public enum K { W, A, S, D, Q, E, R, F, T, V, X, H, B, N1, N2, N3, N4, N5, N6, Space, Escape, F5, F9, Up, Down, Left, Right, Shift }

    /// <summary>Reads keyboard and mouse through whichever input backend the project has active.</summary>
    public static class InputBridge
    {
#if ENABLE_INPUT_SYSTEM && ISIM_INPUTSYS
        static Key Map(K k)
        {
            switch (k)
            {
                case K.W: return Key.W; case K.A: return Key.A; case K.S: return Key.S; case K.D: return Key.D;
                case K.Q: return Key.Q; case K.E: return Key.E; case K.R: return Key.R; case K.F: return Key.F;
                case K.T: return Key.T; case K.V: return Key.V; case K.X: return Key.X; case K.H: return Key.H; case K.B: return Key.B;
                case K.N1: return Key.Digit1; case K.N2: return Key.Digit2; case K.N3: return Key.Digit3;
                case K.N4: return Key.Digit4; case K.N5: return Key.Digit5; case K.N6: return Key.Digit6;
                case K.Space: return Key.Space; case K.Escape: return Key.Escape; case K.F5: return Key.F5; case K.F9: return Key.F9;
                case K.Up: return Key.UpArrow; case K.Down: return Key.DownArrow; case K.Left: return Key.LeftArrow; case K.Right: return Key.RightArrow;
                default: return Key.LeftShift;
            }
        }
        public static bool Held(K k) => Keyboard.current != null && Keyboard.current[Map(k)].isPressed;
        public static bool Pressed(K k) => Keyboard.current != null && Keyboard.current[Map(k)].wasPressedThisFrame;
        public static Vector2 MousePos => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        static UnityEngine.InputSystem.Controls.ButtonControl Btn(int b) =>
            Mouse.current == null ? null : b == 0 ? Mouse.current.leftButton : b == 1 ? Mouse.current.rightButton : Mouse.current.middleButton;
        public static bool MouseHeld(int b) { var c = Btn(b); return c != null && c.isPressed; }
        public static bool MouseDown(int b) { var c = Btn(b); return c != null && c.wasPressedThisFrame; }
        public static bool MouseUp(int b) { var c = Btn(b); return c != null && c.wasReleasedThisFrame; }
        public static float Scroll => Mouse.current != null ? Mathf.Clamp(Mouse.current.scroll.ReadValue().y / 120f, -3, 3) : 0;
#else
        static KeyCode Map(K k)
        {
            switch (k)
            {
                case K.W: return KeyCode.W; case K.A: return KeyCode.A; case K.S: return KeyCode.S; case K.D: return KeyCode.D;
                case K.Q: return KeyCode.Q; case K.E: return KeyCode.E; case K.R: return KeyCode.R; case K.F: return KeyCode.F;
                case K.T: return KeyCode.T; case K.V: return KeyCode.V; case K.X: return KeyCode.X; case K.H: return KeyCode.H; case K.B: return KeyCode.B;
                case K.N1: return KeyCode.Alpha1; case K.N2: return KeyCode.Alpha2; case K.N3: return KeyCode.Alpha3;
                case K.N4: return KeyCode.Alpha4; case K.N5: return KeyCode.Alpha5; case K.N6: return KeyCode.Alpha6;
                case K.Space: return KeyCode.Space; case K.Escape: return KeyCode.Escape; case K.F5: return KeyCode.F5; case K.F9: return KeyCode.F9;
                case K.Up: return KeyCode.UpArrow; case K.Down: return KeyCode.DownArrow; case K.Left: return KeyCode.LeftArrow; case K.Right: return KeyCode.RightArrow;
                default: return KeyCode.LeftShift;
            }
        }
        public static bool Held(K k) => Input.GetKey(Map(k));
        public static bool Pressed(K k) => Input.GetKeyDown(Map(k));
        public static Vector2 MousePos => Input.mousePosition;
        public static bool MouseHeld(int b) => Input.GetMouseButton(b);
        public static bool MouseDown(int b) => Input.GetMouseButtonDown(b);
        public static bool MouseUp(int b) => Input.GetMouseButtonUp(b);
        public static float Scroll => Mathf.Clamp(Input.mouseScrollDelta.y, -3, 3);
#endif
    }
}
