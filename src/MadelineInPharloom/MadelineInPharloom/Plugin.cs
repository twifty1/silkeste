using System.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using System.Threading;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using GlobalEnums;
using HarmonyLib;
using InControl;
using MadelineInPharloom.Sheets;
using Microsoft.Win32;
using UnityEngine;

namespace MadelineInPharloom;

[BepInPlugin("melty.madelineinpharloom", "Silkeste", "1.9.0")]
public class Plugin : BaseUnityPlugin
{
	public const string Id = "melty.madelineinpharloom";

	public const string Version = "2.1.0";

	public static ManualLogSource Log;

	public static readonly MadelineController Controller = new MadelineController();

	private static AssistMode Assist;

	public static bool NextSkinRequested;

	private static bool skinSettingSeeded;

	private static bool lastBackpack = true;

	private readonly Dictionary<string, CelesteAssets> noBackpack = new Dictionary<string, CelesteAssets>();

	private static string lastOutfit;

	public static Func<SkinRow, CelesteAssets> PreviewAssets;

	private static CelesteAssets.Prepared Prebuilt;

	/// <summary>Celeste's Gameplay atlas, once read (the Tech Tutorial's tiles and objects come from it).</summary>
	internal static CelesteAssets.Prepared Atlas => Prebuilt;

	/// <summary>The player's Celeste folder (null when not found).</summary>
	internal static string CelesteDir;

	public static bool Ready;

	private static volatile CelesteAssets.Prepared prepared;

	private static GuiArt guiArt;

	private static string prepareError;

	private CelesteAssets assets;

	private readonly Dictionary<string, CelesteAssets> skins = new Dictionary<string, CelesteAssets>();

	private SkinRow skin = Skins.Madeline;

	private static int hurtHp = -1;

	private static float nextEh;

	private static string voice = Skins.Madeline.Voice;

	private string skinFile;

	private MadelineView view;

	private HeroController viewHero;

	private static bool CelesteMissing;

	private static string clashNotice;

	private static readonly HashSet<MethodBase> ourTargets = new HashSet<MethodBase>();

	private float missingShown;

	private static string controlsFile;

	private static readonly AccessTools.FieldRef<InputHandler, GameSettings> InputSettings = AccessTools.FieldRefAccess<InputHandler, GameSettings>("gs");

	private static HashSet<tk2dSpriteCollectionData> hornetCollections;

	private static readonly Dictionary<string, KeyValuePair<string, string>> PatchFor = new Dictionary<string, KeyValuePair<string, string>>
	{
		{
			Hooks.PlayerController.Id,
			P("Postfix", "FixedUpdatePostfix")
		},
		{
			Hooks.BlockJump.Id,
			P("Prefix", "BlockWhileActive")
		},
		{
			Hooks.BlockDoubleJump.Id,
			P("Prefix", "BlockWhileActive")
		},
		{
			Hooks.BlockWallJump.Id,
			P("Prefix", "BlockWhileActive")
		},
		{
			Hooks.BlockWallSlide.Id,
			P("Prefix", "BlockWhileActive")
		},
		{
			Hooks.BlockFloat.Id,
			P("Prefix", "BlockWhileActive")
		},
		{
			Hooks.BlockSprintDash.Id,
			P("Prefix", "BlockWhileActive")
		},
		{
			Hooks.AttackInput.Id,
			P("Prefix", "BlockWhileActive")
		},
		{
			Hooks.BlockHardLanding.Id,
			P("Prefix", "BlockWhileActive")
		},
		{
			Hooks.BlockNailCharge.Id,
			P("Prefix", "BlockWhileActive")
		},
		{
			Hooks.BlockClamber.Id,
			P("Prefix", "BlockWhileActive")
		},
		{
			Hooks.EnemyContactDamage.Id,
			P("Prefix", "TakeDamagePrefix")
		},
		{
			Hooks.DelayHazardFade.Id,
			P("Prefix", "DelayHazardFadePrefix")
		},
		{
			Hooks.ShortenHazardReload.Id,
			P("Transpiler", "HazardReloadTranspiler")
		},
		{
			Hooks.DelayDeathFade.Id,
			P("Prefix", "DelayFadePrefix")
		},
		{
			Hooks.SkipWalkIn.Id,
			P("Prefix", "SkipWalkInPrefix")
		},
		{
			Hooks.HornetVoice.Id,
			P("Prefix", "HornetVoicePrefix")
		},
		{
			Hooks.UpdraftSpeed.Id,
			P("Postfix", "UpdraftPostfix")
		},
		{
			Hooks.NpcVoice.Id,
			P("Prefix", "NpcVoicePrefix")
		},
		{
			Hooks.ControllerLayout.Id,
			P("Postfix", "ControllerLayoutPostfix")
		},
		{
			Hooks.BossTitle.Id,
			P("Postfix", "BossTitlePostfix")
		},
		{
			Hooks.MuteHornetSelect.Id,
			P("Prefix", "MuteClipPrefix")
		},
		{
			Hooks.MuteHornetSelectAny.Id,
			P("Prefix", "MuteClipPrefix")
		},
		{
			Hooks.MuteHornetSelectRandom.Id,
			P("Prefix", "MuteClipPrefix")
		},
		{
			Hooks.MuteHornetPlay.Id,
			P("Prefix", "MutePlayPrefix")
		},
		{
			Hooks.MuteHornetPlayPitch.Id,
			P("Prefix", "MutePlayPrefix")
		}
	};

	private static readonly HashSet<RandomAudioClipTable> hornetVoice = new HashSet<RandomAudioClipTable>();

	private static HeroController voiceHero;

	private static readonly string[] HornetVoiceFields = new string[9] { "woundAudioTable", "woundHeavyAudioTable", "woundFrostAudioTable", "hazardDamageAudioTable", "pitFallAudioTable", "deathAudioTable", "gruntAudioTable", "attackAudioTable", "warriorRageAttackAudioTable" };

	private static readonly AccessTools.FieldRef<AudioSource> currentVoiceSource = AccessTools.StaticFieldRefAccess<AudioSource>(AccessTools.Field(typeof(NPCSpeakingAudio), "_currentPlayingSource"));

	private static readonly MethodInfo OneHitDeath = AccessTools.Method(typeof(HeroController), "DieFromHazard");

	// HeroController.Die(nonLethal, frostDeath): a non-lethal death respawns at the bench without a cocoon
	private static readonly MethodInfo BenchDeath = AccessTools.Method(typeof(HeroController), "Die");

	private void EnsureSkin(SkinRow k)
	{
		if (!skins.ContainsKey(k.Id) && Prebuilt != null)
		{
			skins[k.Id] = CelesteAssets.Build(Prebuilt, k.CelesteSprite, k.Palette, null, k.FallbackPalette, k.HairNormal);
			if (k.NoBackpack == "same")
			{
				noBackpack[k.Id] = CelesteAssets.Build(Prebuilt, "player_no_backpack", k.Palette, null, "none", k.HairNormal);
			}
			else if (k.NoBackpack != "none")
			{
				noBackpack[k.Id] = CelesteAssets.Build(Prebuilt, k.CelesteSprite, k.Palette, k.NoBackpack, k.FallbackPalette);
			}
		}
	}

	private CelesteAssets SkinAssets(SkinRow k)
	{
		SkinRow k2 = k;
		string o = AssistMode.OutfitName;
		if (o != "Skin default" && k.CelesteSprite == "player" && k.Style != "pico8" && !MadelineView.Masked(k.Style))
		{
			SkinRow skinRow = Array.Find(Skins.All, (SkinRow x) => x.Name == o);
			if (skinRow.Id != null)
			{
				k2 = skinRow;
			}
		}
		EnsureSkin(k2);
		if ((AssistMode.Backpack && !MadelineView.Masked(k.Style)) || !noBackpack.TryGetValue(k2.Id, out var value))
		{
			return skins[k2.Id];
		}
		return value;
	}

	private void Awake()
	{
		Log = base.Logger;
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MadelineInPharloom");
		skinFile = Path.Combine(text, "skin.cfg");
		controlsFile = Path.Combine(text, "controls.cfg");
		try
		{
			string text2 = (File.Exists(skinFile) ? File.ReadAllText(skinFile).Trim() : "");
			SkinRow[] all = Skins.All;
			for (int i = 0; i < all.Length; i++)
			{
				SkinRow skinRow = all[i];
				if (skinRow.Id == text2)
				{
					skin = skinRow;
				}
			}
		}
		catch
		{
		}
		voice = skin.Voice;
		string celeste = FindCeleste(Path.Combine(text, "settings.cfg"));
		CelesteDir = celeste;
		if (celeste == null)
		{
			Log.LogError("Celeste not found. Melty sets CelesteDir in " + Path.Combine(text, "settings.cfg") + "; Hornet stays as she is.");
			CelesteMissing = true;
			return;
		}
		Log.LogInfo("Reading Madeline from the player's Celeste at " + celeste);
		Thread thread = new Thread((ThreadStart)delegate
		{
			try
			{
				guiArt = GuiArt.Prepare(celeste);
			}
			catch (Exception ex2)
			{
				Log.LogWarning("Death counter art unavailable: " + ex2.Message);
			}
			try
			{
				prepared = CelesteAssets.Prepare(celeste);
			}
			catch (Exception ex3)
			{
				prepareError = ex3.Message;
			}
		});
		thread.IsBackground = true;
		thread.Name = "MadelineAtlas";
		thread.Start();
		Sfx.Start(celeste, Path.Combine(text, "cache"), Path.GetDirectoryName(base.Info.Location));
		ApplyHooks(new Harmony("melty.madelineinpharloom"));
		try
		{
			base.gameObject.AddComponent<CelesteWipe>();
			DeathCounter.DataDir = (PostcardView.DataDir = text);
			base.gameObject.AddComponent<DeathCounter>();
			base.gameObject.AddComponent<PostcardView>();
			base.gameObject.AddComponent<StartingAbilities>();
			base.gameObject.AddComponent<CutsceneFx>();
			base.gameObject.AddComponent<EmoteMenu>();
			base.gameObject.AddComponent<TechTipsGui>();
			base.gameObject.AddComponent<TechRoom>();
			AssistMode.DataDir = (BossCard.DataDir = (SpeedrunTimer.DataDir = text));
			BossCard.ResetFlag = Path.Combine(Path.GetDirectoryName(base.Info.Location), "reset_records.flag");
			Assist = base.gameObject.AddComponent<AssistMode>();
			base.gameObject.AddComponent<BossCard>();
			base.gameObject.AddComponent<BossRematch>();
			base.gameObject.AddComponent<RunLog>();
			base.gameObject.AddComponent<SpeedrunTimer>();
			base.gameObject.AddComponent<TheoCamera>();
			base.gameObject.AddComponent<Achievements>();
			base.gameObject.AddComponent<CelesteTextbox>();
			base.gameObject.AddComponent<CelesteYesNo>();
			base.gameObject.AddComponent<MirrorMode>();
			base.gameObject.AddComponent<OneHitMode>();
			base.gameObject.AddComponent<MadelineTouches>();
			base.gameObject.AddComponent<CelesteFileSelect>();
			base.gameObject.AddComponent<CelesteMenus>();
			ToolItemManager.BoundAttackToolUsed += OnAttackToolUsed;
			Controller.DashBegan += delegate
			{
				if (Assist != null)
				{
					Assist.BeginDashFreeze();
				}
				BossCard.OnDash();
			};
			Log.LogInfo("Screens ready: postcards, death counter, Settings (F1), boss cards, speedrun timer (F3)");
		}
		catch (Exception ex)
		{
			Log.LogError("Could not set up the screens: " + ex);
		}
	}

	private static string FindCeleste(string settingsFile)
	{
		try
		{
			string[] array;
			if (File.Exists(settingsFile))
			{
				array = File.ReadAllLines(settingsFile);
				foreach (string text in array)
				{
					int num = text.IndexOf('=');
					if (num > 0 && text.Substring(0, num).Trim() == "CelesteDir")
					{
						string text2 = CelesteIn(text.Substring(num + 1));
						if (text2 != null)
						{
							return text2;
						}
					}
				}
			}
			List<string> list = new List<string> { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam") };
			array = new string[2] { "HKEY_CURRENT_USER\\Software\\Valve\\Steam", "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Valve\\Steam" };
			foreach (string keyName in array)
			{
				string[] array2 = new string[2] { "SteamPath", "InstallPath" };
				foreach (string valueName in array2)
				{
					try
					{
						string text3 = Registry.GetValue(keyName, valueName, null) as string;
						if (!string.IsNullOrEmpty(text3))
						{
							list.Add(text3.Replace('/', '\\'));
						}
					}
					catch (Exception)
					{
					}
				}
			}
			List<string> list2 = new List<string>();
			foreach (string item in list)
			{
				list2.Add(item);
				string path = Path.Combine(item, "steamapps/libraryfolders.vdf");
				if (!File.Exists(path))
				{
					continue;
				}
				foreach (Match item2 in Regex.Matches(File.ReadAllText(path), "\"path\"\\s+\"([^\"]+)\""))
				{
					list2.Add(item2.Groups[1].Value.Replace("\\\\", "\\"));
				}
			}
			foreach (string item3 in list2)
			{
				string text4 = Path.Combine(item3, "steamapps/common/Celeste");
				if (IsCeleste(text4))
				{
					return text4;
				}
			}
			string path2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic\\EpicGamesLauncher\\Data\\Manifests");
			if (Directory.Exists(path2))
			{
				array = Directory.GetFiles(path2, "*.item");
				for (int i = 0; i < array.Length; i++)
				{
					Match match2 = Regex.Match(File.ReadAllText(array[i]), "\"InstallLocation\"\\s*:\\s*\"([^\"]+)\"");
					if (match2.Success)
					{
						string text5 = match2.Groups[1].Value.Replace("\\\\", "\\");
						if (IsCeleste(text5))
						{
							return text5;
						}
					}
				}
			}
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
			string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
			array = new string[5]
			{
				Path.Combine(folderPath, "Epic Games\\Celeste"),
				Path.Combine(folderPath2, "GOG Galaxy\\Games\\Celeste"),
				"C:\\GOG Games\\Celeste",
				Path.Combine(folderPath, "Celeste"),
				Path.Combine(folderPath2, "Celeste")
			};
			foreach (string text6 in array)
			{
				if (IsCeleste(text6))
				{
					return text6;
				}
			}
		}
		catch (Exception ex2)
		{
			Log.LogWarning("Looking for Celeste: " + ex2.Message);
		}
		return null;
	}

	private void Start()
	{
		try
		{
			List<string> list = (from p in Chainloader.PluginInfos.Values
				where p.Metadata.GUID != "melty.madelineinpharloom"
				select p.Metadata.Name + " " + p.Metadata.Version).ToList();
			Log.LogInfo((list.Count == 0) ? "Other mods: none" : ("Other mods: " + string.Join(", ", list.ToArray())));
			HashSet<string> hashSet = new HashSet<string>();
			foreach (MethodBase ourTarget in ourTargets)
			{
				Patches patchInfo = Harmony.GetPatchInfo(ourTarget);
				if (patchInfo == null)
				{
					continue;
				}
				foreach (string owner in patchInfo.Owners)
				{
					if (owner != "melty.madelineinpharloom" && !owner.StartsWith("silkeste", StringComparison.OrdinalIgnoreCase))
					{
						Log.LogWarning("Another mod (" + owner + ") also changes " + ourTarget.DeclaringType?.Name + "." + ourTarget.Name + "; it may clash with Silkeste");
						hashSet.Add(OwnerName(owner));
					}
				}
			}
			if (hashSet.Count > 0)
			{
				clashNotice = "Silkeste: " + string.Join(", ", hashSet.ToArray()) + ((hashSet.Count == 1) ? " changes" : " change") + " Hornet too and may clash with Silkeste.\nIf something breaks, try turning " + ((hashSet.Count == 1) ? "it" : "them") + " off.";
			}
		}
		catch (Exception ex)
		{
			Log.LogWarning("Couldn't check other mods: " + ex.Message);
		}
	}

	private static string OwnerName(string harmonyId)
	{
		foreach (PluginInfo value in Chainloader.PluginInfos.Values)
		{
			if (value.Metadata.GUID == harmonyId)
			{
				return value.Metadata.Name;
			}
		}
		return harmonyId;
	}

	private void OnGUI()
	{
		if ((CelesteMissing || clashNotice != null) && Event.current.type == EventType.Repaint && !((missingShown += Time.unscaledDeltaTime) > 25f))
		{
			GUIStyle style = new GUIStyle(GUI.skin.label)
			{
				fontSize = Math.Max(14, Screen.height / 45),
				wordWrap = true
			};
			string text = (CelesteMissing ? "Silkeste couldn't find Celeste on this PC, so you're still playing as Hornet.\nInstall Celeste, then start the game from Melty again." : clashNotice);
			Rect position = new Rect((float)Screen.width * 0.15f, (float)Screen.height * 0.04f, (float)Screen.width * 0.7f, (float)Screen.height * 0.2f);
			GUI.color = Color.black;
			GUI.Label(new Rect(position.x + 2f, position.y + 2f, position.width, position.height), text, style);
			GUI.color = Color.white;
			GUI.Label(position, text, style);
		}
	}

	// The folder Melty passes may be quoted, hold %VARIABLES%, point at Celeste.exe, or sit one
	// level above the game (some store layouts); find the folder that holds Celeste's Content.
	private static string CelesteIn(string dir)
	{
		try
		{
			dir = Environment.ExpandEnvironmentVariables((dir ?? "").Trim().Trim('"')).Replace('/', '\\').TrimEnd('\\');
			if (dir.Length == 0)
			{
				return null;
			}
			if (File.Exists(dir))
			{
				dir = Path.GetDirectoryName(dir);
			}
			if (IsCeleste(dir))
			{
				return dir;
			}
			if (Directory.Exists(dir))
			{
				string[] directories = Directory.GetDirectories(dir);
				foreach (string text in directories)
				{
					if (IsCeleste(text))
					{
						return text;
					}
				}
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private static bool IsCeleste(string dir)
	{
		if (!string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "Content/Graphics/Atlases/Gameplay.meta")))
		{
			return File.Exists(Path.Combine(dir, "Content/Graphics/Sprites.xml"));
		}
		return false;
	}

	private static void SetDefaultControlsOnce()
	{
		if (controlsFile == null)
		{
			return;
		}
		InputHandler instance = ManagerSingleton<InputHandler>.Instance;
		GameSettings gameSettings = ((instance != null) ? InputSettings(instance) : null);
		if (gameSettings == null)
		{
			return;
		}
		string path = controlsFile;
		controlsFile = null;
		try
		{
			if (!File.Exists(path))
			{
				gameSettings.upKey = "W";
				gameSettings.leftKey = "A";
				gameSettings.downKey = "S";
				gameSettings.rightKey = "D";
				gameSettings.jumpKey = "Space";
				gameSettings.dashKey = "LeftShift";
				gameSettings.castKey = "E";
				gameSettings.dreamNailKey = "F";
				gameSettings.superDashKey = "Q";
				gameSettings.inventoryToolsKey = "R";
				gameSettings.attackKey = "LeftButton";
				gameSettings.quickCastKey = "RightButton";
				gameSettings.SaveKeyboardSettings();
				instance.ResetAllControllerButtonBindings();
				instance.ResetDefaultControllerButtonBindings();
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllText(path, "default_controls_applied=1\n");
				Log.LogInfo("Default controls set (first run only): WASD, Space jump, mouse dash and tools, Shift grab; controller R1 dash, Square tools, rest Silksong's");
			}
		}
		catch (Exception ex)
		{
			Log.LogWarning("Could not set the default controls: " + ex.Message);
		}
	}

	private void Update()
	{
		Sfx.Pump();
		SetDefaultControlsOnce();
		if (assets == null)
		{
			if (prepareError != null)
			{
				Log.LogError("Could not read Madeline from Celeste: " + prepareError);
				prepareError = null;
				base.enabled = false;
				return;
			}
			if (prepared == null)
			{
				return;
			}
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				Prebuilt = prepared;
				EnsureSkin(Skins.Madeline);
				SkinRow skinRow = Array.Find(Skins.All, (SkinRow k) => k.Name == AssistMode.SkinName);
				if (skinRow.Id != null)
				{
					skin = skinRow;
				}
				assets = SkinAssets(skin);
				lastBackpack = AssistMode.Backpack;
				lastOutfit = AssistMode.OutfitName;
				BossCard.GoldFrames = skins[Skins.Madeline.Id].GoldIdle;
				if (guiArt != null)
				{
					guiArt.Build();
					DeathCounter.Art = (PostcardView.Art = (AssistMode.Art = (BossCard.Art = (SpeedrunTimer.Art = guiArt))));
				}
				PreviewAssets = SkinAssets;
				Ready = true;
				Log.LogInfo("Madeline loaded: " + assets.Anims.Count + " animations from Celeste in " + stopwatch.ElapsedMilliseconds + " ms, current " + skin.Name + " (other skins build when first worn)");
			}
			catch (Exception ex)
			{
				Log.LogError("Could not build Madeline: " + ex);
				base.enabled = false;
				return;
			}
		}
		HeroController silentInstance = HeroController.UnsafeInstance;
		if (!(silentInstance == null))
		{
			ReadHornetVoice(silentInstance);
			InputHandler instance = ManagerSingleton<InputHandler>.Instance;
			if (instance != null && instance.inputActions != null)
			{
				Controller.ReadInput(instance.inputActions);
			}
		}
	}

	private void LateUpdate()
	{
		if (!Ready)
		{
			return;
		}
		HeroController silentInstance = HeroController.UnsafeInstance;
		GameManager instance = GameManager.instance;
		if (silentInstance == null || instance == null || instance.GameState == GameState.MAIN_MENU || instance.IsMenuScene() || instance.IsTitleScreenScene())
		{
			if (view != null)
			{
				view.Hide();
			}
			return;
		}
		if (view == null || viewHero != silentInstance)
		{
			if (view != null)
			{
				view.Destroy();
			}
			view = new MadelineView(assets, skin, Controller);
			viewHero = silentInstance;
		}
		if (!skinSettingSeeded)
		{
			skinSettingSeeded = true;
			if (!AssistMode.HasSaved("skin"))
			{
				AssistMode.SetSkin(skin.Name, save: false);
			}
		}
		_ = Keyboard.current;
		if (AssistMode.KeyPressed(AssistOptions.KeySkin) || NextSkinRequested)
		{
			NextSkinRequested = false;
			AssistMode.NextLook();
		}
		SkinRow skinRow = Array.Find(Skins.All, (SkinRow k) => k.Name == AssistMode.SkinName);
		bool backpack = AssistMode.Backpack;
		if (skinRow.Id != null && !AssistMode.IsOpen && (skinRow.Id != skin.Id || backpack != lastBackpack || AssistMode.OutfitName != lastOutfit))
		{
			lastBackpack = backpack;
			lastOutfit = AssistMode.OutfitName;
			skin = skinRow;
			voice = skin.Voice;
			assets = SkinAssets(skin);
			view.SetSkin(assets, skin);
			if (Time.timeScale > 0f)
			{
				Controller.HairFlashTimer = 0.12f;
			}
			Sfx.Play(Sounds.Refill.Id, 0.7f);
			try
			{
				File.WriteAllText(skinFile, skin.Id);
			}
			catch (Exception ex)
			{
				Log.LogWarning("Could not save skin: " + ex.Message);
			}
			Log.LogInfo("Skin: " + skin.Name);
		}
		if (silentInstance.cState.dead || silentInstance.cState.hazardDeath)
		{
			HideHornetDeath(silentInstance);
		}
		int health = silentInstance.playerData.health;
		if (hurtHp >= 0 && health < hurtHp && !MadelineView.Masked(skin.Style) && health > 0 && !silentInstance.cState.dead && !silentInstance.cState.hazardDeath && Time.unscaledTime >= nextEh)
		{
			nextEh = Time.unscaledTime + UnityEngine.Random.Range(7f, 10f);
			_ = 0f;
		}
		hurtHp = health;
		view.LateUpdate(silentInstance, Time.deltaTime);
	}

	private static void HideHornetDeath(HeroController h)
	{
		if (hornetCollections == null)
		{
			hornetCollections = new HashSet<tk2dSpriteCollectionData>();
			tk2dSpriteAnimator component = h.GetComponent<tk2dSpriteAnimator>();
			if (component != null && component.Library != null)
			{
				tk2dSpriteAnimationClip[] clips = component.Library.clips;
				foreach (tk2dSpriteAnimationClip tk2dSpriteAnimationClip in clips)
				{
					if (tk2dSpriteAnimationClip == null || tk2dSpriteAnimationClip.frames == null)
					{
						continue;
					}
					tk2dSpriteAnimationFrame[] frames = tk2dSpriteAnimationClip.frames;
					foreach (tk2dSpriteAnimationFrame tk2dSpriteAnimationFrame in frames)
					{
						if (tk2dSpriteAnimationFrame != null && tk2dSpriteAnimationFrame.spriteCollection != null)
						{
							hornetCollections.Add(tk2dSpriteAnimationFrame.spriteCollection);
						}
					}
				}
			}
			Log.LogInfo("Hornet uses " + hornetCollections.Count + " sprite collections; hidden in death effects");
		}
		tk2dBaseSprite[] array = UnityEngine.Object.FindObjectsByType<tk2dBaseSprite>(FindObjectsSortMode.None);
		foreach (tk2dBaseSprite tk2dBaseSprite in array)
		{
			if (!(tk2dBaseSprite.gameObject == h.gameObject) && !(tk2dBaseSprite.Collection == null) && hornetCollections.Contains(tk2dBaseSprite.Collection))
			{
				Renderer component2 = tk2dBaseSprite.GetComponent<Renderer>();
				if (component2 != null)
				{
					component2.forceRenderingOff = true;
				}
			}
		}
	}

	/// <summary>What could be showing Hornet right now (rematch diagnostics): the hero's own sprite state and any other
	/// sprite drawing from Hornet's sprite sheets.</summary>
	internal static string HornetCheck(HeroController h)
	{
		StringBuilder sb = new StringBuilder();
		MeshRenderer r = h.GetComponent<MeshRenderer>();
		tk2dSpriteAnimator an = h.GetComponent<tk2dSpriteAnimator>();
		sb.Append("hero active=" + h.gameObject.activeInHierarchy + " renderer=" + (r != null ? (r.enabled + " off=" + r.forceRenderingOff) : "none") + " clip=" + (an != null && an.CurrentClip != null ? an.CurrentClip.name : "-") + " lib=" + (an != null && an.Library != null ? an.Library.name : "-"));
		if (hornetCollections != null)
		{
			foreach (tk2dBaseSprite sp in UnityEngine.Object.FindObjectsByType<tk2dBaseSprite>(FindObjectsSortMode.None))
			{
				if (sp == null || sp.gameObject == h.gameObject || sp.Collection == null || !hornetCollections.Contains(sp.Collection))
				{
					continue;
				}
				Renderer rr = sp.GetComponent<Renderer>();
				if (rr != null && rr.enabled && !rr.forceRenderingOff)
				{
					sb.Append(" | visible hornet sprite: " + sp.gameObject.name + " parent=" + (sp.transform.parent != null ? sp.transform.parent.name : "-") + " @" + (Vector2)sp.transform.position);
				}
			}
		}
		foreach (tk2dSpriteAnimator other in UnityEngine.Object.FindObjectsByType<tk2dSpriteAnimator>(FindObjectsSortMode.None))
		{
			if (other != null && other.gameObject != h.gameObject && an != null && other.Library == an.Library)
			{
				Renderer rr = other.GetComponent<Renderer>();
				sb.Append(" | other hornet animator: " + other.gameObject.name + " visible=" + (rr != null && rr.enabled && !rr.forceRenderingOff));
			}
		}
		return sb.ToString();
	}

	private static void ReadHornetVoice(HeroController h)
	{
		if (h == null || h == voiceHero)
		{
			return;
		}
		voiceHero = h;
		hornetVoice.Clear();
		string[] hornetVoiceFields = HornetVoiceFields;
		foreach (string text in hornetVoiceFields)
		{
			try
			{
				RandomAudioClipTable value = Traverse.Create(h).Field(text).GetValue<RandomAudioClipTable>();
				if (value != null)
				{
					hornetVoice.Add(value);
				}
			}
			catch (Exception ex)
			{
				Log.LogWarning("Hornet voice " + text + ": " + ex.Message);
			}
		}
	}

	private static bool Muted(RandomAudioClipTable t)
	{
		if (t != null && Ready)
		{
			return hornetVoice.Contains(t);
		}
		return false;
	}

	private static bool MuteClipPrefix(RandomAudioClipTable __instance, ref AudioClip __result)
	{
		if (!Muted(__instance))
		{
			return true;
		}
		__result = null;
		return false;
	}

	private static bool MutePlayPrefix(RandomAudioClipTable __instance)
	{
		return !Muted(__instance);
	}

	private static void OnAttackToolUsed(AttackToolBinding binding)
	{
		Achievements.UsedNonDash();
		try
		{
			if (Ready && Controller != null)
			{
				ToolItem boundAttackTool = ToolItemManager.GetBoundAttackTool(binding, ToolEquippedReadSource.Active);
				if (boundAttackTool != null && boundAttackTool.Type == ToolItemType.Skill)
				{
					Controller.UseSilkSkill(boundAttackTool.name);
				}
			}
		}
		catch (Exception ex)
		{
			Log.LogWarning("Silk skill: " + ex.Message);
		}
	}

	private static void BossTitlePostfix(object __instance)
	{
		try
		{
			BossCard.OnBossTitle(Traverse.Create(__instance).Field("bossTitle").Property("Value")
				.GetValue<string>());
		}
		catch (Exception ex)
		{
			Log.LogWarning("Boss title: " + ex.Message);
		}
	}

	private static void ControllerLayoutPostfix(GameSettings __instance)
	{
		__instance.controllerMapping.attack = InputControlType.RightBumper;
		__instance.controllerMapping.quickCast = InputControlType.Action3;
	}

	private static KeyValuePair<string, string> P(string kind, string method)
	{
		return new KeyValuePair<string, string>(kind, method);
	}

	private static void ApplyHooks(Harmony harmony)
	{
		HookRow[] all = Hooks.All;
		for (int i = 0; i < all.Length; i++)
		{
			HookRow hookRow = all[i];
			if (hookRow.Patch != "prefix" && hookRow.Patch != "postfix" && hookRow.Patch != "transpiler")
			{
				continue;
			}
			string[] array = hookRow.Target.Split(new char[1] { '.' }, 2);
			Type[] parameters = null;
			int num = array[1].IndexOf('(');
			if (num >= 0)
			{
				parameters = Array.ConvertAll(array[1].Substring(num + 1).TrimEnd(new char[1] { ')' }).Split(new char[1] { ',' }), AccessTools.TypeByName);
				array[1] = array[1].Substring(0, num);
			}
			MethodInfo methodInfo = AccessTools.Method(AccessTools.TypeByName(array[0]), array[1], parameters);
			if (hookRow.Patch == "transpiler" && methodInfo != null)
			{
				methodInfo = AccessTools.EnumeratorMoveNext(methodInfo);
			}
			if (!PatchFor.TryGetValue(hookRow.Id, out var value))
			{
				if (hookRow.Target == Hooks.EnemyContactDamage.Target && hookRow.Patch == "prefix")
				{
					Log.LogInfo("hook " + hookRow.Id + ": shares " + Hooks.EnemyContactDamage.Id);
				}
				else
				{
					Log.LogError("hook " + hookRow.Id + " has no patch in code");
				}
				continue;
			}
			if (methodInfo == null)
			{
				Log.LogError("hook " + hookRow.Id + ": " + hookRow.Target + " not found in this Silksong version");
				continue;
			}
			HarmonyMethod harmonyMethod = new HarmonyMethod(typeof(Plugin).GetMethod(value.Value, BindingFlags.Static | BindingFlags.NonPublic));
			harmonyMethod.priority = ((value.Key == "Prefix") ? 800 : ((value.Key == "Transpiler") ? 400 : 0));
			ourTargets.Add(methodInfo);
			if (value.Key == "Prefix")
			{
				harmony.Patch(methodInfo, harmonyMethod);
			}
			else if (value.Key == "Transpiler")
			{
				harmony.Patch(methodInfo, null, null, harmonyMethod);
			}
			else
			{
				harmony.Patch(methodInfo, null, harmonyMethod);
			}
			Log.LogInfo("hook " + hookRow.Id + ": " + value.Key.ToLower() + " on " + hookRow.Target);
		}
	}

	private static void FixedUpdatePostfix(HeroController __instance)
	{
		if (!Ready || __instance != HeroController.UnsafeInstance)
		{
			return;
		}
		try
		{
			Controller.Step(__instance, Time.fixedDeltaTime);
		}
		catch (Exception ex)
		{
			Log.LogError("Madeline step: " + ex);
		}
	}

	private static bool BlockWhileActive(ref bool __result)
	{
		if (!Controller.Active)
		{
			return true;
		}
		__result = false;
		return false;
	}

	private static void DelayFadePrefix(ref float waitTime)
	{
		if (Ready)
		{
			waitTime = CelesteWipe.FadeStart;
		}
	}

	private static void SkipWalkInPrefix(ref bool enterSkip)
	{
		if (Ready)
		{
			enterSkip = true;
		}
	}

	private static bool HornetVoicePrefix()
	{
		if (!Ready)
		{
			return true;
		}
		AudioSource audioSource = currentVoiceSource();
		if (audioSource != null)
		{
			audioSource.Stop();
		}
		Sfx.PlayVoice(voice);
		return false;
	}

	private static void UpdraftPostfix(float speed)
	{
		MadelineController.UpdraftSpeed = speed;
	}

	private static void NpcVoicePrefix()
	{
		Sfx.StopVoice();
	}

	private static void DelayHazardFadePrefix(ref float waitTime)
	{
		if (Ready)
		{
			waitTime = AssistMode.QuickRespawn == 1 ? 0.05f : CelesteWipe.HazardFadeStart;   // quick respawn: no wait
		}
	}

	private static IEnumerable<CodeInstruction> HazardReloadTranspiler(IEnumerable<CodeInstruction> code)
	{
		int replaced = 0;
		foreach (CodeInstruction item in code)
		{
			if (item.opcode == OpCodes.Ldc_R4 && item.operand is float && Math.Abs((float)item.operand - 0.65f) < 0.0001f)
			{
				item.operand = 0.3f;
				replaced++;
			}
			yield return item;
		}
		if (replaced != 1)
		{
			Log.LogWarning("shorten_hazard_reload: expected one 0.65 wait, found " + replaced);
		}
	}

	private static bool TakeDamagePrefix(HeroController __instance, GameObject go, HazardType hazardType)
	{
		if (Controller.TryPogo(hazardType, go, __instance))
		{
			return false;
		}
		if (AssistMode.Invincible && hazardType != HazardType.PIT && hazardType != HazardType.RESPAWN_PIT)
		{
			return false;
		}
		bool flag = hazardType == HazardType.ENEMY || hazardType == HazardType.NON_HAZARD || hazardType == HazardType.EXPLOSION;
		if (Controller.EnemyImmune && flag)
		{
			Achievements.Dodged(go);
			return false;
		}
		// assist.instant_respawn (user 2026-10-17): a hazard (or a One-Hit hit) is a Celeste death, no mask lost:
		// back to the room's entrance, or to the last bench
		int quick = AssistMode.QuickRespawn;
		bool deadly = !flag || AssistMode.OneHit;
		if (quick != 0 && deadly && hazardType != HazardType.RESPAWN_PIT && !__instance.cState.dead && !__instance.cState.hazardDeath && (!flag || __instance.CanTakeDamage()))
		{
			try
			{
				if (quick == 2)
				{
					__instance.StartCoroutine((IEnumerator)BenchDeath.Invoke(__instance, new object[2] { true, false }));
				}
				else
				{
					OneHitMode.UseEntrance(__instance);
					__instance.StartCoroutine((IEnumerator)OneHitDeath.Invoke(__instance, new object[2] { HazardType.SPIKES, 0f }));
				}
				return false;
			}
			catch (Exception ex)
			{
				Log.LogWarning("Quick respawn: " + ex.Message);
			}
		}
		if (AssistMode.OneHit && flag && __instance.CanTakeDamage() && !__instance.cState.dead && !__instance.cState.hazardDeath)
		{
			try
			{
				OneHitMode.UseEntrance(__instance);
				__instance.StartCoroutine((IEnumerator)OneHitDeath.Invoke(__instance, new object[2]
				{
					HazardType.SPIKES,
					0f
				}));
				return false;
			}
			catch (Exception ex)
			{
				Log.LogWarning("One-hit: " + ex.Message);
			}
		}
		return true;
	}
}
