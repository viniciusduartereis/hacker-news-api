FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
WORKDIR /app
EXPOSE 5005

ENV ASPNETCORE_HTTP_PORTS=5005

RUN apt-get update \
    && apt-get install -y --no-install-recommends 

RUN mkdir -p /var/lib/aspnet-dpkeys \
    && chown -R app:app /var/lib/aspnet-dpkeys

USER app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src


COPY ["HackerNewsApi.csproj", "HackerNewsApi/"]

RUN dotnet restore "HackerNewsApi/HackerNewsApi.csproj"

COPY . .

RUN dotnet build "HackerNewsApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HackerNewsApi.csproj" -c Release -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish --chown=app:app /app/publish .

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -f http://localhost:5005/api/health || exit 1

ENTRYPOINT ["dotnet", "HackerNewsApi.dll"]
