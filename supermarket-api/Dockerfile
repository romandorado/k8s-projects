FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Supermarket.Api/*.csproj src/Supermarket.Api/
RUN dotnet restore src/Supermarket.Api/Supermarket.Api.csproj

COPY src/Supermarket.Api/ src/Supermarket.Api/
RUN dotnet publish src/Supermarket.Api/Supermarket.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8000
EXPOSE 8000

ENTRYPOINT ["dotnet", "Supermarket.Api.dll"]
