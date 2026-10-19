# Preflight and code generation for the design sheets.
#   sheets.ps1 -Preflight -CelesteDir <dir> -DecompDir <dir>   lists every unfilled cell and unresolved reference
#   sheets.ps1 -Generate ...                                   runs the preflight, then writes src/Shared/Sheets.g.cs
param(
  [switch]$Preflight, [switch]$Generate, [switch]$SoundCheck,
  [string]$CelesteDir = "C:\Program Files (x86)\Steam\steamapps\common\Celeste",
  [string]$DecompDir = "$env:USERPROFILE\.melty-tools\decomp\silksong"
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sheetDir = Join-Path $root 'sheets'
$names = 'movement','tech','dash_attack','hooks','animations','sounds','inputs','visuals','unlocks','skins','transitions','surfaces','postcards','cutscenes','assist'
$S = @{}
foreach ($n in $names) { $S[$n] = @((Get-Content (Join-Path $sheetDir "$n.json") -Raw | ConvertFrom-Json) | ForEach-Object { $_ }) }

$problems = New-Object System.Collections.Generic.List[string]
$unfilled = @('', 'verify', 'tbd', 'to find', 'pending', '?')

# 1. Every row x column crossing is a checkbox.
foreach ($n in $names) {
  $cols = $S[$n][0].PSObject.Properties.Name
  foreach ($row in $S[$n]) {
    $rc = $row.PSObject.Properties.Name
    foreach ($c in $cols) { if ($rc -notcontains $c) { $problems.Add("$n/$($row.id): missing column '$c'") } }
    foreach ($c in $rc) {
      if ($cols -notcontains $c) { $problems.Add("$n/$($row.id): extra column '$c'") }
      $v = $row.$c
      if ($null -eq $v) { $problems.Add("$n/$($row.id).${c}: empty"); continue }
      if ($v -is [string] -and $unfilled -contains $v.Trim().ToLower()) { $problems.Add("$n/$($row.id).${c}: unfilled ('$v')") }
      if ($c -eq 'verified' -and $v -eq $false) { $problems.Add("$n/$($row.id): not verified") }
    }
  }
  $dups = $S[$n] | Group-Object id | Where-Object Count -gt 1
  foreach ($d in $dups) { $problems.Add("${n}: duplicate id '$($d.Name)'") }
}

# 2. References between sheets and into the games.
$soundIds = $S.sounds | ForEach-Object id
foreach ($t in $S.tech) {
  if ($t.sfx -ne 'none' -and -not ($soundIds | Where-Object { $_ -eq $t.sfx -or $_.StartsWith($t.sfx + '_') })) { $problems.Add("tech/$($t.id).sfx: '$($t.sfx)' is not in sounds") }
}
foreach ($k in $S.skins) {
  if (-not ($soundIds | Where-Object { $_.StartsWith($k.voice + '_') })) { $problems.Add("skins/$($k.id).voice: no sounds named '$($k.voice)_*'") }
  foreach ($pair in $k.idle_sounds.Split(';')) { $snd = $pair.Split(':')[1]; if ($soundIds -notcontains $snd) { $problems.Add("skins/$($k.id).idle_sounds: '$snd' is not in sounds") } }
  foreach ($pair in $k.idle_moves.Split(',')) { $mv = $pair.Split(':')[0]; if (-not ($S.animations | Where-Object { $_.celeste_anim -ceq $mv })) { $problems.Add("skins/$($k.id).idle_moves: '$mv' is not in animations") } }
}
foreach ($r in $S.sounds) {
  if ($r.params -ne 'none' -and $r.params -notmatch '^\w+=-?[\d.]+(;\w+=-?[\d.]+)*$') { $problems.Add("sounds/$($r.id).params: '$($r.params)' is not name=value;...") }
}
$spritesXml = Join-Path $CelesteDir 'Content\Graphics\Sprites.xml'
if (Test-Path $spritesXml) {
  [xml]$sx = Get-Content $spritesXml -Raw
  $animIds = $sx.Sprites.player.ChildNodes | Where-Object { $_.LocalName -in 'Anim','Loop' } | ForEach-Object { $_.id }
  foreach ($a in $S.animations) { if ($animIds -cnotcontains $a.celeste_anim) { $problems.Add("animations/$($a.id).celeste_anim: '$($a.celeste_anim)' not in Celeste Sprites.xml") } }
  foreach ($k in $S.skins) {
    $node = $sx.Sprites.SelectSingleNode($k.celeste_sprite)
    if (-not $node) { $problems.Add("skins/$($k.id).celeste_sprite: <$($k.celeste_sprite)> not in Celeste Sprites.xml") }
    elseif ($k.celeste_sprite -ne 'player' -and $node.copy -ne 'player') { $problems.Add("skins/$($k.id): <$($k.celeste_sprite)> does not copy player's animations") }
  }
} else { $problems.Add("Celeste Sprites.xml not found at $spritesXml") }
$enviro = Get-ChildItem $DecompDir -Recurse -Filter 'EnvironmentTypes.cs' | Select-Object -First 1
foreach ($sf in $S.surfaces) {
  if (-not $enviro -or -not (Select-String -Path $enviro.FullName -Pattern "^\s*$($sf.environment),?\s*$" -Quiet)) { $problems.Add("surfaces/$($sf.id).environment: EnvironmentTypes.$($sf.environment) not found in Silksong") }
}
# Surface sounds fall back to brick (8) when a surface has no row of their own, so brick must have every one.
foreach ($base in 'footstep','landing','grab','handhold','wallslide') {
  if (-not ($soundIds | Where-Object { $_ -eq "${base}_8" -or $_.StartsWith("${base}_8_") })) { $problems.Add("sounds: no ${base}_8 (the brick fallback)") }
}
foreach ($r in $S.sounds) {
  if ($r.end -ne 'none' -and $r.end -ne 'stop' -and $r.end -notmatch '^\w+=-?[\d.]+(;\w+=-?[\d.]+)*$') { $problems.Add("sounds/$($r.id).end: '$($r.end)' is not none, stop or name=value;...") }
  if ($r.end -ne 'none' -and [double]$r.hold_seconds -le 0) { $problems.Add("sounds/$($r.id): end '$($r.end)' needs hold_seconds > 0") }
}
foreach ($cs in $S.cutscenes) { if (-not ($S.animations | Where-Object { $_.celeste_anim -ceq $cs.celeste_anim })) { $problems.Add("cutscenes/$($cs.id).celeste_anim: '$($cs.celeste_anim)' is not in animations") } }
$unlockIds = $S.unlocks | ForEach-Object id
foreach ($pc in $S.postcards) {
  if ($pc.unlock -ne 'none' -and $unlockIds -notcontains $pc.unlock) { $problems.Add("postcards/$($pc.id).unlock: '$($pc.unlock)' is not in unlocks") }
  foreach ($m in [regex]::Matches($pc.body, '\{unlock:(\w+)\}')) { if ($unlockIds -notcontains $m.Groups[1].Value) { $problems.Add("postcards/$($pc.id).body: {unlock:$($m.Groups[1].Value)} is not in unlocks") } }
}
$skinNames = ($S.skins | ForEach-Object name) -join ','
$skinList = @($S.skins | ForEach-Object name)
foreach ($a in $S.assist) { if ($a.id -in @('skin','outfit')) { foreach ($v in $a.values.Split(',')) { if ($v -ne 'Skin default' -and $skinList -cnotcontains $v) { $problems.Add("assist/$($a.id).values: '$v' is not a skin name ($skinNames)") } } } }
foreach ($v in ($S.visuals | Where-Object id -eq 'look_default_skins').value.Split(',')) { if ($skinList -cnotcontains $v) { $problems.Add("visuals/look_default_skins: '$v' is not a skin name") } }
foreach ($a in $S.assist) { if ($a.values.Split(',') -cnotcontains $a.default) { $problems.Add("assist/$($a.id).default: '$($a.default)' is not one of '$($a.values)'") } }
foreach ($h in $S.hooks) {
  if ($h.target -match '^(\w+)\.(\w+)(\([\w,]*\))?$') {
    $file = Get-ChildItem $DecompDir -Recurse -Filter "$($Matches[1]).cs" | Select-Object -First 1
    if (-not $file) { $problems.Add("hooks/$($h.id): type $($Matches[1]) not found in Silksong") }
    elseif (-not (Select-String -Path $file.FullName -Pattern "\b$($Matches[2])\b" -Quiet)) { $problems.Add("hooks/$($h.id): member $($h.target) not found in Silksong") }
  } elseif ($h.target -like '{game:celeste}/*') {
    $p = Join-Path $CelesteDir ($h.target.Substring('{game:celeste}/'.Length))
    if (-not (Test-Path $p)) { $problems.Add("hooks/$($h.id): $p missing in Celeste") }
  } else { $problems.Add("hooks/$($h.id): target '$($h.target)' is not Type.Member or {game:celeste}/path") }
}
foreach ($i in $S.inputs) {
  if ($i.silksong_action -like "key:*") { if ($i.silksong_action -notmatch "^key:[A-Za-z0-9]+$") { $problems.Add("inputs/$($i.id): bad key '$($i.silksong_action)'") }; continue }
  if (-not (Select-String -Path (Join-Path $DecompDir 'HeroActions.cs') -Pattern "Player\w*Action $($i.silksong_action);" -Quiet)) { $problems.Add("inputs/$($i.id): HeroActions.$($i.silksong_action) not found") }
}

foreach ($u in $S.unlocks) {
  if (-not (Select-String -Path (Join-Path $DecompDir 'PlayerData.cs') -Pattern "public bool $($u.player_data);" -Quiet)) { $problems.Add("unlocks/$($u.id).player_data: PlayerData.$($u.player_data) not found in Silksong") }
}

if ($problems.Count) {
  "PREFLIGHT: $($problems.Count) problem(s)"; $problems | ForEach-Object { "  - $_" }
  # -SoundCheck generates code only so CelesteSfx can verify the sound events; nothing else may be open.
  if (-not ($SoundCheck -and -not ($problems | Where-Object { $_ -notmatch '^sounds/[\w]+\.verified: unfilled' }))) { exit 1 }
  "SOUND CHECK BUILD: generating with only sounds.verified open (not for release)"
}
$cells = ($names | ForEach-Object { $S[$_].Count * $S[$_][0].PSObject.Properties.Name.Count } | Measure-Object -Sum).Sum
"PREFLIGHT CLEAN: $($names.Count) sheets, $cells cells checked"
if (-not $Generate) { exit 0 }

# 3. Code generation: one member per row, built from that row's columns.
function Pascal($s) { ($s -split '_' | ForEach-Object { $_.Substring(0,1).ToUpper() + $_.Substring(1) }) -join '' }
function F($v) { ([double]$v).ToString('R', [Globalization.CultureInfo]::InvariantCulture) + 'f' }
function Q($v) { '"' + ($v -replace '\\','\\' -replace '"','\"' -replace "?
", '\n') + '"' }
$o = New-Object System.Text.StringBuilder
[void]$o.AppendLine('// <auto-generated> by tools/sheets.ps1 from sheets/*.json. Edit the sheets, not this file. </auto-generated>')
[void]$o.AppendLine('namespace MadelineInPharloom.Sheets')
[void]$o.AppendLine('{')
[void]$o.AppendLine('    public static class Movement')
[void]$o.AppendLine('    {')
foreach ($r in $S.movement) { [void]$o.AppendLine("        /// <summary>$($r.unit), $($r.source)</summary>`n        public const float $(Pascal $r.id) = $(F $r.value);") }
[void]$o.AppendLine('    }')
[void]$o.AppendLine('    public struct TechRow { public string Id, Trigger, Mode, Sfx; public float SpeedX, SpeedY, XMult, YMult, VarJumpTime, StaminaCost; }')
[void]$o.AppendLine('    public static class Tech')
[void]$o.AppendLine('    {')
foreach ($r in $S.tech) { [void]$o.AppendLine("        public static readonly TechRow $(Pascal $r.id) = new TechRow { Id = $(Q $r.id), Trigger = $(Q $r.trigger), Mode = $(Q $r.mode), Sfx = $(Q $r.sfx), SpeedX = $(F $r.speed_x), SpeedY = $(F $r.speed_y), XMult = $(F $r.x_mult), YMult = $(F $r.y_mult), VarJumpTime = $(F $r.var_jump_time), StaminaCost = $(F $r.stamina_cost) };") }
[void]$o.AppendLine('    }')
[void]$o.AppendLine('    public static class DashAttack')
[void]$o.AppendLine('    {')
foreach ($r in $S.dash_attack) {
  $n = Pascal $r.id
  if ($r.value -is [bool]) { [void]$o.AppendLine("        public const bool $n = $(([string]$r.value).ToLower());") }
  elseif ($r.value -is [string]) { [void]$o.AppendLine("        public const string $n = $(Q $r.value);") }
  else { [void]$o.AppendLine("        public const float $n = $(F $r.value);") }
}
[void]$o.AppendLine('    }')
[void]$o.AppendLine('    public struct HookRow { public string Id, Target, Patch, Ref; }')
[void]$o.AppendLine('    public static class Hooks')
[void]$o.AppendLine('    {')
foreach ($r in $S.hooks) { [void]$o.AppendLine("        public static readonly HookRow $(Pascal $r.id) = new HookRow { Id = $(Q $r.id), Target = $(Q $r.target), Patch = $(Q $r.patch), Ref = $(Q $r.ref) };") }
[void]$o.AppendLine("        public static readonly HookRow[] All = { $((($S.hooks | ForEach-Object { Pascal $_.id }) -join ', ')) };")
[void]$o.AppendLine('    }')
[void]$o.AppendLine('    public struct AnimRow { public string Id, CelesteAnim, When; }')
[void]$o.AppendLine('    public static class Animations')
[void]$o.AppendLine('    {')
foreach ($r in $S.animations) { [void]$o.AppendLine("        public static readonly AnimRow $(Pascal $r.id) = new AnimRow { Id = $(Q $r.id), CelesteAnim = $(Q $r.celeste_anim), When = $(Q $r.when) };") }
[void]$o.AppendLine("        public static readonly AnimRow[] All = { $((($S.animations | ForEach-Object { Pascal $_.id }) -join ', ')) };")
[void]$o.AppendLine('    }')
[void]$o.AppendLine('    public struct SoundRow { public string Id, Event, Params, End; public int SurfaceIndex; public float HoldSeconds; }')
[void]$o.AppendLine('    public static class Sounds')
[void]$o.AppendLine('    {')
foreach ($r in $S.sounds) { [void]$o.AppendLine("        public static readonly SoundRow $(Pascal $r.id) = new SoundRow { Id = $(Q $r.id), Event = $(Q $r.fmod_event), Params = $(Q $r.params), End = $(Q $r.end), SurfaceIndex = $([int]$r.surface_index), HoldSeconds = $(F $r.hold_seconds) };") }
[void]$o.AppendLine("        public static readonly SoundRow[] All = { $((($S.sounds | ForEach-Object { Pascal $_.id }) -join ', ')) };")
[void]$o.AppendLine('    }')
[void]$o.AppendLine('    public struct InputRow { public string Id, Celeste, SilksongAction, Read; }')
[void]$o.AppendLine('    public static class Inputs')
[void]$o.AppendLine('    {')
foreach ($r in $S.inputs) { [void]$o.AppendLine("        public static readonly InputRow $(Pascal $r.id) = new InputRow { Id = $(Q $r.id), Celeste = $(Q $r.celeste), SilksongAction = $(Q $r.silksong_action), Read = $(Q $r.read) };") }
[void]$o.AppendLine('    }')
[void]$o.AppendLine('    public struct UnlockRow { public string Id, SilksongUnlock, PlayerData, Gives; }')
[void]$o.AppendLine('    public static class Unlocks')
[void]$o.AppendLine('    {')
foreach ($r in $S.unlocks) { [void]$o.AppendLine("        public static readonly UnlockRow $(Pascal $r.id) = new UnlockRow { Id = $(Q $r.id), SilksongUnlock = $(Q $r.silksong_unlock), PlayerData = $(Q $r.player_data), Gives = $(Q $r.gives) };") }
[void]$o.AppendLine("        public static readonly UnlockRow[] All = { $((($S.unlocks | ForEach-Object { Pascal $_.id }) -join ', ')) };")
[void]$o.AppendLine('    }')
[void]$o.AppendLine('    public struct TransitionRow { public string Id, EntryGates, SpeedX, SpeedY, State; public float SpeedYValue, VarJumpTime, DashCooldown; public bool AutoJump, Refill; }')
[void]$o.AppendLine('    public static class Transitions')
[void]$o.AppendLine('    {')
foreach ($r in $S.transitions) { [void]$o.AppendLine("        public static readonly TransitionRow $(Pascal $r.id) = new TransitionRow { Id = $(Q $r.id), EntryGates = $(Q $r.entry_gates), SpeedX = $(Q $r.speed_x), SpeedY = $(Q $r.speed_y), State = $(Q $r.state), SpeedYValue = $(F $r.speed_y_value), VarJumpTime = $(F $r.var_jump_time), DashCooldown = $(F $r.dash_cooldown), AutoJump = $(([string]$r.auto_jump).ToLower()), Refill = $(([string]$r.refill).ToLower()) };") }
[void]$o.AppendLine("        public static readonly TransitionRow[] All = { $((($S.transitions | ForEach-Object { Pascal $_.id }) -join ', ')) };")
[void]$o.AppendLine('    }')
[void]$o.AppendLine('    public struct SkinRow { public string Id, Name, CelesteSprite, Style, Palette, NoBackpack, FallbackPalette, BodyColor, HairNormal, HairUsed, HairTwoDashes, Voice, IdleMoves, IdleSounds; public float GlowCompensation; }')
[void]$o.AppendLine('    public static class Skins')
[void]$o.AppendLine('    {')
foreach ($r in $S.skins) { [void]$o.AppendLine("        public static readonly SkinRow $(Pascal $r.id) = new SkinRow { Id = $(Q $r.id), Name = $(Q $r.name), CelesteSprite = $(Q $r.celeste_sprite), Style = $(Q $r.style), Palette = $(Q $r.palette), NoBackpack = $(Q $r.no_backpack), FallbackPalette = $(Q $r.fallback_palette), BodyColor = $(Q $r.body_color), HairNormal = $(Q $r.hair_normal), HairUsed = $(Q $r.hair_used), HairTwoDashes = $(Q $r.hair_two_dashes), Voice = $(Q $r.voice), IdleMoves = $(Q $r.idle_moves), IdleSounds = $(Q $r.idle_sounds), GlowCompensation = $(F $r.glow_compensation) };") }
[void]$o.AppendLine("        public static readonly SkinRow[] All = { $((($S.skins | ForEach-Object { Pascal $_.id }) -join ', ')) };")
[void]$o.AppendLine('    }')
[void]$o.AppendLine('    public static class Visuals')
[void]$o.AppendLine('    {')
foreach ($r in $S.visuals) {
  if ($r.value -is [string]) { [void]$o.AppendLine("        public const string $(Pascal $r.id) = $(Q $r.value);") }
  else { [void]$o.AppendLine("        public const float $(Pascal $r.id) = $(F $r.value);") }
}
[void]$o.AppendLine('    }')
[void]$o.AppendLine('    public struct SurfaceRow { public string Id, Environment, CelesteName; public int CelesteSurface; }')
[void]$o.AppendLine('    public static class Surfaces')
[void]$o.AppendLine('    {')
foreach ($r in $S.surfaces) { [void]$o.AppendLine("        public static readonly SurfaceRow $(Pascal $r.id) = new SurfaceRow { Id = $(Q $r.id), Environment = $(Q $r.environment), CelesteName = $(Q $r.celeste_name), CelesteSurface = $([int]$r.celeste_surface) };") }
[void]$o.AppendLine("        public static readonly SurfaceRow[] All = { $((($S.surfaces | ForEach-Object { Pascal $_.id }) -join ', ')) };")
[void]$o.AppendLine('    }')
[void]$o.AppendLine('    public struct CutsceneRow { public string Id, HornetClip, CelesteAnim; }')
[void]$o.AppendLine('    public static class Cutscenes')
[void]$o.AppendLine('    {')
foreach ($r in $S.cutscenes) { [void]$o.AppendLine("        public static readonly CutsceneRow $(Pascal $r.id) = new CutsceneRow { Id = $(Q $r.id), HornetClip = $(Q $r.hornet_clip), CelesteAnim = $(Q $r.celeste_anim) };") }
[void]$o.AppendLine("        public static readonly CutsceneRow[] All = { $((($S.cutscenes | ForEach-Object { Pascal $_.id }) -join ', ')) };")
[void]$o.AppendLine('    }')
[void]$o.AppendLine('    public struct PostcardRow { public string Id, Title, Body, Unlock; }')
[void]$o.AppendLine('    public static class Postcards')
[void]$o.AppendLine('    {')
foreach ($r in $S.postcards) { [void]$o.AppendLine("        public static readonly PostcardRow $(Pascal $r.id) = new PostcardRow { Id = $(Q $r.id), Title = $(Q $r.title), Body = $(Q $r.body), Unlock = $(Q $r.unlock) };") }
[void]$o.AppendLine("        public static readonly PostcardRow[] All = { $((($S.postcards | ForEach-Object { Pascal $_.id }) -join ', ')) };")
[void]$o.AppendLine('    }')
[void]$o.AppendLine('    public struct AssistRow { public string Id, Section, Label, Values, Default, Description; }')
[void]$o.AppendLine('    public static class AssistOptions')
[void]$o.AppendLine('    {')
foreach ($r in $S.assist) { [void]$o.AppendLine("        public static readonly AssistRow $(Pascal $r.id) = new AssistRow { Id = $(Q $r.id), Section = $(Q $r.section), Label = $(Q $r.label), Values = $(Q $r.values), Default = $(Q $r.default), Description = $(Q $r.description) };") }
[void]$o.AppendLine("        public static readonly AssistRow[] All = { $((($S.assist | ForEach-Object { Pascal $_.id }) -join ', ')) };")
[void]$o.AppendLine('    }')
[void]$o.AppendLine('}')
$out = Join-Path $root 'src\Shared\Sheets.g.cs'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
[IO.File]::WriteAllText($out, $o.ToString(), (New-Object Text.UTF8Encoding $false))
"GENERATED $out"
