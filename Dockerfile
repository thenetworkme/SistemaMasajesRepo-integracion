# Esta fase usa una imagen de .NET SDK para compilar el proyecto
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore

# Esta fase usa una imagen de .NET SDK para compilar el proyecto de servicio
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
RUN BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SistemaMasajes.Integracion.csproj", "."]
RUN dotnet restore "./SistemaMasajes.Integracion.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "SistemaMasajes.Integracion.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Esta fase usa una imagen de .NET SDK para publicar el proyecto de servicio se usa en la fase final
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "SistemaMasajes.Integracion.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Esta fase usa una imagen de .NET Runtime para ejecutar la aplicación cuando no se usa la configuración de depuración
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SistemaMasajes.Integracion.dll"]