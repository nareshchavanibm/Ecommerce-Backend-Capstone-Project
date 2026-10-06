FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY Services/OrderService/OrderService.Api/OrderService.Api.csproj Services/OrderService/OrderService.Api/
COPY Services/OrderService/OrderService.Application/OrderService.Application.csproj Services/OrderService/OrderService.Application/
COPY Services/OrderService/OrderService.Domain/OrderService.Domain.csproj Services/OrderService/OrderService.Domain/
COPY Services/OrderService/OrderService.Infrastructure/OrderService.Infrastructure.csproj Services/OrderService/OrderService.Infrastructure/

RUN dotnet restore Services/OrderService/OrderService.Api/OrderService.Api.csproj

COPY Services/OrderService/ Services/OrderService/

WORKDIR /src/Services/OrderService/OrderService.Api

RUN dotnet build OrderService.Api.csproj -c Release -o /app/build

FROM build AS publish

RUN dotnet publish OrderService.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "OrderService.Api.dll"]