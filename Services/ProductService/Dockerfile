FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy project files first for better Docker layer caching
COPY Services/ProductService/ProductService.Api/ProductService.Api.csproj Services/ProductService/ProductService.Api/
COPY Services/ProductService/ProductService.Application/ProductService.Application.csproj Services/ProductService/ProductService.Application/
COPY Services/ProductService/ProductService.Domain/ProductService.Domain.csproj Services/ProductService/ProductService.Domain/
COPY Services/ProductService/ProductService.Infrastructure/ProductService.Infrastructure.csproj Services/ProductService/ProductService.Infrastructure/

# Restore NuGet packages inside the Linux container
RUN dotnet restore Services/ProductService/ProductService.Api/ProductService.Api.csproj

# Copy the complete ProductService source code
COPY Services/ProductService/ Services/ProductService/

WORKDIR /src/Services/ProductService/ProductService.Api

# Build
RUN dotnet build ProductService.Api.csproj -c Release -o /app/build

# Publish
FROM build AS publish

RUN dotnet publish ProductService.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "ProductService.Api.dll"]