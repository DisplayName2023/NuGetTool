# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files and restore as distinct layers
COPY ["NuGetTool.Web/NuGetTool.Web.csproj", "NuGetTool.Web/"]
COPY ["NuGetTool.Core/NuGetTool.Core.csproj", "NuGetTool.Core/"]
RUN dotnet restore "NuGetTool.Web/NuGetTool.Web.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/NuGetTool.Web"
RUN dotnet build "NuGetTool.Web.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "NuGetTool.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage - using SDK image because the app needs to run dotnet pack/push at runtime
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NuGetTool.Web.dll"]
