# syntax=docker/dockerfile:1

# ------------------------------------------------------------------------------
# Stage 1 - build the Angular client
#
# Runs first and independently of the .NET stages, so a change to C# code does not
# invalidate the npm layer and vice versa. package.json is copied before the source
# so `npm ci` is cached until dependencies actually change.
# ------------------------------------------------------------------------------
FROM node:22-alpine AS client
WORKDIR /client

COPY client/netwatch-web/package.json client/netwatch-web/package-lock.json ./
RUN npm ci

COPY client/netwatch-web/ ./
RUN npm run build -- --configuration production


# ------------------------------------------------------------------------------
# Stage 2 - restore and publish the API
#
# Only project files are copied before `dotnet restore`, so the restore layer is
# reused for every build that does not change a dependency.
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS server
WORKDIR /src

COPY Directory.Build.props NetWatch.sln ./
COPY src/NetWatch.Domain/NetWatch.Domain.csproj                     src/NetWatch.Domain/
COPY src/NetWatch.Application/NetWatch.Application.csproj           src/NetWatch.Application/
COPY src/NetWatch.Infrastructure/NetWatch.Infrastructure.csproj     src/NetWatch.Infrastructure/
COPY src/NetWatch.Api/NetWatch.Api.csproj                           src/NetWatch.Api/
COPY src/NetWatch.Migrations.Sqlite/NetWatch.Migrations.Sqlite.csproj       src/NetWatch.Migrations.Sqlite/
COPY src/NetWatch.Migrations.SqlServer/NetWatch.Migrations.SqlServer.csproj src/NetWatch.Migrations.SqlServer/

RUN dotnet restore src/NetWatch.Api/NetWatch.Api.csproj

COPY src/ src/
RUN dotnet publish src/NetWatch.Api/NetWatch.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish


# ------------------------------------------------------------------------------
# Stage 3 - runtime
#
# The SPA is served from the API's wwwroot, so one container and one origin serve
# both. That removes CORS from production entirely and halves what has to be
# deployed.
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
WORKDIR /app

# ICMP probes need raw sockets. Alpine's runtime image has no ping capability and
# containers do not get CAP_NET_RAW by default, so ICMP checks report "not
# permitted in this environment" unless the container is granted it explicitly
# (see docker-compose.yml). TCP and HTTP probes work with no extra privileges.
RUN apk add --no-cache iputils

# A non-root user: nothing here needs root, and a monitoring tool that reaches out
# to the network is exactly the kind of process to keep unprivileged.
RUN addgroup -S netwatch && adduser -S netwatch -G netwatch

COPY --from=server /app/publish ./
COPY --from=client /client/dist/netwatch-web/browser ./wwwroot

RUN mkdir -p /app/data && chown -R netwatch:netwatch /app
USER netwatch

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "NetWatch.Api.dll"]
