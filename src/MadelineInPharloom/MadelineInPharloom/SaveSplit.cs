using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// Silkeste's own save files (user 2026-11-10: a player had to delete a randomizer file to start Silkeste). The game's
/// save folder is pointed at a "Silkeste" folder inside its own, so Silkeste has 4 slots of its own and vanilla (and
/// other mods) keep theirs. The first time, files Silkeste was clearly used on are moved over, after a full backup of
/// the game's save folder; nothing else is touched. A Silksong save can be copied in from the file menu.
/// Steam and GOG save to this folder themselves (DesktopPlatform, HandlesGameSaves false).
/// </summary>
public class SaveSplit : MonoBehaviour
{
	public const string FolderName = "Silkeste";

	public const string MarkerName = "silkeste_saves.cfg";

	/// <summary>The game's own save folder (vanilla files), once known.</summary>
	public static string VanillaDir;

	/// <summary>Silkeste's save folder, once the game saves there.</summary>
	public static string SilkDir;

	/// <summary>Shown once on the title after files were moved.</summary>
	public static string Notice;

	private static readonly AccessTools.FieldRef<DesktopPlatform, string> SaveDir = AccessTools.FieldRefAccess<DesktopPlatform, string>("saveDirPath");

	private static readonly AccessTools.FieldRef<DesktopPlatform, Platform.ISharedData> Roaming = AccessTools.FieldRefAccess<DesktopPlatform, Platform.ISharedData>("roamingSharedData");

	private static readonly AccessTools.FieldRef<DesktopPlatform, DesktopSaveRestoreHandler> Restore = AccessTools.FieldRefAccess<DesktopPlatform, DesktopSaveRestoreHandler>("desktopSaveRestoreHandler");

	private static string DataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MadelineInPharloom");

	public static void Install(Harmony h)
	{
		try
		{
			h.Patch(AccessTools.Method(typeof(DesktopPlatform), "Awake"), null, new HarmonyMethod(typeof(SaveSplit), "AwakePostfix"));
			if (Platform.Current is DesktopPlatform already && !string.IsNullOrEmpty(SaveDir(already)))
			{
				AwakePostfix(already);   // the platform woke before Silkeste: point it now
			}
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Silkeste saves: " + e.Message);
		}
	}

	/// <summary>True once any Silkeste save folder exists (the Melty Net guard leaves saves alone then).</summary>
	public static bool AnyMigrated()
	{
		try
		{
			string root = Application.persistentDataPath;
			return Directory.Exists(root) && Directory.GetDirectories(root).Any((string d) => File.Exists(Path.Combine(d, FolderName, MarkerName)));
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static void AwakePostfix(DesktopPlatform __instance)
	{
		string vanilla = SaveDir(__instance);
		if (string.IsNullOrEmpty(vanilla) || Path.GetFileName(vanilla.TrimEnd('/', '\\')) == FolderName)
		{
			return;
		}
		string silk = Path.Combine(vanilla, FolderName);
		try
		{
			Directory.CreateDirectory(silk);
			if (!File.Exists(Path.Combine(silk, MarkerName)))
			{
				Migrate(vanilla, silk);
			}
		}
		catch (Exception e)
		{
			// nothing was moved without a backup; if even the folder can't be made, Silkeste saves where it always did
			Plugin.Log.LogError("Silkeste saves: couldn't set up its own save folder, using the game's: " + e);
			if (!File.Exists(Path.Combine(silk, MarkerName)))
			{
				return;
			}
		}
		VanillaDir = vanilla;
		SilkDir = silk;
		SaveDir(__instance) = silk;
		Roaming(__instance) = new JsonSharedData(silk, "shared.dat", useEncryption: true);
		Restore(__instance) = new DesktopSaveRestoreHandler(silk);
		Plugin.Log.LogInfo("Silkeste saves: its own folder " + silk);
	}

	// ---- the one-time move -------------------------------------------------------------------------------------------

	private static void Migrate(string vanilla, string silk)
	{
		string backup = Path.Combine(DataDir, "save_backup", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
		CopyTree(vanilla, backup, skip: silk);
		if (!SameTree(vanilla, backup, skip: silk))
		{
			throw new IOException("the backup of the save folder didn't match it, so nothing was moved");
		}
		Plugin.Log.LogInfo("Silkeste saves: backup of the game's save folder in " + backup);
		foreach (string f in Directory.GetFiles(vanilla, "shared.dat*"))
		{
			string to = Path.Combine(silk, Path.GetFileName(f));
			if (!File.Exists(to))
			{
				File.Copy(f, to);   // the same shared progress and settings to start from (a copy)
			}
		}
		List<int> moved = new List<int>();
		for (int slot = 1; slot <= 4; slot++)
		{
			string why;
			bool ours = IsSilkesteFile(vanilla, slot, out why);
			Plugin.Log.LogInfo("Silkeste saves: file " + slot + ": " + why);
			if (ours && MoveSlot(vanilla, silk, slot))
			{
				moved.Add(slot);
			}
		}
		File.WriteAllLines(Path.Combine(silk, MarkerName), new string[4]
		{
			"version=1",
			"moved=" + string.Join(",", moved.Select((int s) => s.ToString(CultureInfo.InvariantCulture)).ToArray()),
			"backup=" + backup,
			"at=" + DateTime.Now.ToString("s", CultureInfo.InvariantCulture)
		});
		Notice = "Silkeste now has its own 4 save files, apart from Silksong's." + (moved.Count > 0 ? (" Moved here: " + string.Join(", ", moved.Select((int s) => "File " + s).ToArray()) + ".") : "") + " A backup of all your saves is in " + backup.Replace(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%") + ". Missing a file? Pick an empty file and use Copy a Silksong save.";
		Plugin.Log.LogInfo("Silkeste saves: moved " + moved.Count + " file(s)");
	}

	/// <summary>Silkeste's notes on the slot (its mode, tutorial, run log...) and a play time at least the one it saw.</summary>
	private static bool IsSilkesteFile(string vanilla, int slot, out string why)
	{
		string file = Path.Combine(vanilla, "user" + slot + ".dat");
		if (!File.Exists(file))
		{
			why = "empty";
			return false;
		}
		float seen;
		if (!Notes(slot, out seen))
		{
			why = "no Silkeste notes: left where it is";
			return false;
		}
		float played;
		if (!TryPlayTime(file, out played))
		{
			why = "couldn't read its play time: left where it is";
			return false;
		}
		if (played + 5f < seen)
		{
			why = "played " + played.ToString("0") + " s, less than the " + seen.ToString("0") + " s Silkeste saw: another game since, left where it is";
			return false;
		}
		why = "a Silkeste file (played " + played.ToString("0") + " s, Silkeste saw " + seen.ToString("0") + " s)";
		return true;
	}

	private static bool Notes(int slot, out float seen)
	{
		seen = 0f;
		bool any = false;
		string s = slot.ToString(CultureInfo.InvariantCulture);
		foreach (string line in Lines("save_settings.cfg"))
		{
			any |= line.StartsWith(s + "|", StringComparison.Ordinal);
		}
		foreach (string line in Lines("tech_tips_saves.cfg"))
		{
			any |= line.StartsWith(s + "=", StringComparison.Ordinal);
		}
		foreach (string line in Lines("run_log.cfg"))
		{
			string[] p = line.Split('|');
			if (p.Length >= 2 && p[0] == s)
			{
				any = true;
				if (float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var t))
				{
					seen = Math.Max(seen, t);
				}
			}
		}
		foreach (string name in new string[2] { "assist_saves.cfg", "tutorial.cfg" })
		{
			foreach (string line in Lines(name))
			{
				string[] p = line.Split('=');
				if (p.Length == 2 && p[0] == s)
				{
					any = true;
					if (float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var t))
					{
						seen = Math.Max(seen, t);
					}
				}
			}
		}
		return any;
	}

	private static IEnumerable<string> Lines(string name)
	{
		string path = Path.Combine(DataDir, name);
		try
		{
			return File.Exists(path) ? File.ReadAllLines(path) : new string[0];
		}
		catch (Exception)
		{
			return new string[0];
		}
	}

	private static readonly Regex PlayTimeRx = new Regex("\"playTime\"\\s*:\\s*([0-9.eE+-]+)");

	/// <summary>Reads a save's play time with the game's own decoding (read only).</summary>
	public static bool TryPlayTime(string file, out float seconds)
	{
		seconds = 0f;
		try
		{
			string json = GameManager.GetJsonForSaveBytesStatic(File.ReadAllBytes(file));
			Match m = PlayTimeRx.Match(json ?? "");
			return m.Success && float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Silkeste saves: reading " + Path.GetFileName(file) + ": " + e.Message);
			return false;
		}
	}

	/// <summary>A slot's files (userN.dat, its backups and versions, its restore points): copied, checked, then removed.</summary>
	private static bool MoveSlot(string vanilla, string silk, int slot)
	{
		string[] files = SlotFiles(vanilla, slot);
		string points = Path.Combine(vanilla, "Restore_Points" + slot);
		List<string> made = new List<string>();
		try
		{
			foreach (string f in files)
			{
				string to = Path.Combine(silk, Path.GetFileName(f));
				if (File.Exists(to))
				{
					throw new IOException(Path.GetFileName(f) + " is already in Silkeste's folder");
				}
				File.Copy(f, to);
				made.Add(to);
				if (!SameFile(f, to))
				{
					throw new IOException(Path.GetFileName(f) + " didn't copy right");
				}
			}
			string pointsTo = Path.Combine(silk, Path.GetFileName(points));
			bool hasPoints = Directory.Exists(points);
			if (hasPoints)
			{
				if (Directory.Exists(pointsTo))
				{
					throw new IOException("its restore points are already in Silkeste's folder");
				}
				CopyTree(points, pointsTo, null);
				made.Add(pointsTo);
				if (!SameTree(points, pointsTo, null))
				{
					throw new IOException("its restore points didn't copy right");
				}
			}
			// every copy checked: now the originals go (they're also in the backup)
			foreach (string f in files)
			{
				File.Delete(f);
			}
			if (hasPoints)
			{
				Directory.Delete(points, recursive: true);
			}
			return true;
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Silkeste saves: file " + slot + " left where it is: " + e.Message);
			foreach (string m in made)
			{
				try
				{
					if (Directory.Exists(m))
					{
						Directory.Delete(m, recursive: true);
					}
					else if (File.Exists(m))
					{
						File.Delete(m);
					}
				}
				catch (Exception)
				{
				}
			}
			return false;
		}
	}

	private static string[] SlotFiles(string dir, int slot)
	{
		string a = "user" + slot + ".";
		string b = "user" + slot + "_";
		return Directory.GetFiles(dir).Where(delegate(string f)
		{
			string n = Path.GetFileName(f);
			return n.StartsWith(a, StringComparison.OrdinalIgnoreCase) || n.StartsWith(b, StringComparison.OrdinalIgnoreCase);
		}).ToArray();
	}

	private static void CopyTree(string from, string to, string skip)
	{
		Directory.CreateDirectory(to);
		foreach (string f in Directory.GetFiles(from))
		{
			File.Copy(f, Path.Combine(to, Path.GetFileName(f)), overwrite: true);
		}
		foreach (string d in Directory.GetDirectories(from))
		{
			if (skip != null && string.Equals(Path.GetFullPath(d).TrimEnd('\\', '/'), Path.GetFullPath(skip).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			CopyTree(d, Path.Combine(to, Path.GetFileName(d)), skip);
		}
	}

	private static bool SameTree(string a, string b, string skip)
	{
		foreach (string f in Directory.GetFiles(a))
		{
			if (!SameFile(f, Path.Combine(b, Path.GetFileName(f))))
			{
				return false;
			}
		}
		foreach (string d in Directory.GetDirectories(a))
		{
			if (skip != null && string.Equals(Path.GetFullPath(d).TrimEnd('\\', '/'), Path.GetFullPath(skip).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			if (!SameTree(d, Path.Combine(b, Path.GetFileName(d)), skip))
			{
				return false;
			}
		}
		return true;
	}

	private static bool SameFile(string a, string b)
	{
		if (!File.Exists(b))
		{
			return false;
		}
		byte[] x = File.ReadAllBytes(a);
		byte[] y = File.ReadAllBytes(b);
		return x.Length == y.Length && x.SequenceEqual(y);
	}

	// ---- copying a Silksong save in (the file menu's "Copy a Silksong save") ------------------------------------------

	public struct VanillaFile
	{
		public int Slot;

		public float PlayTime;
	}

	/// <summary>Silksong's own saves that can be copied into a Silkeste slot.</summary>
	public static List<VanillaFile> VanillaFiles()
	{
		List<VanillaFile> list = new List<VanillaFile>();
		if (VanillaDir == null)
		{
			return list;
		}
		for (int s = 1; s <= 4; s++)
		{
			string f = Path.Combine(VanillaDir, "user" + s + ".dat");
			if (File.Exists(f))
			{
				TryPlayTime(f, out var t);
				list.Add(new VanillaFile
				{
					Slot = s,
					PlayTime = t
				});
			}
		}
		return list;
	}

	private static int cachedCount = -1;

	private static float cachedAt = -10f;

	/// <summary>How many Silksong saves there are, checked at most every few seconds (it's asked every frame).</summary>
	public static int CachedVanillaCount()
	{
		if (Time.unscaledTime - cachedAt > 3f)
		{
			cachedAt = Time.unscaledTime;
			cachedCount = 0;
			if (VanillaDir != null)
			{
				for (int s = 1; s <= 4; s++)
				{
					if (File.Exists(Path.Combine(VanillaDir, "user" + s + ".dat")))
					{
						cachedCount++;
					}
				}
			}
		}
		return cachedCount;
	}

	public static string Label(VanillaFile v)
	{
		int m = (int)(v.PlayTime / 60f);
		return "Silksong File " + v.Slot + "  (" + (m / 60) + "h " + (m % 60).ToString("00") + "m)";
	}

	/// <summary>Copies (never moves) a Silksong save into an empty Silkeste slot, and checks the copy.</summary>
	public static bool CopyIn(int fromSlot, int toSlot)
	{
		if (VanillaDir == null || SilkDir == null)
		{
			return false;
		}
		string src = Path.Combine(VanillaDir, "user" + fromSlot + ".dat");
		string dst = Path.Combine(SilkDir, "user" + toSlot + ".dat");
		try
		{
			if (!File.Exists(src) || File.Exists(dst))
			{
				return false;
			}
			File.Copy(src, dst);
			if (!SameFile(src, dst))
			{
				File.Delete(dst);
				return false;
			}
			Plugin.Log.LogInfo("Silkeste saves: Silksong file " + fromSlot + " copied into Silkeste file " + toSlot);
			return true;
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning("Silkeste saves: copy: " + e.Message);
			return false;
		}
	}

	// ---- the notice on the title, once -------------------------------------------------------------------------------

	private float shownFor;

	private void OnGUI()
	{
		GameManager gm = GameManager.SilentInstance;
		GuiArt art = AssistMode.Art;
		if (Notice == null || art == null || gm == null || !gm.IsMenuScene() || Event.current.type != EventType.Repaint)
		{
			return;
		}
		float k = Screen.height / 1080f;
		float sw = Screen.width / k;
		Matrix4x4 old = GUI.matrix;
		GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(k, k, 1f));
		float a = Mathf.Clamp01(shownFor / 0.3f);
		const float s = 0.42f;
		const float inner = 760f;
		List<string> lines = art.Wrap(Notice, inner / s);
		lines.Add("");
		lines.Add("Press any key");
		float h = 32f + lines.Count * art.LineHeight * s;
		Rect card = new Rect(sw / 2f - inner / 2f - 20f, 60f, inner + 40f, h);
		GUI.color = Color.white;
		GUI.DrawTexture(card, Texture2D.whiteTexture, ScaleMode.StretchToFill, alphaBlend: true, 0f, new Color(0.06f, 0.06f, 0.12f, 0.92f * a), 0f, 14f);
		float y = card.y + 16f;
		foreach (string l in lines)
		{
			art.DrawOutlined(l, card.x + 20f, y, s, new Color(0.95f, 0.95f, 1f, a), new Color(0f, 0f, 0f, a), 1.5f);
			y += art.LineHeight * s;
		}
		GUI.matrix = old;
		GUI.color = Color.white;
	}

	private void Update()
	{
		GameManager gm = GameManager.SilentInstance;
		if (Notice == null || gm == null || !gm.IsMenuScene())
		{
			shownFor = 0f;
			return;
		}
		shownFor += Time.unscaledDeltaTime;
		bool pressed = false;
		try
		{
			UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
			UnityEngine.InputSystem.Mouse ms = UnityEngine.InputSystem.Mouse.current;
			InControl.InputDevice pad = InControl.InputManager.ActiveDevice;
			pressed = (kb != null && kb.anyKey.wasPressedThisFrame) || (ms != null && ms.leftButton.wasPressedThisFrame) || (pad != null && pad.AnyButtonWasPressed);
		}
		catch (Exception)
		{
		}
		if (shownFor > 1.5f && (pressed || shownFor > 20f))
		{
			Notice = null;
		}
	}
}
