# --- Build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copie des csproj pour un restore en cache.
COPY src/Backend/InventorySystem.Domain/InventorySystem.Domain.csproj src/Backend/InventorySystem.Domain/
COPY src/Backend/InventorySystem.Application/InventorySystem.Application.csproj src/Backend/InventorySystem.Application/
COPY src/Backend/InventorySystem.Infrastructure/InventorySystem.Infrastructure.csproj src/Backend/InventorySystem.Infrastructure/
COPY src/Backend/InventorySystem.Api/InventorySystem.Api.csproj src/Backend/InventorySystem.Api/
RUN dotnet restore src/Backend/InventorySystem.Api/InventorySystem.Api.csproj

# Copie du reste et publication.
COPY src/Backend/ src/Backend/
RUN dotnet publish src/Backend/InventorySystem.Api/InventorySystem.Api.csproj -c Release -o /app/publish

# --- Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "InventorySystem.Api.dll"]
