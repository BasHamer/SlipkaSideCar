FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 4445
EXPOSE 61710-61920

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Microsoft.AspNetCore.Proxy/ Microsoft.AspNetCore.Proxy/
COPY Slipka/ Slipka/
WORKDIR /src/Slipka
RUN dotnet restore
RUN dotnet publish -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Slipka.dll"]
