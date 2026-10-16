param([Parameter(Mandatory=$true)][string[]]$Files)
$ErrorActionPreference = 'Stop'
$strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
foreach ($checkedFile in $Files) {
    $absoluteFile = (Resolve-Path -LiteralPath $checkedFile).Path
    $contents = $strictUtf8.GetString([System.IO.File]::ReadAllBytes($absoluteFile))
    if ($contents.Contains([string][char]0xfffd)) { throw "Replacement character: $absoluteFile" }
    if ($contents -match '"[^"\r\n]*\?{3,}[^"\r\n]*"' -or
        ($absoluteFile.EndsWith('.md') -and $contents -match '\?{3,}')) {
        throw "Possible lossy shell encoding: $absoluteFile"
    }
}
Write-Output "UTF-8 text check passed: $($Files.Count) files"
