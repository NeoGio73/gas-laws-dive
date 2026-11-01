# Copies what may be public into the local copy of the public repository (C:\Users\giova\gas-laws-dive)
# and commits it there. Pushing is a separate step (add -Push), done only on Prof. Picione's word.
#
#   powershell -ExecutionPolicy Bypass -File tools\publish_public.ps1           (copy and commit)
#   powershell -ExecutionPolicy Bypass -File tools\publish_public.ps1 -Push     (also push)
#
# Public: the browser build (Builds\WebGL), our own scripts (without StudySecrets.cs and the test
# benches), the page template, questions.json (inside the build), tools\ (without tools\private,
# tools\public, tools\browser_steps and reference_codes.mjs), the README and the two licenses
# (from tools\public). Never public: StudySecrets.cs, the test usernames, purchased or BlenderKit
# assets, docs\, the Unity project. The script stops if it finds a secret value, a test username
# (in any file, the build included) or a purchased package's files.
param([switch]$Push, [string]$Message = "")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$pub = "C:\Users\giova\gas-laws-dive"
$build = Join-Path $root "Builds\WebGL"

if (-not (Test-Path (Join-Path $build "index.html"))) { throw "No browser build in $build. Run Dive Game > Build for the Browser first." }
if (-not (Test-Path $pub)) { New-Item -ItemType Directory $pub | Out-Null }
if (-not (Test-Path (Join-Path $pub ".git"))) { git -C $pub init -q -b main }

# Start from an empty folder (keeping git's own records), so removed files disappear too.
Get-ChildItem $pub -Force | Where-Object { $_.Name -ne ".git" } | Remove-Item -Recurse -Force

# 1. The game
Copy-Item (Join-Path $build "*") $pub -Recurse
New-Item -ItemType File (Join-Path $pub ".nojekyll") | Out-Null   # GitHub Pages serves the files as they are

# 2. Our own source, without the secrets or Unity's .meta files
$src = Join-Path $pub "source"
foreach ($part in "Scripts", "Editor", "Plugins") {
    $from = Join-Path $root "Assets\DiveGame\$part"
    Copy-Item $from (Join-Path $src $part) -Recurse
}
Copy-Item (Join-Path $root "Assets\WebGLTemplates\DiveGame") (Join-Path $src "WebGLTemplate") -Recurse
Get-ChildItem $src -Recurse -Filter *.meta | Remove-Item -Force
Remove-Item (Join-Path $src "Scripts\Study\StudySecrets.cs") -Force
Copy-Item (Join-Path $PSScriptRoot "public\StudySecrets.example.cs") (Join-Path $src "Scripts\Study\StudySecrets.example.cs")
# The test benches name the test usernames: private until the study ends (his rule, 2026-10-31).
Remove-Item (Join-Path $src "Scripts\Testing") -Recurse -Force

# 3. The tools, without the private parts, this folder's public templates and the test-username tools
$tools = Join-Path $pub "tools"
Copy-Item $PSScriptRoot $tools -Recurse
# tools\models holds the Blender working files, which contain BlenderKit models (royalty-free, not
# ours to share): never public.
foreach ($leave in "private", "public", "__pycache__", "browser_steps", "reference_codes.mjs", "models") {
    $p = Join-Path $tools $leave; if (Test-Path $p) { Remove-Item $p -Recurse -Force }
}

# 4. The README and the licenses
foreach ($f in "README.md", "LICENSE", "LICENSE-CONTENT.md") { Copy-Item (Join-Path $PSScriptRoot "public\$f") (Join-Path $pub $f) }

# 5. Safety checks: no secret values in any readable file, no purchased packages' files
$secrets = Select-String -Path (Join-Path $root "Assets\DiveGame\Scripts\Study\StudySecrets.cs") -Pattern '"([A-Za-z0-9]{16,})"' -AllMatches |
    ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value }
$text = Get-ChildItem $pub -Recurse -File | Where-Object { $_.FullName -notmatch '\\\.git\\' -and $_.Extension -notin '.unityweb', '.wasm', '.data' }
foreach ($s in $secrets) {
    $hit = $text | Select-String -SimpleMatch $s -List
    if ($hit) { throw "A secret value is in $($hit.Path). Nothing was committed." }
}
$bought = Get-ChildItem $pub -Recurse -File | Where-Object { $_.FullName -match 'Stylized Water|Staggart|Mixall|BlenderKit|blenderkit|LiquidVolume|HighlightPlus' }
if ($bought) { throw "Purchased package files found: $($bought[0].FullName). Nothing was committed." }
# No model files at all outside the build (the build's are packed inside its data file).
$models = Get-ChildItem $pub -Recurse -File | Where-Object { $_.FullName -notmatch '\\\.git\\' -and $_.Extension -in '.blend', '.blend1', '.fbx', '.glb', '.gltf', '.obj' }
if ($models) { throw "Model file found: $($models[0].FullName). Nothing was committed." }
# No test username anywhere, the build included (its files are compressed, so they are opened to look).
$tag = 'dive' + 'test'   # built from two parts so this script does not match itself
$users = $text | Select-String -Pattern $tag -List
if ($users) { throw "A test username is in $($users[0].Path). Nothing was committed." }
foreach ($b in Get-ChildItem (Join-Path $pub "Build") -Filter *.unityweb) {
    $in = [IO.File]::OpenRead($b.FullName)
    $z = New-Object System.IO.Compression.GZipStream($in, [IO.Compression.CompressionMode]::Decompress)
    $out = New-Object System.IO.MemoryStream; $z.CopyTo($out); $z.Dispose(); $in.Dispose()
    $plain = [Text.Encoding]::ASCII.GetString($out.ToArray()) + [Text.Encoding]::Unicode.GetString($out.ToArray())
    if ($plain -match $tag) { throw "A test username is inside the build ($($b.Name)). Nothing was committed." }
}

# 6. Commit
$version = (Select-String -Path (Join-Path $root "Assets\DiveGame\Scripts\Core\GameConfig.cs") -Pattern 'BuildVersion = "([^"]+)"').Matches[0].Groups[1].Value
$private = (git -C $root rev-parse --short HEAD)
if (-not $Message) { $Message = "Build $version (private project $private)" }
git -C $pub add -A
git -C $pub diff --cached --quiet
if ($LASTEXITCODE -eq 0) { Write-Host "Nothing changed since the last publish." }
else { git -C $pub commit -q -m $Message; Write-Host "Committed: $Message" }

if ($Push) {
    git -C $pub push -u origin main
    Write-Host "Pushed. Give GitHub Pages a minute, then open https://neogio73.github.io/gas-laws-dive/"
}
