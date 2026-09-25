FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/FlowForge.Api/FlowForge.Api.csproj", "src/FlowForge.Api/"]
COPY ["src/FlowForge.Application/FlowForge.Application.csproj", "src/FlowForge.Application/"]
COPY ["src/FlowForge.Abstractions/FlowForge.Abstractions.csproj", "src/FlowForge.Abstractions/"]
COPY ["src/FlowForge.Core/FlowForge.Core.csproj", "src/FlowForge.Core/"]
RUN dotnet restore "src/FlowForge.Api/FlowForge.Api.csproj"

COPY . .
WORKDIR /src/src/FlowForge.Api
RUN dotnet publish "FlowForge.Api.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "FlowForge.Api.dll"]
