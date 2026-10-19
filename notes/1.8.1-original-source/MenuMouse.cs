using System;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>The mouse in the Settings and Records menus (user 2026-10-13), in their 1080p GUI units.</summary>
    public static class MenuMouse
    {
        /// <summary>The controller was used last (user 2026-10-13: touching it hides the mouse; moving the mouse brings it back).</summary>
        public static bool PadActive;
        public static Vector2 Gui()
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m == null) return new Vector2(-1f, -1f);
            float s = Screen.height / 1080f;
            Vector2 p = m.position.ReadValue();
            return new Vector2(p.x / s, (Screen.height - p.y) / s);
        }
        public static bool Clicked(Rect r)
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame && r.Contains(Gui());
        }
        public static float Wheel()
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null ? m.scroll.ReadValue().y : 0f;
        }

        /// <summary>The small button left of a PlayStation touchpad (Share on PS4, Create on PS5): opens Records (user 2026-10-13).</summary>
        public static bool SharePressed()
        {
            try
            {
                var dev = InControl.InputManager.ActiveDevice;
                if (dev == null || dev == InControl.InputDevice.Null) return false;
                foreach (var t in new[] { InControl.InputControlType.Share, InControl.InputControlType.Create })
                {
                    var c = dev.GetControl(t);
                    if (c != null && c != InControl.InputControl.Null && c.WasPressed) return true;
                }
            }
            catch (Exception) { }
            return false;
        }

        /// <summary>Did the player last use a controller? Any button or stick says yes; a key or the mouse says no.</summary>
        public static bool PadHints(bool was)
        {
            try
            {
                var dev = InControl.InputManager.ActiveDevice;
                if (dev != null && dev != InControl.InputDevice.Null && (dev.AnyButtonWasPressed || dev.LeftStick.Vector.sqrMagnitude > 0.25f || dev.DPad.Vector.sqrMagnitude > 0.25f)) was = true;
            }
            catch (Exception) { }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) was = false;
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m != null && (m.leftButton.wasPressedThisFrame || m.delta.ReadValue().sqrMagnitude > 4f)) was = false;
            return was;
        }
    }

    /// <summary>The Settings menu's look, shared with Records: title, tab bar, hint line.</summary>
    static class MenuStyle
    {
        static GuiArt Art { get { return SpeedrunTimer.Art; } }
        static Color Hex(string h) { Color c; return ColorUtility.TryParseHtmlString("#" + h, out c) ? c : Color.white; }

        /// <summary>A big outlined title centred at cx; returns the y under it.</summary>
        public static float Title(string text, GuiArt.Img icon, float cx, float top, float alpha)
        {
            var ink = new Color(0f, 0f, 0f, alpha);
            if (icon != null)
            {
                float isz = 90f;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(new Rect(cx - isz / 2f, top, isz, isz), icon.Tex);
                GUI.color = Color.white;
                top += isz + 6f;
            }
            Art.DrawOutlined(text, cx - Art.Measure(text) * 1.2f / 2f, top, 1.2f, new Color(1f, 1f, 1f, alpha), ink, 3f);
            return top + Art.LineHeight * 1.2f + 14f;
        }

        /// <summary>The tab bar with L1/R1 (or Q/E) at its ends; fills rects for the mouse; returns the y under it.</summary>
        public static float Tabs(string[] names, int current, bool pad, float cx, float top, float alpha, Rect[] rects)
        {
            var ink = new Color(0f, 0f, 0f, alpha);
            float ts = Visuals.AssistTextScale * 0.75f, gap = 50f, tw = 0f;
            for (int t = 0; t < names.Length; t++) tw += Art.Measure(names[t]) * ts + (t > 0 ? gap : 0f);
            float tx = cx - tw / 2f;
            string lk = pad ? "L1" : "Q", rk = pad ? "R1" : "E";
            float ks = ts * 0.8f;
            Art.DrawOutlined(lk, tx - 40f - Art.Measure(lk) * ks, top + 4f, ks, new Color(0.7f, 0.7f, 0.7f, alpha), ink, 2f);
            Art.DrawOutlined(rk, tx + tw + 40f, top + 4f, ks, new Color(0.7f, 0.7f, 0.7f, alpha), ink, 2f);
            Vector2 mp = MenuMouse.Gui();
            for (int t = 0; t < names.Length; t++)
            {
                float lw = Art.Measure(names[t]) * ts;
                rects[t] = new Rect(tx - gap / 2f, top - 8f, lw + gap, Art.LineHeight * ts + 20f);
                if (t != current && !pad && rects[t].Contains(mp)) { GUI.color = new Color(1f, 1f, 1f, 0.06f * alpha); GUI.DrawTexture(rects[t], Texture2D.whiteTexture); GUI.color = Color.white; }
                Color tc = t == current ? Hex(Visuals.MenuHighlightA) : new Color(0.55f, 0.55f, 0.55f, 1f);
                tc.a = alpha;
                Art.DrawOutlined(names[t], tx, top, ts, tc, ink, 2f);
                if (t == current) { GUI.color = tc; GUI.DrawTexture(new Rect(tx, top + Art.LineHeight * ts + 4f, lw, 4f), Texture2D.whiteTexture); GUI.color = Color.white; }
                tx += lw + gap;
            }
            return top + Art.LineHeight * ts + 34f;
        }

        public static void Hint(string hint, float cx, float y, float alpha)
        {
            float hs = 0.45f;
            Art.DrawOutlined(hint, cx - Art.Measure(hint) * hs / 2f, y, hs, new Color(1f, 1f, 1f, alpha * 0.8f), new Color(0f, 0f, 0f, alpha), 2f);
        }

        /// <summary>A thin panel with a faint border, behind a block of the menu.</summary>
        public static void Panel(Rect r, float alpha)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.05f * alpha); GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.12f * alpha);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
