using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom
{
    /// <summary>
    /// Silksong's yes/no question (shops, paying rosaries, handing items over) in Celeste's style (user 2026-10-14:
    /// "make it look like Celeste as closely as possible"). Silksong keeps running the question: its buttons, the
    /// selection, Jump to confirm, not having enough. Its own art is hidden and this draws, in the middle of the
    /// screen, Madeline's plum box with the question typed in, what it costs (each item and the rosaries or shards in
    /// their colours, with Silksong's own icons), and the two answers as Celeste's choice menu: the chosen one in
    /// Celeste's flashing green with a little arrow, sliding over as the choice moves, "Yes" greyed with the reason
    /// when it can't be picked.
    /// </summary>
    public class CelesteYesNo : MonoBehaviour
    {
        static GuiArt Art { get { return SpeedrunTimer.Art; } }

        static readonly System.Reflection.FieldInfo Inst = AccessTools.Field(typeof(DialogueYesNoBox), "_instance");
        static readonly System.Reflection.FieldInfo PaneF = AccessTools.Field(typeof(YesNoBox), "pane");
        static readonly System.Reflection.FieldInfo YesBtnF = AccessTools.Field(typeof(YesNoBox), "yesButton");
        static readonly System.Reflection.FieldInfo TextF = AccessTools.Field(typeof(DialogueYesNoBox), "text");
        static readonly System.Reflection.FieldInfo CurTypeF = AccessTools.Field(typeof(DialogueYesNoBox), "requiredCurrencyType");
        static readonly System.Reflection.FieldInfo CurAmtF = AccessTools.Field(typeof(DialogueYesNoBox), "requiredCurrencyAmount");
        static readonly System.Reflection.FieldInfo ItemsF = AccessTools.Field(typeof(DialogueYesNoBox), "requiredItems");
        static readonly System.Reflection.FieldInfo AmountsF = AccessTools.Field(typeof(DialogueYesNoBox), "requiredItemAmounts");
        static readonly System.Reflection.FieldInfo DisplaysF = AccessTools.Field(typeof(DialogueYesNoBox), "currencyDisplays");
        static readonly System.Reflection.FieldInfo UiListF = AccessTools.Field(typeof(YesNoBox), "uiList");
        static readonly System.Reflection.FieldInfo SelIndexF = AccessTools.Field(typeof(UISelectionList), "selectedIndex");
        static readonly System.Reflection.FieldInfo ListItemsF = AccessTools.Field(typeof(UISelectionList), "listItems");
        static readonly System.Reflection.PropertyInfo InactiveP = AccessTools.Property(typeof(DialogueYesNoBox), "InactiveYesText");

        bool open;
        float ease, typed, openedAt;
        string question = "", inactive = "";
        int amount; bool shards;
        readonly List<KeyValuePair<string, int>> items = new List<KeyValuePair<string, int>>();
        readonly List<Sprite> itemIcons = new List<Sprite>();
        Sprite currencyIcon;
        bool yesSelected = true;
        float choiceY;            // the highlight's position, sliding between the two answers
        float flash;
        bool loggedError, logged;
        const int HiddenLayer = 31;

        void LateUpdate()
        {
            try { Tick(); }
            catch (Exception e) { if (!loggedError) { loggedError = true; Plugin.Log.LogError("CelesteYesNo: " + e); } }
        }

        void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            flash += dt;
            var box = Inst != null ? Inst.GetValue(null) as DialogueYesNoBox : null;
            var pane = box != null && PaneF != null ? PaneF.GetValue(box) as InventoryPaneBase : null;
            bool nowOpen = box != null && pane != null && pane.IsPaneActive && Art != null && Art.Image("textbox/default") != null;
            if (nowOpen && !open)
            {
                ease = 0f; typed = 0f; openedAt = Time.unscaledTime; choiceY = 0f;
                Sfx.Play(Sounds.MenuRolloverDown.Id, 0.5f);
                if (!logged) { logged = true; Plugin.Log.LogInfo("Celeste yes/no box: a question opened"); }
            }
            open = nowOpen;
            ease = open ? Mathf.MoveTowards(ease, 1f, dt / 0.2f) : Mathf.MoveTowards(ease, 0f, dt / 0.15f);
            if (!open) return;

            // Silksong's own look stays out of sight: a layer no camera draws, and its UI faded right out
            foreach (Transform t in box.GetComponentsInChildren<Transform>(true)) if (t.gameObject.layer != HiddenLayer) t.gameObject.layer = HiddenLayer;
            var cg = box.GetComponent<CanvasGroup>() ?? box.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            // what it asks, and what it costs
            object tm = TextF != null ? TextF.GetValue(box) : null;
            string q = tm != null ? (tm.GetType().GetProperty("text")?.GetValue(tm, null) as string ?? "") : "";
            question = CleanText(q);
            object ct = CurTypeF != null ? CurTypeF.GetValue(box) : null;
            amount = CurAmtF != null ? (int)CurAmtF.GetValue(box) : 0;
            shards = ct != null && ct.ToString() == "Shard";
            currencyIcon = null;
            if (amount > 0 && DisplaysF != null && DisplaysF.GetValue(box) is GameObject[] disp)
                foreach (GameObject g in disp) if (g != null && g.activeSelf) { currencyIcon = FindSprite(g); break; }
            items.Clear(); itemIcons.Clear();
            if (ItemsF != null && ItemsF.GetValue(box) is IList list)
            {
                var amounts = AmountsF != null ? AmountsF.GetValue(box) as IList : null;
                for (int i = 0; i < list.Count; i++)
                {
                    var it = list[i] as SavedItem;
                    if (it == null) continue;
                    string nm = ""; Sprite ic = null;
                    try { nm = CleanText(it.GetPopupName() ?? ""); } catch (Exception) { }
                    try { ic = it.GetPopupIcon(); } catch (Exception) { }
                    items.Add(new KeyValuePair<string, int>(nm, amounts != null && i < amounts.Count ? (int)amounts[i] : 1));
                    itemIcons.Add(ic);
                }
            }
            try { inactive = InactiveP != null ? CleanText(InactiveP.GetValue(box, null) as string ?? "") : ""; } catch (Exception) { inactive = ""; }

            // which answer Silksong has selected: its own list keeps the index (user 2026-10-14: the highlight never
            // moved, because Unity's usual selection isn't used); this only mirrors it, Silksong handles the input
            var uiList = UiListF != null ? UiListF.GetValue(box) as UISelectionList : null;
            var yesBtn = YesBtnF != null ? YesBtnF.GetValue(box) as UISelectionListItem : null;
            if (uiList != null && SelIndexF != null && ListItemsF != null)
            {
                int si = (int)SelIndexF.GetValue(uiList);
                var li = ListItemsF.GetValue(uiList) as IList;
                if (li != null && si >= 0 && si < li.Count)
                {
                    bool y = yesBtn != null ? ReferenceEquals(li[si], yesBtn) : si == 0;
                    if (y != yesSelected) { yesSelected = y; flash = 0f; }
                }
            }            choiceY = Mathf.MoveTowards(choiceY, yesSelected ? 0f : 1f, dt / 0.08f);
            if (ease >= 1f) typed += dt * 70f;
        }

        static string CleanText(string s)
        {
            s = System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]*>", "");
            s = s.Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"').Replace('—', '-').Replace('–', '-').Replace("…", "...");
            return s.Trim();
        }

        static Sprite FindSprite(GameObject g)
        {
            var sr = g.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null && sr.sprite != null) return sr.sprite;
            var im = g.GetComponentInChildren<UnityEngine.UI.Image>(true);
            return im != null ? im.sprite : null;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || ease <= 0f || Art == null || TheoCamera.Hiding) return;
            GuiArt.Img tex = Art.Image("textbox/default");
            if (tex == null) return;
            float s = Screen.height / 1080f, sw = Screen.width / s;
            Matrix4x4 old = GUI.matrix;
            GUI.depth = -1;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            float now = Time.unscaledTime;
            Color white = new Color(0.94f, 0.92f, 0.95f, 1f);

            // a soft dim behind, as Celeste darkens the scene for a choice
            float a = Mathf.Clamp01(ease * 1.5f);
            GUI.color = new Color(0f, 0f, 0f, 0.35f * a); GUI.DrawTexture(new Rect(0f, 0f, sw, 1080f), Texture2D.whiteTexture);

            // the box, popping open from its middle
            // sized to what it holds (user 2026-10-14: a short question gets a short box)
            const float Scale = 0.82f, W = 1000f;
            float qts = 0.56f;
            List<string> qlines = Art.Wrap(question, (W - 120f) / qts);
            float costRows = (amount > 0 ? 1 : 0) + items.Count;
            float H = Math.Max(140f, 70f + qlines.Count * Art.LineHeight * qts + 10f);
            float boxH = H + costRows * 46f + (costRows > 0 ? 10f : 0f);
            float top = 300f;
            float k = open ? BackOutE(ease) : ease * ease * ease, h = boxH * k;
            float x = sw / 2f - W / 2f, y = top + (boxH - h) / 2f;
            var boxRect = new Rect(x, y, W, Math.Max(1f, h));
            GUI.color = new Color(1f, 1f, 1f, a * 0.55f); CelesteTextbox.Slices9(tex, boxRect, Scale);
            GUI.color = new Color(1f, 1f, 1f, a); CelesteTextbox.Slices9(tex, boxRect, Scale);

            float ta = Mathf.Clamp01((ease - 0.6f) / 0.4f) * a;
            if (ta > 0f && h > 60f)
            {
                // the question, typed in with each letter hopping up (as the conversation box does)
                float ts = qts;
                List<string> lines = qlines;
                Color[] cols = CelesteTextbox.NameColorsFor(question);
                float ly = top + (H - lines.Count * Art.LineHeight * ts) / 2f;
                int idx = 0, shown = (int)typed;
                foreach (string line in lines)
                {
                    float lw = Art.Measure(line) * ts, lx = sw / 2f - lw / 2f;   // centred, like Celeste's question boxes
                    foreach (char ch in line)
                    {
                        if (idx >= shown) break;
                        float kk = Mathf.Clamp01((typed - idx) / 6f), hop = (1f - kk) * (1f - kk);
                        Color c = cols != null && idx < cols.Length && cols[idx].a > 0f ? cols[idx] : white; c.a = ta * kk;
                        lx = Glyph(ch, lx, ly - hop * 8f * ts, ts, c);
                        idx++;
                    }
                    idx++;
                    ly += Art.LineHeight * ts;
                }

                // what it costs: each item with its icon and count, then the rosaries or shards in their colour
                float cy = top + H - 20f;
                bool done = shown >= question.Length;
                float ca = done ? Mathf.Clamp01((typed - question.Length) / 12f) * ta : 0f;
                for (int i = 0; i < items.Count; i++)
                {
                    string label = (items[i].Value > 1 ? items[i].Value + " x " : "") + items[i].Key;
                    CostRow(label, itemIcons[i], CelesteTextbox.ColorOf(items[i].Key, white), cy, sw, ca);
                    cy += 46f;
                }
                if (amount > 0)
                {
                    string label = amount + (shards ? " Shell Shards" : amount == 1 ? " Rosary" : " Rosaries");
                    CostRow(label, currencyIcon, CelesteTextbox.ColorOf(shards ? "Shell Shards" : "Rosaries", white), cy, sw, ca);
                }

                // the answers: Celeste's choice menu below the box. The chosen one flashes Celeste's highlight green,
                // sits a little to the right with a ">" before it, and the highlight slides between them.
                float oa = done ? Mathf.Clamp01((typed - question.Length - 6f) / 10f) * ta : 0f;
                if (oa > 0f)
                {
                    // side by side, as Silksong's own answers are, so left/right moves between them (user 2026-10-14)
                    float oy = top + boxH + 34f, os = 0.75f, rowH = Art.LineHeight * os + 10f, gapX = 170f;
                    Color hi = (flash % 0.2f) < 0.1f ? Hex(Visuals.MenuHighlightA) : Hex(Visuals.MenuHighlightB);
                    // a soft bar behind the chosen answer, sliding across
                    float barX = sw / 2f - gapX + choiceY * gapX * 2f;
                    GUI.color = new Color(0f, 0f, 0f, 0.45f * oa); GUI.DrawTexture(new Rect(barX - 110f, oy - 4f, 220f, rowH), Texture2D.whiteTexture);
                    string[] opts = { "Yes", "No" };
                    for (int i = 0; i < 2; i++)
                    {
                        bool sel = (i == 0) == yesSelected;
                        bool dead = i == 0 && inactive.Length > 0;
                        string o = opts[i];
                        float cx = sw / 2f + (i == 0 ? -gapX : gapX), w = Art.Measure(o) * os, ox = cx - w / 2f;
                        float lift = sel ? -4f - Mathf.Abs(Mathf.Sin(flash * 6f)) * 3f : 0f;
                        Color c = dead ? new Color(0.45f, 0.45f, 0.45f, oa) : sel ? new Color(hi.r, hi.g, hi.b, oa) : new Color(0.85f, 0.85f, 0.85f, oa);
                        if (sel)
                        {
                            Art.DrawOutlined(">", ox - 40f, oy + lift, os, c, new Color(0f, 0f, 0f, oa), 2f);
                            Art.DrawOutlined("<", ox + w + 16f, oy + lift, os, c, new Color(0f, 0f, 0f, oa), 2f);
                        }
                        Art.DrawOutlined(o, ox, oy + lift, os, c, new Color(0f, 0f, 0f, oa), 2f);
                    }                    // why "Yes" can't be picked (not enough rosaries, already at the most), under the answers
                    if (inactive.Length > 0)
                    {
                        float rs = 0.45f;
                        Art.DrawOutlined(inactive, sw / 2f - Art.Measure(inactive) * rs / 2f, oy + rowH + 12f, rs, new Color(1f, 0.55f, 0.5f, oa), new Color(0f, 0f, 0f, oa), 2f);
                    }
                }
            }
            GUI.matrix = old;
            GUI.color = Color.white;
        }

        void CostRow(string label, Sprite icon, Color c, float y, float sw, float a)
        {
            if (a <= 0f) return;
            float ts = 0.5f, isz = 40f, w = Art.Measure(label) * ts + (icon != null ? isz + 12f : 0f), x = sw / 2f - w / 2f;
            if (icon != null && icon.texture != null)
            {
                Rect tr = icon.textureRect;
                var uv = new Rect(tr.x / icon.texture.width, tr.y / icon.texture.height, tr.width / icon.texture.width, tr.height / icon.texture.height);
                float iw = isz * Mathf.Min(1f, tr.width / Math.Max(1f, tr.height)), ih = isz * Mathf.Min(1f, tr.height / Math.Max(1f, tr.width));
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.DrawTextureWithTexCoords(new Rect(x + (isz - iw) / 2f, y + (isz - ih) / 2f, iw, ih), icon.texture, uv);
                x += isz + 12f;
            }
            c.a = a;
            Art.DrawLine(label, x, y + (isz - Art.LineHeight * ts) / 2f, ts, c);
        }

        static float Glyph(char ch, float x, float top, float scale, Color c)
        {
            GuiArt.Img g;
            if (!Art.Glyphs.TryGetValue(ch, out g)) return x;
            if (g.Tex != null && c.a > 0f) { GUI.color = c; GUI.DrawTexture(new Rect(x + g.XOffset * scale, top + g.YOffset * scale, g.W * scale, g.H * scale), g.Tex); }
            return x + g.XAdvance * scale;
        }

        static float BackOutE(float t) { t = Mathf.Clamp01(t) - 1f; const float s = 1.2f; return t * t * ((s + 1f) * t + s) + 1f; }
        static Color Hex(string h) { Color c; return ColorUtility.TryParseHtmlString("#" + h, out c) ? c : Color.white; }
    }
}
