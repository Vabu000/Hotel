# DVO-L3-7F2A91
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore HotelPlatform.Aggregator/HotelPlatform.Aggregator.csproj
RUN mkdir -p /app/publish
RUN dotnet publish HotelPlatform.Aggregator/HotelPlatform.Aggregator.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
ARG APP_VERSION=1.0.0
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
LABEL org.opencontainers.image.version="${APP_VERSION}"
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HotelPlatform.Aggregator.dll"]
