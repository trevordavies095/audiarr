# Sequential Implementation Plan for Taxonomy Enhancements

## Phase 1: Critical Playlist Infrastructure (Week 1)

### Step 1: Enhance Playlist Entity
**File**: `src/Audiarr.Core/Entities/Playlist.cs`
- Add `Type` property (enum: Manual, Smart, System)
- Add `Rules` property (JSON for smart playlist criteria)
- Add `LastModified` timestamp
- Add `PlayCount` counter
- Add `SortOrder` property

### Step 2: Create Playlist Endpoints
**File**: `src/Audiarr.Api/Endpoints/PlaylistEndpoints.cs` (NEW)
```
GET    /api/v2/playlists - List user's playlists
GET    /api/v2/playlists/{id} - Get playlist details with tracks
POST   /api/v2/playlists - Create new playlist
PUT    /api/v2/playlists/{id} - Update playlist metadata
DELETE /api/v2/playlists/{id} - Delete playlist
POST   /api/v2/playlists/{id}/tracks - Add tracks to playlist
DELETE /api/v2/playlists/{id}/tracks/{trackId} - Remove track
PUT    /api/v2/playlists/{id}/tracks/reorder - Reorder tracks
POST   /api/v2/playlists/{id}/duplicate - Clone playlist
```

### Step 3: Update Database Context
**File**: `src/Audiarr.Data/Context/AudiarrContext.cs`
- Add playlist type configuration
- Add indexes for performance
- Create migration for schema changes

## Phase 2: Playback Queue System (Week 1-2)

### Step 4: Create Queue Entity
**File**: `src/Audiarr.Core/Entities/PlaybackQueue.cs` (NEW)
```csharp
public class PlaybackQueue : BaseEntity
{
    public string UserId { get; set; }
    public string? CurrentTrackId { get; set; }
    public int CurrentIndex { get; set; }
    public RepeatMode RepeatMode { get; set; }
    public bool ShuffleEnabled { get; set; }
    public string QueueItems { get; set; } // JSON array of track IDs
    public DateTime LastUpdated { get; set; }
}
```

### Step 5: Implement Queue Endpoints
**File**: `src/Audiarr.Api/Endpoints/QueueEndpoints.cs` (NEW)
```
GET    /api/v2/queue - Get current queue
POST   /api/v2/queue/add - Add tracks to queue
POST   /api/v2/queue/add-next - Add track as next
POST   /api/v2/queue/clear - Clear queue
PUT    /api/v2/queue/shuffle - Toggle shuffle
PUT    /api/v2/queue/repeat - Set repeat mode
DELETE /api/v2/queue/{position} - Remove item at position
PUT    /api/v2/queue/reorder - Reorder queue items
```

## Phase 3: User Personalization (Week 2)

### Step 6: Create Favorites System
**File**: `src/Audiarr.Core/Entities/UserFavorite.cs` (NEW)
```csharp
public class UserFavorite
{
    public string UserId { get; set; }
    public string EntityId { get; set; }
    public FavoriteType EntityType { get; set; } // Track, Album, Artist
    public DateTime FavoritedAt { get; set; }
}
```

### Step 7: Add Favorites Endpoints
**File**: `src/Audiarr.Api/Endpoints/FavoritesEndpoints.cs` (NEW)
```
GET    /api/v2/favorites - Get all favorites
GET    /api/v2/favorites/tracks - Get favorite tracks
GET    /api/v2/favorites/albums - Get favorite albums
GET    /api/v2/favorites/artists - Get favorite artists
POST   /api/v2/favorites/{type}/{id} - Add to favorites
DELETE /api/v2/favorites/{type}/{id} - Remove from favorites
GET    /api/v2/{type}/{id}/is-favorite - Check if favorited
```

## Phase 4: Performance Optimizations (Week 2-3)

### Step 8: Implement Batch Operations
**File**: `src/Audiarr.Api/Endpoints/BatchEndpoints.cs` (NEW)
```
POST /api/v2/batch/tracks - Update multiple tracks
POST /api/v2/batch/playlists/{id}/tracks - Add multiple tracks
POST /api/v2/batch/favorites - Add multiple favorites
DELETE /api/v2/batch/favorites - Remove multiple favorites
```

### Step 9: Add Statistics Endpoints
**File**: `src/Audiarr.Api/Endpoints/StatsEndpoints.cs` (NEW)
```
GET /api/v2/stats/library - Overall library statistics
GET /api/v2/stats/user/{id} - User listening statistics
GET /api/v2/stats/recent-activity - Recent library activity
GET /api/v2/stats/top-played - Most played tracks/albums
```

## Phase 5: Advanced Features (Week 3)

### Step 10: Extend SignalR Hub
**File**: `src/Audiarr.Api/Hubs/LibraryHub.cs` (NEW)
- Real-time library change notifications
- Playback state synchronization
- Collaborative playlist updates

### Step 11: Create Tags System
**File**: `src/Audiarr.Core/Entities/UserTag.cs` (NEW)
```csharp
public class UserTag : BaseEntity
{
    public string UserId { get; set; }
    public string Name { get; set; }
    public string? Color { get; set; }
    public string? Description { get; set; }
}

public class TrackTag
{
    public string TrackId { get; set; }
    public string TagId { get; set; }
    public DateTime TaggedAt { get; set; }
}
```

### Step 12: Add Tags Endpoints
**File**: `src/Audiarr.Api/Endpoints/TagsEndpoints.cs` (NEW)
```
GET    /api/v2/tags - List user's tags
POST   /api/v2/tags - Create tag
PUT    /api/v2/tags/{id} - Update tag
DELETE /api/v2/tags/{id} - Delete tag
POST   /api/v2/tags/{id}/tracks - Add tracks to tag
GET    /api/v2/tags/{id}/tracks - Get tagged tracks
```

## Phase 6: Documentation & Testing (Week 3-4)

### Step 13: Update API Documentation
- Update `docs/API_INTEGRATION.md` with new endpoints
- Update `docs/API_MODELS.md` with new entities
- Update `docs/POSTMAN_COLLECTION.md` with test requests

### Step 14: Create DTOs
**Files**: `src/Audiarr.Core/DTOs/`
- `PlaylistDto.cs`
- `QueueDto.cs`
- `FavoriteDto.cs`
- `TagDto.cs`
- `StatsDto.cs`

### Step 15: Database Migrations
- Create migrations for all new entities
- Add appropriate indexes for performance
- Seed sample data for testing

## Implementation Priority

**Critical (Do First)**:
1. Playlist CRUD operations (Steps 1-3)
2. Queue management (Steps 4-5)

**High Priority**:
3. Favorites system (Steps 6-7)
4. Batch operations (Step 8)

**Medium Priority**:
5. Statistics (Step 9)
6. Real-time updates (Step 10)

**Nice to Have**:
7. Tags system (Steps 11-12)

## Success Metrics
- All playlist operations < 200ms response time
- Queue state persists across sessions
- Batch operations handle 100+ items efficiently
- Real-time updates delivered < 100ms
- Zero breaking changes to existing APIs

## Current Gaps Analysis

### Critical Missing Components
1. **Playlist Management APIs** - Currently NO dedicated playlist endpoints exist
2. **Queue/Now Playing Management** - No queue management system exists
3. **Favorites/Likes System** - No favoriting mechanism

### Impact on Client Development
Without these changes, clients will need to:
- Implement complex local state management
- Create inconsistent user experiences
- Lack basic music app features users expect

### Current Strengths (No Changes Needed)
✅ Core hierarchy (Artist/Album/Track) - Well-structured
✅ Search capabilities - Good text and advanced search
✅ Streaming infrastructure - Range requests working
✅ Authentication/Authorization - JWT implementation solid
✅ Pagination - Consistent across endpoints
✅ Metadata handling - Comprehensive track details

## Recommendation Summary
The current taxonomy is **70% complete** for robust client development. The missing 30% consists primarily of playlist management, queue/playback state, and user personalization features. The core taxonomy structure doesn't need changes - it needs **expansion** with user-centric features that enable rich client experiences.

This phased approach ensures critical features are delivered first while maintaining system stability.