# --- Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY AssinaMais.Api/AssinaMais.Api.csproj AssinaMais.Api/
RUN dotnet restore AssinaMais.Api/AssinaMais.Api.csproj

COPY AssinaMais.Api/ AssinaMais.Api/
RUN dotnet publish AssinaMais.Api/AssinaMais.Api.csproj -c Release -o /app --no-restore

# --- Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AssinaMais.Api.dll"]
