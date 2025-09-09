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

### Using Docker (Recommended)

Audiarr is designed to run in Docker for consistent behavior across all environments.

#### Option 1: Using Pre-built Image

```bash
docker run -d \
  --name audiarr \
  -p 8080:8080 \
  -v /path/to/your/music:/music:ro \
  -v audiarr_data:/data \
  ghcr.io/trevordavies095/audiarr:latest
```

#### Option 2: Using Docker Compose

1. Clone the repository:
```bash
git clone https://github.com/trevordavies095/audiarr.git
cd audiarr
```

2. Copy the example environment file and configure:
```bash
cp .env.example .env
# Edit .env to set your music library path
```

3. Start the server:
```bash
# For production deployment
docker-compose up -d

# For development with local builds
docker-compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

4. Access the admin interface at `http://localhost:8080/admin`
   - Default credentials: `admin` / `admin`
   - **Important**: Change the admin password after first login

5. Initiate a library scan from the admin panel or via API

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
- Docker and Docker Compose
- .NET 9 SDK (only for code editing/IDE support)

### Development Workflow

Audiarr uses a Docker-first development approach to ensure consistency between development and production environments.

1. Clone the repository:
```bash
git clone https://github.com/trevordavies095/audiarr.git
cd audiarr
```

2. Set up your development environment:
```bash
# Copy the example environment file
cp .env.example .env
# Edit .env to configure your settings
```

3. Start the development environment:
```bash
# This builds from source and enables hot reload
docker-compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

4. Access the application:
   - Admin interface: `http://localhost:8080/admin`
   - API: `http://localhost:8080/api/v2`

### Building for Production

Build the production Docker image:
```bash
docker build -t audiarr:latest .
```

Or use Docker Compose:
```bash
docker-compose build
```

## Configuration

Configuration is managed through environment variables and Docker volumes:

### Environment Variables
- `MUSIC_PATH`: Path to your music library (required)
- `HOST_PORT`: Port to expose Audiarr on (default: 8080)
- `TZ`: Timezone (default: UTC)
- `AUDIARR_TAG`: Docker image tag to use (default: latest)

### Advanced Configuration
JWT and other settings can be configured via environment variables:
- `JWT_SECRET_KEY`: JWT signing key (auto-generated if not set)
- `JWT_ISSUER`: Token issuer (default: AudiarrAPI)
- `JWT_AUDIENCE`: Token audience (default: AudiarrClient)
- `JWT_EXPIRATION_MINUTES`: Access token expiration (default: 60)
- `JWT_REFRESH_EXPIRATION_DAYS`: Refresh token expiration (default: 7)

See `.env.example` for all available options.

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
