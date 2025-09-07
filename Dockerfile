# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy solution and project files
COPY Audiarr.sln .
COPY Audiarr.Api/Audiarr.Api.csproj ./Audiarr.Api/

# Restore dependencies
RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish Audiarr.Api/Audiarr.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine
WORKDIR /app

# Install ffmpeg for audio processing
RUN apk add --no-cache ffmpeg

# Copy published application
COPY --from=build /app/publish .

# Create data directory for SQLite database
RUN mkdir -p /data

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV MUSIC_LIBRARY_PATH="/music"

EXPOSE 8080
ENTRYPOINT ["dotnet", "Audiarr.Api.dll"]
