<#
.SYNOPSIS
Lists the game versions to test against, as a GitHub Actions matrix: one entry per version, with the Steam branch that
serves it.

.DESCRIPTION
The build registry of Bannerlord.ReferenceAssemblies records which build each Steam branch points at, and the version
of every build. A version is served by its own branch (v1.3.4) where TaleWorlds made one, else by public or beta.

By default every release version from the oldest the project supports (GameVersion in build/common.props) up to the
current beta. With -Current, only the versions public and beta serve right now.

DepotDownloader falls back to public when asked for a branch that does not exist, so the branch here is always one the
registry has seen; the workflow still checks the version it got.
#>
param(
    [switch] $Current,
    [string] $Registry = 'https://raw.githubusercontent.com/BUTR/Bannerlord.ReferenceAssemblies/master/builds/steam/261550.json'
)

$ErrorActionPreference = 'Stop'

$builds = Invoke-RestMethod $Registry
$versionOf = @{}
foreach ($build in $builds.builds) { $versionOf["$($build.buildId)"] = $build.version }

$candidates = foreach ($branch in $builds.current.PSObject.Properties) {
    $name = $branch.Name
    $own = $name -match '^v\d+\.\d+\.\d+$'
    if (-not $own -and $name -notin 'public', 'beta') { continue }
    if ($Current -and $name -notin 'public', 'beta') { continue }
    if ($versionOf["$($branch.Value)"] -notmatch '^v(\d+\.\d+\.\d+)$') { continue }
    [pscustomobject]@{
        Version = [version] $Matches[1]
        Branch  = $name
        # Its own branch first; public over beta when both serve the same version
        Rank    = if ($Current) { @{ public = 0; beta = 1 }[$name] } elseif ($own) { 0 } elseif ($name -eq 'public') { 1 } else { 2 }
    }
}

$minimum = [version] (Select-Xml -Path build/common.props -XPath '//GameVersion').Node.InnerText
$matrix = @($candidates |
    Where-Object { $Current -or $_.Version -ge $minimum } |
    Group-Object Version |
    ForEach-Object { $_.Group | Sort-Object Rank | Select-Object -First 1 } |
    Sort-Object Version |
    ForEach-Object { [ordered]@{ version = "$($_.Version)"; branch = $_.Branch } })

$json = ConvertTo-Json -InputObject $matrix -Compress
Write-Host "Game versions: $json"
if ($env:GITHUB_OUTPUT) { "matrix=$json" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8 }
