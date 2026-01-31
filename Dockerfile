# Use .NET 10.0 (Current LTS)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# FIX: Copy csproj from the ROOT directory, not a subfolder
COPY ["VoTales.API.csproj", "./"]
RUN dotnet restore "./VoTales.API.csproj"

# Copy everything else from the root
COPY . .

# Build
RUN dotnet build "./VoTales.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish Stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./VoTales.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final Stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VoTales.API.dll"]
