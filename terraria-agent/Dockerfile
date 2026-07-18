FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Terraria.Agent.Api/*.csproj src/Terraria.Agent.Api/
RUN dotnet restore src/Terraria.Agent.Api/Terraria.Agent.Api.csproj

COPY src/Terraria.Agent.Api/ src/Terraria.Agent.Api/
RUN dotnet publish src/Terraria.Agent.Api/Terraria.Agent.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Terraria.Agent.Api.dll"]
