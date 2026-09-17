# Use official ASP.NET Core runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

USER root
RUN apt-get update && apt-get install -y libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
USER app

EXPOSE 8080

# Build stage: Use SDK image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files and restore dependencies (optimizes Docker layer caching)
COPY ["TennisBruck/TennisBruck.csproj", "TennisBruck/"]
COPY ["TennisDb/TennisDb.csproj", "TennisDb/"]
RUN dotnet restore "TennisBruck/TennisBruck.csproj"

# Copy remaining source code and publish
COPY . .
WORKDIR "/src/TennisBruck"
RUN dotnet publish "TennisBruck.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM base AS final
WORKDIR /app
COPY --from=build --chown=app:app /app/publish .

ENTRYPOINT ["dotnet", "TennisBruck.dll"]
