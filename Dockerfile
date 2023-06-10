FROM mcr.microsoft.com/dotnet/runtime:7.0-jammy AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["AutoDbBackup.csproj", "./"]
RUN dotnet restore "AutoDbBackup.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "AutoDbBackup.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AutoDbBackup.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN sh -c 'echo "deb http://apt.postgresql.org/pub/repos/apt $(lsb_release -cs)-pgdg main" > /etc/apt/sources.list.d/pgdg.list'
RUN wget -qO- https://www.postgresql.org/media/keys/ACCC4CF8.asc | sudo tee /etc/apt/trusted.gpg.d/pgdg.asc &>/dev/null
RUN apt update
RUN apt install -y postgresql-client

RUN mkdir /backups

ENTRYPOINT ["dotnet", "AutoDbBackup.dll"]
