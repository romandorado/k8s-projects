FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/InvestigationTeam.Chat.Api/*.csproj src/InvestigationTeam.Chat.Api/
RUN dotnet restore src/InvestigationTeam.Chat.Api/InvestigationTeam.Chat.Api.csproj

COPY src/InvestigationTeam.Chat.Api/ src/InvestigationTeam.Chat.Api/
RUN dotnet publish src/InvestigationTeam.Chat.Api/InvestigationTeam.Chat.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8000
EXPOSE 8000

ENTRYPOINT ["dotnet", "InvestigationTeam.Chat.Api.dll"]
