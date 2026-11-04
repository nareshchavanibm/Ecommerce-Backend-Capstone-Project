FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY Services/Shared/ Services/Shared/
COPY Services/ReviewService/ReviewService.Api/ReviewService.Api.csproj Services/ReviewService/ReviewService.Api/

RUN dotnet restore Services/ReviewService/ReviewService.Api/ReviewService.Api.csproj

COPY Services/ReviewService/ Services/ReviewService/

WORKDIR /src/Services/ReviewService/ReviewService.Api

RUN dotnet build ReviewService.Api.csproj -c Release -o /app/build

FROM build AS publish

RUN dotnet publish ReviewService.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "ReviewService.Api.dll"]
