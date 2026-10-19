# Celeste-style main menus: work plan (handoff)

Goal (user, 2026-10-14): restyle every main menu of Silkeste (Silksong mod, BepInEx plugin MadelineInPharloom) in the
same Celeste style as the finished save file screen (`src/MadelineInPharloom/CelesteFileSelect.cs`). Work until it
looks perfect: restart the game, drive the menus, screenshot, fix, repeat. Do NOT publish to Melty; test build only.

## Menus to do
1. Title screen main menu (Start Game / Options / Extras / Quit ...).
2. Options and every sub-page (Game, Audio, Video, Controls, keyboard + controller rebinding, etc.).
3. Extras / Achievements, quit confirmation, any other pre-game menu screens.
4. The in-game pause menu too (default choice; user wasn't asked to confirm, may change).

## Style (match CelesteFileSelect.cs)
- Full-screen Celeste night sky with the icy low-poly mountain (reuse `CelesteFileSelect.Sky()` / snow / stars code;
  factor it out into a shared helper, e.g. `CelesteMenuStyle`).
- Celeste font via `SpeedrunTimer.Art` (GuiArt): `Art.DrawOutlined`, `Art.Measure`, `Art.LineHeight`.
- Celeste TextMenu look: centred list, selected item flashes `Visuals.MenuHighlightA/B` with a small bounce,
  values as `< value >`, hints bottom right ("Confirm Cross / Back Circle" on a pad via `MenuMouse.PadActive`,
  keys otherwise). Title menu: Celeste-like list on the left with Silksong's logo kept (default choice).
- Sounds: Celeste menu sounds via `Sfx.Play(Sounds.MenuRolloverUp/Down/ButtonSelect/...)` (works on menus now).
  Silksong's own select sound is muted while a Celeste screen is up (see `CelesteFileSelect.QuietSelect` patch on
  `MenuAudioController.PlaySelect`; generalise the flag).

## Safety approach (what worked for the save screen)
- Silksong's menus keep doing ALL the work (settings, sliders, rebinding, navigation, submit). Only the look changes.
- Read Silksong's active `MenuScreen` / its `Selectable`s (MenuButton, MenuOptionHorizontal, sliders, Text labels) and
  draw Celeste versions in OnGUI over a full-screen sky; mirror the current selection from
  `EventSystem.current.currentSelectedGameObject` (the save screen instead drove input itself because its slots
  are horizontal; prefer mirroring for normal vertical menus).
- Any screen not understood -> draw nothing (fall back to Silksong's look). Never block input.
- Appear instantly (no fade-in that shows Silksong's look first).
- Decompiled Silksong source: `C:\Users\Twifty\.melty-tools\decomp\silksong\` (UIManager.cs, MenuScreen, MenuButton,
  MenuOptionHorizontal, MenuAudioController.cs ...).

## Build / install / run
- Build test: `C:\Users\Twifty\.melty-tools\dotnet\dotnet.exe build src\MadelineInPharloom -c Release --nologo -v q -p:DefineConstants=TESTHARNESS -o build\test`
  (run from the Silkeste folder). Also check the release build compiles (`-o build\release`, no define).
- Install: stop the game process, copy `build\test\MadelineInPharloom.dll` to
  `C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\MadelineInPharloom\`,
  start with `Start-Process 'steam://rungameid/1030300'`.
- New .cs files must be added as components in `Plugin.cs` next to `gameObject.AddComponent<CelesteFileSelect>();`.
- Log: `...\Hollow Knight Silksong\BepInEx\LogOutput.log`; Unity errors: `%USERPROFILE%\AppData\LocalLow\Team Cherry\Hollow Knight Silksong\Player.log`.
- Screenshots: capture ONLY the game window, and only if it is the foreground window (check the foreground window's
  process id equals the Silksong process; if not, skip; never capture the desktop or other windows). Delete
  screenshots after looking. Navigate menus with synthesized key presses (keybd_event / SendInput) to the game.
- PowerShell gotcha: a command containing certain comment-like strings got blocked by a safety filter; when editing
  C# use the Edit/Write tools instead of big PowerShell string replaces.

## User's answers (2026-10-14 01:10)
1. Pause menu: yes, all of it.
2. Title menu layout: Celeste style, my choice, must look good and all work.
3. Logo: REPLACE Silksong's logo with a "SILKESTE" title in Celeste's style.
4. User is asleep; the PC is free to drive.
5. Never spend money or buy anything. Just continue when usage is back.

## Progress log (update as you go)
- 2026-10-14 01:05 +03:00: plan written; nothing built yet for these menus.
- 2026-10-14 ~01:14: first draft `src/MadelineInPharloom/CelesteMenus.cs` written and added in Plugin.cs (reads the open
  MenuScreen, draws title/list/pause in Celeste style, mirrors EventSystem selection, mouse support). Not yet tested.
- 2026-10-14 07:58 (scheduled run): resuming: build + first in-game test of CelesteMenus.
- 2026-10-14 08:05 (scheduled run): the test build compiles (0 errors). STOPPED without testing in the game:
  while this run worked, `CelesteMenus.cs` changed under it (18820 -> 19954 bytes, 07:59:49), the plugin DLL in the
  game folder was replaced (07:59:52), Silksong was started and closed, and there was live keyboard/mouse input with
  the Claude app in front. That looks like the user (or the main "silkeste" session) working on this same task, so
  this run made no code changes, didn't restart the game and didn't send key presses. The menus are still untested
  in the game.
  Helper for the next run: a drive script (keys to the game + a screenshot of only the game window, only when it is
  in front) was in that run's temporary folder; recreate it if needed (SendInput/keybd_event + CopyFromScreen of the
  client area, after checking that the foreground window's process is the game).

## Summary (scheduled run, 2026-10-14 08:05)
Done so far: a first version of the Celeste-style menus (`CelesteMenus.cs`) is written, turned on in Plugin.cs and
compiles. It covers the title menu (with a SILKESTE title), every Options page, Extras/Achievements, the quit and
return-to-menu prompts, and the pause menu. Silksong's menus still do all the work.
Left to do: check every screen in the game and fix how it looks: title layout and icons, long Options lists,
key and controller rebinding pages, sliders, the pause menu dim. Also check the release build compiles.
This run stopped early because someone else was working on the same files and the game at the same time.
