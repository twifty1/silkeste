using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using MadelineInPharloom.Sheets;
using UnityEngine;
using UnityEngine.UI;

namespace MadelineInPharloom;

public class CelesteYesNo : MonoBehaviour
{
	private static readonly FieldInfo Inst = AccessTools.Field(typeof(DialogueYesNoBox), "_instance");

	private static readonly FieldInfo PaneF = AccessTools.Field(typeof(YesNoBox), "pane");

	private static readonly FieldInfo YesBtnF = AccessTools.Field(typeof(YesNoBox), "yesButton");

	private static readonly FieldInfo TextF = AccessTools.Field(typeof(DialogueYesNoBox), "text");

	private static readonly FieldInfo CurTypeF = AccessTools.Field(typeof(DialogueYesNoBox), "requiredCurrencyType");

	private static readonly FieldInfo CurAmtF = AccessTools.Field(typeof(DialogueYesNoBox), "requiredCurrencyAmount");

	private static readonly FieldInfo ItemsF = AccessTools.Field(typeof(DialogueYesNoBox), "requiredItems");

	private static readonly FieldInfo AmountsF = AccessTools.Field(typeof(DialogueYesNoBox), "requiredItemAmounts");

	private static readonly FieldInfo DisplaysF = AccessTools.Field(typeof(DialogueYesNoBox), "currencyDisplays");

	private static readonly FieldInfo UiListF = AccessTools.Field(typeof(YesNoBox), "uiList");

	private static readonly FieldInfo SelIndexF = AccessTools.Field(typeof(UISelectionList), "selectedIndex");

	private static readonly FieldInfo ListItemsF = AccessTools.Field(typeof(UISelectionList), "listItems");

	private static readonly PropertyInfo InactiveP = AccessTools.Property(typeof(DialogueYesNoBox), "InactiveYesText");

	private bool open;

	private float ease;

	private float typed;

	private float openedAt;

	private string question = "";

	private string inactive = "";

	private int amount;

	private bool shards;

	private readonly List<KeyValuePair<string, int>> items = new List<KeyValuePair<string, int>>();

	private readonly List<Sprite> itemIcons = new List<Sprite>();

	private Sprite currencyIcon;

	private bool yesSelected = true;

	private float choiceY;

	private float flash;

	private bool loggedError;

	private bool logged;

	private const int HiddenLayer = 31;

	private static GuiArt Art => SpeedrunTimer.Art;

	private void LateUpdate()
	{
		try
		{
			Tick();
		}
		catch (Exception ex)
		{
			if (!loggedError)
			{
				loggedError = true;
				Plugin.Log.LogError("CelesteYesNo: " + ex);
			}
		}
	}

	private void Tick()
	{
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		flash += unscaledDeltaTime;
		DialogueYesNoBox dialogueYesNoBox = ((Inst != null) ? (Inst.GetValue(null) as DialogueYesNoBox) : null);
		InventoryPaneBase inventoryPaneBase = ((dialogueYesNoBox != null && PaneF != null) ? (PaneF.GetValue(dialogueYesNoBox) as InventoryPaneBase) : null);
		bool flag = dialogueYesNoBox != null && inventoryPaneBase != null && inventoryPaneBase.IsPaneActive && Art != null && Art.Image("textbox/default") != null;
		if (flag && !open)
		{
			ease = 0f;
			typed = 0f;
			openedAt = Time.unscaledTime;
			choiceY = 0f;
			Sfx.Play(Sounds.MenuRolloverDown.Id, 0.5f);
			if (!logged)
			{
				logged = true;
				Plugin.Log.LogInfo("Celeste yes/no box: a question opened");
			}
		}
		open = flag;
		ease = (open ? Mathf.MoveTowards(ease, 1f, unscaledDeltaTime / 0.2f) : Mathf.MoveTowards(ease, 0f, unscaledDeltaTime / 0.15f));
		if (!open)
		{
			return;
		}
		Transform[] componentsInChildren = dialogueYesNoBox.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			if (transform.gameObject.layer != 31)
			{
				transform.gameObject.layer = 31;
			}
		}
		(dialogueYesNoBox.GetComponent<CanvasGroup>() ?? dialogueYesNoBox.gameObject.AddComponent<CanvasGroup>()).alpha = 0f;
		object obj = ((TextF != null) ? TextF.GetValue(dialogueYesNoBox) : null);
		string s = ((obj != null) ? ((obj.GetType().GetProperty("text")?.GetValue(obj, null) as string) ?? "") : "");
		question = CleanText(s);
		object obj2 = ((CurTypeF != null) ? CurTypeF.GetValue(dialogueYesNoBox) : null);
		amount = ((CurAmtF != null) ? ((int)CurAmtF.GetValue(dialogueYesNoBox)) : 0);
		shards = obj2 != null && obj2.ToString() == "Shard";
		currencyIcon = null;
		if (amount > 0 && DisplaysF != null && DisplaysF.GetValue(dialogueYesNoBox) is GameObject[] array)
		{
			GameObject[] array2 = array;
			foreach (GameObject gameObject in array2)
			{
				if (gameObject != null && gameObject.activeSelf)
				{
					currencyIcon = FindSprite(gameObject);
					break;
				}
			}
		}
		items.Clear();
		itemIcons.Clear();
		if (ItemsF != null && ItemsF.GetValue(dialogueYesNoBox) is IList list)
		{
			IList list2 = ((AmountsF != null) ? (AmountsF.GetValue(dialogueYesNoBox) as IList) : null);
			for (int j = 0; j < list.Count; j++)
			{
				SavedItem savedItem = list[j] as SavedItem;
				if (!(savedItem == null))
				{
					string key = "";
					Sprite item = null;
					try
					{
						key = CleanText(savedItem.GetPopupName() ?? "");
					}
					catch (Exception)
					{
					}
					try
					{
						item = savedItem.GetPopupIcon();
					}
					catch (Exception)
					{
					}
					items.Add(new KeyValuePair<string, int>(key, (list2 == null || j >= list2.Count) ? 1 : ((int)list2[j])));
					itemIcons.Add(item);
				}
			}
		}
		try
		{
			inactive = ((InactiveP != null) ? CleanText((InactiveP.GetValue(dialogueYesNoBox, null) as string) ?? "") : "");
		}
		catch (Exception)
		{
			inactive = "";
		}
		UISelectionList uISelectionList = ((UiListF != null) ? (UiListF.GetValue(dialogueYesNoBox) as UISelectionList) : null);
		UISelectionListItem uISelectionListItem = ((YesBtnF != null) ? (YesBtnF.GetValue(dialogueYesNoBox) as UISelectionListItem) : null);
		if (uISelectionList != null && SelIndexF != null && ListItemsF != null)
		{
			int num = (int)SelIndexF.GetValue(uISelectionList);
			if (ListItemsF.GetValue(uISelectionList) is IList list3 && num >= 0 && num < list3.Count)
			{
				bool flag2 = ((uISelectionListItem != null) ? (list3[num] == uISelectionListItem) : (num == 0));
				if (flag2 != yesSelected)
				{
					yesSelected = flag2;
					flash = 0f;
				}
			}
		}
		choiceY = Mathf.MoveTowards(choiceY, yesSelected ? 0f : 1f, unscaledDeltaTime / 0.08f);
		if (ease >= 1f)
		{
			typed += unscaledDeltaTime * 70f;
		}
	}

	private static string CleanText(string s)
	{
		s = Regex.Replace(s ?? "", "<[^>]*>", "");
		s = s.Replace('’', '\'').Replace('‘', '\'').Replace('“', '"')
			.Replace('”', '"')
			.Replace('—', '-')
			.Replace('–', '-')
			.Replace("…", "...");
		return s.Trim();
	}

	private static Sprite FindSprite(GameObject g)
	{
		SpriteRenderer componentInChildren = g.GetComponentInChildren<SpriteRenderer>(includeInactive: true);
		if (componentInChildren != null && componentInChildren.sprite != null)
		{
			return componentInChildren.sprite;
		}
		Image componentInChildren2 = g.GetComponentInChildren<Image>(includeInactive: true);
		if (!(componentInChildren2 != null))
		{
			return null;
		}
		return componentInChildren2.sprite;
	}

	private void OnGUI()
	{
		if (Event.current.type != EventType.Repaint || ease <= 0f || Art == null || TheoCamera.Hiding)
		{
			return;
		}
		GuiArt.Img img = Art.Image("textbox/default");
		if (img == null)
		{
			return;
		}
		float num = (float)Screen.height / 1080f;
		float num2 = (float)Screen.width / num;
		Matrix4x4 matrix = GUI.matrix;
		GUI.depth = -1;
		GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num, num, 1f));
		_ = Time.unscaledTime;
		Color color = new Color(0.94f, 0.92f, 0.95f, 1f);
		float num3 = Mathf.Clamp01(ease * 1.5f);
		GUI.color = new Color(0f, 0f, 0f, 0.35f * num3);
		GUI.DrawTexture(new Rect(0f, 0f, num2, 1080f), Texture2D.whiteTexture);
		float num4 = 0.56f;
		List<string> list = Art.Wrap(question, 880f / num4);
		float num5 = ((amount > 0) ? 1 : 0) + items.Count;
		float num6 = Math.Max(140f, 70f + (float)(list.Count * Art.LineHeight) * num4 + 10f);
		float num7 = num6 + num5 * 46f + ((num5 > 0f) ? 10f : 0f);
		float num8 = 300f;
		float num9 = (open ? BackOutE(ease) : (ease * ease * ease));
		float num10 = num7 * num9;
		float x = num2 / 2f - 500f;
		float y = num8 + (num7 - num10) / 2f;
		Rect r = new Rect(x, y, 1000f, Math.Max(1f, num10));
		GUI.color = new Color(1f, 1f, 1f, num3 * 0.55f);
		CelesteTextbox.Slices9(img, r, 0.82f);
		GUI.color = new Color(1f, 1f, 1f, num3);
		CelesteTextbox.Slices9(img, r, 0.82f);
		float num11 = Mathf.Clamp01((ease - 0.6f) / 0.4f) * num3;
		if (num11 > 0f && num10 > 60f)
		{
			float num12 = num4;
			List<string> list2 = list;
			Color[] array = CelesteTextbox.NameColorsFor(question);
			float num13 = num8 + (num6 - (float)(list2.Count * Art.LineHeight) * num12) / 2f;
			int num14 = 0;
			int num15 = (int)typed;
			foreach (string item in list2)
			{
				float num16 = Art.Measure(item) * num12;
				float x2 = num2 / 2f - num16 / 2f;
				string text = item;
				foreach (char ch in text)
				{
					if (num14 >= num15)
					{
						break;
					}
					float num17 = Mathf.Clamp01((typed - (float)num14) / 6f);
					float num18 = (1f - num17) * (1f - num17);
					Color c = ((array != null && num14 < array.Length && array[num14].a > 0f) ? array[num14] : color);
					c.a = num11 * num17;
					x2 = Glyph(ch, x2, num13 - num18 * 8f * num12, num12, c);
					num14++;
				}
				num14++;
				num13 += (float)Art.LineHeight * num12;
			}
			float num19 = num8 + num6 - 20f;
			bool flag = num15 >= question.Length;
			float a = (flag ? (Mathf.Clamp01((typed - (float)question.Length) / 12f) * num11) : 0f);
			for (int j = 0; j < items.Count; j++)
			{
				string label = ((items[j].Value > 1) ? (items[j].Value + " x ") : "") + items[j].Key;
				CostRow(label, itemIcons[j], CelesteTextbox.ColorOf(items[j].Key, color), num19, num2, a);
				num19 += 46f;
			}
			if (amount > 0)
			{
				string label2 = amount + (shards ? " Shell Shards" : ((amount == 1) ? " Rosary" : " Rosaries"));
				CostRow(label2, currencyIcon, CelesteTextbox.ColorOf(shards ? "Shell Shards" : "Rosaries", color), num19, num2, a);
			}
			float num20 = (flag ? (Mathf.Clamp01((typed - (float)question.Length - 6f) / 10f) * num11) : 0f);
			if (num20 > 0f)
			{
				float num21 = num8 + num7 + 34f;
				float num22 = 0.75f;
				float num23 = (float)Art.LineHeight * num22 + 10f;
				float num24 = 170f;
				Color color2 = ((flash % 0.2f < 0.1f) ? Hex("84FF54") : Hex("FCFF59"));
				float num25 = num2 / 2f - num24 + choiceY * num24 * 2f;
				GUI.color = new Color(0f, 0f, 0f, 0.45f * num20);
				GUI.DrawTexture(new Rect(num25 - 110f, num21 - 4f, 220f, num23), Texture2D.whiteTexture);
				string[] array2 = new string[2] { "Yes", "No" };
				for (int k = 0; k < 2; k++)
				{
					bool flag2 = k == 0 == yesSelected;
					bool num26 = k == 0 && inactive.Length > 0;
					string line = array2[k];
					float num27 = num2 / 2f + ((k == 0) ? (0f - num24) : num24);
					float num28 = Art.Measure(line) * num22;
					float num29 = num27 - num28 / 2f;
					float num30 = (flag2 ? (-4f - Mathf.Abs(Mathf.Sin(flash * 6f)) * 3f) : 0f);
					Color c2 = (num26 ? new Color(0.45f, 0.45f, 0.45f, num20) : (flag2 ? new Color(color2.r, color2.g, color2.b, num20) : new Color(0.85f, 0.85f, 0.85f, num20)));
					if (flag2)
					{
						Art.DrawOutlined(">", num29 - 40f, num21 + num30, num22, c2, new Color(0f, 0f, 0f, num20), 2f);
						Art.DrawOutlined("<", num29 + num28 + 16f, num21 + num30, num22, c2, new Color(0f, 0f, 0f, num20), 2f);
					}
					Art.DrawOutlined(line, num29, num21 + num30, num22, c2, new Color(0f, 0f, 0f, num20), 2f);
				}
				if (inactive.Length > 0)
				{
					float num31 = 0.45f;
					Art.DrawOutlined(inactive, num2 / 2f - Art.Measure(inactive) * num31 / 2f, num21 + num23 + 12f, num31, new Color(1f, 0.55f, 0.5f, num20), new Color(0f, 0f, 0f, num20), 2f);
				}
			}
		}
		GUI.matrix = matrix;
		GUI.color = Color.white;
	}

	private void CostRow(string label, Sprite icon, Color c, float y, float sw, float a)
	{
		if (!(a <= 0f))
		{
			float num = 0.5f;
			float num2 = 40f;
			float num3 = Art.Measure(label) * num + ((icon != null) ? (num2 + 12f) : 0f);
			float num4 = sw / 2f - num3 / 2f;
			if (icon != null && icon.texture != null)
			{
				Rect textureRect = icon.textureRect;
				Rect texCoords = new Rect(textureRect.x / (float)icon.texture.width, textureRect.y / (float)icon.texture.height, textureRect.width / (float)icon.texture.width, textureRect.height / (float)icon.texture.height);
				float num5 = num2 * Mathf.Min(1f, textureRect.width / Math.Max(1f, textureRect.height));
				float num6 = num2 * Mathf.Min(1f, textureRect.height / Math.Max(1f, textureRect.width));
				GUI.color = new Color(1f, 1f, 1f, a);
				GUI.DrawTextureWithTexCoords(new Rect(num4 + (num2 - num5) / 2f, y + (num2 - num6) / 2f, num5, num6), icon.texture, texCoords);
				num4 += num2 + 12f;
			}
			c.a = a;
			Art.DrawLine(label, num4, y + (num2 - (float)Art.LineHeight * num) / 2f, num, c);
		}
	}

	private static float Glyph(char ch, float x, float top, float scale, Color c)
	{
		if (!Art.Glyphs.TryGetValue(ch, out var value))
		{
			return x;
		}
		if (value.Tex != null && c.a > 0f)
		{
			GUI.color = c;
			GUI.DrawTexture(new Rect(x + (float)value.XOffset * scale, top + (float)value.YOffset * scale, (float)value.W * scale, (float)value.H * scale), value.Tex);
		}
		return x + (float)value.XAdvance * scale;
	}

	private static float BackOutE(float t)
	{
		t = Mathf.Clamp01(t) - 1f;
		return t * t * (2.2f * t + 1.2f) + 1f;
	}

	private static Color Hex(string h)
	{
		if (!ColorUtility.TryParseHtmlString("#" + h, out var color))
		{
			return Color.white;
		}
		return color;
	}
}
