# https://hub.docker.com/_/microsoft-dotnet
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app

COPY . .
RUN dotnet publish -c Release -o /out WebApi/WebApi.csproj


# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app
COPY --from=build /out ./

ENV ENV_PROJECT_NAME "./WebApi"

CMD $ENV_PROJECT_NAME