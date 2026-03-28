# --- STAGE 1: Angular Build ---
FROM node:25-alpine AS angular-builder
WORKDIR /app
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/. .
RUN npm run build

# --- STAGE 2: .NET Build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS dotnet-build
WORKDIR /src
COPY ["backend/Zaster/Zaster.csproj", "backend/Zaster/"]
RUN dotnet restore "backend/Zaster/Zaster.csproj"
COPY . .
WORKDIR "/src/backend/Zaster"
RUN dotnet publish -c Release -o /app/publish

# --- STAGE 3: Final Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# 1. Copy the C# backend
COPY --from=dotnet-build /app/publish .

# 2. Copy the Angular files directly into the backend's wwwroot
# Adjust path: usually dist/[projectname]/browser
COPY --from=angular-builder /app/dist/*/browser ./wwwroot

ENTRYPOINT ["dotnet", "Zaster.dll"]