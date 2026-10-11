FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY Services/ChatService/ChatService.Api/ChatService.Api.csproj Services/ChatService/ChatService.Api/

RUN dotnet restore Services/ChatService/ChatService.Api/ChatService.Api.csproj

COPY Services/ChatService/ Services/ChatService/

WORKDIR /src/Services/ChatService/ChatService.Api

RUN dotnet build ChatService.Api.csproj -c Release -o /app/build

FROM build AS publish

RUN dotnet publish ChatService.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "ChatService.Api.dll"]
