FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SYSTEM-headless-client/*.csproj ./SYSTEM-headless-client/
RUN dotnet restore SYSTEM-headless-client/SYSTEM.HeadlessClient.csproj

COPY SYSTEM-headless-client/ ./SYSTEM-headless-client/
RUN dotnet publish SYSTEM-headless-client/SYSTEM.HeadlessClient.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .

ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "SYSTEM.HeadlessClient.dll"]
