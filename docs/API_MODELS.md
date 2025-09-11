# Audiarr API Models Reference

## Table of Contents
1. [Authentication Models](#authentication-models)
2. [User Models](#user-models)
3. [Music Entity Models](#music-entity-models)
4. [Playlist Models](#playlist-models)
5. [Queue Models](#queue-models)
6. [Library Scanner Models](#library-scanner-models)
7. [Diagnostics Models](#diagnostics-models)
8. [Data Cleanup Models](#data-cleanup-models)
9. [Response Wrapper Models](#response-wrapper-models)
10. [Search Models](#search-models)
11. [Playback Models](#playback-models)
12. [System Models](#system-models)
13. [Field Constraints](#field-constraints)

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

| Field | Type | Required | Description | Constraints |
|-------|------|----------|-------------|-------------|
| currentPassword | string | Yes | User's current password | Must match existing password |
| newPassword | string | Yes | New password to set | Min 8 chars, uppercase, lowercase, number, special char |

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

### PaginatedResponse<T>
Generic wrapper for paginated API responses.

```json
{
  "items": [...],
  "totalCount": 250,
  "pageNumber": 1,
  "pageSize": 20,
  "totalPages": 13
}
```

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| items | array[T] | Array of items for current page | - |
| totalCount | integer | Total number of items across all pages | >= 0 |
| pageNumber | integer | Current page number | >= 1 |
| pageSize | integer | Number of items per page | 1-100 |
| totalPages | integer | Total number of pages | >= 1 |

### UserListRequest
Request parameters for listing users with filtering and sorting.

```json
{
  "pageNumber": 1,
  "pageSize": 20,
  "sortBy": "username",
  "sortOrder": "asc",
  "searchTerm": "john"
}
```

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| pageNumber | integer | No | >= 1, Default: 1 | Page number to retrieve |
| pageSize | integer | No | 1-100, Default: 20 | Items per page |
| sortBy | string | No | "username", "email", "role", "lastLogin", "createdAt" | Field to sort by |
| sortOrder | string | No | "asc", "desc", Default: "asc" | Sort direction |
| searchTerm | string | No | Max 100 chars | Filter by username or email |

### CreateUserRequest
Request to create a new user account.

```json
{
  "username": "johndoe",
  "email": "john@example.com",
  "password": "SecurePass123!",
  "role": "User"
}
```

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| username | string | Yes | 3-50 chars, alphanumeric + underscore | Unique username |
| email | string | Yes | Valid email format | User's email address |
| password | string | Yes | Min 8 chars, uppercase, lowercase, number, special char | User's password |
| role | string | No | "Admin", "User", Default: "User" | User role |

### CreateUserResponse
Response after successful user creation.

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "username": "johndoe",
  "email": "john@example.com",
  "role": "User",
  "createdAt": "2024-01-15T10:30:00Z"
}
```

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| id | string (UUID) | Unique user identifier | UUID v4 |
| username | string | User's username | 3-50 characters |
| email | string | User's email address | Valid email format |
| role | string | Assigned user role | "Admin" or "User" |
| createdAt | datetime | Account creation timestamp | ISO 8601 |

### ResetPasswordRequest
Request to reset a user's password.

```json
{
  "generateRandom": true,
  "manualPassword": null
}
```

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| generateRandom | boolean | No | Default: true | Generate random password |
| manualPassword | string | No | Min 8 chars if provided | Manually specified password |

### ResetPasswordResponse
Response containing new password information.

```json
{
  "newPassword": "TempPass456!",
  "method": "generated"
}
```

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| newPassword | string | The new password | Min 8 characters |
| method | string | How password was created | "generated" or "manual" |

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

## Queue Models

### QueueStateDto
Represents the current state of a user's playback queue.

```json
{
  "queueId": "string",
  "userId": "string",
  "trackIds": ["string"],
  "currentTrackId": "string",
  "currentIndex": 0,
  "repeatMode": 0,
  "isShuffled": false,
  "totalTracks": 0,
  "queueSource": "string",
  "lastActivity": "2024-01-15T10:30:00Z",
  "version": 1
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| queueId | string | Yes | Unique queue identifier |
| userId | string | Yes | User ID who owns the queue |
| trackIds | string[] | Yes | Array of track IDs in order |
| currentTrackId | string? | No | Currently playing track ID |
| currentIndex | integer | Yes | Index of current track (0-based) |
| repeatMode | integer | Yes | Repeat mode (0=None, 1=One, 2=All) |
| isShuffled | boolean | Yes | Whether queue is shuffled |
| totalTracks | integer | Yes | Total number of tracks |
| queueSource | string? | No | Source that created the queue |
| lastActivity | datetime | Yes | Last activity timestamp |
| version | integer | Yes | Version for optimistic concurrency |

### QueueItemDto
Represents a single track item in the queue.

```json
{
  "index": 0,
  "trackId": "string",
  "track": {
    "id": "string",
    "title": "string",
    "artistName": "string",
    "albumTitle": "string",
    "durationMs": 215000
  },
  "addedAt": "2024-01-15T10:30:00Z",
  "source": "string"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| index | integer | Yes | Position in queue (0-based) |
| trackId | string | Yes | Track identifier |
| track | TrackDto | Yes | Full track information |
| addedAt | datetime | Yes | When track was added to queue |
| source | string? | No | Source that added the track |

### AddToQueueRequest
Request to add tracks to the queue.

```json
{
  "trackIds": ["string"],
  "source": "string",
  "playNext": false
}
```

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| trackIds | string[] | Yes | 1-100 items | Track IDs to add |
| source | string? | No | Max 100 chars | Source identifier |
| playNext | boolean | No | Default: false | Add after current track |

### UpdateQueueRequest
Request to update queue settings.

```json
{
  "repeatMode": 0,
  "isShuffled": false,
  "currentIndex": 0
}
```

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| repeatMode | integer? | No | 0-2 | Repeat mode (0=None, 1=One, 2=All) |
| isShuffled | boolean? | No | - | Shuffle state |
| currentIndex | integer? | No | ≥ 0 | Current track index |

### ReorderQueueRequest
Request to reorder a track in the queue.

```json
{
  "trackId": "string",
  "newIndex": 0
}
```

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| trackId | string | Yes | - | Track ID to move |
| newIndex | integer | Yes | ≥ 0 | New position index |

### ReplaceQueueRequest
Request to replace the entire queue.

```json
{
  "trackIds": ["string"],
  "startIndex": 0,
  "source": "string"
}
```

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| trackIds | string[] | Yes | 1-1000 items | New track IDs |
| startIndex | integer | No | ≥ 0, Default: 0 | Starting playback index |
| source | string? | No | Max 100 chars | Source identifier |

### ClearQueueRequest
Request to clear the user's playback queue.

```json
{
  "keepCurrentTrack": false
}
```

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| keepCurrentTrack | boolean | No | Default: false | Whether to keep the currently playing track |

### RepeatMode Enum
Queue repeat modes.

| Value | Name | Description |
|-------|------|-------------|
| 0 | None | Play queue once |
| 1 | One | Repeat current track |
| 2 | All | Repeat entire queue |

## Library Scanner Models

### ScanRequest
Request to queue a library scan operation.

```json
{
  "libraryPath": "/path/to/music/library",
  "requestId": "scan_12345",
  "requestedAt": "2024-01-15T10:30:00Z"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| libraryPath | string | Yes | Path to the directory containing audio files |
| requestId | string | No | Unique identifier for the scan request |
| requestedAt | datetime | No | When the scan was requested |

### ScanResult
Result of a completed scan operation.

```json
{
  "totalFiles": 500,
  "processedFiles": 495,
  "newTracks": 45,
  "updatedTracks": 12,
  "errors": 5,
  "errorMessages": [
    "Failed to read metadata from /path/corrupted.mp3",
    "Unsupported format: /path/file.txt"
  ],
  "startTime": "2024-01-15T10:30:00Z",
  "endTime": "2024-01-15T10:45:00Z",
  "duration": "00:15:00"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| totalFiles | integer | Yes | Total number of files found |
| processedFiles | integer | Yes | Number of files successfully processed |
| newTracks | integer | Yes | Number of new tracks added |
| updatedTracks | integer | Yes | Number of existing tracks updated |
| errors | integer | Yes | Number of files that failed to process |
| errorMessages | string[] | Yes | List of error messages |
| startTime | datetime | Yes | When the scan started |
| endTime | datetime | Yes | When the scan completed |
| duration | timespan | Yes | Total scan duration |

### ScanProgress
Real-time progress information during scan operation.

```json
{
  "processedFiles": 150,
  "totalFiles": 500,
  "currentFile": "/path/to/current/file.mp3",
  "percentComplete": 30.0
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| processedFiles | integer | Yes | Number of files processed so far |
| totalFiles | integer | Yes | Total number of files to process |
| currentFile | string | Yes | Path of the file currently being processed |
| percentComplete | decimal | Yes | Completion percentage (0-100) |

### ScanQueueResponse
Response when a scan is successfully queued.

```json
{
  "message": "Scan request queued",
  "requestId": "scan_12345",
  "path": "/path/to/music/library"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| message | string | Yes | Status message |
| requestId | string | Yes | Unique identifier for tracking |
| path | string | Yes | Library path being scanned |

### SupportedFormatsResponse
List of supported audio file formats.

```json
{
  "formats": [
    ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".opus",
    ".wav", ".wma", ".alac", ".ape", ".wv", ".mka"
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| formats | string[] | Yes | Array of supported file extensions |

### SignalR Event Models

#### ScanProgress Event
```json
{
  "processed": 150,
  "total": 500,
  "message": "Processing: /music/artist/album/track.mp3",
  "percentComplete": 30.0
}
```

#### ScanComplete Event
```json
{
  "totalFiles": 500,
  "newTracks": 45,
  "updatedTracks": 12,
  "errors": 3,
  "completedAt": "2024-01-15T10:45:00Z"
}
```

#### ScanError Event
```json
{
  "error": "Failed to access directory: /invalid/path"
}
```

## Diagnostics Models

### DatabaseDiagnosticResponse
Response from the database diagnostic endpoint providing comprehensive health information.

```json
{
  "totalCounts": {
    "artistCount": 156,
    "albumCount": 423,
    "trackCount": 2847
  },
  "duplicateArtists": [
    {
      "name": "The Beatles",
      "count": 2,
      "ids": [45, 127]
    }
  ],
  "duplicateAlbums": [
    {
      "title": "Abbey Road",
      "artistId": 45,
      "count": 2,
      "ids": [89, 156]
    }
  ],
  "sampleArtists": [
    {
      "id": 1,
      "name": "The Beatles",
      "albumCount": 13,
      "trackCount": 213
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| totalCounts | TotalCountsInfo | Overall database entity counts |
| duplicateArtists | DuplicateArtistInfo[] | Artists with duplicate names |
| duplicateAlbums | DuplicateAlbumInfo[] | Albums with duplicate titles per artist |
| sampleArtists | SampleArtistInfo[] | Sample of first 5 artists with related counts |

### TotalCountsInfo
Basic counts of all main entities in the database.

```json
{
  "artistCount": 156,
  "albumCount": 423,
  "trackCount": 2847
}
```

| Field | Type | Description |
|-------|------|-------------|
| artistCount | number | Total number of artists in library |
| albumCount | number | Total number of albums in library |
| trackCount | number | Total number of tracks in library |

### DuplicateArtistInfo
Information about artists that have duplicate entries based on name matching.

```json
{
  "name": "The Beatles",
  "count": 2,
  "ids": [45, 127]
}
```

| Field | Type | Description |
|-------|------|-------------|
| name | string | The duplicate artist name |
| count | number | Number of duplicate entries found |
| ids | number[] | Array of all artist IDs with this name |

### DuplicateAlbumInfo
Information about albums that have duplicate entries based on title and artist matching.

```json
{
  "title": "Abbey Road",
  "artistId": 45,
  "count": 2,
  "ids": [89, 156]
}
```

| Field | Type | Description |
|-------|------|-------------|
| title | string | The duplicate album title |
| artistId | number | ID of the artist for this album |
| count | number | Number of duplicate entries found |
| ids | number[] | Array of all album IDs with this title and artist |

### SampleArtistInfo
Sample artist data with aggregated statistics for database assessment.

```json
{
  "id": 1,
  "name": "The Beatles",
  "albumCount": 13,
  "trackCount": 213
}
```

| Field | Type | Description |
|-------|------|-------------|
| id | number | Artist ID |
| name | string | Artist name |
| albumCount | number | Number of albums by this artist |
| trackCount | number | Number of tracks by this artist |

**Diagnostic Operation Details:**
- **Duplicate Detection**: Uses exact string matching for artist names and title+artistId combination for albums
- **Sample Size**: Returns first 5 artists from the database with calculated statistics
- **Performance**: Loads all artists and albums into memory for duplicate analysis
- **Use Cases**: Database health monitoring, cleanup planning, data quality assessment

## Data Cleanup Models

### CleanupResponse
Generic response for cleanup operations.

```json
{
  "message": "Merged 5 duplicate artists",
  "duplicateGroupsFound": 3
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| message | string | Yes | Human-readable cleanup result message |
| duplicateGroupsFound | integer | Yes | Number of duplicate groups found |

### ArtistCleanupResponse
Response from merging duplicate artists.

```json
{
  "message": "Merged 5 duplicate artists",
  "duplicateGroupsFound": 3
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| message | string | Yes | Summary of artist merge operation |
| duplicateGroupsFound | integer | Yes | Number of duplicate artist groups found |

### AlbumCleanupResponse
Response from merging duplicate albums.

```json
{
  "message": "Merged 8 duplicate albums",
  "duplicateGroupsFound": 4
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| message | string | Yes | Summary of album merge operation |
| duplicateGroupsFound | integer | Yes | Number of duplicate album groups found |

### ComprehensiveCleanupResponse
Response from cleaning all data types.

```json
{
  "message": "Database cleanup completed",
  "artistsMerged": 5,
  "albumsMerged": 8,
  "duplicateArtistGroupsFound": 3,
  "duplicateAlbumGroupsFound": 4
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| message | string | Yes | Overall cleanup completion message |
| artistsMerged | integer | Yes | Number of duplicate artists merged |
| albumsMerged | integer | Yes | Number of duplicate albums merged |
| duplicateArtistGroupsFound | integer | Yes | Number of duplicate artist groups processed |
| duplicateAlbumGroupsFound | integer | Yes | Number of duplicate album groups processed |

### Cleanup Operation Details

#### Artist Deduplication
- **Matching Criteria**: Exact name match (case-sensitive)
- **Primary Selection**: First artist found in each duplicate group
- **Data Preservation**: All tracks and albums reassigned to primary artist
- **Deletion**: Duplicate artist records removed

#### Album Deduplication
- **Matching Criteria**: Exact title match + same artist ID
- **Primary Selection**: First album found in each duplicate group
- **Cover Art Logic**: Primary album inherits artwork from duplicates if missing
- **Data Preservation**: All tracks reassigned to primary album
- **Deletion**: Duplicate album records removed

#### Processing Order
1. **Artists First**: Duplicate artists merged before albums
2. **Albums Second**: Duplicate albums merged after artist cleanup
3. **Atomic Operations**: Each merge group processed in a transaction
4. **Database Consistency**: Foreign key relationships maintained throughout

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

## Playlist Models

### PlaylistDto
Basic playlist information without track details.

```json
{
  "id": "string",
  "name": "string",
  "description": "string",
  "userId": "string",
  "username": "string",
  "isPublic": boolean,
  "imagePath": "string",
  "trackCount": 0,
  "totalDuration": "00:00:00",
  "createdAt": "2024-01-01T12:00:00Z",
  "updatedAt": "2024-01-01T12:00:00Z",
  "lastModified": "2024-01-01T12:00:00Z",
  "playCount": 0
}
```

| Field | Type | Description |
|-------|------|-------------|
| id | string | Unique playlist identifier |
| name | string | Playlist name (1-255 characters) |
| description | string | Optional description (max 1000 characters) |
| userId | string | ID of the playlist owner |
| username | string | Username of the playlist owner |
| isPublic | boolean | Whether the playlist is publicly visible |
| imagePath | string | Optional path to playlist cover image |
| trackCount | integer | Number of tracks in the playlist |
| totalDuration | string | Total duration in HH:MM:SS format |
| createdAt | string | ISO 8601 creation timestamp |
| updatedAt | string | ISO 8601 last update timestamp |
| lastModified | string | ISO 8601 last modification timestamp |
| playCount | integer | Number of times playlist has been played |

### PlaylistDetailsDto
Complete playlist information including all tracks.

```json
{
  "id": "string",
  "name": "string",
  "description": "string",
  "userId": "string",
  "username": "string",
  "isPublic": boolean,
  "imagePath": "string",
  "trackCount": 0,
  "totalDuration": "00:00:00",
  "createdAt": "2024-01-01T12:00:00Z",
  "updatedAt": "2024-01-01T12:00:00Z",
  "lastModified": "2024-01-01T12:00:00Z",
  "playCount": 0,
  "tracks": [
    {
      "trackId": "string",
      "title": "string",
      "artistId": "string",
      "artistName": "string",
      "albumId": "string",
      "albumTitle": "string",
      "trackNumber": 1,
      "discNumber": 1,
      "durationMs": 240000,
      "genre": "string",
      "year": 2024,
      "filePath": "string",
      "position": 0,
      "positionFloat": 0.0,
      "addedAt": "2024-01-01T12:00:00Z",
      "addedBy": "string"
    }
  ]
}
```

Extends `PlaylistDto` with a `tracks` array containing `PlaylistTrackDto` objects.

### PlaylistTrackDto
Track information within a playlist context.

```json
{
  "trackId": "string",
  "title": "string",
  "artistId": "string",
  "artistName": "string",
  "albumId": "string",
  "albumTitle": "string",
  "trackNumber": 1,
  "discNumber": 1,
  "durationMs": 240000,
  "genre": "string",
  "year": 2024,
  "filePath": "string",
  "position": 0,
  "positionFloat": 0.0,
  "addedAt": "2024-01-01T12:00:00Z",
  "addedBy": "string"
}
```

| Field | Type | Description |
|-------|------|-------------|
| trackId | string | Unique track identifier |
| title | string | Track title |
| artistId | string | Artist identifier |
| artistName | string | Artist name |
| albumId | string | Album identifier |
| albumTitle | string | Album title |
| trackNumber | integer | Track number on album |
| discNumber | integer | Disc number for multi-disc albums |
| durationMs | integer | Track duration in milliseconds |
| genre | string | Music genre |
| year | integer | Release year |
| filePath | string | File system path to audio file |
| position | integer | Integer position in playlist (0-based) |
| positionFloat | double | Floating-point position for precise ordering |
| addedAt | string | ISO 8601 timestamp when track was added |
| addedBy | string | Username who added the track |

### CreatePlaylistRequest
Request to create a new playlist.

```json
{
  "name": "string",
  "description": "string",
  "isPublic": false,
  "initialTrackIds": ["string"]
}
```

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| name | string | Yes | 1-255 characters | Playlist name |
| description | string | No | Max 1000 characters | Playlist description |
| isPublic | boolean | No | Default: false | Whether playlist is public |
| initialTrackIds | array | No | - | Track IDs to add initially |

### UpdatePlaylistRequest
Request to update playlist metadata.

```json
{
  "name": "string",
  "description": "string",
  "isPublic": true
}
```

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| name | string | Yes | 1-255 characters | Updated playlist name |
| description | string | No | Max 1000 characters | Updated description |
| isPublic | boolean | Yes | - | Updated visibility setting |

### AddTracksRequest
Request to add tracks to a playlist.

```json
{
  "trackIds": ["string"],
  "position": 5
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| trackIds | array | Yes | Array of track IDs to add |
| position | integer | No | 0-based position to insert (default: end) |

### RemoveTracksRequest
Request to remove tracks from a playlist.

```json
{
  "trackIds": ["string"]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| trackIds | array | Yes | Array of track IDs to remove |

### ReorderTracksRequest
Request to reorder tracks in a playlist.

```json
{
  "tracks": [
    {
      "trackId": "string",
      "newPosition": 1.5
    }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| tracks | array | Yes | Array of track reorder items |

### TrackReorderItem
Individual track reorder specification.

```json
{
  "trackId": "string",
  "newPosition": 1.5
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| trackId | string | Yes | Track ID to reorder |
| newPosition | double | Yes | New floating-point position in playlist |

### UpdatePlaylistImageRequest
Request to update playlist cover image.

```json
{
  "imagePath": "/images/playlists/my-playlist-cover.jpg"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| imagePath | string | Yes | Path to the new cover image |

### CopyPlaylistRequest
Request to duplicate/copy an existing playlist.

```json
{
  "name": "Copy of My Awesome Playlist",
  "description": "A copy of my original playlist"
}
```

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| name | string | Yes | 1-255 characters | Name for the copied playlist |
| description | string | No | Max 1000 characters | Description for the copied playlist |

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
Context for playing a playlist with ordered tracks for continuous playback.

```json
{
  "playlist": {
    "id": "playlist_id",
    "name": "My Awesome Playlist",
    "description": "A collection of great songs",
    "trackCount": 12
  },
  "tracks": [
    {
      "id": "track_1",
      "title": "Song Title",
      "artistName": "Artist Name", 
      "albumTitle": "Album Title",
      "trackNumber": 3,
      "durationMs": 240000,
      "streamUrl": "/api/v2/tracks/track_1/stream",
      "position": 0,
      "nextTrackId": "track_2",
      "previousTrackId": null
    }
  ],
  "totalDurationMs": 2880000
}
```

| Field | Type | Description |
|-------|------|-------------|
| playlist | object | Playlist metadata |
| playlist.id | string | Unique playlist identifier |
| playlist.name | string | Playlist name |
| playlist.description | string? | Optional playlist description |
| playlist.trackCount | integer | Total number of tracks in playlist |
| tracks | array | Ordered array of tracks in playlist |
| tracks[].id | string | Track identifier |
| tracks[].title | string | Track title |
| tracks[].artistName | string | Artist name |
| tracks[].albumTitle | string? | Album title (nullable) |
| tracks[].trackNumber | integer? | Track number on album (nullable) |
| tracks[].durationMs | integer | Track duration in milliseconds |
| tracks[].streamUrl | string | Direct streaming URL for the track |
| tracks[].position | integer | Position within the playlist (0-based) |
| tracks[].nextTrackId | string? | ID of next track (null for last track) |
| tracks[].previousTrackId | string? | ID of previous track (null for first track) |
| totalDurationMs | integer | Total duration of all tracks in milliseconds |

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