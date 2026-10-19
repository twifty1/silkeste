# Builds Silkeste's three plugins. -Install copies them into the game; -Package makes dist\Silkeste-<version>.zip.
param([switch]$Install, [switch]$Package, [string]$Version)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dotnet = Join-Path $env:USERPROFILE '.melty-tools\dotnet\dotnet.exe'
$game = 'C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong'
$plug = Join-Path $game 'BepInEx\plugins\MadelineInPharloom'

# the main plugin first: Extras and Remix compile against its output
foreach ($p in @(@('MadelineInPharloom', 'main'), @('SilkesteExtras', 'SilkesteExtras'), @('SilkesteRemix', 'SilkesteRemix'))) {
  & $dotnet build (Join-Path $root "src\$($p[0])\$($p[0]).csproj") -c Release --nologo -v q -o (Join-Path $root "build\$($p[1])")
  if ($LASTEXITCODE -ne 0) { throw "$($p[0]) failed to build" }
}
$dlls = @((Join-Path $root 'build\main\MadelineInPharloom.dll'), (Join-Path $root 'build\SilkesteExtras\SilkesteExtras.dll'), (Join-Path $root 'build\SilkesteRemix\SilkesteRemix.dll'))

if ($Install) {
  if (Get-Process | Where-Object { $_.Name -like '*Silksong*' }) { Get-Process | Where-Object { $_.Name -like '*Silksong*' } | Stop-Process -Force; Start-Sleep 3 }
  foreach ($d in $dlls) { Copy-Item $d $plug -Force }
  Start-Process 'steam://rungameid/1030300'
  'installed and started'
}

if ($Package) {
  if (-not $Version) { throw 'pass -Version x.y.z' }
  Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
  $zip = Join-Path $root "dist\Silkeste-$Version.zip"
  if (Test-Path $zip) { Remove-Item $zip }
  $fs = [IO.File]::Open($zip, 'Create'); $za = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create)
  $files = @{}
  foreach ($d in $dlls) { $files['plugins/MadelineInPharloom/' + [IO.Path]::GetFileName($d)] = $d }
  foreach ($n in 'CelesteSfx.exe', 'NVorbis.dll', 'README.txt', 'LICENSE.txt', 'THIRD_PARTY_LICENSES.txt') { $files["plugins/MadelineInPharloom/$n"] = Join-Path $root "lib\$n" }
  foreach ($k in $files.Keys) {   # forward slashes: Melty wants them
    $e = $za.CreateEntry($k); $o = $e.Open(); $b = [IO.File]::ReadAllBytes($files[$k]); $o.Write($b, 0, $b.Length); $o.Dispose()
  }
  $za.Dispose(); $fs.Dispose()
  "zip $((Get-Item $zip).Length) bytes, sha256 $((Get-FileHash $zip -Algorithm SHA256).Hash.ToLower())"
}
