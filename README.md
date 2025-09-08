# Audiarr

A self-hosted music streaming server built with .NET 9 that provides a comprehensive API for managing and streaming your personal music library. Designed as a backend service for client applications to integrate with.

## Features

- **Music Library Management**: Automatic scanning and metadata extraction for all common audio formats (MP3, FLAC, M4A, AAC, OGG, Opus, WAV, WMA, ALAC, APE, WavPack)
- **RESTful API**: Complete API v2 with JWT authentication and refresh tokens
- **Audio Streaming**: HTTP range request support for efficient streaming and seeking
- **Admin Interface**: Built-in Blazor Server admin panel for library management and testing
- **Real-time Updates**: SignalR/WebSocket support for live scan progress and notifications
- **Album Artwork**: Automatic extraction and serving of embedded album art
- **Search**: Full-text search across artists, albums, and tracks
- **Docker Support**: Optimized Alpine Linux containers with multi-stage builds

**Note**: While the server includes a web UI for music playback, this is primarily for testing and administration. Audiarr is designed as a backend service for dedicated client applications to integrate with.

## Tech Stack

- **.NET 9** with C# 13
- **Entity Framework Core 9** with SQLite
- **Blazor Server** for admin interface
- **SignalR** for real-time communication
- **Serilog** for structured logging
- **Docker** with Alpine Linux base images

## Quick Start

### Using Docker Compose

1. Clone the repository:
```bash
git clone https://github.com/yourusername/audiarr.git
cd audiarr
```

2. Create docker-compose.yml or use the provided one:
```yaml
services:
  audiarr:
    build: .
    ports:
      - "8080:8080"
    volumes:
      - /path/to/your/music:/music:ro
      - audiarr_data:/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    restart: unless-stopped

volumes:
  audiarr_data:
```

3. Start the server:
```bash
docker-compose up -d
```

4. Access the admin interface at `http://localhost:8080/admin`
   - Default credentials: `admin` / `admin`

5. Initiate a library scan from the admin panel or via API

### Using Pre-built Docker Image

```bash
docker run -d \
  --name audiarr \
  -p 8080:8080 \
  -v /path/to/your/music:/music:ro \
  -v audiarr_data:/data \
  ghcr.io/yourusername/audiarr:latest
```

## API Usage

### Authentication
```bash
# Login
curl -X POST http://localhost:8080/api/v2/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin"}'

# Returns: { "accessToken": "...", "refreshToken": "...", "expiresAt": "..." }
```

### Library Operations
```bash
# Start library scan
curl -X POST http://localhost:8080/api/v2/scanner/scan \
  -H "Authorization: Bearer <token>"

# Get all tracks
curl http://localhost:8080/api/v2/tracks \
  -H "Authorization: Bearer <token>"

# Stream a track
curl http://localhost:8080/api/v2/stream/{trackId} \
  -H "Authorization: Bearer <token>" \
  -H "Range: bytes=0-" \
  --output track.mp3
```

## Documentation

Comprehensive documentation is available in the `/docs` directory:

- [API Integration Guide](docs/API_INTEGRATION.md) - Complete API reference
- [Quick Start Guide](docs/QUICK_START.md) - Detailed setup instructions
- [API Models](docs/API_MODELS.md) - Data structures and DTOs
- [iOS Client Guide](docs/iOS_CLIENT_GUIDE.md) - iOS app integration
- [Postman Collection](docs/POSTMAN_COLLECTION.md) - API testing collection

## Development

### Prerequisites
- .NET 9 SDK
- Docker (optional)
- SQLite

### Local Development
```bash
# Clone repository
git clone https://github.com/yourusername/audiarr.git
cd audiarr

# Restore dependencies
dotnet restore

# Run migrations
dotnet ef database update -p src/Audiarr.Data -s src/Audiarr.Api

# Run the server
dotnet run --project src/Audiarr.Api

# Access at http://localhost:5279 (development) or configured port
```

### Building Docker Image
```bash
docker build -t audiarr:latest .
```

## Configuration

Key configuration options in `appsettings.json`:

- `JwtSettings`: Token expiration and signing keys
- `ConnectionStrings`: Database location
- `Logging`: Log levels and output
- Music library path: Set via `MUSIC_LIBRARY_PATH` environment variable

## System Requirements

- **Minimum**: 512MB RAM, 1 CPU core
- **Recommended**: 1GB RAM, 2 CPU cores
- **Storage**: Varies based on library size (database typically < 100MB for 10,000 tracks)

## License

MIT License - See [LICENSE](LICENSE) file for details

## Contributing

Contributions are welcome! Please read the contributing guidelines before submitting pull requests.

## Support

For issues, feature requests, or questions, please use the [GitHub Issues](https://github.com/yourusername/audiarr/issues) page.
