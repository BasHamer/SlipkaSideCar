$proj = 'c:\Github\SlipkaSideCar\src\Slipka\Slipka.csproj'
Write-Host "Updating top-level PackageReferences for $proj"

$updates = @(
    @{ Id = 'graphiql'; Version = '2.0.0' },
    @{ Id = 'GraphQL.Server.Transports.AspNetCore'; Version = '8.3.2' },
    @{ Id = 'Microsoft.VisualStudio.Web.CodeGeneration.Design'; Version = '9.0.0' },
    @{ Id = 'MongoDB.Driver'; Version = '3.5.0' },
    @{ Id = 'MongoDB.Driver.GridFS'; Version = '2.30.0' },
    @{ Id = 'Newtonsoft.Json'; Version = '13.0.4' }
)

foreach ($u in $updates) {
    Write-Host "dotnet add $proj package $($u.Id) --version $($u.Version)"
    dotnet add $proj package $($u.Id) --version $($u.Version)
}

Write-Host 'Restore and build'
dotnet restore $proj
dotnet build $proj
Write-Host 'Done'
