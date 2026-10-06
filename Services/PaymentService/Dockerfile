FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY Services/PaymentService/PaymentService.Api/PaymentService.Api.csproj Services/PaymentService/PaymentService.Api/
COPY Services/PaymentService/PaymentService.Application/PaymentService.Application.csproj Services/PaymentService/PaymentService.Application/
COPY Services/PaymentService/PaymentService.Domain/PaymentService.Domain.csproj Services/PaymentService/PaymentService.Domain/
COPY Services/PaymentService/PaymentService.Infrastructure/PaymentService.Infrastructure.csproj Services/PaymentService/PaymentService.Infrastructure/

RUN dotnet restore Services/PaymentService/PaymentService.Api/PaymentService.Api.csproj

COPY Services/PaymentService/ Services/PaymentService/

WORKDIR /src/Services/PaymentService/PaymentService.Api

RUN dotnet build PaymentService.Api.csproj -c Release -o /app/build

FROM build AS publish

RUN dotnet publish PaymentService.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "PaymentService.Api.dll"]