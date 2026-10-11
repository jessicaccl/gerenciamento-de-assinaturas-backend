# --- Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY GerenciamentodeAssinaturasBackend/GerenciamentodeAssinaturasBackend.csproj GerenciamentodeAssinaturasBackend/
RUN dotnet restore GerenciamentodeAssinaturasBackend/GerenciamentodeAssinaturasBackend.csproj

COPY GerenciamentodeAssinaturasBackend/ GerenciamentodeAssinaturasBackend/
RUN dotnet publish GerenciamentodeAssinaturasBackend/GerenciamentodeAssinaturasBackend.csproj -c Release -o /app --no-restore

# --- Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "GerenciamentodeAssinaturasBackend.dll"]
