# Use official ASP.NET Core runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Set ASP.NET Core environment (Production mode)
ENV DOTNET_ENVIRONMENT=Production

# Build stage: Use SDK image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["TennisBruck/TennisBruck.csproj", "TennisBruck/"]
COPY ["TennisDb/TennisDb.csproj", "TennisDb/"]
RUN dotnet restore "TennisBruck/TennisBruck.csproj"

# Copy remaining source code and build the project
COPY . .
WORKDIR "/src/TennisBruck"
RUN dotnet build "TennisBruck.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "TennisBruck.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Debugging: Print the environment
RUN echo "Running in environment: $DOTNET_ENVIRONMENT"

# Set the correct entrypoint
ENTRYPOINT ["dotnet", "TennisBruck.dll"]
