# Builds dist\DoraSaverSetup.exe: one self-contained installer.
#
#   1. Fetch the pinned Ruffle web build (verified by SHA-256) into assets\ruffle
#   2. Run the unit tests
#   3. Build one small .scr host per screensaver (the id is baked in with -p:SaverId=...)
#   4. Pack Ruffle, the player page, the WebView2 SDK files and the hosts into payload.zip
#   5. Build the setup with that payload embedded
#
# No Doraemon assets are involved: the setup downloads the originals from the Internet Archive.
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$src = Join-Path $root 'src'
$work = Join-Path $root 'obj-payload'
$dist = Join-Path $root 'dist'

$RuffleUrl = 'https://github.com/ruffle-rs/ruffle/releases/download/nightly-2026-09-27/ruffle-nightly-2026_09_27-web-selfhosted.zip'
$RuffleSha256 = 'B6D0769BCB1FD523D3B5F9346D1581DE5C9B19A90AABDDF2E779A12A455CA1B6'

function Invoke-Checked([scriptblock]$Command, [string]$What) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$What failed (exit code $LASTEXITCODE)" }
}

# 1. Ruffle: the zip is cached under its hash, verified and freshly expanded on every build
$ruffleDir = Join-Path $root 'assets\ruffle'
$ruffleZip = Join-Path $root "vendor\ruffle-$RuffleSha256.zip"
if (-not (Test-Path $ruffleZip) -or (Get-FileHash $ruffleZip -Algorithm SHA256).Hash -ne $RuffleSha256) {
    New-Item -ItemType Directory -Force (Split-Path $ruffleZip) | Out-Null
    Write-Host "Downloading Ruffle: $RuffleUrl"
    Invoke-WebRequest -UseBasicParsing $RuffleUrl -OutFile $ruffleZip
}
$actual = (Get-FileHash $ruffleZip -Algorithm SHA256).Hash
if ($actual -ne $RuffleSha256) { throw "Ruffle SHA-256 mismatch: $actual" }
if (Test-Path $ruffleDir) { Remove-Item $ruffleDir -Recurse -Force }
Expand-Archive $ruffleZip -DestinationPath $ruffleDir

# 2. Tests
Invoke-Checked { dotnet test (Join-Path $src 'DoraSaver.Tests\DoraSaver.Tests.csproj') -c $Configuration --nologo -v q } 'Unit tests'

# 3. Hosts
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
$stage = Join-Path $work 'stage'
New-Item -ItemType Directory -Force (Join-Path $stage 'scr'), (Join-Path $stage 'ruffle') | Out-Null
$catalog = Get-Content (Join-Path $src 'DoraSaver\Core\SaverCatalog.cs') -Raw -Encoding UTF8
$ids = [regex]::Matches($catalog, '(?m)^\s+new\("([a-z0-9]+)",') | ForEach-Object { $_.Groups[1].Value }
if ($ids.Count -lt 1) { throw 'No screensaver ids found in SaverCatalog.cs' }

foreach ($id in $ids) {
    $out = Join-Path $work "host\$id"
    Invoke-Checked { dotnet build (Join-Path $src 'DoraSaver\DoraSaver.csproj') -c $Configuration --nologo -v q -p:SaverId=$id -p:DebugType=none -o $out } "Host build ($id)"
    $hostExe = Join-Path $out 'DoraSaver.exe'
    if ((Get-Item $hostExe).VersionInfo.ProductName -ne 'DoraSaver') { throw "Host $id has the wrong ProductName" }
    # AssemblyMetadata("SaverId", id) is stored as UTF-8 in the attribute blob.
    $text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($hostExe))
    if ($text -notmatch ('(?s)SaverId.{1,2}' + [regex]::Escape($id) + '\x00')) { throw "Host $id does not carry SaverId=$id" }
    Copy-Item $hostExe (Join-Path $stage "scr\$id.scr")
}

# 4. Payload
$firstHost = Join-Path $work "host\$($ids[0])"
Copy-Item (Join-Path $firstHost 'Microsoft.Web.WebView2.Core.dll'), (Join-Path $firstHost 'Microsoft.Web.WebView2.WinForms.dll') $stage
Copy-Item (Join-Path $firstHost 'runtimes') $stage -Recurse
Copy-Item (Join-Path $root 'assets\player.html'), (Join-Path $root 'assets\player.js') $stage
Get-ChildItem $ruffleDir -File | Where-Object Extension -ne '.map' | Copy-Item -Destination (Join-Path $stage 'ruffle')
Copy-Item (Join-Path $root 'LICENSE'), (Join-Path $root 'THIRD-PARTY-NOTICES.md') $stage
$payload = Join-Path $work 'payload.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $payload -CompressionLevel Optimal

# 5. Setup
$setupOut = Join-Path $work 'setup'
Invoke-Checked { dotnet build (Join-Path $src 'DoraSaver.Setup\DoraSaver.Setup.csproj') -c $Configuration --nologo -v q -p:DebugType=none -p:RequirePayload=true "-p:PayloadZip=$payload" -o $setupOut } 'Setup build'
New-Item -ItemType Directory -Force $dist | Out-Null
Copy-Item (Join-Path $setupOut 'DoraSaverSetup.exe') $dist -Force

$exe = Get-Item (Join-Path $dist 'DoraSaverSetup.exe')
'{0}  {1:N1} MB  SHA-256 {2}' -f $exe.FullName, ($exe.Length / 1MB), (Get-FileHash $exe.FullName -Algorithm SHA256).Hash
