# Multi-stage build with layer caching optimization
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy solution and project files first for better caching
COPY audiarr.sln .
COPY src/Audiarr.Core/Audiarr.Core.csproj ./src/Audiarr.Core/
COPY src/Audiarr.Data/Audiarr.Data.csproj ./src/Audiarr.Data/
COPY src/Audiarr.Services/Audiarr.Services.csproj ./src/Audiarr.Services/
COPY src/Audiarr.Api/Audiarr.Api.csproj ./src/Audiarr.Api/
COPY tests/Audiarr.Tests/Audiarr.Tests.csproj ./tests/Audiarr.Tests/

# Restore dependencies as a separate layer
RUN dotnet restore

# Copy source code
COPY src/ ./src/
COPY tests/ ./tests/

# Build and publish the application
RUN dotnet publish src/Audiarr.Api/Audiarr.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:PublishTrimmed=false \
    /p:PublishSingleFile=false

# Runtime stage - optimized for production
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine
WORKDIR /app

# Install runtime dependencies
RUN apk add --no-cache \
    ffmpeg \
    ca-certificates \
    tzdata \
    && rm -rf /var/cache/apk/*

# Create non-root user and group
RUN addgroup -g 1000 audiarr && \
    adduser -u 1000 -G audiarr -s /bin/sh -D audiarr

# Create necessary directories with proper permissions
RUN mkdir -p /data /data/logs /data/artwork /music && \
    chown -R audiarr:audiarr /data /music

# Copy published application from build stage
COPY --from=build --chown=audiarr:audiarr /app/publish .

# Switch to non-root user
USER audiarr

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    MUSIC_LIBRARY_PATH=/music \
    DATA_PATH=/data

# Health check configuration
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

# Expose port
EXPOSE 8080

# Set the entry point
ENTRYPOINT ["dotnet", "Audiarr.Api.dll"]