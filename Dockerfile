FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY GestionePrenotazioni.slnx ./
COPY GestionePrenotazioni.Web/GestionePrenotazioni.Web.csproj GestionePrenotazioni.Web/
RUN dotnet restore GestionePrenotazioni.Web/GestionePrenotazioni.Web.csproj
COPY . .
RUN dotnet publish GestionePrenotazioni.Web/GestionePrenotazioni.Web.csproj -c Release -o /app/publish --no-restore

FROM runtime AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GestionePrenotazioni.Web.dll"]
