using System;
using System.Collections;
using System.IO;
using InControl;
using MadelineInPharloom.Sheets;
using UnityEngine;

namespace MadelineInPharloom;

public class TheoCamera : MonoBehaviour
{
	public static bool Hiding;

	public static bool HidingHud;

	private bool busy;

	private float flash;

	private float showT = -1f;

	private Texture2D shot;

	private bool loggedError;

	public static TheoCamera Instance;

	private bool showAfterCard;

	private static GuiArt Art => SpeedrunTimer.Art;

	private void Awake()
	{
		Instance = this;
	}

	public static void BossPhoto(string boss)
	{
		if (Instance != null && !Instance.busy)
		{
			Instance.StartCoroutine(Instance.Take(withCard: true, boss));
		}
	}

	private void Update()
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
				Plugin.Log.LogError("TheoCamera: " + ex);
			}
		}
	}

	private void Tick()
	{
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		if (flash > 0f)
		{
			flash = Mathf.Max(0f, flash - unscaledDeltaTime * 2.5f);
		}
		if (showT >= 0f && (showT += unscaledDeltaTime) > 3.2f)
		{
			showT = -1f;
		}
		if (showAfterCard && !BossCard.CardShowing)
		{
			showAfterCard = false;
			showT = 0f;
		}
		GameManager silentInstance = GameManager.SilentInstance;
		if (Plugin.Ready && !busy && !(silentInstance == null) && !(HeroController.UnsafeInstance == null) && !silentInstance.IsMenuScene())
		{
			bool flag = false;
			try
			{
				flag = InputManager.ActiveDevice?.LeftStickButton.WasPressed ?? false;
			}
			catch (Exception)
			{
			}
			if ((AssistMode.KeyPressed(AssistOptions.KeyCamera) || flag) && !AssistMode.IsOpen && !BossCard.RecordsOpen && !silentInstance.isPaused)
			{
				StartCoroutine(Take(withCard: false, null));
			}
		}
	}

	private IEnumerator Take(bool withCard, string boss)
	{
		busy = true;
		Hiding = !withCard;
		HidingHud = true;
		Camera hud = ((GameCameras.instance != null) ? GameCameras.instance.hudCamera : null);
		bool hudWas = hud != null && hud.enabled;
		if (hud != null)
		{
			hud.enabled = false;
		}
		yield return new WaitForEndOfFrame();
		yield return new WaitForEndOfFrame();
		Texture2D texture2D = null;
		try
		{
			texture2D = ScreenCapture.CaptureScreenshotAsTexture();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Photo: " + ex.Message);
		}
		if (hud != null)
		{
			hud.enabled = hudWas;
		}
		Hiding = false;
		HidingHud = false;
		if (texture2D != null)
		{
			try
			{
				string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Silkeste");
				Directory.CreateDirectory(text);
				// the boss in the name, or else the area as "A-Name" (the Photos page shows them on the frame)
				string area = BossCard.CurrentAreaName;
				string text2 = (!string.IsNullOrEmpty(boss)) ? ("_" + new string(Array.FindAll(boss.ToCharArray(), (char ch) => char.IsLetterOrDigit(ch)))) : (string.IsNullOrEmpty(area) ? "" : ("_A-" + new string(Array.FindAll(area.ToCharArray(), (char ch) => char.IsLetterOrDigit(ch)))));
				string text3 = Path.Combine(text, "Silkeste" + text2 + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
				File.WriteAllBytes(text3, texture2D.EncodeToPNG());
				Plugin.Log.LogInfo("Photo saved: " + text3);
			}
			catch (Exception ex2)
			{
				Plugin.Log.LogWarning("Photo: " + ex2.Message);
			}
			if (shot != null)
			{
				UnityEngine.Object.Destroy(shot);
			}
			shot = texture2D;
			if (withCard)
			{
				showAfterCard = true;
			}
			else
			{
				flash = 1f;
				showT = 0f;
				Sfx.Play(Sounds.MenuButtonSelect.Id);
				Achievements.Photo();
			}
		}
		busy = false;
	}

	private void OnGUI()
	{
		if (Event.current.type != EventType.Repaint || Hiding)
		{
			return;
		}
		float num = (float)Screen.height / 1080f;
		float num2 = (float)Screen.width / num;
		Matrix4x4 matrix = GUI.matrix;
		GUI.depth = -3;
		if (flash > 0f)
		{
			GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(num, num, 1f));
			GUI.color = new Color(1f, 1f, 1f, flash * 0.9f);
			GUI.DrawTexture(new Rect(0f, 0f, num2, 1080f), Texture2D.whiteTexture);
		}
		if (showT >= 0f && shot != null)
		{
			float value = ((showT < 0.35f) ? (showT / 0.35f) : ((showT > 2.7f) ? (1f - (showT - 2.7f) / 0.5f) : 1f));
			value = Mathf.Clamp01(value);
			float num3 = 1f - (1f - value) * (1f - value) * (1f - value);
			float num4 = 420f;
			float num5 = num4 * (float)shot.height / (float)shot.width;
			float num6 = 18f;
			float num7 = 70f;
			float num8 = num4 + num6 * 2f;
			float num9 = num5 + num6 + 70f;
			Vector2 vector = new Vector2(num2 - 60f - num8 / 2f, 1040f - num9 / 2f + (1f - num3) * 300f);
			GUI.matrix = Matrix4x4.TRS(new Vector3(vector.x * num, vector.y * num, 0f), Quaternion.Euler(0f, 0f, -4f), new Vector3(num, num, 1f));
			GUI.color = new Color(0f, 0f, 0f, 0.35f * num3);
			GUI.DrawTexture(new Rect((0f - num8) / 2f + 8f, (0f - num9) / 2f + 10f, num8, num9), Texture2D.whiteTexture);
			GUI.color = new Color(0.97f, 0.96f, 0.92f, num3);
			GUI.DrawTexture(new Rect((0f - num8) / 2f, (0f - num9) / 2f, num8, num9), Texture2D.whiteTexture);
			GUI.color = new Color(1f, 1f, 1f, num3);
			GUI.DrawTexture(new Rect((0f - num8) / 2f + num6, (0f - num9) / 2f + num6, num4, num5), shot);
			if (Art != null)
			{
				float num10 = 0.5f;
				Art.DrawLine("Saved to Pictures/Silkeste", (0f - Art.Measure("Saved to Pictures/Silkeste")) * num10 / 2f, num9 / 2f - num7 / 2f - (float)Art.LineHeight * num10 / 2f, num10, new Color(0.2f, 0.16f, 0.14f, num3));
			}
		}
		GUI.matrix = matrix;
		GUI.color = Color.white;
	}
}
