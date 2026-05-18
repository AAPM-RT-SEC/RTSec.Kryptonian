FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY RTSec.Kryptonian.DimseTlsProxy.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
VOLUME ["/certs"]
EXPOSE 4243 8044
ENTRYPOINT ["dotnet", "RTSec.Kryptonian.DimseTlsProxy.dll"]
