FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["TennisBruck/TennisBruck.csproj", "TennisBruck/"]
COPY ["TennisDb/TennisDb.csproj", "TennisDb/"]
RUN dotnet restore "TennisBruck/TennisBruck.csproj"
COPY . .
WORKDIR "/src/TennisBruck"
RUN dotnet build "TennisBruck.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "TennisBruck.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TennisBruck.dll"]
