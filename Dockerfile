FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY OstrunAuthService.sln .
COPY src/OstrunAuthService.Domain/*.csproj src/OstrunAuthService.Domain/
COPY src/OstrunAuthService.Application/*.csproj src/OstrunAuthService.Application/
COPY src/OstrunAuthService.Infrastructure/*.csproj src/OstrunAuthService.Infrastructure/
COPY src/OstrunAuthService.Api/*.csproj src/OstrunAuthService.Api/
RUN dotnet restore src/OstrunAuthService.Api/OstrunAuthService.Api.csproj

COPY src/ src/
RUN dotnet publish src/OstrunAuthService.Api/OstrunAuthService.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN useradd --uid 5678 --user-group --no-create-home appuser
COPY --from=build /app/publish .
USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "OstrunAuthService.Api.dll"]
