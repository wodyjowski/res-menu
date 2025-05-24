FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["res-menu.csproj", "./"]
RUN dotnet restore "res-menu.csproj"
COPY . .
RUN dotnet build "res-menu.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "res-menu.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "res-menu.dll"] 