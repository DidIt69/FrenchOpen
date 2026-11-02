FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/OpenDeFrance.Core/OpenDeFrance.Core.csproj", "src/OpenDeFrance.Core/"]
COPY ["src/OpenDeFrance.Infrastructure/OpenDeFrance.Infrastructure.csproj", "src/OpenDeFrance.Infrastructure/"]
COPY ["src/OpenDeFrance.Web/OpenDeFrance.Web.csproj", "src/OpenDeFrance.Web/"]

RUN dotnet restore "src/OpenDeFrance.Web/OpenDeFrance.Web.csproj"

COPY . .
WORKDIR "/src/src/OpenDeFrance.Web"
RUN dotnet publish "OpenDeFrance.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "OpenDeFrance.Web.dll"]
