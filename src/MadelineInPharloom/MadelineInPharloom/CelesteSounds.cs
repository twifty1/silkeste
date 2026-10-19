using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MadelineInPharloom.Sheets;

namespace MadelineInPharloom;

public static class CelesteSounds
{
	private static readonly Dictionary<int, string> Surfaces = new Dictionary<int, string>
	{
		{ 3, "00_dirt" },
		{ 4, "04_snow" },
		{ 5, "03_wood" },
		{ 7, "01_metalgirder" },
		{ 8, "01_brick" },
		{ 17, "03_linens" },
		{ 25, "04_drygrass" },
		{ 33, "07_grass" }
	};

	private static readonly Dictionary<string, string> Named = new Dictionary<string, string>
	{
		{ "dash_right", "char_mad_dash_red_right" },
		{ "dash_left", "char_mad_dash_red_left" },
		{ "dash_pink_right", "char_mad_dash_pink_right" },
		{ "dash_pink_left", "char_mad_dash_pink_left" },
		{ "jump", "char_mad_jump" },
		{ "jump_super", "char_mad_jump_super" },
		{ "jump_superslide", "char_mad_jump_superslide" },
		{ "jump_superwall", "char_mad_jump_superwall" },
		{ "jump_wall_left", "char_mad_jump_wall_left" },
		{ "jump_wall_right", "char_mad_jump_wall_right" },
		{ "jump_climb_left", "char_mad_jump_wall_climblayer_left" },
		{ "jump_climb_right", "char_mad_jump_wall_climblayer_right" },
		{ "grab_letgo", "char_mad_grab_letgo" },
		{ "climb_ledge", "char_mad_climb_ledge" },
		{ "predeath", "char_mad_predeath" },
		{ "death", "char_mad_death" },
		{ "refill", "game_gen_diamond_touch" },
		{ "idle_scratch", "char_mad_idle_scratch" },
		{ "idle_sneeze", "char_mad_idle_sneeze" },
		{ "idle_crackknuckles", "char_mad_idle_crackknuckles" },
		{ "campfire_stand", "char_mad_campfire_stand" },
		{ "revive", "char_mad_revive" },
		{ "feather_get", "game_06_feather_get" },
		{ "feather_state_end", "game_06_feather_state_end" },
		{ "postcard_in", "ui_main_postcard_ch1_in" },
		{ "postcard_out", "ui_main_postcard_ch1_out" },
		{ "button_lowkey", "ui_main_button_lowkey" },
		{ "glider_engage", "game_10_glider_engage" },
		{ "menu_pause", "ui_game_pause" },
		{ "menu_unpause", "ui_game_unpause" },
		{ "menu_rollover_up", "ui_main_roll_up" },
		{ "menu_rollover_down", "ui_main_roll_down" },
		{ "menu_toggle_on", "ui_main_button_toggle_on" },
		{ "menu_toggle_off", "ui_main_button_toggle_off" },
		{ "menu_button_select", "ui_main_button_select" },
		{ "card_in", "ui_world_chapter_pane_expand" },
		{ "card_out", "ui_world_chapter_pane_contract" },
		{ "golden_get", "game_gen_strawberry_gold_get" },
		{ "golden_laugh", "game_gen_strawberry_laugh" },
		{ "golden_flyaway", "game_gen_strawberry_flyaway" }
	};

	private static readonly Dictionary<string, string> Babble = new Dictionary<string, string>
	{
		{ "talk_madeline_normal", "char_dia_madeline_normal" },
		{ "talk_madeline_distracted", "char_dia_madeline_distracted" },
		{ "talk_madeline_determined", "char_dia_madeline_determined" },
		{ "talk_madeline_surprised", "char_dia_madeline_surprised" },
		{ "talk_badeline_normal", "char_dia_badeline_normal" },
		{ "talk_badeline_scoff", "char_dia_badeline_scoff" },
		{ "talk_badeline_serious", "char_dia_badeline_serious" },
		{ "talk_badeline_angry", "char_dia_badeline_angry" }
	};

	private static readonly Dictionary<string, float> Gains = new Dictionary<string, float>
	{
		{ "button_lowkey", 0.154f },
		{ "campfire_stand", 0.046f },
		{ "card_in", 0.127f },
		{ "card_out", 0.128f },
		{ "climb_ledge", 0.095f },
		{ "dash_left", 0.336f },
		{ "dash_pink_left", 0.326f },
		{ "dash_pink_right", 0.332f },
		{ "dash_right", 0.332f },
		{ "death", 0.37f },
		{ "feather_get", 0.228f },
		{ "feather_state_end", 0.229f },
		{ "footstep_17_a", 0.151f },
		{ "footstep_17_b", 0.167f },
		{ "footstep_25_a", 0.144f },
		{ "footstep_25_b", 0.146f },
		{ "footstep_3_a", 0.118f },
		{ "footstep_3_b", 0.142f },
		{ "footstep_33_a", 0.124f },
		{ "footstep_33_b", 0.14f },
		{ "footstep_4_a", 0.152f },
		{ "footstep_4_b", 0.155f },
		{ "footstep_5_a", 0.081f },
		{ "footstep_5_b", 0.099f },
		{ "footstep_7_a", 0.143f },
		{ "footstep_7_b", 0.156f },
		{ "footstep_8_a", 0.082f },
		{ "footstep_8_b", 0.115f },
		{ "glider_engage", 0.107f },
		{ "glider_movement", 0.236f },
		{ "golden_flyaway", 0.445f },
		{ "golden_get", 0.372f },
		{ "golden_laugh", 0.303f },
		{ "grab_17", 0.231f },
		{ "grab_25", 0.184f },
		{ "grab_3", 0.205f },
		{ "grab_33", 0.221f },
		{ "grab_4", 0.287f },
		{ "grab_7", 0.191f },
		{ "grab_8", 0.174f },
		{ "grab_letgo", 0.129f },
		{ "handhold_17", 0.319f },
		{ "handhold_25", 0.196f },
		{ "handhold_3", 0.234f },
		{ "handhold_33", 0.171f },
		{ "handhold_4", 0.284f },
		{ "handhold_7", 0.19f },
		{ "handhold_8", 0.119f },
		{ "idle_crackknuckles", 0.052f },
		{ "idle_scratch", 0.022f },
		{ "idle_sneeze", 0.047f },
		{ "jump", 0.142f },
		{ "jump_climb_left", 0.149f },
		{ "jump_climb_right", 0.148f },
		{ "jump_super", 0.1f },
		{ "jump_superslide", 0.097f },
		{ "jump_superwall", 0.095f },
		{ "jump_wall_left", 0.149f },
		{ "jump_wall_right", 0.151f },
		{ "landing_17", 0.166f },
		{ "landing_25", 0.167f },
		{ "landing_3", 0.182f },
		{ "landing_33", 0.156f },
		{ "landing_4", 0.12f },
		{ "landing_5", 0.095f },
		{ "landing_7", 0.148f },
		{ "landing_8", 0.126f },
		{ "menu_button_select", 0.281f },
		{ "menu_pause", 0.19f },
		{ "menu_rollover_down", 0.129f },
		{ "menu_rollover_up", 0.128f },
		{ "menu_toggle_off", 0.281f },
		{ "menu_toggle_on", 0.274f },
		{ "menu_unpause", 0.195f },
		{ "postcard_in", 0.259f },
		{ "postcard_out", 0.26f },
		{ "predeath", 0.417f },
		{ "refill", 0.458f },
		{ "revive", 0.415f },
		{ "talk_badeline_angry", 1.012f },
		{ "talk_badeline_normal", 1.614f },
		{ "talk_badeline_scoff", 1.005f },
		{ "talk_badeline_serious", 1.102f },
		{ "talk_madeline_determined", 0.744f },
		{ "talk_madeline_distracted", 0.62f },
		{ "talk_madeline_normal", 0.578f },
		{ "talk_madeline_surprised", 0.905f },
		{ "wallslide_17", 0.202f },
		{ "wallslide_3", 0.202f },
		{ "wallslide_4", 0.202f },
		{ "wallslide_5", 0.202f },
		{ "wallslide_7", 0.202f },
		{ "wallslide_8", 0.202f }
	};

	private static readonly Dictionary<string, string[]> Sequences = new Dictionary<string, string[]> { 
	{
		"glider_movement",
		new string[6] { "game_10_glider_float_loop_1", "game_10_glider_float_loop_2", "game_10_glider_float_loop_3", "game_10_glider_float_loop_4", "game_10_glider_float_loop_5", "game_10_glider_float_loop_6" }
	} };

	public static int Render(string celesteDir, string sfxDir, StringBuilder log)
	{
		Directory.CreateDirectory(sfxDir);
		string path = Path.Combine(celesteDir, Path.Combine("Content", Path.Combine("FMOD", "Desktop")));
		List<CelesteBank> list = new List<CelesteBank>();
		string[] array = new string[3] { "sfx.bank", "ui.bank", "dlc_sfx.bank" };
		foreach (string text in array)
		{
			try
			{
				list.Add(new CelesteBank(Path.Combine(path, text)));
			}
			catch (Exception ex)
			{
				log.AppendLine(text + ": " + ex.Message);
			}
		}
		int num = 0;
		int num2 = 0;
		SoundRow[] all = Sounds.All;
		for (int i = 0; i < all.Length; i++)
		{
			SoundRow row = all[i];
			string path2 = Path.Combine(sfxDir, row.Id + ".wav");
			try
			{
				int ch;
				int rate;
				float[] array2 = Make(row, list, out ch, out rate);
				if (array2 == null || array2.Length == 0)
				{
					num++;
					log.AppendLine(row.Id + ": no sample found");
					continue;
				}
				if (row.HoldSeconds > 0f && Babble.ContainsKey(row.Id))
				{
					array2 = Cut(array2, ch, (int)(row.HoldSeconds * (float)rate));
				}
				if (Gains.TryGetValue(row.Id, out var value))
				{
					for (int j = 0; j < array2.Length; j++)
					{
						array2[j] *= value;
					}
				}
				WriteWav(path2, array2, ch, rate);
				num2++;
			}
			catch (Exception ex2)
			{
				num++;
				log.AppendLine(row.Id + ": " + ex2.Message);
			}
		}
		log.AppendLine("rendered " + num2 + " of " + Sounds.All.Length + " from the banks");
		if (num == 0)
		{
			File.WriteAllText(Path.Combine(sfxDir, "ready.json"), "{\"sounds\":" + num2 + ",\"from\":\"banks\"}");
		}
		return num;
	}

	private static float[] Make(SoundRow row, List<CelesteBank> banks, out int ch, out int rate)
	{
		ch = 1;
		rate = 48000;
		if (Sequences.TryGetValue(row.Id, out var value))
		{
			List<float[]> list = new List<float[]>();
			string[] array = value;
			foreach (string name in array)
			{
				int ch2;
				int rate2;
				float[] array2 = DecodeName(banks, name, out ch2, out rate2);
				if (array2 != null && (list.Count <= 0 || ch2 == ch))
				{
					ch = ch2;
					rate = rate2;
					list.Add(array2);
				}
			}
			if (list.Count == 0)
			{
				return null;
			}
			int chs = ch;
			list.Sort((float[] x, float[] y) => Pitch(x, chs).CompareTo(Pitch(y, chs)));
			int num = (int)(0.245f * (float)rate);
			int num2 = num * list.Count;
			int val = 0;
			foreach (float[] item in list)
			{
				val = Math.Max(val, item.Length / ch);
			}
			float[] array3 = new float[num2 * ch];
			for (int num3 = 0; num3 < list.Count; num3++)
			{
				float[] array4 = list[num3];
				for (int num4 = 0; num4 < array4.Length / ch; num4++)
				{
					int num5 = (num3 * num + num4) % num2;
					for (int num6 = 0; num6 < ch; num6++)
					{
						array3[num5 * ch + num6] += array4[num4 * ch + num6];
					}
				}
			}
			return array3;
		}
		if (Babble.TryGetValue(row.Id, out var value2))
		{
			List<float> list2 = new List<float>();
			List<float[]> list3 = new List<float[]>();
			string[] array = new string[3] { "_mid_A", "_mid_B", "_mid_C" };
			foreach (string text in array)
			{
				int ch3;
				int rate3;
				float[] array5 = DecodeName(banks, value2 + text, out ch3, out rate3);
				if (array5 != null && (list3.Count <= 0 || ch3 == ch))
				{
					ch = ch3;
					rate = rate3;
					list3.Add(array5);
				}
			}
			if (list3.Count == 0)
			{
				return null;
			}
			int[] array6 = new int[9] { 0, 1, 2, 1, 0, 2, 0, 2, 1 };
			int num7 = 0;
			while ((float)list2.Count < row.HoldSeconds * (float)rate * (float)ch && num7 < 40)
			{
				list2.AddRange(list3[array6[num7 % array6.Length] % list3.Count]);
				num7++;
			}
			return list2.ToArray();
		}
		if (!Named.TryGetValue(row.Id, out var value3))
		{
			string text2 = row.Id.Substring(0, row.Id.IndexOf('_'));
			if (!Surfaces.TryGetValue(row.SurfaceIndex, out var value4))
			{
				return null;
			}
			switch (text2)
			{
			case "footstep":
				value3 = "char_mad_foot_" + value4 + (row.Id.EndsWith("_b", StringComparison.Ordinal) ? "_04" : "_01");
				break;
			case "landing":
				value3 = "char_mad_land_" + value4;
				break;
			case "grab":
				value3 = "char_mad_grab_" + value4;
				break;
			case "handhold":
				value3 = "char_mad_handhold_" + value4;
				break;
			case "wallslide":
				value3 = "char_mad_wallslide_general";
				break;
			default:
				return null;
			}
		}
		return DecodeName(banks, value3, out ch, out rate);
	}

	private static float[] DecodeName(List<CelesteBank> banks, string name, out int ch, out int rate)
	{
		ch = 1;
		rate = 48000;
		foreach (CelesteBank bank in banks)
		{
			CelesteBank.Sample sample = bank.Find(name);
			if (sample != null)
			{
				return bank.Decode(sample, out ch, out rate);
			}
		}
		return null;
	}

	private static float Pitch(float[] pcm, int ch)
	{
		int num = pcm.Length / ch;
		int num2 = 0;
		int num3 = 0;
		float num4 = 0f;
		for (int i = 0; i < num; i++)
		{
			num4 = Math.Max(num4, Math.Abs(pcm[i * ch]));
		}
		float num5 = 0f;
		for (int j = 0; j < num; j++)
		{
			float num6 = pcm[j * ch];
			if (Math.Abs(num6) < num4 * 0.1f)
			{
				num5 = num6;
				continue;
			}
			num3++;
			if (num6 >= 0f != num5 >= 0f)
			{
				num2++;
			}
			num5 = num6;
		}
		if (num3 <= 0)
		{
			return 0f;
		}
		return (float)num2 / (float)num3;
	}

	private static float[] Cut(float[] pcm, int ch, int frames)
	{
		int num = Math.Min(pcm.Length, frames * ch);
		if (num == pcm.Length)
		{
			return pcm;
		}
		float[] array = new float[num];
		Array.Copy(pcm, array, num);
		int num2 = Math.Min(num / ch, 2400);
		for (int i = 0; i < num2; i++)
		{
			for (int j = 0; j < ch; j++)
			{
				array[(num / ch - 1 - i) * ch + j] *= (float)i / (float)num2;
			}
		}
		return array;
	}

	private static void WriteWav(string path, float[] pcm, int ch, int rate)
	{
		using BinaryWriter binaryWriter = new BinaryWriter(File.Create(path));
		int num = pcm.Length;
		binaryWriter.Write(Encoding.ASCII.GetBytes("RIFF"));
		binaryWriter.Write(36 + num * 2);
		binaryWriter.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
		binaryWriter.Write(16);
		binaryWriter.Write((short)1);
		binaryWriter.Write((short)ch);
		binaryWriter.Write(rate);
		binaryWriter.Write(rate * ch * 2);
		binaryWriter.Write((short)(ch * 2));
		binaryWriter.Write((short)16);
		binaryWriter.Write(Encoding.ASCII.GetBytes("data"));
		binaryWriter.Write(num * 2);
		foreach (float num2 in pcm)
		{
			binaryWriter.Write((short)Math.Max(-32768, Math.Min(32767, (int)(num2 * 32767f))));
		}
	}
}
