# Audiarr API Integration Guide

## Table of Contents
1. [Overview](#overview)
2. [Base Configuration](#base-configuration)
3. [Authentication](#authentication)
4. [API Endpoints](#api-endpoints)
5. [WebSocket/SignalR](#websocketsignalr)
6. [Streaming Audio](#streaming-audio)
7. [Error Handling](#error-handling)
8. [Best Practices](#best-practices)

## Overview

Audiarr is a self-hosted music streaming server that provides a RESTful API for accessing your music library. The API uses JWT authentication and supports real-time updates via SignalR/WebSockets.

### Key Features
- JWT-based authentication with refresh tokens
- RESTful API with JSON responses
- Real-time updates via SignalR
- HTTP range request support for audio streaming
- Comprehensive metadata management
- Album artwork support

### API Version
Current API version: `v2`
Base path: `/api/v2`

## Base Configuration

### Server URL
```
http://your-server:8080
```

### Headers
All API requests should include:
```http
Content-Type: application/json
Accept: application/json
Authorization: Bearer <access_token>  # For authenticated endpoints
```

## Authentication

Audiarr uses JWT (JSON Web Tokens) for authentication with a refresh token mechanism.

### Login Flow

#### 1. Initial Login
```http
POST /api/v2/auth/login
Content-Type: application/json

{
  "username": "your_username",
  "password": "your_password"
}
```

**Response:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "d2f8c3a9-...",
  "expiresAt": "2024-01-15T10:30:00Z",
  "user": {
    "id": "user_id",
    "username": "your_username",
    "email": "user@example.com",
    "role": "User",
    "lastLogin": "2024-01-15T09:30:00Z"
  }
}
```

#### 2. Using the Access Token
Include the access token in the Authorization header:
```http
GET /api/v2/tracks
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

#### 3. Refreshing Tokens
When the access token expires (after 60 minutes by default):
```http
POST /api/v2/auth/refresh
Content-Type: application/json

{
  "refreshToken": "d2f8c3a9-..."
}
```

**Response:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "a1b2c3d4-...",
  "expiresAt": "2024-01-15T11:30:00Z"
}
```

#### 4. Logout
```http
POST /api/v2/auth/logout
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
Content-Type: application/json

{
  "refreshToken": "d2f8c3a9-..."
}
```

### Token Storage Best Practices
- **Access Token**: Store in memory or secure temporary storage
- **Refresh Token**: Store in secure persistent storage (Keychain on iOS, Keystore on Android)
- Never store tokens in plain text files or UserDefaults/SharedPreferences

## API Endpoints

### Artists

#### Get All Artists
```http
GET /api/v2/artists?page=1&limit=50
Authorization: Bearer <token>
```

**Response:**
```json
{
  "data": [
    {
      "id": "artist_id",
      "name": "Artist Name",
      "sortName": "Artist Name",
      "albumCount": 5,
      "trackCount": 47
    }
  ],
  "page": 1,
  "limit": 50,
  "total": 120,
  "totalPages": 3
}
```

#### Get Artist Details
```http
GET /api/v2/artists/{id}
Authorization: Bearer <token>
```

#### Get Artist's Albums
```http
GET /api/v2/artists/{id}/albums
Authorization: Bearer <token>
```

#### Get Artist's Tracks
```http
GET /api/v2/artists/{id}/tracks
Authorization: Bearer <token>
```

### Albums

#### Get All Albums
```http
GET /api/v2/albums?page=1&limit=50
Authorization: Bearer <token>
```

#### Get Album Details
```http
GET /api/v2/albums/{id}
Authorization: Bearer <token>
```

**Response:**
```json
{
  "id": "album_id",
  "title": "Album Title",
  "artistId": "artist_id",
  "artistName": "Artist Name",
  "year": 2023,
  "trackCount": 12,
  "genre": "Rock",
  "coverArtPath": "/artwork/album_id.jpg",
  "totalDurationMs": 2580000,
  "tracks": [
    {
      "id": "track_id",
      "title": "Track Title",
      "trackNumber": 1,
      "discNumber": 1,
      "durationMs": 215000
    }
  ]
}
```

#### Get Album Cover Art
```http
GET /api/v2/albums/{id}/cover
```
Returns the actual image file (JPEG/PNG).

#### Get Recent Albums
```http
GET /api/v2/albums/recent?limit=20
Authorization: Bearer <token>
```

### Tracks

#### Get All Tracks
```http
GET /api/v2/tracks?page=1&limit=50
Authorization: Bearer <token>
```

#### Get Track Details
```http
GET /api/v2/tracks/{id}
Authorization: Bearer <token>
```

#### Stream Track
```http
GET /api/v2/tracks/{id}/stream
Range: bytes=0-1048575  # Optional, for partial content
```

**Response Headers:**
```http
HTTP/1.1 206 Partial Content
Content-Type: audio/mpeg
Content-Length: 1048576
Content-Range: bytes 0-1048575/5242880
Accept-Ranges: bytes
```

#### Download Track
```http
GET /api/v2/tracks/{id}/download
Authorization: Bearer <token>
```

#### Update Play Count
```http
POST /api/v2/tracks/{id}/play
Authorization: Bearer <token>
```

#### Get Popular Tracks
```http
GET /api/v2/tracks/popular?limit=50
Authorization: Bearer <token>
```

#### Get Recently Played
```http
GET /api/v2/tracks/recent?limit=20
Authorization: Bearer <token>
```

### Search

#### Basic Search
```http
GET /api/v2/search?q=search_term&limit=5
```

**Response:**
```json
{
  "query": "search_term",
  "totalResults": 15,
  "artists": [...],
  "albums": [...],
  "tracks": [...]
}
```

#### Advanced Search
```http
POST /api/v2/search/advanced
Content-Type: application/json
Authorization: Bearer <token>

{
  "title": "song title",
  "artist": "artist name",
  "album": "album name",
  "genre": "rock",
  "yearFrom": 2020,
  "yearTo": 2023,
  "minBitrate": 256,
  "sortBy": "title",
  "sortDescending": false,
  "page": 1,
  "pageSize": 50
}
```

#### Search Suggestions
```http
GET /api/v2/search/suggestions?q=par
```

### Playback Context

#### Get Album Play Context
```http
GET /api/v2/albums/{id}/play
```

Returns album info with ordered tracks for continuous playback.

#### Get Track Play Context
```http
GET /api/v2/tracks/{id}/play
```

Returns track info with navigation links (next/previous).

## User Management

All user management endpoints require admin role authorization.

### Users

#### Get All Users
`GET /api/v2/users`

Query parameters:
- `pageNumber` (default: 1)
- `pageSize` (default: 20, max: 100)
- `sortBy` (username|email|lastlogin|createdat)
- `sortOrder` (asc|desc)
- `searchTerm` (searches username and email)

Response:
```json
{
  "items": [
    {
      "id": "user_id",
      "username": "john_doe",
      "email": "john@example.com",
      "role": "user",
      "isActive": true,
      "lastLogin": "2024-01-15T10:30:00Z",
      "createdAt": "2024-01-01T00:00:00Z"
    }
  ],
  "totalCount": 50,
  "pageNumber": 1,
  "pageSize": 20,
  "totalPages": 3
}
```

#### Get User by ID
`GET /api/v2/users/{id}`

#### Create User
`POST /api/v2/users`

Request:
```json
{
  "username": "new_user",
  "email": "new@example.com",
  "password": "SecurePassword123!",
  "role": "user"  // "user" or "admin"
}
```

#### Update User Status
`PUT /api/v2/users/{id}/status`

Enable or disable a user account. Disabled users cannot log in and their sessions are invalidated.

Request:
```json
{
  "isActive": false,
  "reason": "Account suspended for policy violation"  // optional
}
```

Response:
```json
{
  "userId": "user_id",
  "isActive": false,
  "updatedAt": "2024-01-15T10:30:00Z"
}
```

Restrictions:
- Admins cannot disable their own account
- Cannot disable the last admin account
- Disabling a user invalidates all their sessions

#### Reset User Password
`POST /api/v2/users/{id}/reset-password`

Request:
```json
{
  "generateRandom": true,  // or false with manualPassword
  "manualPassword": "NewPassword123!"  // required if generateRandom is false
}
```

Response:
```json
{
  "newPassword": "GeneratedPassword123!",
  "method": "generated"  // or "manual"
}
```

#### Delete User
`DELETE /api/v2/users/{id}`

Permanently deletes a user account. Admins cannot delete their own account.

#### Check Username Availability
`GET /api/v2/users/check-username/{username}`

Returns `true` if username is available.

#### Check Email Availability
`GET /api/v2/users/check-email/{email}`

Returns `true` if email is available.

## WebSocket/SignalR

Audiarr uses SignalR for real-time updates. Connect to the hub at `/hubs/scan`.

### Connection Example (JavaScript)
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://your-server:8080/hubs/scan", {
        accessTokenFactory: () => localStorage.getItem("accessToken")
    })
    .build();

// Subscribe to events
connection.on("ScanProgress", (data) => {
    console.log(`Scan progress: ${data.processed}/${data.total}`);
});

connection.on("ScanComplete", (data) => {
    console.log(`Scan complete: ${data.newTracks} new tracks`);
});

connection.on("ScanError", (error) => {
    console.error(`Scan error: ${error}`);
});

// Start connection
await connection.start();
```

### Events

#### ScanProgress
```json
{
  "processed": 150,
  "total": 500,
  "message": "Processing: song.mp3",
  "percentComplete": 30.0
}
```

#### ScanComplete
```json
{
  "totalFiles": 500,
  "newTracks": 45,
  "updatedTracks": 5,
  "errors": 2,
  "completedAt": "2024-01-15T10:30:00Z"
}
```

## Streaming Audio

Audiarr supports HTTP range requests for efficient audio streaming and seeking.

### Basic Streaming
```http
GET /api/v2/tracks/{id}/stream
```

### Seeking (Range Requests)
```http
GET /api/v2/tracks/{id}/stream
Range: bytes=1048576-2097151
```

### Client Implementation Tips

#### iOS (AVPlayer)
```swift
let url = URL(string: "http://server:8080/api/v2/tracks/\(trackId)/stream")!
let playerItem = AVPlayerItem(url: url)
let player = AVPlayer(playerItem: playerItem)
player.play()
```

#### Android (ExoPlayer)
```kotlin
val mediaItem = MediaItem.fromUri("http://server:8080/api/v2/tracks/$trackId/stream")
player.setMediaItem(mediaItem)
player.prepare()
player.play()
```

#### Web (HTML5 Audio)
```javascript
const audio = new Audio(`http://server:8080/api/v2/tracks/${trackId}/stream`);
audio.play();
```

## Error Handling

### HTTP Status Codes
- `200 OK`: Successful request
- `201 Created`: Resource created successfully
- `204 No Content`: Successful request with no response body
- `400 Bad Request`: Invalid request parameters
- `401 Unauthorized`: Missing or invalid authentication
- `403 Forbidden`: Insufficient permissions
- `404 Not Found`: Resource not found
- `500 Internal Server Error`: Server error

### Error Response Format
```json
{
  "error": "Error message describing what went wrong",
  "details": "Additional error details (optional)"
}
```

### Common Error Scenarios

#### Expired Token
```json
{
  "error": "Token has expired"
}
```
**Action**: Use refresh token to get new access token

#### Invalid Credentials
```json
{
  "error": "Invalid username or password"
}
```
**Action**: Prompt user to re-enter credentials

#### Resource Not Found
```json
{
  "error": "Track not found"
}
```
**Action**: Remove from UI or show appropriate message

## Best Practices

### 1. Token Management
- Implement automatic token refresh before expiration
- Store refresh tokens securely
- Handle token expiration gracefully
- Clear tokens on logout

### 2. Caching
- Cache artist and album metadata locally
- Implement cache invalidation on library scan
- Use ETags when available
- Cache album artwork

### 3. Network Optimization
- Use pagination for large lists
- Implement lazy loading for track lists
- Prefetch next track for gapless playback
- Use appropriate image sizes for thumbnails

### 4. Error Recovery
- Implement exponential backoff for retries
- Queue failed requests for retry
- Provide offline mode when possible
- Show meaningful error messages to users

### 5. Audio Streaming
- Implement buffering indicators
- Support background playback
- Handle network interruptions gracefully
- Implement gapless playback for albums

### 6. Security
- Always use HTTPS in production
- Never log sensitive information
- Validate SSL certificates
- Implement certificate pinning for mobile apps

### Example: Complete Authentication Flow (Swift/iOS)

```swift
class AudiarrClient {
    private let baseURL = "http://your-server:8080/api/v2"
    private var accessToken: String?
    private var refreshToken: String?
    
    func login(username: String, password: String) async throws -> User {
        let url = URL(string: "\(baseURL)/auth/login")!
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        let body = ["username": username, "password": password]
        request.httpBody = try JSONEncoder().encode(body)
        
        let (data, _) = try await URLSession.shared.data(for: request)
        let response = try JSONDecoder().decode(LoginResponse.self, from: data)
        
        // Store tokens securely
        self.accessToken = response.accessToken
        KeychainHelper.store(response.refreshToken, for: "refreshToken")
        
        return response.user
    }
    
    func makeAuthenticatedRequest(to endpoint: String) async throws -> Data {
        guard let token = accessToken else {
            throw AuthError.notAuthenticated
        }
        
        let url = URL(string: "\(baseURL)\(endpoint)")!
        var request = URLRequest(url: url)
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        
        do {
            let (data, response) = try await URLSession.shared.data(for: request)
            
            if let httpResponse = response as? HTTPURLResponse {
                if httpResponse.statusCode == 401 {
                    // Token expired, try to refresh
                    try await refreshAccessToken()
                    // Retry request with new token
                    return try await makeAuthenticatedRequest(to: endpoint)
                }
            }
            
            return data
        } catch {
            throw error
        }
    }
    
    private func refreshAccessToken() async throws {
        guard let refreshToken = KeychainHelper.retrieve("refreshToken") else {
            throw AuthError.noRefreshToken
        }
        
        let url = URL(string: "\(baseURL)/auth/refresh")!
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        let body = ["refreshToken": refreshToken]
        request.httpBody = try JSONEncoder().encode(body)
        
        let (data, _) = try await URLSession.shared.data(for: request)
        let response = try JSONDecoder().decode(TokenResponse.self, from: data)
        
        self.accessToken = response.accessToken
        KeychainHelper.store(response.refreshToken, for: "refreshToken")
    }
}
```

## Rate Limiting

Currently, Audiarr does not implement rate limiting, but clients should be prepared for potential future implementation:
- Respect `429 Too Many Requests` responses
- Implement exponential backoff
- Consider client-side request throttling

## Versioning

The API uses URL versioning (e.g., `/api/v2/`). Clients should:
- Always specify the API version
- Handle version deprecation notices
- Test against new versions before migrating

## Support & Resources

- GitHub Repository: https://github.com/yourusername/audiarr
- Docker Image: ghcr.io/yourusername/audiarr:latest
- API Testing: Use the provided Postman collection
- WebSocket Testing: Use SignalR client libraries

## Migration from v1

If migrating from an older API version:
1. Update authentication to use JWT tokens
2. Update endpoint paths to include `/v2/`
3. Handle new response formats
4. Implement SignalR for real-time updates