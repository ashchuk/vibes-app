FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Vibes.API/Vibes.API.csproj", "Vibes.API/"]
RUN dotnet restore "Vibes.API/Vibes.API.csproj"
COPY . .
WORKDIR "/src/Vibes.API"
RUN dotnet build "./Vibes.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Vibes.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

USER root
RUN apt-get update && apt-get install -y --no-install-recommends \
    git \
    procps \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Vibes.API.dll"]