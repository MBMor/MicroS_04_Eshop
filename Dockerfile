# syntax=docker/dockerfile:1.7

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS restore

ARG PROJECT_PATH

WORKDIR /src

COPY . .

RUN test -n "${PROJECT_PATH}" \
    && dotnet restore "${PROJECT_PATH}"


FROM restore AS publish

ARG PROJECT_PATH
ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "${PROJECT_PATH}" \
    --configuration "${BUILD_CONFIGURATION}" \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final

ARG APP_DLL

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    APP_DLL=${APP_DLL}

WORKDIR /app

COPY --from=publish /app/publish .

RUN test -n "${APP_DLL}"

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]
