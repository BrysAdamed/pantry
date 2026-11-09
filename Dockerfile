# syntax=docker/dockerfile:1

FROM node:24-alpine AS web
WORKDIR /web
COPY src/Pantry.Web/package*.json ./
RUN npm ci
COPY src/Pantry.Web/ ./
RUN npx ng build --configuration production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Pantry.Api/Pantry.Api.csproj src/Pantry.Api/
RUN dotnet restore src/Pantry.Api/Pantry.Api.csproj
COPY src/Pantry.Api/ src/Pantry.Api/
COPY --from=web /web/dist/pantry-web/browser/ src/Pantry.Api/wwwroot/
RUN dotnet publish src/Pantry.Api/Pantry.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
RUN mkdir -p /data/recipe-photos && chown app:app /data/recipe-photos
USER app
ENTRYPOINT ["dotnet", "Pantry.Api.dll"]
