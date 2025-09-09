FROM mcr.microsoft.com/dotnet/sdk:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy source code
COPY . .

# Restore dependencies
RUN dotnet restore "Redish.Server/Redish.Server.csproj"

# Build the application
WORKDIR /src/Redish.Server
RUN dotnet build Redish.Server.csproj -c Release -o /app/build -p:GeneratePackageOnBuild=false

FROM build AS publish
RUN dotnet publish Redish.Server.csproj -c Release -o /app/publish /p:UseAppHost=false -p:GeneratePackageOnBuild=false

FROM base AS final
RUN apt-get update && apt-get install -y iputils-ping traceroute net-tools curl wget dnsutils iproute2 vim file && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=publish /app/publish .

# Create directories
RUN mkdir -p /app/logs

# Expose port
EXPOSE 6379

ENTRYPOINT ["dotnet", "Redish.Server.dll"]