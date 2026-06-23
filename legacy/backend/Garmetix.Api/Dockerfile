FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/Garmetix.Domain/Garmetix.Domain.csproj backend/Garmetix.Domain/
COPY backend/Garmetix.Infrastructure/Garmetix.Infrastructure.csproj backend/Garmetix.Infrastructure/
COPY backend/Garmetix.Api/Garmetix.Api.csproj backend/Garmetix.Api/
RUN dotnet restore backend/Garmetix.Api/Garmetix.Api.csproj
COPY backend/ backend/
RUN dotnet publish backend/Garmetix.Api/Garmetix.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends postgresql-client \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5080
EXPOSE 5080
ENTRYPOINT ["dotnet", "Garmetix.Api.dll"]
