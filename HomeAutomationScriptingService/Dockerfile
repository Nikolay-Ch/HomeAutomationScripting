#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:9.0-noble-chiseled AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0.101-noble AS build
COPY ["HomeAutomationScriptingService/HomeAutomationScriptingService.csproj", "HomeAutomationScriptingService/"]
COPY ["HomeAutomationScriptingService/HomeAutomationScriptingService.csproj", "HomeAutomationScriptingService/"]

ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["HomeAutomationScriptingService/HomeAutomationScriptingService.csproj", "HomeAutomationScriptingService/"]
RUN dotnet restore "./HomeAutomationScriptingService/./HomeAutomationScriptingService.csproj"
COPY . .
WORKDIR "/src/HomeAutomationScriptingService"
RUN dotnet build "./HomeAutomationScriptingService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN groupadd -g 1538 scripting_service
RUN useradd -u 1538 -g 1538 scripting_service

ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./HomeAutomationScriptingService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

RUN mkdir -p /app/publish/scripts
RUN mkdir -p /app/publish/logs

FROM base AS final
COPY --from=publish /etc/passwd etc/passwd
COPY --from=publish /etc/group etc/group

USER scripting_service

WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "HomeAutomationScriptingService.dll"]
