$proj = 'c:\Github\SlipkaSideCar\src\Slipka\Slipka.csproj'
Write-Host "Restoring project $proj"
dotnet restore $proj

Write-Host "Getting outdated packages (json)"
$json = dotnet list $proj package --outdated --include-transitive --format json
if (-not $json) {
    Write-Host 'No json output from dotnet list --outdated'
    exit 0
}

try { $data = $json | ConvertFrom-Json } catch { Write-Host 'Failed parsing json'; exit 1 }

$updates = @()
foreach ($p in $data.projects) {
    foreach ($pkg in $p.packages) {
        if ($pkg.latestVersion -and $pkg.resolvedVersion -ne $pkg.latestVersion) {
            $updates += $pkg
        }
    }
}

if (-not $updates) { Write-Host 'No updates found'; exit 0 }

foreach ($pkg in $updates) {
    Write-Host "Updating $($pkg.id) $($pkg.resolvedVersion) -> $($pkg.latestVersion)"
    dotnet add $proj package $($pkg.id) --version $($pkg.latestVersion)
}

Write-Host 'Restoring and building'
dotnet restore $proj
$buildOut = dotnet build $proj
Write-Host $buildOut
Write-Host 'Done'
exit 0
