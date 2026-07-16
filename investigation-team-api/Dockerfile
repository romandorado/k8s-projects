FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/InvestigationTeam.Api/*.csproj src/InvestigationTeam.Api/
RUN dotnet restore src/InvestigationTeam.Api/InvestigationTeam.Api.csproj

COPY src/InvestigationTeam.Api/ src/InvestigationTeam.Api/
RUN dotnet publish src/InvestigationTeam.Api/InvestigationTeam.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8000
EXPOSE 8000

ENTRYPOINT ["dotnet", "InvestigationTeam.Api.dll"]
