FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the dependency layer is cached between code changes.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/BovinoVision.Dominio/BovinoVision.Dominio.csproj src/BovinoVision.Dominio/
COPY src/BovinoVision.Aplicacion/BovinoVision.Aplicacion.csproj src/BovinoVision.Aplicacion/
COPY src/BovinoVision.Infraestructura/BovinoVision.Infraestructura.csproj src/BovinoVision.Infraestructura/
COPY src/BovinoVision.Api/BovinoVision.Api.csproj src/BovinoVision.Api/
RUN dotnet restore src/BovinoVision.Api/BovinoVision.Api.csproj

COPY src/ src/
RUN dotnet publish src/BovinoVision.Api/BovinoVision.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8000
COPY --from=build /app .
RUN mkdir -p /app/uploads && chown -R app:app /app/uploads
USER app
EXPOSE 8000
ENTRYPOINT ["dotnet", "BovinoVision.Api.dll"]
