# Build context is the repository root: docker build -f src/Starter.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore in its own layer so package downloads are cached until a project file changes.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Starter.Domain/Starter.Domain.csproj src/Starter.Domain/
COPY src/Starter.Application/Starter.Application.csproj src/Starter.Application/
COPY src/Starter.Infrastructure/Starter.Infrastructure.csproj src/Starter.Infrastructure/
COPY src/Starter.Api/Starter.Api.csproj src/Starter.Api/
RUN dotnet restore src/Starter.Api/Starter.Api.csproj

COPY .editorconfig ./
COPY src/ src/
RUN dotnet publish src/Starter.Api/Starter.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# The base image provides a non-root "app" user; the API never needs root at runtime.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Starter.Api.dll"]
