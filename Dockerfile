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

RUN apt update
RUN apt install -y curl ca-certificates gnupg
RUN curl https://www.postgresql.org/media/keys/ACCC4CF8.asc | gpg --dearmor | tee /etc/apt/trusted.gpg.d/apt.postgresql.org.gpg >/dev/null
RUN sh -c 'echo "deb http://apt.postgresql.org/pub/repos/apt/ jammy-pgdg main" >> /etc/apt/sources.list.d/postgresql.list'
RUN apt update
RUN apt install -y postgresql-client-15

RUN mkdir /backups

ENTRYPOINT ["dotnet", "AutoDbBackup.dll"]
