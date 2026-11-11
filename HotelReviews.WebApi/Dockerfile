# DVO-L3-7F2A91
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore HotelReviews.WebApi/HotelReviews.WebApi.csproj
RUN dotnet publish HotelReviews.WebApi/HotelReviews.WebApi.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
ARG APP_VERSION=1.0.0
WORKDIR /app
EXPOSE 8080 8081
ENV ASPNETCORE_HTTP_PORTS=8080
LABEL org.opencontainers.image.version="${APP_VERSION}"
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HotelReviews.WebApi.dll"]
