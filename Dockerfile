# One image for the whole app (ADR 0018): the SPA is built, then copied into the API's wwwroot so one origin serves both.

FROM node:26-alpine AS frontend
WORKDIR /src/frontend
COPY src/frontend/package.json src/frontend/package-lock.json ./
# --ignore-scripts: the "prepare" script installs git hooks, and there is no git here.
RUN npm ci --ignore-scripts
COPY src/frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/backend/ src/backend/
RUN dotnet publish src/backend/BabysittingPool.Api/BabysittingPool.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend /app ./
COPY --from=frontend /src/frontend/dist ./wwwroot
# The database and the rate-limit file live on the mounted volume.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Default="Data Source=/data/babysitting.db"
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "BabysittingPool.Api.dll"]
