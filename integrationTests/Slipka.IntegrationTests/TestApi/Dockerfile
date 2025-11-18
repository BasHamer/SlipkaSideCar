# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["Slipka.IntegrationTests/TestApi/TestApi.csproj", "TestApi/"]
RUN dotnet restore "TestApi/TestApi.csproj"

# Copy everything else and build
COPY ["Slipka.IntegrationTests/TestApi/", "TestApi/"]
WORKDIR "/src/TestApi"
RUN dotnet build "TestApi.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "TestApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create non-root user
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser:appuser /app
USER appuser

EXPOSE 80
ENTRYPOINT ["dotnet", "TestApi.dll"]
