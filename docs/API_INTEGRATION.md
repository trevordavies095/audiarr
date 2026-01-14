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

#### 5. Get Current User
```http
GET /api/v2/auth/me
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

**Response:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "username": "your_username",
  "email": "user@example.com",
  "role": "User",
  "lastLogin": "2024-01-15T09:30:00Z"
}
```

**Error Responses:**
- `401 Unauthorized`: Invalid or missing access token
- `404 Not Found`: User account not found (rare edge case)

#### 6. Change Password
```http
POST /api/v2/auth/change-password
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
Content-Type: application/json

{
  "currentPassword": "current_password",
  "newPassword": "new_secure_password"
}
```

**Response:**
```json
{
  "message": "Password changed successfully. Please login again."
}
```

**Password Requirements:**
- Minimum 8 characters
- Must contain at least one uppercase letter
- Must contain at least one lowercase letter  
- Must contain at least one number
- Must contain at least one special character

**Error Responses:**
- `400 Bad Request`: Current password is incorrect or new password doesn't meet requirements
- `401 Unauthorized`: Invalid or missing access token

**Important Security Notes:**
- Changing password revokes all existing sessions for the user
- User must re-authenticate after password change
- All refresh tokens for the user are invalidated

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

**Response (Single Artist Album):**
```json
{
  "id": "album_id",
  "title": "Album Title",
  "artistId": "artist_id",
  "artistName": "Artist Name",
  "artistIds": ["artist_id"],
  "artistNames": ["Artist Name"],
  "year": 2023,
  "trackCount": 12,
  "genre": "Rock",
  "genres": ["Rock"],
  "coverArtPath": "/artwork/album_id.jpg",
  "releaseDate": "2023-01-15T00:00:00Z",
  "primaryArtistId": "artist_id",
  "primaryArtistName": "Artist Name",
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

**Response (Multi-Artist Album):**
```json
{
  "id": "collab_album_id",
  "title": "Collaboration Album",
  "artistId": "primary_artist_id",
  "artistName": "Primary Artist",
  "artistIds": ["primary_artist_id", "secondary_artist_id"],
  "artistNames": ["Primary Artist", "Secondary Artist"],
  "year": 2023,
  "trackCount": 10,
  "genre": "Electronic",
  "genres": ["Electronic", "House", "Techno"],
  "coverArtPath": "/artwork/collab_album.jpg",
  "releaseDate": "2023-06-15T00:00:00Z",
  "primaryArtistId": "primary_artist_id",
  "primaryArtistName": "Primary Artist",
  "totalDurationMs": 2400000,
  "tracks": [
    {
      "id": "track_id",
      "title": "Track Title",
      "trackNumber": 1,
      "discNumber": 1,
      "durationMs": 240000
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

**Response (Single Artist):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440003",
  "title": "Money",
  "artistId": "550e8400-e29b-41d4-a716-446655440001",
  "artistName": "Pink Floyd",
  "artistIds": ["550e8400-e29b-41d4-a716-446655440001"],
  "artistNames": ["Pink Floyd"],
  "albumId": "550e8400-e29b-41d4-a716-446655440002",
  "albumTitle": "The Dark Side of the Moon",
  "trackNumber": 6,
  "discNumber": 1,
  "durationMs": 382000,
  "genre": "Progressive Rock",
  "genres": ["Progressive Rock"],
  "year": 1973,
  "fileSize": 9175040,
  "bitrate": 320,
  "codec": "MP3",
  "filePath": "/music/Pink Floyd/Dark Side/06 - Money.mp3",
  "primaryArtistId": "550e8400-e29b-41d4-a716-446655440001",
  "primaryArtistName": "Pink Floyd"
}
```

**Response (Multi-Artist Track):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440004",
  "title": "Collaboration Track",
  "artistId": "550e8400-e29b-41d4-a716-446655440001",
  "artistName": "Primary Artist",
  "artistIds": ["550e8400-e29b-41d4-a716-446655440001", "550e8400-e29b-41d4-a716-446655440005"],
  "artistNames": ["Primary Artist", "Secondary Artist"],
  "albumId": "550e8400-e29b-41d4-a716-446655440002",
  "albumTitle": "Collaboration Album",
  "trackNumber": 1,
  "discNumber": 1,
  "durationMs": 240000,
  "genre": "Electronic",
  "genres": ["Electronic", "House"],
  "year": 2023,
  "fileSize": 5242880,
  "bitrate": 320,
  "codec": "MP3",
  "filePath": "/music/Collaboration Album/01 - Collaboration Track.mp3",
  "primaryArtistId": "550e8400-e29b-41d4-a716-446655440001",
  "primaryArtistName": "Primary Artist"
}
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
  "artists": [
    {
      "id": "artist_id",
      "name": "Artist Name",
      "type": "artist",
      "albumCount": 5,
      "trackCount": 47
    }
  ],
  "albums": [
    {
      "id": "album_id",
      "title": "Album Title",
      "artistName": "Primary Artist",
      "artistIds": ["primary_artist_id", "secondary_artist_id"],
      "artistNames": ["Primary Artist", "Secondary Artist"],
      "year": 2023,
      "coverArtPath": "/artwork/album.jpg"
    }
  ],
  "tracks": [
    {
      "id": "track_id",
      "title": "Track Title",
      "artistName": "Primary Artist",
      "artistIds": ["primary_artist_id", "secondary_artist_id"],
      "artistNames": ["Primary Artist", "Secondary Artist"],
      "albumTitle": "Album Title",
      "genres": ["Electronic", "House"],
      "durationMs": 240000
    }
  ]
}
```

**Search Behavior:**
- Search finds tracks/albums by **any contributing artist**, not just the primary artist
- Searching for "Secondary Artist" will return tracks/albums where that artist appears in the `artistIds` array
- Genre search finds tracks/albums where the genre appears in the `genres` array
- This provides more comprehensive discovery of collaborations and multi-artist works

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

**Response:**
```json
{
  "tracks": [
    {
      "id": "track_id",
      "title": "Song Title",
      "artistId": "primary_artist_id",
      "artistName": "Primary Artist",
      "artistIds": ["primary_artist_id", "secondary_artist_id"],
      "artistNames": ["Primary Artist", "Secondary Artist"],
      "albumId": "album_id",
      "albumTitle": "Album Name",
      "year": 2022,
      "genre": "Rock",
      "genres": ["Rock", "Alternative"],
      "durationMs": 215000,
      "bitrate": 320,
      "trackNumber": 1,
      "discNumber": 1,
      "filePath": "/music/album/song.mp3"
    }
  ],
  "total": 25,
  "page": 1,
  "pageSize": 50,
  "totalPages": 1
}
```

**Advanced Search Notes:**
- **Artist Filter**: Searches across all artists in `artistIds`, not just the primary artist
- **Genre Filter**: Searches across all genres in `genres`, not just the primary genre
- Results include full multi-valued tag arrays for complete artist/genre information

#### Search Suggestions
```http
GET /api/v2/search/suggestions?q=par
```

### Multi-Valued Tags

Audiarr supports multi-valued tags for artists and genres, allowing tracks and albums to have multiple artists and genres. This feature improves library organization, especially for electronic music with frequent collaborations.

#### How It Works

**Native Multi-Valued Tags:**
- The library scanner reads native multi-valued tags from audio files (e.g., ID3v2.4 `TPE2` frame with multiple values)
- These are the preferred method and provide the most accurate results

**Delimiter Parsing:**
- When native multi-valued tags are not available, the scanner falls back to parsing delimiter-separated values
- Example: A tag containing "Artist A / Artist B" will be parsed into two artists
- Configurable delimiters: `/`, `;`, `,` (default: `/`)

**Database Storage:**
- Each artist and genre is stored as a separate entity in the database
- Many-to-many relationships link tracks/albums to their artists and genres
- This prevents cluttered artist lists with combined strings like "Artist A & Artist B"

#### Backward Compatibility

The API maintains full backward compatibility with existing clients:

1. **Single-Value Fields**: The original fields (`artistId`, `artistName`, `genre`) are always present and contain the primary (first) artist/genre
   - `artistId`: Primary artist ID (first in the artist list)
   - `artistName`: Primary artist name (first in the artist list)
   - `genre`: Primary genre (first in the genre list)

2. **Array Fields**: New array fields contain all values, with the primary value first
   - `artistIds`: Array of all artist IDs (primary first)
   - `artistNames`: Array of all artist names (primary first)
   - `genres`: Array of all genre names (primary first)

3. **Alias Properties**: Explicit naming for backward compatibility
   - `primaryArtistId`: Alias for `artistId`
   - `primaryArtistName`: Alias for `artistName`

**Example:**
```json
{
  "artistId": "artist_1_id",           // Primary artist (backward compatible)
  "artistName": "Primary Artist",      // Primary artist name (backward compatible)
  "artistIds": ["artist_1_id", "artist_2_id"],  // All artists
  "artistNames": ["Primary Artist", "Secondary Artist"],  // All artist names
  "primaryArtistId": "artist_1_id",    // Explicit alias
  "primaryArtistName": "Primary Artist"  // Explicit alias
}
```

#### Configuration

Multi-valued tag parsing is configured in `appsettings.json`:

```json
{
  "MultiValuedTags": {
    "Delimiter": "/",
    "EnableDelimiterParsing": true,
    "PreferredDelimiters": ["/", ";", ","]
  }
}
```

**Configuration Options:**
- `Delimiter`: Primary delimiter for parsing (default: `/`)
- `EnableDelimiterParsing`: Enable/disable delimiter parsing fallback (default: `true`)
- `PreferredDelimiters`: Array of delimiters to try, in order of preference (default: `["/", ";", ","]`)

**Environment Variables:**
Configuration can be overridden using environment variables:
- `MultiValuedTags__Delimiter`
- `MultiValuedTags__EnableDelimiterParsing`
- `MultiValuedTags__PreferredDelimiters__0`, `MultiValuedTags__PreferredDelimiters__1`, etc.

#### Search Behavior

Search endpoints have been updated to find tracks/albums by any contributing artist or genre, not just the primary:

- **Artist Search**: Finds tracks/albums where the artist appears in the `artistIds` array, not just as the primary artist
- **Genre Search**: Finds tracks/albums where the genre appears in the `genres` array, not just as the primary genre
- **Improved Discovery**: Users can find all contributions from an artist, including collaborations

**Example:**
Searching for "Secondary Artist" will return:
- Tracks where "Secondary Artist" is the primary artist
- Tracks where "Secondary Artist" is a contributing artist
- Albums where "Secondary Artist" appears in any capacity

#### Best Practices for API Consumers

1. **Use Array Fields for New Features**: When building new features, use `artistIds`, `artistNames`, and `genres` arrays to display all artists/genres

2. **Maintain Backward Compatibility**: Existing code using `artistId`, `artistName`, and `genre` will continue to work without modification

3. **Display All Artists**: When showing track/album information, consider displaying all artists:
   ```javascript
   // Display primary artist for compatibility
   const primaryArtist = track.artistName;
   
   // Display all artists for new features
   const allArtists = track.artistNames.join(", ");
   ```

4. **Handle Empty Arrays**: Arrays may be empty for tracks/albums without multi-valued tags:
   ```javascript
   const artists = track.artistNames.length > 0 
     ? track.artistNames 
     : [track.artistName]; // Fallback to single value
   ```

5. **Search Considerations**: When implementing search, remember that search now finds tracks/albums by any contributing artist, providing more comprehensive results

6. **Genre Handling**: Genres can be null/empty, so always check array length:
   ```javascript
   const genres = track.genres.length > 0 
     ? track.genres 
     : (track.genre ? [track.genre] : []);
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

#### Get Playlist Play Context
```http
GET /api/v2/playlists/{id}/play
Authorization: Bearer <token>
```

Returns playlist information with ordered tracks for continuous playback.

**Response:**
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
    },
    {
      "id": "track_2", 
      "title": "Second Song",
      "artistName": "Another Artist",
      "albumTitle": "Another Album",
      "trackNumber": 1,
      "durationMs": 195000,
      "streamUrl": "/api/v2/tracks/track_2/stream",
      "position": 1,
      "nextTrackId": "track_3",
      "previousTrackId": "track_1"
    }
  ],
  "totalDurationMs": 2880000
}
```

**Key Features:**
- Tracks are ordered by their position in the playlist
- Each track includes a `streamUrl` for immediate playback
- Navigation links (`nextTrackId`, `previousTrackId`) enable continuous playback
- Total duration is calculated for the entire playlist

**Error Responses:**
- `401 Unauthorized`: Missing or invalid access token
- `404 Not Found`: Playlist not found or user doesn't have access

**Use Cases:**
- Initialize playlist playback in music players
- Build continuous playback queue from playlist
- Display playlist overview with track navigation

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

### Playlists

Playlists allow users to create custom collections of tracks. All playlist endpoints require authentication.

#### Get User's Playlists
```http
GET /api/v2/playlists?page=1&limit=50&includePublic=false
Authorization: Bearer <token>
```

**Parameters:**
- `page` (optional): Page number (default: 1)
- `limit` (optional): Items per page (1-100, default: 50)
- `includePublic` (optional): Include public playlists from other users (default: false)

**Response:**
```json
{
  "data": [
    {
      "id": "playlist_id",
      "name": "My Playlist",
      "description": "A collection of my favorite songs",
      "userId": "user_id",
      "username": "john_doe",
      "isPublic": false,
      "imagePath": null,
      "trackCount": 12,
      "totalDuration": "00:45:23",
      "createdAt": "2024-01-01T12:00:00Z",
      "updatedAt": "2024-01-15T14:30:00Z",
      "lastModified": "2024-01-15T14:30:00Z",
      "playCount": 5
    }
  ],
  "page": 1,
  "limit": 50,
  "total": 3,
  "totalPages": 1
}
```

#### Get Playlist Details
```http
GET /api/v2/playlists/{id}
Authorization: Bearer <token>
```

Returns a playlist with all tracks included. Users can view their own playlists or public playlists from other users.

**Response:**
```json
{
  "id": "playlist_id",
  "name": "My Playlist",
  "description": "A collection of my favorite songs",
  "userId": "user_id",
  "username": "john_doe",
  "isPublic": false,
  "imagePath": null,
  "trackCount": 2,
  "totalDuration": "00:07:45",
  "createdAt": "2024-01-01T12:00:00Z",
  "updatedAt": "2024-01-15T14:30:00Z",
  "lastModified": "2024-01-15T14:30:00Z",
  "playCount": 5,
  "tracks": [
    {
      "trackId": "track_1",
      "title": "Song Title",
      "artistId": "artist_1",
      "artistName": "Artist Name",
      "albumId": "album_1",
      "albumTitle": "Album Title",
      "trackNumber": 1,
      "discNumber": 1,
      "durationMs": 240000,
      "genre": "Rock",
      "year": 2023,
      "filePath": "/music/artist/album/track.mp3",
      "position": 0,
      "positionFloat": 0.0,
      "addedAt": "2024-01-01T12:00:00Z",
      "addedBy": "john_doe"
    }
  ]
}
```

#### Create New Playlist
```http
POST /api/v2/playlists
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "New Playlist",
  "description": "Optional description",
  "isPublic": false,
  "initialTrackIds": ["track_1", "track_2"]
}
```

**Request Body:**
- `name` (required): Playlist name (1-255 characters)
- `description` (optional): Playlist description (max 1000 characters)
- `isPublic` (optional): Whether playlist is public (default: false)
- `initialTrackIds` (optional): Array of track IDs to add initially

**Response:** Returns the created playlist (same structure as `PlaylistDto`)

#### Update Playlist Metadata
```http
PUT /api/v2/playlists/{id}
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "Updated Playlist Name",
  "description": "Updated description",
  "isPublic": true
}
```

Only the playlist owner can update their playlist metadata.

#### Delete Playlist
```http
DELETE /api/v2/playlists/{id}
Authorization: Bearer <token>
```

Permanently deletes a playlist and all track associations. Only the playlist owner can delete their playlist.

**Response:**
```json
{
  "message": "Playlist deleted successfully"
}
```

#### Get Public Playlists
```http
GET /api/v2/playlists/public?page=1&limit=50
Authorization: Bearer <token>
```

Returns all public playlists, ordered by play count and last modified date.

#### Add Tracks to Playlist
```http
POST /api/v2/playlists/{id}/tracks
Authorization: Bearer <token>
Content-Type: application/json

{
  "trackIds": ["track_1", "track_2", "track_3"],
  "position": 5
}
```

**Request Body:**
- `trackIds` (required): Array of track IDs to add
- `position` (optional): Position to insert tracks (0-based index, defaults to end)

**Response:**
```json
{
  "message": "Added 3 track(s) to playlist",
  "addedCount": 3,
  "totalTracks": 8
}
```

#### Remove Tracks from Playlist
```http
DELETE /api/v2/playlists/{id}/tracks
Authorization: Bearer <token>
Content-Type: application/json

{
  "trackIds": ["track_1", "track_2"]
}
```

**Response:**
```json
{
  "message": "Removed 2 track(s) from playlist",
  "removedCount": 2,
  "remainingTracks": 6
}
```

#### Reorder Playlist Tracks
```http
PUT /api/v2/playlists/{id}/tracks/reorder
Authorization: Bearer <token>
Content-Type: application/json

{
  "tracks": [
    {
      "trackId": "track_1",
      "newPosition": 2.5
    },
    {
      "trackId": "track_2", 
      "newPosition": 1.0
    }
  ]
}
```

Uses decimal positioning to avoid conflicts when reordering multiple tracks simultaneously.

**Response:**
```json
{
  "message": "Playlist tracks reordered successfully",
  "reorderedCount": 2
}
```

### Queue Management

Queue management allows users to control their playback queue with support for shuffle, repeat modes, and queue manipulation. All queue endpoints require authentication.

#### Get Queue State
```http
GET /api/v2/queue
Authorization: Bearer <token>
```

**Response:**
```json
{
  "queueId": "queue_123",
  "userId": "user_456",
  "trackIds": ["track1", "track2", "track3"],
  "currentTrackId": "track2",
  "currentIndex": 1,
  "repeatMode": 0,
  "isShuffled": false,
  "totalTracks": 3,
  "queueSource": "album:album_id",
  "lastActivity": "2024-01-15T10:30:00Z",
  "version": 5
}
```

**Repeat Modes:**
- `0`: None - Play queue once
- `1`: One - Repeat current track
- `2`: All - Repeat entire queue

#### Add Tracks to Queue
```http
POST /api/v2/queue/tracks
Authorization: Bearer <token>
Content-Type: application/json

{
  "trackIds": ["track4", "track5"],
  "source": "search:rock music",
  "playNext": false
}
```

**Parameters:**
- `trackIds`: Array of track IDs to add (1-100 tracks)
- `source`: Optional source identifier
- `playNext`: If true, adds tracks after current track

**Response:**
```json
{
  "queueId": "queue_123",
  "userId": "user_456",
  "trackIds": ["track1", "track2", "track3", "track4", "track5"],
  "currentTrackId": "track2",
  "currentIndex": 1,
  "repeatMode": 0,
  "isShuffled": false,
  "totalTracks": 5,
  "queueSource": "mixed",
  "lastActivity": "2024-01-15T10:35:00Z",
  "version": 6
}
```

#### Remove Track from Queue
```http
DELETE /api/v2/queue/tracks/{index}
Authorization: Bearer <token>
```

**Example:**
```http
DELETE /api/v2/queue/tracks/2
Authorization: Bearer <token>
```

**Response:** Returns updated queue state

#### Clear Queue
```http
DELETE /api/v2/queue/clear?keepCurrentTrack=false
Authorization: Bearer <token>
```

**Parameters:**
- `keepCurrentTrack`: If true, only removes other tracks

**Response:** Returns updated queue state

#### Reorder Queue
```http
PUT /api/v2/queue/reorder
Authorization: Bearer <token>
Content-Type: application/json

{
  "trackId": "track3",
  "newIndex": 0
}
```

**Response:** Returns updated queue state

#### Update Queue Settings
```http
PUT /api/v2/queue/settings
Authorization: Bearer <token>
Content-Type: application/json

{
  "repeatMode": 2,
  "isShuffled": true,
  "currentIndex": 0
}
```

**Parameters:**
- `repeatMode`: Optional repeat mode (0=None, 1=One, 2=All)
- `isShuffled`: Optional shuffle state
- `currentIndex`: Optional current track index

**Response:** Returns updated queue state

#### Replace Entire Queue
```http
POST /api/v2/queue/replace
Authorization: Bearer <token>
Content-Type: application/json

{
  "trackIds": ["new_track1", "new_track2"],
  "startIndex": 0,
  "source": "album:new_album"
}
```

**Parameters:**
- `trackIds`: Array of track IDs (1-1000 tracks)
- `startIndex`: Index to start playing from (default: 0)
- `source`: Optional source identifier

**Response:** Returns updated queue state

#### Skip to Next Track
```http
POST /api/v2/queue/next
Authorization: Bearer <token>
```

**Response:** Returns updated queue state

#### Go to Previous Track
```http
POST /api/v2/queue/previous
Authorization: Bearer <token>
```

**Response:** Returns updated queue state

#### Jump to Specific Position
```http
PUT /api/v2/queue/position/{index}
Authorization: Bearer <token>
```

**Example:**
```http
PUT /api/v2/queue/position/3
Authorization: Bearer <token>
```

**Response:** Returns updated queue state

#### Common Error Responses

**400 Bad Request - Invalid Index:**
```json
{
  "title": "Invalid index",
  "detail": "Index 5 is out of range for queue with 3 tracks",
  "status": 400
}
```

**404 Not Found - Empty Queue:**
```json
{
  "title": "Queue empty",
  "detail": "Cannot perform operation on empty queue",
  "status": 404
}
```

### Library Scanner

The Library Scanner manages scanning and indexing of audio files in your music library. It supports both full library scans and single file operations, with real-time progress updates via SignalR.

#### Queue Library Scan
```http
POST /api/v2/scanner/scan
Authorization: Bearer <token>
Content-Type: application/json

{
  "libraryPath": "/path/to/music/library",
  "requestId": "scan_12345",
  "requestedAt": "2024-01-15T10:30:00Z"
}
```

**Parameters:**
- `libraryPath`: Path to the directory containing audio files
- `requestId`: Optional unique identifier (auto-generated if not provided)
- `requestedAt`: Optional timestamp (defaults to current time)

**Response (202 Accepted):**
```json
{
  "message": "Scan request queued",
  "requestId": "scan_12345",
  "path": "/path/to/music/library"
}
```

The response includes a `Location` header pointing to: `/api/v2/scanner/status/{requestId}`

#### Scan Single File
```http
POST /api/v2/scanner/scan/single?filePath=/path/to/song.mp3
Authorization: Bearer <token>
```

**Parameters:**
- `filePath`: Full path to the audio file to scan

**Response:**
```json
{
  "totalFiles": 1,
  "processedFiles": 1,
  "newTracks": 1,
  "updatedTracks": 0,
  "errors": 0,
  "errorMessages": [],
  "startTime": "2024-01-15T10:30:00Z",
  "endTime": "2024-01-15T10:30:05Z",
  "duration": "00:00:05"
}
```

#### Get Supported Audio Formats
```http
GET /api/v2/scanner/supported-formats
Authorization: Bearer <token>
```

**Response:**
```json
{
  "formats": [
    ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".opus",
    ".wav", ".wma", ".alac", ".ape", ".wv", ".mka"
  ]
}
```

#### Real-time Scan Progress (SignalR)

Connect to the scan hub at `/hubs/scan` to receive real-time progress updates:

**Connection Example:**
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://your-server:8080/hubs/scan", {
        accessTokenFactory: () => localStorage.getItem("accessToken")
    })
    .build();

// Listen for scan progress
connection.on("ScanProgress", (progress) => {
    console.log(`Progress: ${progress.processed}/${progress.total} (${progress.percentComplete}%)`);
    console.log(`Current: ${progress.message}`);
});

// Listen for scan completion
connection.on("ScanComplete", (result) => {
    console.log(`Scan completed: ${result.newTracks} new, ${result.updatedTracks} updated`);
    console.log(`Errors: ${result.errors}, Total files: ${result.totalFiles}`);
});

// Listen for scan errors
connection.on("ScanError", (error) => {
    console.error("Scan error:", error);
});

await connection.start();
```

**Progress Event Format:**
```json
{
  "processed": 150,
  "total": 500,
  "message": "Processing: /music/artist/album/track.mp3",
  "percentComplete": 30.0
}
```

**Completion Event Format:**
```json
{
  "totalFiles": 500,
  "newTracks": 45,
  "updatedTracks": 12,
  "errors": 3,
  "completedAt": "2024-01-15T10:45:00Z"
}
```

#### Common Error Responses

**404 Not Found - File doesn't exist:**
```json
{
  "error": "File not found"
}
```

**400 Bad Request - Invalid file type:**
```json
{
  "error": "Not an audio file"
}
```

**500 Internal Server Error - Scan failed to queue:**
```json
{
  "title": "Internal Server Error",
  "detail": "Failed to queue scan request",
  "status": 500
}
```

#### Best Practices

1. **Progress Monitoring**: Always connect to SignalR before starting a scan to receive progress updates
2. **Path Validation**: Ensure library paths exist and are accessible before scanning
3. **Format Checking**: Use the supported formats endpoint to validate files before single file scans
4. **Error Handling**: Monitor for scan errors and handle failed file processing gracefully
5. **Resource Management**: Avoid running multiple concurrent full library scans

### Diagnostics

The Diagnostics API provides database health monitoring and data quality assessment tools. These endpoints help administrators understand the current state of their music library and identify potential issues.

**Important**: This endpoint is not protected by authentication and should be restricted to admin users in production environments.

#### Database Data Check
```http
GET /api/v2/diagnostic/data-check
Accept: application/json
```

Provides comprehensive database diagnostics including total counts, duplicate detection, and sample data for quality assessment.

**Response:**
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
    },
    {
      "id": 2,
      "name": "Pink Floyd",
      "albumCount": 15,
      "trackCount": 147
    }
  ]
}
```

**Use Cases:**
- **Database Health Monitoring**: Regular checks of library size and growth
- **Duplicate Detection**: Identify artists and albums that need cleanup
- **Data Quality Assessment**: Verify library integrity after imports
- **Performance Planning**: Understand database size for capacity planning
- **Troubleshooting**: Diagnose data inconsistencies and import issues

**Best Practices:**
1. **Regular Monitoring**: Check diagnostics after bulk imports or library changes
2. **Duplicate Management**: Use duplicate information to plan cleanup operations
3. **Performance Awareness**: Monitor total counts for performance impact assessment
4. **Data Validation**: Verify sample data matches expected library content

### Data Cleanup

The Data Cleanup API provides tools for maintaining database integrity by identifying and merging duplicate entries. These operations help optimize storage and resolve data inconsistencies that may occur over time.

**Important**: These endpoints are not protected by authentication and should be used with caution in production environments.

#### Merge Duplicate Artists
```http
POST /api/v2/cleanup/merge-duplicate-artists
Content-Type: application/json
```

Identifies artists with identical names and merges them into single entries. The first artist found is kept as the primary record, and all tracks and albums are reassigned to it.

**Response:**
```json
{
  "message": "Merged 5 duplicate artists",
  "duplicateGroupsFound": 3
}
```

**Process:**
1. Groups artists by exact name match
2. Keeps the first artist in each group as primary
3. Updates all tracks to reference the primary artist
4. Updates all albums to reference the primary artist
5. Removes duplicate artist records

#### Merge Duplicate Albums
```http
POST /api/v2/cleanup/merge-duplicate-albums
Content-Type: application/json
```

Identifies albums with identical titles and artists, then merges them. Preserves cover art by preferring albums that have artwork.

**Response:**
```json
{
  "message": "Merged 8 duplicate albums",
  "duplicateGroupsFound": 4
}
```

**Process:**
1. Groups albums by title and artist ID
2. Keeps the first album as primary
3. If primary album lacks cover art, copies from duplicate with artwork
4. Updates all tracks to reference the primary album
5. Removes duplicate album records

#### Clean All Data
```http
POST /api/v2/cleanup/clean-all
Content-Type: application/json
```

Performs a comprehensive cleanup by running both artist and album deduplication in sequence.

**Response:**
```json
{
  "message": "Database cleanup completed",
  "artistsMerged": 5,
  "albumsMerged": 8,
  "duplicateArtistGroupsFound": 3,
  "duplicateAlbumGroupsFound": 4
}
```

**Process:**
1. First merges duplicate artists (same as individual operation)
2. Then merges duplicate albums (same as individual operation)
3. Returns combined statistics for both operations

#### Common Use Cases

**Regular Maintenance:**
```bash
# Weekly cleanup to maintain data quality
curl -X POST http://localhost:8080/api/v2/cleanup/clean-all
```

**Targeted Cleanup:**
```bash
# Only merge duplicate artists
curl -X POST http://localhost:8080/api/v2/cleanup/merge-duplicate-artists

# Only merge duplicate albums
curl -X POST http://localhost:8080/api/v2/cleanup/merge-duplicate-albums
```

#### Best Practices

1. **Backup First**: Always backup your database before running cleanup operations
2. **Test Environment**: Run cleanup operations in a test environment first
3. **Off-Peak Hours**: Execute during low-usage periods to minimize impact
4. **Monitor Results**: Review the response to understand what was merged
5. **Sequential Operations**: Use individual endpoints for fine-grained control

#### Important Considerations

- **Data Loss Prevention**: The cleanup process preserves all track and album associations
- **Cover Art Preservation**: Album merging intelligently preserves artwork when possible
- **Irreversible Operations**: Cleanup operations cannot be undone automatically
- **Performance Impact**: Large libraries may experience temporary slowdowns during cleanup
- **No Authentication**: These endpoints are currently unprotected - implement access controls as needed

#### Error Responses

**500 Internal Server Error - Database Operation Failed:**
```json
{
  "title": "Internal Server Error",
  "detail": "Database operation failed during cleanup",
  "status": 500
}
```

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