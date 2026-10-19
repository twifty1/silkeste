using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MadelineInPharloom.Sheets;

namespace MadelineInPharloom
{
    /// <summary>
    /// Madeline's sounds (sheets/sounds.json) taken straight from the player's Celeste banks, inside the game process
    /// (user 2026-10-13: the CelesteSfx.exe helper was blocked by Windows Smart App Control on some PCs). Each sound
    /// row maps to the raw sample Celeste's event plays; the WAVs go to the same cache the helper used to fill.
    /// Celeste's events add a little on top (random pitch, layers); these are the plain recordings.
    /// </summary>
    public static class CelesteSounds
    {
        // sounds.json surface_index (Celeste's SurfaceIndex numbers, see sheets/surfaces.json) -> the bank's sample material
        static readonly Dictionary<int, string> Surfaces = new Dictionary<int, string>
        {
            { 3, "00_dirt" }, { 4, "04_snow" }, { 5, "03_wood" }, { 7, "01_metalgirder" },
            { 8, "01_brick" }, { 17, "03_linens" }, { 25, "04_drygrass" }, { 33, "07_grass" },
        };

        // every other row: the sample its event plays
        static readonly Dictionary<string, string> Named = new Dictionary<string, string>
        {
            { "dash_right", "char_mad_dash_red_right" }, { "dash_left", "char_mad_dash_red_left" },
            { "dash_pink_right", "char_mad_dash_pink_right" }, { "dash_pink_left", "char_mad_dash_pink_left" },
            { "jump", "char_mad_jump" }, { "jump_super", "char_mad_jump_super" }, { "jump_superslide", "char_mad_jump_superslide" },
            { "jump_superwall", "char_mad_jump_superwall" }, { "jump_wall_left", "char_mad_jump_wall_left" }, { "jump_wall_right", "char_mad_jump_wall_right" },
            { "jump_climb_left", "char_mad_jump_wall_climblayer_left" }, { "jump_climb_right", "char_mad_jump_wall_climblayer_right" },
            { "grab_letgo", "char_mad_grab_letgo" }, { "climb_ledge", "char_mad_climb_ledge" },
            { "predeath", "char_mad_predeath" }, { "death", "char_mad_death" }, { "refill", "game_gen_diamond_touch" },
            { "idle_scratch", "char_mad_idle_scratch" }, { "idle_sneeze", "char_mad_idle_sneeze" }, { "idle_crackknuckles", "char_mad_idle_crackknuckles" },
            { "campfire_stand", "char_mad_campfire_stand" }, { "revive", "char_mad_revive" },
            { "feather_get", "game_06_feather_get" }, { "feather_state_end", "game_06_feather_state_end" },
            { "postcard_in", "ui_main_postcard_ch1_in" }, { "postcard_out", "ui_main_postcard_ch1_out" }, { "button_lowkey", "ui_main_button_lowkey" },
            { "glider_engage", "game_10_glider_engage" },
            { "menu_pause", "ui_game_pause" }, { "menu_unpause", "ui_game_unpause" },
            { "menu_rollover_up", "ui_main_roll_up" }, { "menu_rollover_down", "ui_main_roll_down" },
            { "menu_toggle_on", "ui_main_button_toggle_on" }, { "menu_toggle_off", "ui_main_button_toggle_off" }, { "menu_button_select", "ui_main_button_select" },
            { "card_in", "ui_world_chapter_pane_expand" }, { "card_out", "ui_world_chapter_pane_contract" },
            { "golden_get", "game_gen_strawberry_gold_get" }, { "golden_laugh", "game_gen_strawberry_laugh" }, { "golden_flyaway", "game_gen_strawberry_flyaway" },
        };

        // the dialogue babble: Celeste strings its "mid" syllables together while a line types out
        static readonly Dictionary<string, string> Babble = new Dictionary<string, string>
        {
            { "talk_madeline_normal", "char_dia_madeline_normal" }, { "talk_madeline_distracted", "char_dia_madeline_distracted" },
            { "talk_madeline_determined", "char_dia_madeline_determined" }, { "talk_madeline_surprised", "char_dia_madeline_surprised" },
            { "talk_badeline_normal", "char_dia_badeline_normal" }, { "talk_badeline_scoff", "char_dia_badeline_scoff" },
            { "talk_badeline_serious", "char_dia_badeline_serious" }, { "talk_badeline_angry", "char_dia_badeline_angry" },
        };

        // each sound's level in Celeste's own mix, measured against FMOD's render of the same event (2026-10-13):
        // the raw recordings are much louder than Celeste plays them
        static readonly Dictionary<string, float> Gains = new Dictionary<string, float>
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
            { "wallslide_8", 0.202f },
        };

        // sounds Celeste's event builds from several pieces in a row: the jellyfish glide flutters through six
        static readonly Dictionary<string, string[]> Sequences = new Dictionary<string, string[]>
        {
            { "glider_movement", new[] { "game_10_glider_float_loop_1", "game_10_glider_float_loop_2", "game_10_glider_float_loop_3", "game_10_glider_float_loop_4", "game_10_glider_float_loop_5", "game_10_glider_float_loop_6" } },
        };

        /// <summary>Writes every sound's WAV into sfxDir; ready.json once all are there. Returns how many failed and why.</summary>
        public static int Render(string celesteDir, string sfxDir, StringBuilder log)
        {
            Directory.CreateDirectory(sfxDir);
            string banksDir = Path.Combine(celesteDir, Path.Combine("Content", Path.Combine("FMOD", "Desktop")));
            var banks = new List<CelesteBank>();
            foreach (string b in new[] { "sfx.bank", "ui.bank", "dlc_sfx.bank" })
            {
                try { banks.Add(new CelesteBank(Path.Combine(banksDir, b))); }
                catch (Exception e) { log.AppendLine(b + ": " + e.Message); }
            }
            int failed = 0, ok = 0;
            foreach (SoundRow row in Sounds.All)
            {
                string wav = Path.Combine(sfxDir, row.Id + ".wav");
                try
                {
                    int ch, rate;
                    float[] pcm = Make(row, banks, out ch, out rate);
                    if (pcm == null || pcm.Length == 0) { failed++; log.AppendLine(row.Id + ": no sample found"); continue; }
                    // Celeste's looping samples (the glide, the wall slide) stay whole so they loop seamlessly; only the
                    // babble is cut to one line's length
                    if (row.HoldSeconds > 0f && Babble.ContainsKey(row.Id)) pcm = Cut(pcm, ch, (int)(row.HoldSeconds * rate));
                    float gain;
                    if (Gains.TryGetValue(row.Id, out gain)) for (int k = 0; k < pcm.Length; k++) pcm[k] *= gain;   // Celeste's own level
                    WriteWav(wav, pcm, ch, rate);
                    ok++;
                }
                catch (Exception e) { failed++; log.AppendLine(row.Id + ": " + e.Message); }
            }
            log.AppendLine("rendered " + ok + " of " + Sounds.All.Length + " from the banks");
            if (failed == 0) File.WriteAllText(Path.Combine(sfxDir, "ready.json"), "{\"sounds\":" + ok + ",\"from\":\"banks\"}");
            return failed;
        }

        static float[] Make(SoundRow row, List<CelesteBank> banks, out int ch, out int rate)
        {
            ch = 1; rate = 48000;
            string[] seq;
            if (Sequences.TryGetValue(row.Id, out seq))
            {
                // Celeste starts each flutter about a quarter second after the last, overlapping (its render loops
                // in ~1.5 s, user 2026-10-13: strung end to end it felt too slow); mixed the same way here
                var parts = new List<float[]>();
                foreach (string part in seq)
                {
                    int c, r;
                    float[] p = DecodeName(banks, part, out c, out r);
                    if (p == null || (parts.Count > 0 && c != ch)) continue;
                    ch = c; rate = r;
                    parts.Add(p);
                }
                if (parts.Count == 0) return null;
                // lowest pitch first, rising to the highest, then round again (user 2026-10-13)
                int chs = ch;
                parts.Sort((x, y) => Pitch(x, chs).CompareTo(Pitch(y, chs)));
                int step = (int)(0.245f * rate);
                int loopFrames = step * parts.Count, longest = 0;
                foreach (float[] p in parts) longest = Math.Max(longest, p.Length / ch);
                var mix = new float[loopFrames * ch];
                for (int k = 0; k < parts.Count; k++)
                {
                    float[] p = parts[k];
                    for (int f = 0; f < p.Length / ch; f++)
                    {
                        int at = (k * step + f) % loopFrames;   // wraps round, so the loop joins seamlessly
                        for (int c = 0; c < ch; c++) mix[at * ch + c] += p[f * ch + c];
                    }
                }
                return mix;
            }
            string babble;
            if (Babble.TryGetValue(row.Id, out babble))
            {
                // mid A, B, C in a row: a short burst of babble like one typed-out line
                var all = new List<float>();
                var syl = new List<float[]>();
                foreach (string part in new[] { "_mid_A", "_mid_B", "_mid_C" })
                {
                    int c, r;
                    float[] p = DecodeName(banks, babble + part, out c, out r);
                    if (p == null || (syl.Count > 0 && c != ch)) continue;
                    ch = c; rate = r;
                    syl.Add(p);
                }
                if (syl.Count == 0) return null;
                // syllables in a varied order until a line's worth (sounds.json hold_seconds), as Celeste keeps talking
                int[] order = { 0, 1, 2, 1, 0, 2, 0, 2, 1 };
                for (int k = 0; all.Count < row.HoldSeconds * rate * ch && k < 40; k++) all.AddRange(syl[order[k % order.Length] % syl.Count]);
                return all.ToArray();
            }
            string name;
            if (!Named.TryGetValue(row.Id, out name))
            {
                // footstep_8_a, landing_3, grab_17, handhold_25, wallslide_7...
                string kind = row.Id.Substring(0, row.Id.IndexOf('_'));
                string mat;
                if (!Surfaces.TryGetValue(row.SurfaceIndex, out mat)) return null;
                switch (kind)
                {
                    case "footstep": name = "char_mad_foot_" + mat + (row.Id.EndsWith("_b", StringComparison.Ordinal) ? "_04" : "_01"); break;
                    case "landing": name = "char_mad_land_" + mat; break;
                    case "grab": name = "char_mad_grab_" + mat; break;
                    case "handhold": name = "char_mad_handhold_" + mat; break;
                    case "wallslide": name = "char_mad_wallslide_general"; break;
                    default: return null;
                }
            }
            return DecodeName(banks, name, out ch, out rate);
        }

        static float[] DecodeName(List<CelesteBank> banks, string name, out int ch, out int rate)
        {
            ch = 1; rate = 48000;
            foreach (CelesteBank bank in banks)
            {
                CelesteBank.Sample s = bank.Find(name);
                if (s != null) return bank.Decode(s, out ch, out rate);
            }
            return null;
        }

        // a rough pitch: zero crossings per frame, over the louder part of the sound
        static float Pitch(float[] pcm, int ch)
        {
            int frames = pcm.Length / ch, crossings = 0, counted = 0;
            float peak = 0f;
            for (int f = 0; f < frames; f++) peak = Math.Max(peak, Math.Abs(pcm[f * ch]));
            float prev = 0f;
            for (int f = 0; f < frames; f++)
            {
                float x = pcm[f * ch];
                if (Math.Abs(x) < peak * 0.1f) { prev = x; continue; }
                counted++;
                if ((x >= 0f) != (prev >= 0f)) crossings++;
                prev = x;
            }
            return counted > 0 ? crossings / (float)counted : 0f;
        }

        static float[] Cut(float[] pcm, int ch, int frames)
        {
            int n = Math.Min(pcm.Length, frames * ch);
            if (n == pcm.Length) return pcm;
            var o = new float[n];
            Array.Copy(pcm, o, n);
            int fade = Math.Min(n / ch, 2400);   // 50 ms fade so the cut doesn't click
            for (int f = 0; f < fade; f++)
                for (int c = 0; c < ch; c++) o[(n / ch - 1 - f) * ch + c] *= f / (float)fade;
            return o;
        }

        // 16-bit PCM WAV, as Sfx.ReadPcm16 reads it
        static void WriteWav(string path, float[] pcm, int ch, int rate)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int n = pcm.Length;
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16); w.Write((short)1); w.Write((short)ch); w.Write(rate); w.Write(rate * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
                foreach (float x in pcm) w.Write((short)Math.Max(-32768, Math.Min(32767, (int)(x * 32767f))));
            }
        }
    }
}
