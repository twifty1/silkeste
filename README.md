# Silkeste

**Play all of Hollow Knight: Silksong as Madeline from Celeste.** Her real sprites, hair, sounds and movement (8-way dash, climbing, supers, hypers and wavedashes), with your dash as your attack, Celeste-style menus, golden berries, boss rematches and lots of ways to customize her.

**Play it:** [melty.gg/m/silkeste](https://melty.gg/m/silkeste) (free, one-click install). You need Hollow Knight: Silksong and Celeste.

Madeline's art and sounds are read from your own copy of Celeste while you play. Nothing from Celeste or Silksong is in this repository or in the download.

## What's here

| Folder | What it is |
| --- | --- |
| `src/MadelineInPharloom` | The main BepInEx 5 plugin: Madeline, her movement, menus, Records, settings |
| `src/SilkesteExtras`, `src/SilkesteRemix` | Two small add-on plugins (needle attack off, pogo, speedrun extras, loading screen) |
| `lib` | CelesteSfx.exe (turns Celeste's sounds into files the game can play), NVorbis.dll, the README and licenses shipped in the download |
| `build.ps1` | Builds all three plugins, installs them, or makes the release zip |
| `notes` | Older source, drafts and listing text |

## Building

You need Hollow Knight: Silksong (Steam), BepInEx 5 in it, and the .NET SDK. The projects reference the game's own DLLs from its install folder (`src/*/*.csproj`), so change those paths if your game is somewhere else.

```powershell
./build.ps1                         # build
./build.ps1 -Install                # build, copy into the game, start it
./build.ps1 -Package -Version 2.1.0 # make dist/Silkeste-2.1.0.zip
```

## License

Silkeste's own code is [MIT](LICENSE): remixes are welcome. NVorbis and python-fsb5 are MIT too (see `lib/THIRD_PARTY_LICENSES.txt`).

Fan-made and not affiliated with Team Cherry or Maddy Makes Games. Celeste, Madeline and Badeline belong to Maddy Makes Games (Extremely OK Games). Hollow Knight: Silksong belongs to Team Cherry. Made by Twifty.
