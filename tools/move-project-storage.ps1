[CmdletBinding()]
param(
    [string]$DestinationRoot = 'E:\StutterFixWork',
    [switch]$Execute,
    [switch]$RemoveOriginals,
    [switch]$CompatibilityLinks
)

# Preview by default. Run from a terminal outside this repository after stopping
# game/editor/Git work. Existing E: folders and C:\SFBundle's junction stay intact.
$ErrorActionPreference = 'Stop'
$repoSource = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent)).TrimEnd('\')
$destination = [IO.Path]::GetFullPath($DestinationRoot).TrimEnd('\')
if ($destination -notlike 'E:\*' -or $destination -eq 'E:') {
    throw 'Destination must be a named directory under E:, never the drive root.'
}
if (-not (Test-Path -LiteralPath 'E:\')) { throw 'E: is unavailable.' }
$candidates = @(
    @{ Source = $repoSource; Name = 'StutterFix' },
    @{ Source = 'C:\Users\Public\StutterFixTrace'; Name = 'StutterFixTrace' },
    @{ Source = (Join-Path ([IO.Path]::GetTempPath()) 'PerfView'); Name = 'PerfViewCache' }
)
$plan = @()
foreach ($candidate in $candidates) {
    if (-not (Test-Path -LiteralPath $candidate.Source)) { continue }
    $source = (Get-Item -LiteralPath $candidate.Source -Force).FullName.TrimEnd('\')
    if ($source -notlike 'C:\*') { throw "Unexpected source drive: $source" }
    if ((Get-Item -LiteralPath $source -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Source is already a link; do not move it: $source"
    }
    $links = @(Get-ChildItem -LiteralPath $source -Force -Recurse -Attributes ReparsePoint)
    if ($links.Count) { throw "Review nested links before migration: $source" }
    $target = [IO.Path]::GetFullPath((Join-Path $destination $candidate.Name))
    if (-not $target.StartsWith($destination + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Target escapes destination: $target"
    }
    if (Test-Path -LiteralPath $target) { throw "Preserve existing destination: $target" }
    $files = @(Get-ChildItem -LiteralPath $source -File -Recurse -Force)
    $bytes = ($files | Measure-Object Length -Sum).Sum
    $plan += [pscustomobject]@{ Source = $source; Target = $target; Bytes = $bytes; Files = $files.Count }
}
$plan | Format-Table Source,Target,Files,@{n='GiB';e={[math]::Round($_.Bytes/1GB,3)}} -AutoSize
Write-Output 'C:\SFBundle is excluded: it already points to E:\SFBundle.'
if (-not $Execute) { Write-Output 'Preview only: no files changed.'; return }
[Environment]::CurrentDirectory = $destination
if ($CompatibilityLinks -and -not $RemoveOriginals) { throw 'Compatibility links require a completed move.' }
if (Get-Process -Name git,Unity,'A Dance of Fire and Ice' -ErrorAction SilentlyContinue) {
    throw 'Close game, Unity Editor and Git operations before migration.'
}
$required = ($plan | Measure-Object Bytes -Sum).Sum + 5GB
if ((Get-PSDrive E).Free -lt $required) { throw 'E: needs source bytes plus 5 GiB free.' }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Set-Location -LiteralPath $destination
$verification = @()
foreach ($item in $plan) {
    Write-Output "COPY $($item.Source) -> $($item.Target)"
    # One copy worker, no /MOVE or /MIR. Originals survive any copy/hash failure.
    & robocopy $item.Source $item.Target /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /XJ /NP /NFL /NDL
    if ($LASTEXITCODE -ge 8) { throw "Copy failed; originals retained: $($item.Source)" }
    $sourceFiles = @(Get-ChildItem -LiteralPath $item.Source -File -Recurse -Force)
    $targetFiles = @(Get-ChildItem -LiteralPath $item.Target -File -Recurse -Force)
    if ($sourceFiles.Count -ne $item.Files -or $targetFiles.Count -ne $item.Files) {
        throw "File count changed; originals retained: $($item.Source)"
    }
    $hashes = [Collections.Generic.List[object]]::new()
    foreach ($file in $sourceFiles) {
        $relative = $file.FullName.Substring($item.Source.Length + 1)
        $copy = Join-Path $item.Target $relative
        if (-not (Test-Path -LiteralPath $copy) -or (Get-Item -LiteralPath $copy).Length -ne $file.Length) {
            throw "Copy missing or size mismatch: $relative"
        }
        $sourceHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $sourceHash) {
            throw "SHA256 mismatch; originals retained: $relative"
        }
        $hashes.Add([pscustomobject]@{ Relative = $relative; SHA256 = $sourceHash })
        if ($hashes.Count % 1000 -eq 0) { Write-Output "HASH $($item.Source): $($hashes.Count)/$($item.Files)" }
    }
    Write-Output "VERIFIED $($item.Source): $($hashes.Count) files"
    $verification += [pscustomobject]@{ Source=$item.Source; Target=$item.Target; Files=$hashes }
}
$journal = Join-Path $destination 'migration-verified.json'
$verification | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $journal -Encoding utf8
if (-not $RemoveOriginals) {
    Write-Output "All copies verified. C: originals retained. Journal: $journal"
    Write-Output 'Open E:\StutterFixWork\StutterFix as the project before further work.'
    return
}
# Verify ALL sources again before deleting ANY source. Never derive a delete
# target from external command output, wildcards, or another shell.
foreach ($entry in $verification) {
    Write-Output "RECHECK $($entry.Source)"
    $absolute = [IO.Path]::GetFullPath($entry.Source).TrimEnd('\')
    if ($absolute -notin @($plan.Source) -or $absolute -notlike 'C:\*' -or $absolute -eq 'C:') {
        throw "Refuse unsafe source removal: $absolute"
    }
    if (@(Get-ChildItem -LiteralPath $absolute -File -Recurse -Force).Count -ne $entry.Files.Count) {
        throw "Source changed; all originals retained: $absolute"
    }
    foreach ($file in $entry.Files) {
        if ((Get-FileHash -LiteralPath (Join-Path $absolute $file.Relative)).Hash -ne $file.SHA256) {
            throw "Source changed; all originals retained: $absolute"
        }
    }
}
foreach ($entry in $verification) {
    $absolute = [IO.Path]::GetFullPath($entry.Source).TrimEnd('\')
    $target = [IO.Path]::GetFullPath($entry.Target)
    if ($absolute -notin @($plan.Source) -or
        -not $target.StartsWith($destination+'\',[StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $target)) { throw 'Removal path verification failed.' }
    if ((Get-Item -LiteralPath $absolute -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Source became a link; refuse removal: $absolute"
    }
    if ($CompatibilityLinks) {
        # Full filesystem access is required. These aliases preserve old tool
        # paths after moving physical data; they never grant extra permissions.
        $backup = $absolute + '.migration-verified-old'
        if (Test-Path -LiteralPath $backup) { throw "Preserve existing backup: $backup" }
        Rename-Item -LiteralPath $absolute -NewName ([IO.Path]::GetFileName($backup))
        try { New-Item -ItemType Junction -Path $absolute -Target $target | Out-Null }
        catch {
            Rename-Item -LiteralPath $backup -NewName ([IO.Path]::GetFileName($absolute))
            throw
        }
        $actualTarget = (Get-Item -LiteralPath $absolute -Force).Target
        if ($actualTarget -ne $target -or
            [IO.Path]::GetFullPath($backup) -ne $entry.Source+'.migration-verified-old') {
            throw 'Compatibility link verification failed; backup retained.'
        }
        Remove-Item -LiteralPath $backup -Recurse -Force
        Write-Output "MOVED $absolute -> $target (old path is a junction)"
    } else {
        Remove-Item -LiteralPath $absolute -Recurse -Force
        Write-Output "MOVED $absolute -> $target"
    }
}
Write-Output "Migration complete. Verified copies and journal: $destination"
