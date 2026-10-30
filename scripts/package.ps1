[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$app=Join-Path $root 'build/DragonCodexBoot'
if (!(Test-Path -LiteralPath (Join-Path $app 'DragonCodexBoot.exe'))) { throw 'Build and test first.' }
$version=(Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
$stage=Join-Path $root ('.local/package-'+[guid]::NewGuid().ToString('N'))
$dist=Join-Path $root 'dist'
New-Item -ItemType Directory -Path $stage,$dist -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $app 'DragonCodexBoot.exe') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'config/launcher.example.json') -Destination (Join-Path $stage 'launcher.json')
foreach($name in @('LICENSE','README.md','THIRD_PARTY_NOTICES.md','UPSTREAM.md','VERSION','Install-Start-Menu.cmd','Uninstall-Start-Menu.cmd','Rescan-Entrypoints.cmd','Restore-Original-Entrypoints.cmd')) {
 Copy-Item -LiteralPath (Join-Path $root $name) -Destination $stage
}
foreach($name in @('assets','media','docs')) { Copy-Item -LiteralPath (Join-Path $root $name) -Destination $stage -Recurse }
New-Item -ItemType Directory -Path (Join-Path $stage 'scripts') -Force | Out-Null
foreach($name in @('Import-Animation.ps1','install-start-menu.ps1','integrate-entrypoints.ps1')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $stage 'scripts') }
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
$zip=Join-Path $dist ('DragonCodexBoot-'+$version+'-win-x64-with-video.zip')
if(Test-Path -LiteralPath $zip) { throw 'Archive already exists; choose a new version or archive name.' }
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try {
 $prefix=$stage.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
 foreach($file in Get-ChildItem -LiteralPath $stage -File -Recurse) {
  $entry=$file.FullName.Substring($prefix.Length).Replace('\','/')
  $null=[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$entry,[IO.Compression.CompressionLevel]::Optimal)
 }
} finally { $archive.Dispose() }
$sha=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
($sha+'  '+[IO.Path]::GetFileName($zip)) | Set-Content -LiteralPath ($zip+'.sha256') -Encoding ASCII
Write-Output $zip
