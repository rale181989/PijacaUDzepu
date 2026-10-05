# Stage 1: Build Angular frontend
FROM node:22-alpine AS frontend-build
WORKDIR /app/client
COPY PijacaUDzepu.Client/package*.json ./
RUN npm ci --legacy-peer-deps
COPY PijacaUDzepu.Client/ ./
RUN npx ng build --configuration production

# Stage 2: Build .NET backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /app
COPY PijacaUDzepu.API/PijacaUDzepu.API.sln PijacaUDzepu.API/
COPY PijacaUDzepu.API/PijacaUDzepu.API/ PijacaUDzepu.API/PijacaUDzepu.API/
RUN dotnet publish PijacaUDzepu.API/PijacaUDzepu.API/PijacaUDzepu.API.csproj -c Release -o /app/publish

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=backend-build /app/publish ./
COPY --from=frontend-build /app/client/www ./wwwroot/
RUN mkdir -p /app/wwwroot/Resources/Images
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "PijacaUDzepu.API.dll"]
