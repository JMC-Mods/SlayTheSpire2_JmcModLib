param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestPath,

    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ManifestPath)) {
    throw "找不到 MOD 发布清单：$ManifestPath"
}

$resolvedPath = (Resolve-Path -LiteralPath $ManifestPath).ProviderPath
$rawJson = Get-Content -LiteralPath $resolvedPath -Raw -Encoding UTF8
$manifest = $rawJson | ConvertFrom-Json

if ($null -eq $manifest.PSObject.Properties['version']) {
    throw "MOD 发布清单缺少 version 字段：$resolvedPath"
}

$versionPattern = [System.Text.RegularExpressions.Regex]::new('("version"\s*:\s*")[^"]*(")')
if (-not $versionPattern.IsMatch($rawJson)) {
    throw "MOD 发布清单的 version 字段不是字符串：$resolvedPath"
}

$json = $versionPattern.Replace(
    $rawJson,
    {
        param($match)
        $match.Groups[1].Value + $Version + $match.Groups[2].Value
    },
    1).TrimEnd("`r", "`n")

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedPath, $json + [Environment]::NewLine, $utf8NoBom)
