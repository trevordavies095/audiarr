# Audiarr API Models Reference

## Table of Contents
1. [Authentication Models](#authentication-models)
2. [User Models](#user-models)
3. [Music Entity Models](#music-entity-models)
4. [Response Wrapper Models](#response-wrapper-models)
5. [Search Models](#search-models)
6. [Playback Models](#playback-models)
7. [System Models](#system-models)
8. [Field Constraints](#field-constraints)

## Authentication Models

### LoginRequest
Used for user authentication.

```json
{
  "username": "string",
  "password": "string"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| username | string | Yes | User's username |
| password | string | Yes | User's password |

### LoginResponse
Returned after successful authentication.

```json
{
  "accessToken": "string",
  "refreshToken": "string",
  "expiresAt": "2024-01-15T10:30:00Z",
  "user": {
    "id": "string",
    "username": "string",
    "email": "string",
    "role": "string",
    "lastLogin": "2024-01-15T09:30:00Z"
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| accessToken | string | JWT access token for API requests |
| refreshToken | string | Token for refreshing access token |
| expiresAt | datetime | Access token expiration time (ISO 8601) |
| user | User | Current user information |

### RefreshTokenRequest
Used to refresh an expired access token.

```json
{
  "refreshToken": "string"
}
```

### TokenResponse
Returned when refreshing tokens.

```json
{
  "accessToken": "string",
  "refreshToken": "string",
  "expiresAt": "2024-01-15T11:30:00Z"
}
```

### ChangePasswordRequest
Used to change user password.

```json
{
  "currentPassword": "string",
  "newPassword": "string"
}
```

## User Models

### User
Represents a user account.

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "username": "johndoe",
  "email": "john@example.com",
  "role": "User",
  "lastLogin": "2024-01-15T09:30:00Z"
}
```

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| id | string (UUID) | Unique user identifier | UUID v4 |
| username | string | User's username | 3-50 characters |
| email | string | User's email address | Valid email format |
| role | string | User role | "Admin" or "User" |
| lastLogin | datetime? | Last login timestamp | ISO 8601, nullable |

### UserListDto
Extended user information for admin user management.

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "username": "johndoe",
  "email": "john@example.com",
  "role": "User",
  "isActive": true,
  "lastLogin": "2024-01-15T09:30:00Z",
  "createdAt": "2024-01-01T00:00:00Z"
}
```

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| id | string (UUID) | Unique user identifier | UUID v4 |
| username | string | User's username | 3-50 characters |
| email | string | User's email address | Valid email format |
| role | string | User role | "Admin" or "User" |
| isActive | boolean | Account active status | true/false |
| lastLogin | datetime? | Last login timestamp | ISO 8601, nullable |
| createdAt | datetime | Account creation timestamp | ISO 8601 |

### UserStatusRequest
Request to update user account status.

```json
{
  "isActive": false,
  "reason": "Account suspended for policy violation"
}
```

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| isActive | boolean | Desired account status | Required |
| reason | string? | Reason for status change | Optional, for audit log |

### UserStatusResponse
Response after updating user status.

```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "isActive": false,
  "updatedAt": "2024-01-15T10:30:00Z"
}
```

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| userId | string (UUID) | User identifier | UUID v4 |
| isActive | boolean | Current account status | true/false |
| updatedAt | datetime | Timestamp of update | ISO 8601 |

## Music Entity Models

### Artist
Represents a music artist.

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440001",
  "name": "Pink Floyd",
  "sortName": "Pink Floyd",
  "albumCount": 15,
  "trackCount": 165
}
```

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| id | string (UUID) | Unique artist identifier | UUID v4 |
| name | string | Artist display name | 1-255 characters |
| sortName | string? | Name used for sorting | Nullable, 1-255 characters |
| albumCount | integer | Number of albums | >= 0 |
| trackCount | integer | Number of tracks | >= 0 |

### ArtistDetail
Extended artist information with albums.

```json
{
  "id": "string",
  "name": "string",
  "sortName": "string",
  "albumCount": 0,
  "trackCount": 0,
  "albums": [
    {
      "id": "string",
      "title": "string",
      "year": 2023,
      "trackCount": 12,
      "coverArtPath": "/artwork/album_id.jpg"
    }
  ]
}
```

### Album
Represents a music album.

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440002",
  "title": "The Dark Side of the Moon",
  "artistId": "550e8400-e29b-41d4-a716-446655440001",
  "artistName": "Pink Floyd",
  "year": 1973,
  "trackCount": 10,
  "genre": "Progressive Rock",
  "coverArtPath": "/artwork/550e8400.jpg",
  "releaseDate": "1973-03-01T00:00:00Z"
}
```

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| id | string (UUID) | Unique album identifier | UUID v4 |
| title | string | Album title | 1-255 characters |
| artistId | string (UUID) | Artist identifier | UUID v4 |
| artistName | string | Artist name | 1-255 characters |
| year | integer? | Release year | Nullable, 1900-current year |
| trackCount | integer | Number of tracks | >= 0 |
| genre | string? | Primary genre | Nullable, 1-100 characters |
| coverArtPath | string? | Path to cover art | Nullable, relative URL |
| releaseDate | datetime? | Full release date | Nullable, ISO 8601 |

### AlbumDetail
Extended album information with tracks.

```json
{
  "id": "string",
  "title": "string",
  "artistId": "string",
  "artistName": "string",
  "year": 2023,
  "trackCount": 12,
  "genre": "string",
  "coverArtPath": "/artwork/album.jpg",
  "releaseDate": "2023-01-01T00:00:00Z",
  "totalDurationMs": 2580000,
  "tracks": [
    {
      "id": "string",
      "title": "string",
      "trackNumber": 1,
      "discNumber": 1,
      "durationMs": 215000
    }
  ]
}
```

### Track
Represents a music track.

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440003",
  "title": "Money",
  "artistId": "550e8400-e29b-41d4-a716-446655440001",
  "artistName": "Pink Floyd",
  "albumId": "550e8400-e29b-41d4-a716-446655440002",
  "albumTitle": "The Dark Side of the Moon",
  "trackNumber": 6,
  "discNumber": 1,
  "durationMs": 382000,
  "genre": "Progressive Rock",
  "year": 1973,
  "fileSize": 9175040,
  "bitrate": 320,
  "codec": "MP3",
  "filePath": "/music/Pink Floyd/Dark Side/06 - Money.mp3"
}
```

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| id | string (UUID) | Unique track identifier | UUID v4 |
| title | string | Track title | 1-255 characters |
| artistId | string (UUID) | Artist identifier | UUID v4 |
| artistName | string | Artist name | 1-255 characters |
| albumId | string? | Album identifier | Nullable, UUID v4 |
| albumTitle | string? | Album title | Nullable, 1-255 characters |
| trackNumber | integer? | Track number on album | Nullable, 1-999 |
| discNumber | integer? | Disc number | Nullable, 1-99 |
| durationMs | integer | Duration in milliseconds | >= 0 |
| genre | string? | Track genre | Nullable, 1-100 characters |
| year | integer? | Release year | Nullable, 1900-current year |
| fileSize | integer? | File size in bytes | Nullable, >= 0 |
| bitrate | integer? | Audio bitrate in kbps | Nullable, 32-320 |
| codec | string? | Audio codec | Nullable, e.g., "MP3", "FLAC" |
| filePath | string? | File system path | Nullable, system path |

### TrackDetail
Extended track information.

```json
{
  "id": "string",
  "title": "string",
  "artistId": "string",
  "artistName": "string",
  "albumId": "string",
  "albumTitle": "string",
  "trackNumber": 1,
  "discNumber": 1,
  "durationMs": 215000,
  "genre": "string",
  "year": 2023,
  "fileSize": 5242880,
  "bitrate": 320,
  "codec": "MP3",
  "sampleRate": 44100,
  "channels": 2,
  "filePath": "/music/track.mp3",
  "fileHash": "sha256hash",
  "addedDate": "2024-01-01T00:00:00Z",
  "modifiedDate": "2024-01-15T00:00:00Z",
  "playCount": 42,
  "lastPlayedDate": "2024-01-14T20:00:00Z"
}
```

Additional fields:

| Field | Type | Description |
|-------|------|-------------|
| sampleRate | integer? | Audio sample rate in Hz |
| channels | integer? | Number of audio channels |
| fileHash | string? | SHA256 hash of file |
| addedDate | datetime? | When track was added |
| modifiedDate | datetime? | Last modification time |
| playCount | integer | Number of times played |
| lastPlayedDate | datetime? | Last played timestamp |

## Response Wrapper Models

### PagedResponse<T>
Generic wrapper for paginated responses.

```json
{
  "data": [...],
  "page": 1,
  "limit": 50,
  "total": 245,
  "totalPages": 5
}
```

| Field | Type | Description |
|-------|------|-------------|
| data | array[T] | Array of items |
| page | integer | Current page number (1-indexed) |
| limit | integer | Items per page |
| total | integer | Total number of items |
| totalPages | integer | Total number of pages |

### DataResponse<T>
Simple wrapper for array responses.

```json
{
  "data": [...]
}
```

## Search Models

### SearchResponse
Combined search results across all entity types.

```json
{
  "query": "pink",
  "totalResults": 15,
  "artists": [
    {
      "id": "string",
      "name": "Pink Floyd",
      "albumCount": 15,
      "trackCount": 165
    }
  ],
  "albums": [
    {
      "id": "string",
      "title": "The Wall",
      "artistName": "Pink Floyd",
      "year": 1979,
      "coverArtPath": "/artwork/wall.jpg"
    }
  ],
  "tracks": [
    {
      "id": "string",
      "title": "Another Brick in the Wall",
      "artistName": "Pink Floyd",
      "albumTitle": "The Wall",
      "durationMs": 238000
    }
  ]
}
```

### AdvancedSearchRequest
Parameters for advanced search.

```json
{
  "title": "string",
  "artist": "string",
  "album": "string",
  "genre": "string",
  "yearFrom": 1970,
  "yearTo": 1980,
  "minBitrate": 256,
  "sortBy": "title",
  "sortDescending": false,
  "page": 1,
  "pageSize": 50
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| title | string? | No | Track title filter |
| artist | string? | No | Artist name filter |
| album | string? | No | Album title filter |
| genre | string? | No | Genre filter |
| yearFrom | integer? | No | Minimum year |
| yearTo | integer? | No | Maximum year |
| minBitrate | integer? | No | Minimum bitrate (kbps) |
| sortBy | string? | No | Sort field: "title", "artist", "album", "year", "duration" |
| sortDescending | boolean | No | Sort direction (default: false) |
| page | integer? | No | Page number (default: 1) |
| pageSize | integer? | No | Items per page (default: 50, max: 100) |

### SearchSuggestion
Autocomplete suggestion.

```json
{
  "value": "Pink Floyd",
  "type": "artist",
  "id": "550e8400-e29b-41d4-a716-446655440001"
}
```

| Field | Type | Description |
|-------|------|-------------|
| value | string | Display value |
| type | string | Entity type: "artist", "album", "track" |
| id | string | Entity identifier |

## Playback Models

### TrackPlayContext
Context for playing a single track.

```json
{
  "track": {
    "id": "string",
    "title": "string",
    "artistName": "string",
    "albumTitle": "string",
    "trackNumber": 1,
    "discNumber": 1,
    "durationMs": 215000,
    "streamUrl": "/api/v2/tracks/id/stream",
    "genre": "string",
    "year": 2023,
    "coverArtPath": "/artwork/album.jpg"
  },
  "nextTrackId": "string",
  "previousTrackId": "string"
}
```

### AlbumPlayContext
Context for playing an entire album.

```json
{
  "album": {
    "id": "string",
    "title": "string",
    "artistName": "string",
    "year": 2023,
    "coverArtPath": "/artwork/album.jpg",
    "trackCount": 12
  },
  "tracks": [
    {
      "id": "string",
      "title": "string",
      "artistName": "string",
      "trackNumber": 1,
      "discNumber": 1,
      "durationMs": 215000,
      "streamUrl": "/api/v2/tracks/id/stream",
      "nextTrackId": "string",
      "previousTrackId": null
    }
  ],
  "totalDurationMs": 2580000
}
```

### PlaylistPlayContext
Context for playing a playlist.

```json
{
  "playlist": {
    "id": "string",
    "name": "My Playlist",
    "description": "Favorite tracks",
    "trackCount": 25
  },
  "tracks": [...],
  "totalDurationMs": 5400000
}
```

### PlayCountUpdate
Response after updating play count.

```json
{
  "message": "Play count updated",
  "playCount": 43,
  "lastPlayedDate": "2024-01-15T10:30:00Z"
}
```

## System Models

### ScanProgress
Real-time scan progress via SignalR.

```json
{
  "processed": 150,
  "total": 500,
  "message": "Processing: Pink Floyd - Money.mp3",
  "percentComplete": 30.0
}
```

### ScanComplete
Scan completion notification.

```json
{
  "totalFiles": 500,
  "newTracks": 45,
  "updatedTracks": 5,
  "errors": 2,
  "completedAt": "2024-01-15T10:30:00Z"
}
```

### ScanResult
Result of a library scan operation.

```json
{
  "startTime": "2024-01-15T10:00:00Z",
  "endTime": "2024-01-15T10:30:00Z",
  "totalFiles": 500,
  "processedFiles": 498,
  "newTracks": 45,
  "updatedTracks": 5,
  "errors": 2,
  "errorMessages": [
    "Error processing file1.mp3: Corrupted file",
    "Error processing file2.mp3: Unsupported format"
  ]
}
```

### ErrorResponse
Standard error response format.

```json
{
  "error": "Resource not found",
  "details": "Track with ID 550e8400-e29b-41d4-a716-446655440003 was not found"
}
```

| Field | Type | Description |
|-------|------|-------------|
| error | string | Error message |
| details | string? | Additional error details (optional) |

## Field Constraints

### String Fields
- **Username**: 3-50 characters, alphanumeric + underscore
- **Password**: 8-128 characters
- **Email**: Valid email format (RFC 5322)
- **Titles** (track/album): 1-255 characters
- **Artist Names**: 1-255 characters
- **Genre**: 1-100 characters
- **File Paths**: System-dependent, typically < 4096 characters

### Numeric Fields
- **Year**: 1900 - current year
- **Track Number**: 1-999
- **Disc Number**: 1-99
- **Duration**: 0 - 2147483647 milliseconds (~24 days)
- **Bitrate**: 32-320 kbps (common values: 128, 192, 256, 320)
- **Sample Rate**: Common values: 44100, 48000, 96000, 192000 Hz
- **Channels**: 1 (mono), 2 (stereo), 6 (5.1), 8 (7.1)
- **Page**: 1 - INT_MAX
- **Limit/PageSize**: 1-100

### Date/Time Fields
- All timestamps use ISO 8601 format
- UTC timezone
- Format: `YYYY-MM-DDTHH:mm:ss.sssZ`
- Example: `2024-01-15T10:30:45.123Z`

### UUID Fields
- Version 4 UUIDs
- Format: `xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx`
- Example: `550e8400-e29b-41d4-a716-446655440000`

### File Formats
#### Audio Formats Supported
- MP3 (.mp3)
- FLAC (.flac)
- M4A (.m4a)
- AAC (.aac)
- OGG Vorbis (.ogg)
- Opus (.opus)
- WAV (.wav)
- WMA (.wma)
- ALAC (.alac)
- APE (.ape)
- WavPack (.wv)
- Matroska Audio (.mka)

#### Image Formats (Album Art)
- JPEG (.jpg, .jpeg)
- PNG (.png)
- GIF (.gif)
- WebP (.webp)
- BMP (.bmp)

### HTTP Status Codes
- **200 OK**: Successful GET/PUT request
- **201 Created**: Successful POST request creating resource
- **204 No Content**: Successful request with no response body
- **206 Partial Content**: Successful range request (streaming)
- **400 Bad Request**: Invalid request parameters
- **401 Unauthorized**: Missing or invalid authentication
- **403 Forbidden**: Insufficient permissions
- **404 Not Found**: Resource not found
- **429 Too Many Requests**: Rate limit exceeded
- **500 Internal Server Error**: Server error
- **503 Service Unavailable**: Server temporarily unavailable

## Enum Values

### User Roles
```
Admin
User
```

### Sort Fields
```
title
artist
album
year
duration
```

### Entity Types (Search)
```
artist
album
track
```

### Repeat Modes
```
none
one
all
```

## Notes

1. **Nullable Fields**: Fields marked with `?` are nullable/optional
2. **Authentication**: Most endpoints require Bearer token authentication
3. **Pagination**: Default page size is 50, maximum is 100
4. **Streaming**: Audio streaming supports HTTP range requests
5. **Real-time Updates**: SignalR hub at `/hubs/scan` for live updates
6. **Rate Limiting**: Currently not implemented but clients should be prepared
7. **API Versioning**: Current version is v2, specified in URL path