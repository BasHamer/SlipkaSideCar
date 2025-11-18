Param(
    [string]$Proj = 'c:\Github\SlipkaSideCar\src\Slipka\Slipka.csproj'
)

Write-Host "Checking outdated packages for $Proj"
$json = dotnet list $Proj package --outdated --include-transitive --format json
if (-not $json) { Write-Host 'No JSON output from dotnet list'; exit 0 }

try { $data = $json | ConvertFrom-Json } catch { Write-Host 'Failed to parse JSON'; exit 1 }

$updates = @()
foreach ($p in $data.projects) {
    foreach ($pkg in $p.packages) {
        if ($pkg.resolvedVersion -and $pkg.latestVersion -and $pkg.resolvedVersion -ne $pkg.latestVersion) {
            $rMajor = ($pkg.resolvedVersion.Split('.'))[0]
            $lMajor = ($pkg.latestVersion.Split('.'))[0]
            if ($rMajor -eq $lMajor) {
                $updates += [PSCustomObject]@{ Id = $pkg.id; Resolved = $pkg.resolvedVersion; Latest = $pkg.latestVersion }
            }
        }
    }
}

if (-not $updates) { Write-Host 'No same-major updates found'; exit 0 }

foreach ($u in $updates) {
    Write-Host "Updating $($u.Id) $($u.Resolved) -> $($u.Latest)"
    dotnet add $Proj package $($u.Id) --version $($u.Latest)
}

Write-Host "Restoring and building $Proj"
dotnet restore $Proj
$build = dotnet build $Proj
Write-Host $build

Write-Host 'Done'
exit 0
