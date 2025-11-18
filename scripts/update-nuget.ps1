Param(
    [string]$Root = (Get-Location)
)

$root = Resolve-Path $Root
$csprojs = Get-ChildItem -Path $root -Recurse -Filter *.csproj -ErrorAction SilentlyContinue
if (-not $csprojs) { Write-Host "No csproj files found in $root"; exit 0 }

foreach ($f in $csprojs) {
    $proj = $f.FullName
    Write-Host "\n=== Processing: $proj ==="
    dotnet restore $proj

    $json = dotnet list "$proj" package --outdated --include-transitive --format json 2>$null
    if (-not $json) { Write-Host "No outdated packages or failed to get JSON output"; continue }

    try { $data = $json | ConvertFrom-Json } catch { Write-Host "Failed to parse JSON for $proj"; continue }

    $updates = @()
    if ($data.projects) {
        foreach ($p in $data.projects) {
            if ($p.packages) {
                foreach ($pkg in $p.packages) {
                    if ($pkg.resolvedVersion -and $pkg.latestVersion -and $pkg.resolvedVersion -ne $pkg.latestVersion) {
                        $updates += [PSCustomObject]@{ Id = $pkg.id; Latest = $pkg.latestVersion }
                    }
                }
            }
        }
    }

    if (-not $updates) { Write-Host "No packages to update for this project"; continue }

    foreach ($u in $updates) {
        Write-Host "Updating $($u.Id) -> $($u.Latest)"
        dotnet add "$proj" package $($u.Id) --version $($u.Latest)
    }
}

Write-Host "\nRestoring all projects"
dotnet restore $root

Write-Host "\nBuilding projects"
foreach ($p in $csprojs) { dotnet build $p.FullName }

Write-Host "\nDone"
exit 0
