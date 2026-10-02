# FoundU API - ASP.NET Core 8. Built from the api/ folder: docker build -t foundu-api api
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Project files first, so a code-only change reuses the restored packages.
COPY src/FoundU.Domain/FoundU.Domain.csproj src/FoundU.Domain/
COPY src/FoundU.Application/FoundU.Application.csproj src/FoundU.Application/
COPY src/FoundU.Infrastructure/FoundU.Infrastructure.csproj src/FoundU.Infrastructure/
COPY src/FoundU.Api/FoundU.Api.csproj src/FoundU.Api/
RUN dotnet restore src/FoundU.Api/FoundU.Api.csproj
COPY src/ src/
RUN dotnet publish src/FoundU.Api/FoundU.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
# Uploaded photos live here. On Render, mount a persistent disk at this path to keep them
# across deploys - without one they are lost on every redeploy.
RUN mkdir -p /app/wwwroot/uploads
EXPOSE 10000
# Render chooses the port and passes it as PORT.
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000} exec dotnet FoundU.Api.dll"]
