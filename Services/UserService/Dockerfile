# =========================
# Build Stage
# =========================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy project files
COPY Services/UserService/UserService.Api/UserService.Api.csproj Services/UserService/UserService.Api/
COPY Services/UserService/UserService.Application/UserService.Application.csproj Services/UserService/UserService.Application/
COPY Services/UserService/UserService.Domain/UserService.Domain.csproj Services/UserService/UserService.Domain/
COPY Services/UserService/UserService.Infrastructure/UserService.Infrastructure.csproj Services/UserService/UserService.Infrastructure/

# Restore dependencies inside Linux container
RUN dotnet restore Services/UserService/UserService.Api/UserService.Api.csproj

# Copy source code
COPY Services/UserService/ Services/UserService/

# Build
WORKDIR /src/Services/UserService/UserService.Api

RUN dotnet build UserService.Api.csproj \
    -c Release \
    -o /app/build


# =========================
# Publish Stage
# =========================

FROM build AS publish

RUN dotnet publish UserService.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false


# =========================
# Runtime Stage
# =========================

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "UserService.Api.dll"]