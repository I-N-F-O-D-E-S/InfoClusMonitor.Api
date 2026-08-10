# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar csproj y restaurar paquetes NuGet
COPY ["InfoClusMonitor.Api.csproj", "./"]
RUN dotnet restore "InfoClusMonitor.Api.csproj"

# Copiar todo el código fuente y publicar
COPY . .
RUN dotnet publish "InfoClusMonitor.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "InfoClusMonitor.Api.dll"]
