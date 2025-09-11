# Audiarr iOS Client Development Guide

## Table of Contents
1. [Project Setup](#project-setup)
2. [Authentication Manager](#authentication-manager)
3. [API Client](#api-client)
4. [Models](#models)
5. [Audio Streaming](#audio-streaming)
6. [Playlist Management](#playlist-management)
7. [Queue Management](#queue-management)
8. [Library Scanner](#library-scanner)
9. [Database Diagnostics](#database-diagnostics)
10. [Data Cleanup](#data-cleanup)
11. [SignalR Integration](#signalr-integration)
12. [Offline Support](#offline-support)
13. [UI Components](#ui-components)
14. [Best Practices](#best-practices)

## Project Setup

### Requirements
- iOS 15.0+
- Xcode 14+
- Swift 5.7+

### Dependencies

Add to your `Package.swift` or via Xcode:
```swift
dependencies: [
    .package(url: "https://github.com/microsoft/signalr-client-swift", from: "1.0.0"),
    .package(url: "https://github.com/kishikawakatsumi/KeychainAccess", from: "5.0.0"),
    .package(url: "https://github.com/SDWebImage/SDWebImage", from: "5.0.0")
]
```

### Info.plist Configuration
```xml
<key>NSAppTransportSecurity</key>
<dict>
    <!-- Only for development/local servers -->
    <key>NSAllowsArbitraryLoads</key>
    <true/>
</dict>
<key>UIBackgroundModes</key>
<array>
    <string>audio</string>
</array>
```

## Authentication Manager

```swift
import Foundation
import KeychainAccess

class AuthenticationManager: ObservableObject {
    static let shared = AuthenticationManager()
    
    private let keychain = Keychain(service: "com.yourapp.audiarr")
    private let baseURL: String
    
    @Published var isAuthenticated = false
    @Published var currentUser: User?
    
    private var accessToken: String?
    private var refreshToken: String? {
        get { try? keychain.getString("refreshToken") }
        set { 
            if let value = newValue {
                try? keychain.set(value, key: "refreshToken")
            } else {
                try? keychain.remove("refreshToken")
            }
        }
    }
    
    private var tokenExpirationDate: Date?
    private var refreshTimer: Timer?
    
    init(baseURL: String = "http://your-server:8080") {
        self.baseURL = baseURL
        self.loadStoredTokens()
    }
    
    // MARK: - Public Methods
    
    func login(username: String, password: String) async throws {
        let url = URL(string: "\(baseURL)/api/v2/auth/login")!
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        let credentials = LoginRequest(username: username, password: password)
        request.httpBody = try JSONEncoder().encode(credentials)
        
        let (data, response) = try await URLSession.shared.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse,
              httpResponse.statusCode == 200 else {
            throw AuthError.invalidCredentials
        }
        
        let loginResponse = try JSONDecoder().decode(LoginResponse.self, from: data)
        
        await MainActor.run {
            self.accessToken = loginResponse.accessToken
            self.refreshToken = loginResponse.refreshToken
            self.tokenExpirationDate = loginResponse.expiresAt
            self.currentUser = loginResponse.user
            self.isAuthenticated = true
            self.scheduleTokenRefresh()
        }
    }
    
    func logout() async throws {
        guard let token = refreshToken else { return }
        
        let url = URL(string: "\(baseURL)/api/v2/auth/logout")!
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("Bearer \(accessToken ?? "")", forHTTPHeaderField: "Authorization")
        
        let body = ["refreshToken": token]
        request.httpBody = try JSONEncoder().encode(body)
        
        _ = try? await URLSession.shared.data(for: request)
        
        await MainActor.run {
            self.clearTokens()
        }
    }
    
    func getAccessToken() async throws -> String {
        if let token = accessToken,
           let expiration = tokenExpirationDate,
           expiration > Date().addingTimeInterval(60) { // 1 minute buffer
            return token
        }
        
        try await refreshAccessToken()
        
        guard let token = accessToken else {
            throw AuthError.notAuthenticated
        }
        
        return token
    }
    
    func getCurrentUser() async throws -> User {
        let token = try await getAccessToken()
        
        let url = URL(string: "\(baseURL)/api/v2/auth/me")!
        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        
        let (data, response) = try await URLSession.shared.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse else {
            throw AuthError.invalidResponse
        }
        
        switch httpResponse.statusCode {
        case 200:
            let userDto = try JSONDecoder().decode(UserDto.self, from: data)
            return User(
                id: userDto.id,
                username: userDto.username,
                email: userDto.email,
                role: userDto.role,
                lastLogin: userDto.lastLogin
            )
        case 401:
            throw AuthError.notAuthenticated
        case 404:
            throw AuthError.userNotFound
        default:
            throw AuthError.invalidResponse
        }
    }
    
    func changePassword(currentPassword: String, newPassword: String) async throws -> String {
        let token = try await getAccessToken()
        
        let url = URL(string: "\(baseURL)/api/v2/auth/change-password")!
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        
        let changeRequest = ChangePasswordRequest(
            currentPassword: currentPassword,
            newPassword: newPassword
        )
        request.httpBody = try JSONEncoder().encode(changeRequest)
        
        let (data, response) = try await URLSession.shared.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse else {
            throw AuthError.invalidResponse
        }
        
        switch httpResponse.statusCode {
        case 200:
            let responseData = try JSONDecoder().decode([String: String].self, from: data)
            let message = responseData["message"] ?? "Password changed successfully"
            
            // Clear tokens since all sessions are revoked
            await MainActor.run {
                self.clearTokens()
            }
            
            return message
        case 400:
            throw AuthError.invalidCredentials // Current password incorrect or new password invalid
        case 401:
            throw AuthError.notAuthenticated
        default:
            throw AuthError.invalidResponse
        }
    }
    
    // MARK: - Private Methods
    
    private func refreshAccessToken() async throws {
        guard let refreshToken = refreshToken else {
            throw AuthError.noRefreshToken
        }
        
        let url = URL(string: "\(baseURL)/api/v2/auth/refresh")!
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        let body = ["refreshToken": refreshToken]
        request.httpBody = try JSONEncoder().encode(body)
        
        let (data, response) = try await URLSession.shared.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse,
              httpResponse.statusCode == 200 else {
            await MainActor.run {
                self.clearTokens()
            }
            throw AuthError.refreshFailed
        }
        
        let tokenResponse = try JSONDecoder().decode(TokenResponse.self, from: data)
        
        await MainActor.run {
            self.accessToken = tokenResponse.accessToken
            self.refreshToken = tokenResponse.refreshToken
            self.tokenExpirationDate = tokenResponse.expiresAt
            self.scheduleTokenRefresh()
        }
    }
    
    private func scheduleTokenRefresh() {
        refreshTimer?.invalidate()
        
        guard let expiration = tokenExpirationDate else { return }
        
        // Refresh 5 minutes before expiration
        let refreshTime = expiration.addingTimeInterval(-300)
        let timeInterval = refreshTime.timeIntervalSinceNow
        
        if timeInterval > 0 {
            refreshTimer = Timer.scheduledTimer(withTimeInterval: timeInterval, repeats: false) { _ in
                Task {
                    try? await self.refreshAccessToken()
                }
            }
        }
    }
    
    private func loadStoredTokens() {
        if let _ = refreshToken {
            Task {
                do {
                    try await refreshAccessToken()
                    await MainActor.run {
                        self.isAuthenticated = true
                    }
                } catch {
                    print("Failed to refresh token on launch: \(error)")
                }
            }
        }
    }
    
    private func clearTokens() {
        accessToken = nil
        refreshToken = nil
        tokenExpirationDate = nil
        currentUser = nil
        isAuthenticated = false
        refreshTimer?.invalidate()
        refreshTimer = nil
    }
}

// MARK: - Error Types

enum AuthError: LocalizedError {
    case invalidCredentials
    case notAuthenticated
    case noRefreshToken
    case refreshFailed
    
    var errorDescription: String? {
        switch self {
        case .invalidCredentials:
            return "Invalid username or password"
        case .notAuthenticated:
            return "Not authenticated"
        case .noRefreshToken:
            return "No refresh token available"
        case .refreshFailed:
            return "Failed to refresh authentication"
        }
    }
}
```

## API Client

```swift
import Foundation

class AudiarrAPIClient {
    static let shared = AudiarrAPIClient()
    
    private let baseURL: String
    private let session: URLSession
    private let decoder: JSONDecoder
    
    init(baseURL: String = "http://your-server:8080") {
        self.baseURL = baseURL
        
        let config = URLSessionConfiguration.default
        config.timeoutIntervalForRequest = 30
        config.timeoutIntervalForResource = 300
        self.session = URLSession(configuration: config)
        
        self.decoder = JSONDecoder()
        self.decoder.dateDecodingStrategy = .iso8601
    }
    
    // MARK: - Generic Request Method
    
    private func request<T: Decodable>(
        _ endpoint: String,
        method: String = "GET",
        body: Data? = nil,
        authenticated: Bool = true
    ) async throws -> T {
        let url = URL(string: "\(baseURL)/api/v2\(endpoint)")!
        var request = URLRequest(url: url)
        request.httpMethod = method
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        if authenticated {
            let token = try await AuthenticationManager.shared.getAccessToken()
            request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        }
        
        request.httpBody = body
        
        let (data, response) = try await session.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse else {
            throw APIError.invalidResponse
        }
        
        switch httpResponse.statusCode {
        case 200...299:
            return try decoder.decode(T.self, from: data)
        case 401:
            throw APIError.unauthorized
        case 404:
            throw APIError.notFound
        default:
            throw APIError.serverError(httpResponse.statusCode)
        }
    }
    
    // MARK: - Artists
    
    func getArtists(page: Int = 1, limit: Int = 50) async throws -> PagedResponse<Artist> {
        return try await request("/artists?page=\(page)&limit=\(limit)")
    }
    
    func getArtist(id: String) async throws -> ArtistDetail {
        return try await request("/artists/\(id)")
    }
    
    func getArtistAlbums(artistId: String) async throws -> AlbumsResponse {
        return try await request("/artists/\(artistId)/albums")
    }
    
    // MARK: - Albums
    
    func getAlbums(page: Int = 1, limit: Int = 50) async throws -> PagedResponse<Album> {
        return try await request("/albums?page=\(page)&limit=\(limit)")
    }
    
    func getAlbum(id: String) async throws -> AlbumDetail {
        return try await request("/albums/\(id)")
    }
    
    func getRecentAlbums(limit: Int = 20) async throws -> AlbumsResponse {
        return try await request("/albums/recent?limit=\(limit)")
    }
    
    // MARK: - Tracks
    
    func getTracks(page: Int = 1, limit: Int = 50) async throws -> PagedResponse<Track> {
        return try await request("/tracks?page=\(page)&limit=\(limit)")
    }
    
    func getTrack(id: String) async throws -> TrackDetail {
        return try await request("/tracks/\(id)")
    }
    
    func getPopularTracks(limit: Int = 50) async throws -> TracksResponse {
        return try await request("/tracks/popular?limit=\(limit)")
    }
    
    func getRecentTracks(limit: Int = 20) async throws -> TracksResponse {
        return try await request("/tracks/recent?limit=\(limit)")
    }
    
    func updatePlayCount(trackId: String) async throws {
        let _: PlayCountResponse = try await request(
            "/tracks/\(trackId)/play",
            method: "POST"
        )
    }
    
    // MARK: - Search
    
    func search(query: String, limit: Int = 5) async throws -> SearchResponse {
        let encoded = query.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? ""
        return try await request("/search?q=\(encoded)&limit=\(limit)", authenticated: false)
    }
    
    func searchSuggestions(query: String) async throws -> SuggestionsResponse {
        let encoded = query.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? ""
        return try await request("/search/suggestions?q=\(encoded)", authenticated: false)
    }
    
    // MARK: - Playlists
    
    func getPlaylists(page: Int = 1, limit: Int = 50, includePublic: Bool = false) async throws -> PlaylistsResponse {
        return try await request("/playlists?page=\(page)&limit=\(limit)&includePublic=\(includePublic)")
    }
    
    func getPlaylist(id: String) async throws -> PlaylistDetails {
        return try await request("/playlists/\(id)")
    }
    
    func createPlaylist(_ request: CreatePlaylistRequest) async throws -> Playlist {
        let body = try JSONEncoder().encode(request)
        return try await self.request("/playlists", method: "POST", body: body)
    }
    
    func updatePlaylist(id: String, _ request: UpdatePlaylistRequest) async throws -> Playlist {
        let body = try JSONEncoder().encode(request)
        return try await self.request("/playlists/\(id)", method: "PUT", body: body)
    }
    
    func deletePlaylist(id: String) async throws {
        let _: EmptyResponse = try await request("/playlists/\(id)", method: "DELETE")
    }
    
    func addTracksToPlaylist(playlistId: String, _ request: AddTracksRequest) async throws {
        let body = try JSONEncoder().encode(request)
        let _: EmptyResponse = try await self.request("/playlists/\(playlistId)/tracks", method: "POST", body: body)
    }
    
    func removeTracksFromPlaylist(playlistId: String, _ request: RemoveTracksRequest) async throws {
        let body = try JSONEncoder().encode(request)
        let _: EmptyResponse = try await self.request("/playlists/\(playlistId)/tracks", method: "DELETE", body: body)
    }
    
    func reorderPlaylistTracks(playlistId: String, _ request: ReorderTracksRequest) async throws {
        let body = try JSONEncoder().encode(request)
        let _: EmptyResponse = try await self.request("/playlists/\(playlistId)/tracks/reorder", method: "PUT", body: body)
    }
    
    func updatePlaylistImage(playlistId: String, _ request: UpdatePlaylistImageRequest) async throws {
        let body = try JSONEncoder().encode(request)
        let _: EmptyResponse = try await self.request("/playlists/\(playlistId)/image", method: "PUT", body: body)
    }
    
    func copyPlaylist(playlistId: String, _ request: CopyPlaylistRequest) async throws -> Playlist {
        let body = try JSONEncoder().encode(request)
        return try await self.request("/playlists/\(playlistId)/copy", method: "POST", body: body)
    }
    
    // MARK: - Queue Management
    
    func getQueueState() async throws -> QueueState {
        return try await request("/queue")
    }
    
    func addTracksToQueue(_ request: AddToQueueRequest) async throws -> QueueState {
        let body = try JSONEncoder().encode(request)
        return try await self.request("/queue/tracks", method: "POST", body: body)
    }
    
    func removeTrackFromQueue(at index: Int) async throws -> QueueState {
        return try await request("/queue/tracks/\(index)", method: "DELETE")
    }
    
    func clearQueue(keepCurrentTrack: Bool = false) async throws -> QueueState {
        return try await request("/queue/clear?keepCurrentTrack=\(keepCurrentTrack)", method: "DELETE")
    }
    
    func reorderQueue(_ request: ReorderQueueRequest) async throws -> QueueState {
        let body = try JSONEncoder().encode(request)
        return try await self.request("/queue/reorder", method: "PUT", body: body)
    }
    
    func updateQueueSettings(_ request: UpdateQueueRequest) async throws -> QueueState {
        let body = try JSONEncoder().encode(request)
        return try await self.request("/queue/settings", method: "PUT", body: body)
    }
    
    func replaceQueue(_ request: ReplaceQueueRequest) async throws -> QueueState {
        let body = try JSONEncoder().encode(request)
        return try await self.request("/queue/replace", method: "POST", body: body)
    }
    
    func nextTrack() async throws -> QueueState {
        return try await request("/queue/next", method: "POST")
    }
    
    func previousTrack() async throws -> QueueState {
        return try await request("/queue/previous", method: "POST")
    }
    
    func jumpToPosition(_ index: Int) async throws -> QueueState {
        return try await request("/queue/position/\(index)", method: "PUT")
    }
    
    // MARK: - Library Scanner
    
    func getSupportedFormats() async throws -> SupportedFormatsResponse {
        return try await request("/scanner/supported-formats")
    }
    
    func queueLibraryScan(_ request: ScanRequest) async throws -> ScanQueueResponse {
        let body = try JSONEncoder().encode(request)
        return try await self.request("/scanner/scan", method: "POST", body: body)
    }
    
    func scanSingleFile(filePath: String) async throws -> ScanResult {
        let encodedPath = filePath.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? filePath
        return try await request("/scanner/scan/single?filePath=\(encodedPath)", method: "POST")
    }
    
    func isAudioFile(_ filePath: String) async throws -> Bool {
        do {
            let formats = try await getSupportedFormats()
            let fileExtension = URL(fileURLWithPath: filePath).pathExtension.lowercased()
            return formats.formats.contains(".\(fileExtension)")
        } catch {
            return false
        }
    }
    
    // MARK: - Data Cleanup
    
    func mergeDuplicateArtists() async throws -> ArtistCleanupResponse {
        return try await request("/cleanup/merge-duplicate-artists", method: "POST")
    }
    
    func mergeDuplicateAlbums() async throws -> AlbumCleanupResponse {
        return try await request("/cleanup/merge-duplicate-albums", method: "POST")
    }
    
    func cleanAllData() async throws -> ComprehensiveCleanupResponse {
        return try await request("/cleanup/clean-all", method: "POST")
    }
    
    // MARK: - Streaming
    
    func getStreamURL(for trackId: String) -> URL {
        return URL(string: "\(baseURL)/api/v2/tracks/\(trackId)/stream")!
    }
    
    func getAlbumCoverURL(for albumId: String) -> URL {
        return URL(string: "\(baseURL)/api/v2/albums/\(albumId)/cover")!
    }
    
    func getPlayContext(for trackId: String) async throws -> TrackPlayContext {
        return try await request("/tracks/\(trackId)/play", authenticated: false)
    }
    
    func getAlbumPlayContext(for albumId: String) async throws -> AlbumPlayContext {
        return try await request("/albums/\(albumId)/play", authenticated: false)
    }
}

// MARK: - Error Types

enum APIError: LocalizedError {
    case invalidResponse
    case unauthorized
    case notFound
    case serverError(Int)
    
    var errorDescription: String? {
        switch self {
        case .invalidResponse:
            return "Invalid server response"
        case .unauthorized:
            return "Authentication required"
        case .notFound:
            return "Resource not found"
        case .serverError(let code):
            return "Server error (\(code))"
        }
    }
}
```

## Models

```swift
import Foundation

// MARK: - Authentication Models

struct LoginRequest: Codable {
    let username: String
    let password: String
}

struct LoginResponse: Codable {
    let accessToken: String
    let refreshToken: String
    let expiresAt: Date
    let user: User
}

struct TokenResponse: Codable {
    let accessToken: String
    let refreshToken: String
    let expiresAt: Date
}

struct User: Codable, Identifiable {
    let id: String
    let username: String
    let email: String
    let role: String
    let lastLogin: Date?
}

// MARK: - Music Models

struct Artist: Codable, Identifiable {
    let id: String
    let name: String
    let sortName: String?
    let albumCount: Int
    let trackCount: Int
}

struct ArtistDetail: Codable {
    let id: String
    let name: String
    let sortName: String?
    let albumCount: Int
    let trackCount: Int
    let albums: [Album]
}

struct Album: Codable, Identifiable {
    let id: String
    let title: String
    let artistId: String
    let artistName: String
    let year: Int?
    let trackCount: Int
    let genre: String?
    let coverArtPath: String?
    let releaseDate: Date?
}

struct AlbumDetail: Codable {
    let id: String
    let title: String
    let artistId: String
    let artistName: String
    let year: Int?
    let trackCount: Int
    let genre: String?
    let coverArtPath: String?
    let releaseDate: Date?
    let totalDurationMs: Int
    let tracks: [Track]
}

struct Track: Codable, Identifiable {
    let id: String
    let title: String
    let artistId: String
    let artistName: String
    let albumId: String?
    let albumTitle: String?
    let trackNumber: Int?
    let discNumber: Int?
    let durationMs: Int
    let genre: String?
    let year: Int?
    let fileSize: Int?
    let bitrate: Int?
    let codec: String?
    let filePath: String?
}

struct TrackDetail: Codable {
    let id: String
    let title: String
    let artistId: String
    let artistName: String
    let albumId: String?
    let albumTitle: String?
    let trackNumber: Int?
    let discNumber: Int?
    let durationMs: Int
    let genre: String?
    let year: Int?
    let fileSize: Int?
    let bitrate: Int?
    let codec: String?
    let sampleRate: Int?
    let channels: Int?
    let filePath: String?
    let fileHash: String?
    let addedDate: Date?
    let modifiedDate: Date?
    let playCount: Int
    let lastPlayedDate: Date?
}

// MARK: - Response Wrappers

struct PagedResponse<T: Codable>: Codable {
    let data: [T]
    let page: Int
    let limit: Int
    let total: Int
    let totalPages: Int
}

struct AlbumsResponse: Codable {
    let data: [Album]
}

struct TracksResponse: Codable {
    let data: [Track]
}

struct SearchResponse: Codable {
    let query: String
    let totalResults: Int
    let artists: [Artist]
    let albums: [Album]
    let tracks: [Track]
}

struct SuggestionsResponse: Codable {
    let suggestions: [SearchSuggestion]
}

struct SearchSuggestion: Codable {
    let value: String
    let type: String
    let id: String
}

struct TrackPlayContext: Codable {
    let track: TrackInfo
    let nextTrackId: String?
    let previousTrackId: String?
    
    struct TrackInfo: Codable {
        let id: String
        let title: String
        let artistName: String
        let albumTitle: String?
        let trackNumber: Int?
        let discNumber: Int?
        let durationMs: Int
        let streamUrl: String
        let genre: String?
        let year: Int?
        let coverArtPath: String?
    }
}

struct AlbumPlayContext: Codable {
    let album: AlbumInfo
    let tracks: [TrackInfo]
    let totalDurationMs: Int
    
    struct AlbumInfo: Codable {
        let id: String
        let title: String
        let artistName: String
        let year: Int?
        let coverArtPath: String?
        let trackCount: Int
    }
    
    struct TrackInfo: Codable {
        let id: String
        let title: String
        let artistName: String
        let trackNumber: Int?
        let discNumber: Int?
        let durationMs: Int
        let streamUrl: String
        let nextTrackId: String?
        let previousTrackId: String?
    }
}

struct PlayCountResponse: Codable {
    let message: String
    let playCount: Int
    let lastPlayedDate: Date
}

// MARK: - Playlist Models

struct Playlist: Codable, Identifiable {
    let id: String
    let name: String
    let description: String?
    let isPublic: Bool
    let userId: String
    let userName: String
    let trackCount: Int
    let createdAt: Date
    let updatedAt: Date
    let imagePath: String?
}

struct PlaylistDetails: Codable {
    let id: String
    let name: String
    let description: String?
    let isPublic: Bool
    let userId: String
    let userName: String
    let trackCount: Int
    let createdAt: Date
    let updatedAt: Date
    let imagePath: String?
    let tracks: [PlaylistTrack]
}

struct PlaylistTrack: Codable, Identifiable {
    let trackId: String
    let title: String
    let artistId: String
    let artistName: String
    let albumId: String
    let albumTitle: String
    let trackNumber: Int?
    let discNumber: Int?
    let durationMs: Int
    let genre: String?
    let year: Int?
    let filePath: String
    let position: Int
    let positionFloat: Double
    let addedAt: Date
    let addedBy: String?
    
    var id: String { trackId }
}

// MARK: - Playlist Request Models

struct CreatePlaylistRequest: Codable {
    let name: String
    let description: String?
    let isPublic: Bool
    let initialTrackIds: [String]?
}

struct UpdatePlaylistRequest: Codable {
    let name: String
    let description: String?
    let isPublic: Bool
}

struct AddTracksRequest: Codable {
    let trackIds: [String]
    let position: Int?
}

struct RemoveTracksRequest: Codable {
    let trackIds: [String]
}

struct ReorderTracksRequest: Codable {
    let tracks: [TrackReorderItem]
}

struct TrackReorderItem: Codable {
    let trackId: String
    let newPosition: Double
}

struct UpdatePlaylistImageRequest: Codable {
    let imagePath: String
}

struct CopyPlaylistRequest: Codable {
    let name: String
    let description: String?
}

// MARK: - Playlist Response Models

struct PlaylistsResponse: Codable {
    let data: [Playlist]
    let page: Int
    let limit: Int
    let total: Int
    let totalPages: Int
}

struct EmptyResponse: Codable {}

// MARK: - Queue Models

struct QueueState: Codable, Identifiable {
    let queueId: String
    let userId: String
    let trackIds: [String]
    let currentTrackId: String?
    let currentIndex: Int
    let repeatMode: RepeatMode
    let isShuffled: Bool
    let totalTracks: Int
    let queueSource: String?
    let lastActivity: Date
    let version: Int
    
    var id: String { queueId }
}

struct QueueItem: Codable, Identifiable {
    let index: Int
    let trackId: String
    let track: Track
    let addedAt: Date
    let source: String?
    
    var id: String { trackId }
}

enum RepeatMode: Int, Codable, CaseIterable {
    case none = 0
    case one = 1
    case all = 2
    
    var title: String {
        switch self {
        case .none: return "Off"
        case .one: return "One"
        case .all: return "All"
        }
    }
    
    var iconName: String {
        switch self {
        case .none: return "repeat"
        case .one: return "repeat.1"
        case .all: return "repeat"
        }
    }
}

// MARK: - Queue Request Models

struct AddToQueueRequest: Codable {
    let trackIds: [String]
    let source: String?
    let playNext: Bool
}

struct UpdateQueueRequest: Codable {
    let repeatMode: RepeatMode?
    let isShuffled: Bool?
    let currentIndex: Int?
}

struct ReorderQueueRequest: Codable {
    let trackId: String
    let newIndex: Int
}

struct ReplaceQueueRequest: Codable {
    let trackIds: [String]
    let startIndex: Int
    let source: String?
}

// MARK: - Library Scanner Models

struct ScanRequest: Codable {
    let libraryPath: String
    let requestId: String
    let requestedAt: Date
    
    init(libraryPath: String, requestId: String = UUID().uuidString, requestedAt: Date = Date()) {
        self.libraryPath = libraryPath
        self.requestId = requestId
        self.requestedAt = requestedAt
    }
}

struct ScanResult: Codable {
    let totalFiles: Int
    let processedFiles: Int
    let newTracks: Int
    let updatedTracks: Int
    let errors: Int
    let errorMessages: [String]
    let startTime: Date
    let endTime: Date
    
    var duration: TimeInterval {
        endTime.timeIntervalSince(startTime)
    }
    
    var formattedDuration: String {
        let formatter = DateComponentsFormatter()
        formatter.allowedUnits = [.hour, .minute, .second]
        formatter.unitsStyle = .abbreviated
        return formatter.string(from: duration) ?? "0s"
    }
}

struct ScanProgress: Codable {
    let processedFiles: Int
    let totalFiles: Int
    let currentFile: String
    
    var percentComplete: Double {
        guard totalFiles > 0 else { return 0 }
        return Double(processedFiles) / Double(totalFiles) * 100
    }
}

struct ScanQueueResponse: Codable {
    let message: String
    let requestId: String
    let path: String
}

struct SupportedFormatsResponse: Codable {
    let formats: [String]
}

// MARK: - Scanner SignalR Events

struct ScanProgressEvent: Codable {
    let processed: Int
    let total: Int
    let message: String
    let percentComplete: Double
}

struct ScanCompleteEvent: Codable {
    let totalFiles: Int
    let newTracks: Int
    let updatedTracks: Int
    let errors: Int
    let completedAt: Date
}

struct ScanErrorEvent: Codable {
    let error: String
}

// MARK: - Data Cleanup Models

struct CleanupResponse: Codable {
    let message: String
    let duplicateGroupsFound: Int
    
    var formattedMessage: String {
        return "✅ \(message)"
    }
}

struct ArtistCleanupResponse: Codable {
    let message: String
    let duplicateGroupsFound: Int
}

struct AlbumCleanupResponse: Codable {
    let message: String
    let duplicateGroupsFound: Int
}

struct ComprehensiveCleanupResponse: Codable {
    let message: String
    let artistsMerged: Int
    let albumsMerged: Int
    let duplicateArtistGroupsFound: Int
    let duplicateAlbumGroupsFound: Int
    
    var totalMerged: Int {
        artistsMerged + albumsMerged
    }
    
    var summary: String {
        "Merged \(totalMerged) items: \(artistsMerged) artists, \(albumsMerged) albums"
    }
}

enum CleanupOperation: String, CaseIterable {
    case artists = "Artists"
    case albums = "Albums"
    case all = "All Data"
    
    var description: String {
        switch self {
        case .artists:
            return "Merge duplicate artists by name"
        case .albums:
            return "Merge duplicate albums by title and artist"
        case .all:
            return "Comprehensive cleanup of all data types"
        }
    }
    
    var iconName: String {
        switch self {
        case .artists:
            return "person.2.fill"
        case .albums:
            return "opticaldisc"
        case .all:
            return "sparkles"
        }
    }
}
```

## Playlist Management

The Audiarr API provides comprehensive playlist management capabilities. Here's how to implement playlist functionality in your iOS app:

### Playlist Manager

```swift
import Foundation

@MainActor
class PlaylistManager: ObservableObject {
    static let shared = PlaylistManager()
    
    @Published var playlists: [Playlist] = []
    @Published var isLoading = false
    @Published var errorMessage: String?
    
    private let apiClient = AudiarrAPIClient.shared
    private var currentPage = 1
    private let pageSize = 50
    private var hasMorePages = true
    
    // MARK: - Playlist CRUD
    
    func loadPlaylists(includePublic: Bool = false) async {
        guard !isLoading else { return }
        
        isLoading = true
        errorMessage = nil
        
        do {
            let response = try await apiClient.getPlaylists(
                page: currentPage,
                limit: pageSize,
                includePublic: includePublic
            )
            
            if currentPage == 1 {
                playlists = response.data
            } else {
                playlists.append(contentsOf: response.data)
            }
            
            hasMorePages = currentPage < response.totalPages
            currentPage += 1
        } catch {
            errorMessage = error.localizedDescription
        }
        
        isLoading = false
    }
    
    func createPlaylist(name: String, description: String? = nil, isPublic: Bool = false, initialTrackIds: [String]? = nil) async throws -> Playlist {
        let request = CreatePlaylistRequest(
            name: name,
            description: description,
            isPublic: isPublic,
            initialTrackIds: initialTrackIds
        )
        
        let newPlaylist = try await apiClient.createPlaylist(request)
        playlists.insert(newPlaylist, at: 0)
        return newPlaylist
    }
    
    func updatePlaylist(_ playlist: Playlist, name: String, description: String?, isPublic: Bool) async throws {
        let request = UpdatePlaylistRequest(
            name: name,
            description: description,
            isPublic: isPublic
        )
        
        let updatedPlaylist = try await apiClient.updatePlaylist(id: playlist.id, request)
        
        if let index = playlists.firstIndex(where: { $0.id == playlist.id }) {
            playlists[index] = updatedPlaylist
        }
    }
    
    func deletePlaylist(_ playlist: Playlist) async throws {
        try await apiClient.deletePlaylist(id: playlist.id)
        playlists.removeAll { $0.id == playlist.id }
    }
    
    func copyPlaylist(_ playlist: Playlist, newName: String, description: String? = nil) async throws -> Playlist {
        let request = CopyPlaylistRequest(name: newName, description: description)
        let copiedPlaylist = try await apiClient.copyPlaylist(playlistId: playlist.id, request)
        playlists.insert(copiedPlaylist, at: 0)
        return copiedPlaylist
    }
    
    // MARK: - Track Management
    
    func addTracks(to playlistId: String, trackIds: [String], at position: Int? = nil) async throws {
        let request = AddTracksRequest(trackIds: trackIds, position: position)
        try await apiClient.addTracksToPlaylist(playlistId: playlistId, request)
        
        // Update local playlist track count
        if let index = playlists.firstIndex(where: { $0.id == playlistId }) {
            var updatedPlaylist = playlists[index]
            updatedPlaylist = Playlist(
                id: updatedPlaylist.id,
                name: updatedPlaylist.name,
                description: updatedPlaylist.description,
                isPublic: updatedPlaylist.isPublic,
                userId: updatedPlaylist.userId,
                userName: updatedPlaylist.userName,
                trackCount: updatedPlaylist.trackCount + trackIds.count,
                createdAt: updatedPlaylist.createdAt,
                updatedAt: Date(),
                imagePath: updatedPlaylist.imagePath
            )
            playlists[index] = updatedPlaylist
        }
    }
    
    func removeTracks(from playlistId: String, trackIds: [String]) async throws {
        let request = RemoveTracksRequest(trackIds: trackIds)
        try await apiClient.removeTracksFromPlaylist(playlistId: playlistId, request)
        
        // Update local playlist track count
        if let index = playlists.firstIndex(where: { $0.id == playlistId }) {
            var updatedPlaylist = playlists[index]
            updatedPlaylist = Playlist(
                id: updatedPlaylist.id,
                name: updatedPlaylist.name,
                description: updatedPlaylist.description,
                isPublic: updatedPlaylist.isPublic,
                userId: updatedPlaylist.userId,
                userName: updatedPlaylist.userName,
                trackCount: max(0, updatedPlaylist.trackCount - trackIds.count),
                createdAt: updatedPlaylist.createdAt,
                updatedAt: Date(),
                imagePath: updatedPlaylist.imagePath
            )
            playlists[index] = updatedPlaylist
        }
    }
    
    func reorderTracks(in playlistId: String, reorderItems: [TrackReorderItem]) async throws {
        let request = ReorderTracksRequest(tracks: reorderItems)
        try await apiClient.reorderPlaylistTracks(playlistId: playlistId, request)
    }
    
    // MARK: - Utilities
    
    func refresh() async {
        currentPage = 1
        hasMorePages = true
        await loadPlaylists()
    }
    
    func loadMoreIfNeeded() async {
        guard hasMorePages && !isLoading else { return }
        await loadPlaylists()
    }
}
```

### Playlist Detail View Model

```swift
@MainActor
class PlaylistDetailViewModel: ObservableObject {
    @Published var playlist: PlaylistDetails?
    @Published var isLoading = false
    @Published var errorMessage: String?
    
    private let apiClient = AudiarrAPIClient.shared
    private let playlistId: String
    
    init(playlistId: String) {
        self.playlistId = playlistId
    }
    
    func loadPlaylist() async {
        isLoading = true
        errorMessage = nil
        
        do {
            playlist = try await apiClient.getPlaylist(id: playlistId)
        } catch {
            errorMessage = error.localizedDescription
        }
        
        isLoading = false
    }
    
    func addTracks(_ trackIds: [String], at position: Int? = nil) async throws {
        try await PlaylistManager.shared.addTracks(to: playlistId, trackIds: trackIds, at: position)
        await loadPlaylist() // Refresh to get updated track list
    }
    
    func removeTracks(_ trackIds: [String]) async throws {
        try await PlaylistManager.shared.removeTracks(from: playlistId, trackIds: trackIds)
        await loadPlaylist() // Refresh to get updated track list
    }
    
    func reorderTracks(from sourceIndex: Int, to destinationIndex: Int) async throws {
        guard let playlist = playlist,
              sourceIndex < playlist.tracks.count,
              destinationIndex < playlist.tracks.count else { return }
        
        let track = playlist.tracks[sourceIndex]
        
        // Calculate new position using decimal positioning
        let newPosition: Double
        if destinationIndex == 0 {
            // Moving to first position
            newPosition = playlist.tracks[0].positionFloat / 2.0
        } else if destinationIndex == playlist.tracks.count - 1 {
            // Moving to last position
            newPosition = playlist.tracks.last!.positionFloat + 1.0
        } else {
            // Moving between tracks
            let prevTrack = playlist.tracks[destinationIndex - 1]
            let nextTrack = playlist.tracks[destinationIndex]
            newPosition = (prevTrack.positionFloat + nextTrack.positionFloat) / 2.0
        }
        
        let reorderItem = TrackReorderItem(trackId: track.trackId, newPosition: newPosition)
        try await PlaylistManager.shared.reorderTracks(in: playlistId, reorderItems: [reorderItem])
        await loadPlaylist() // Refresh to get updated order
    }
    
    // MARK: - Playback Context
    
    func getPlaybackContext() async throws -> PlaylistPlayContext {
        return try await apiClient.getPlaylistPlayContext(id: playlistId)
    }
    
    func startPlayback(startFromIndex: Int = 0) async throws {
        let playContext = try await getPlaybackContext()
        
        guard startFromIndex < playContext.tracks.count else {
            throw PlaybackError.invalidTrackIndex
        }
        
        // Initialize playback with the playlist context
        await AudioPlayer.shared.startPlaylistPlayback(
            context: playContext,
            startIndex: startFromIndex
        )
    }
}
```

### Playlist Playback Context

Add this method to your `AudiarrAPIClient` class to support playlist playback:

```swift
extension AudiarrAPIClient {
    func getPlaylistPlayContext(id: String) async throws -> PlaylistPlayContext {
        let token = try await authManager.getAccessToken()
        
        let url = baseURL.appendingPathComponent("api/v2/playlists/\(id)/play")
        var request = URLRequest(url: url)
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        
        let (data, response) = try await URLSession.shared.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse else {
            throw APIError.invalidResponse
        }
        
        switch httpResponse.statusCode {
        case 200:
            return try JSONDecoder().decode(PlaylistPlayContext.self, from: data)
        case 401:
            throw APIError.unauthorized
        case 404:
            throw APIError.playlistNotFound
        default:
            throw APIError.invalidResponse
        }
    }
}
```

### Enhanced Audio Player for Playlist Playback

Add playlist support to your `AudioPlayer` class:

```swift
extension AudioPlayer {
    private var currentPlaylistContext: PlaylistPlayContext?
    private var currentTrackIndex: Int = 0
    
    @MainActor
    func startPlaylistPlayback(context: PlaylistPlayContext, startIndex: Int = 0) async {
        self.currentPlaylistContext = context
        self.currentTrackIndex = startIndex
        
        guard startIndex < context.tracks.count else { return }
        
        let track = context.tracks[startIndex]
        await playTrack(id: track.id, streamUrl: track.streamUrl)
        
        // Update now playing info
        updateNowPlayingInfo(
            title: track.title,
            artist: track.artistName,
            album: track.albumTitle,
            duration: TimeInterval(track.durationMs) / 1000.0
        )
    }
    
    @MainActor
    func playNextTrack() async {
        guard let context = currentPlaylistContext,
              currentTrackIndex < context.tracks.count - 1 else { return }
        
        currentTrackIndex += 1
        let nextTrack = context.tracks[currentTrackIndex]
        
        await playTrack(id: nextTrack.id, streamUrl: nextTrack.streamUrl)
        updateNowPlayingInfo(
            title: nextTrack.title,
            artist: nextTrack.artistName,
            album: nextTrack.albumTitle,
            duration: TimeInterval(nextTrack.durationMs) / 1000.0
        )
    }
    
    @MainActor
    func playPreviousTrack() async {
        guard let context = currentPlaylistContext,
              currentTrackIndex > 0 else { return }
        
        currentTrackIndex -= 1
        let previousTrack = context.tracks[currentTrackIndex]
        
        await playTrack(id: previousTrack.id, streamUrl: previousTrack.streamUrl)
        updateNowPlayingInfo(
            title: previousTrack.title,
            artist: previousTrack.artistName,
            album: previousTrack.albumTitle,
            duration: TimeInterval(previousTrack.durationMs) / 1000.0
        )
    }
    
    private func playTrack(id: String, streamUrl: String) async {
        let url = URL(string: streamUrl)!
        let playerItem = AVPlayerItem(url: url)
        
        await MainActor.run {
            self.playerItem = playerItem
            
            if self.player == nil {
                self.player = AVPlayer(playerItem: playerItem)
                self.addTimeObserver()
            } else {
                self.player?.replaceCurrentItem(with: playerItem)
            }
            
            self.player?.play()
            self.isPlaying = true
        }
    }
    
    private func updateNowPlayingInfo(title: String, artist: String, album: String?, duration: TimeInterval) {
        var nowPlayingInfo = [String: Any]()
        nowPlayingInfo[MPMediaItemPropertyTitle] = title
        nowPlayingInfo[MPMediaItemPropertyArtist] = artist
        if let album = album {
            nowPlayingInfo[MPMediaItemPropertyAlbumTitle] = album
        }
        nowPlayingInfo[MPMediaItemPropertyPlaybackDuration] = duration
        nowPlayingInfo[MPNowPlayingInfoPropertyElapsedPlaybackTime] = currentTime
        
        MPNowPlayingInfoCenter.default().nowPlayingInfo = nowPlayingInfo
    }
}
```

### Usage Examples

#### Creating a Playlist
```swift
// Create a new playlist
Task {
    do {
        let playlist = try await PlaylistManager.shared.createPlaylist(
            name: "My Favorites",
            description: "My favorite tracks",
            isPublic: false,
            initialTrackIds: ["track1", "track2", "track3"]
        )
        print("Created playlist: \(playlist.name)")
    } catch {
        print("Failed to create playlist: \(error)")
    }
}
```

#### Adding Tracks to a Playlist
```swift
// Add tracks to an existing playlist
Task {
    do {
        try await PlaylistManager.shared.addTracks(
            to: playlistId,
            trackIds: ["track4", "track5"],
            at: 2 // Insert at position 2
        )
        print("Added tracks to playlist")
    } catch {
        print("Failed to add tracks: \(error)")
    }
}
```

#### Reordering Tracks
```swift
// Reorder tracks using decimal positioning
Task {
    do {
        let reorderItems = [
            TrackReorderItem(trackId: "track1", newPosition: 1.5),
            TrackReorderItem(trackId: "track2", newPosition: 0.5)
        ]
        
        try await PlaylistManager.shared.reorderTracks(
            in: playlistId,
            reorderItems: reorderItems
        )
        print("Reordered tracks")
    } catch {
        print("Failed to reorder tracks: \(error)")
    }
}
```

#### Copying a Playlist
```swift
// Copy an existing playlist
Task {
    do {
        let copiedPlaylist = try await PlaylistManager.shared.copyPlaylist(
            playlist,
            newName: "Copy of \(playlist.name)",
            description: "Copy created on \(Date())"
        )
        print("Copied playlist: \(copiedPlaylist.name)")
    } catch {
        print("Failed to copy playlist: \(error)")
    }
}
```

## Queue Management

The Audiarr API provides comprehensive queue management for controlling playback order, repeat modes, and shuffle. Here's how to implement queue functionality in your iOS app:

### Queue Manager

```swift
import Foundation

@MainActor
class QueueManager: ObservableObject {
    static let shared = QueueManager()
    
    @Published var queueState: QueueState?
    @Published var isLoading = false
    @Published var errorMessage: String?
    
    private let apiClient = AudiarrAPIClient.shared
    
    // MARK: - Queue State
    
    func loadQueueState() async {
        isLoading = true
        errorMessage = nil
        
        do {
            queueState = try await apiClient.getQueueState()
        } catch {
            errorMessage = error.localizedDescription
        }
        
        isLoading = false
    }
    
    func addTracks(_ trackIds: [String], source: String? = nil, playNext: Bool = false) async throws {
        let request = AddToQueueRequest(trackIds: trackIds, source: source, playNext: playNext)
        queueState = try await apiClient.addTracksToQueue(request)
    }
    
    func removeTrack(at index: Int) async throws {
        queueState = try await apiClient.removeTrackFromQueue(at: index)
    }
    
    func clearQueue(keepCurrentTrack: Bool = false) async throws {
        queueState = try await apiClient.clearQueue(keepCurrentTrack: keepCurrentTrack)
    }
    
    func reorderTrack(trackId: String, to newIndex: Int) async throws {
        let request = ReorderQueueRequest(trackId: trackId, newIndex: newIndex)
        queueState = try await apiClient.reorderQueue(request)
    }
    
    func replaceQueue(with trackIds: [String], startIndex: Int = 0, source: String? = nil) async throws {
        let request = ReplaceQueueRequest(trackIds: trackIds, startIndex: startIndex, source: source)
        queueState = try await apiClient.replaceQueue(request)
    }
    
    // MARK: - Playback Control
    
    func nextTrack() async throws {
        queueState = try await apiClient.nextTrack()
    }
    
    func previousTrack() async throws {
        queueState = try await apiClient.previousTrack()
    }
    
    func jumpToPosition(_ index: Int) async throws {
        queueState = try await apiClient.jumpToPosition(index)
    }
    
    // MARK: - Settings
    
    func setRepeatMode(_ mode: RepeatMode) async throws {
        let request = UpdateQueueRequest(repeatMode: mode, isShuffled: nil, currentIndex: nil)
        queueState = try await apiClient.updateQueueSettings(request)
    }
    
    func toggleShuffle() async throws {
        guard let currentState = queueState else { return }
        let request = UpdateQueueRequest(repeatMode: nil, isShuffled: !currentState.isShuffled, currentIndex: nil)
        queueState = try await apiClient.updateQueueSettings(request)
    }
    
    func setCurrentIndex(_ index: Int) async throws {
        let request = UpdateQueueRequest(repeatMode: nil, isShuffled: nil, currentIndex: index)
        queueState = try await apiClient.updateQueueSettings(request)
    }
    
    // MARK: - Utilities
    
    func refresh() async {
        await loadQueueState()
    }
    
    var currentTrack: Track? {
        guard let state = queueState,
              let currentTrackId = state.currentTrackId else { return nil }
        // You would need to fetch the track details here
        // This is a simplified example
        return nil
    }
    
    var canPlayNext: Bool {
        guard let state = queueState else { return false }
        return state.currentIndex < state.totalTracks - 1 || state.repeatMode != .none
    }
    
    var canPlayPrevious: Bool {
        guard let state = queueState else { return false }
        return state.currentIndex > 0 || state.repeatMode != .none
    }
}
```

### Enhanced Audio Player with Queue Integration

```swift
import AVFoundation
import MediaPlayer
import Combine

class EnhancedAudioPlayer: ObservableObject {
    static let shared = EnhancedAudioPlayer()
    
    private var player: AVPlayer?
    private var playerItem: AVPlayerItem?
    private var timeObserver: Any?
    
    @Published var isPlaying = false
    @Published var currentTime: TimeInterval = 0
    @Published var duration: TimeInterval = 0
    @Published var isBuffering = false
    
    private let queueManager = QueueManager.shared
    private var cancellables = Set<AnyCancellable>()
    
    override init() {
        super.init()
        setupAudioSession()
        setupRemoteControls()
        setupQueueObserver()
    }
    
    private func setupQueueObserver() {
        queueManager.$queueState
            .sink { [weak self] queueState in
                if let currentTrackId = queueState?.currentTrackId {
                    // Load and play the current track
                    Task {
                        await self?.loadCurrentTrack(trackId: currentTrackId)
                    }
                }
            }
            .store(in: &cancellables)
    }
    
    private func loadCurrentTrack(trackId: String) async {
        let url = AudiarrAPIClient.shared.getStreamURL(for: trackId)
        
        await MainActor.run {
            self.playerItem = AVPlayerItem(url: url)
            self.player = AVPlayer(playerItem: self.playerItem)
            
            // Add time observer
            let interval = CMTime(seconds: 1, preferredTimescale: 1)
            self.timeObserver = self.player?.addPeriodicTimeObserver(forInterval: interval, queue: .main) { [weak self] time in
                self?.currentTime = time.seconds
            }
            
            self.player?.play()
            self.isPlaying = true
        }
    }
    
    private func setupRemoteControls() {
        let commandCenter = MPRemoteCommandCenter.shared()
        
        commandCenter.nextTrackCommand.addTarget { [weak self] _ in
            Task {
                try? await self?.queueManager.nextTrack()
            }
            return .success
        }
        
        commandCenter.previousTrackCommand.addTarget { [weak self] _ in
            Task {
                try? await self?.queueManager.previousTrack()
            }
            return .success
        }
        
        commandCenter.playCommand.addTarget { [weak self] _ in
            self?.play()
            return .success
        }
        
        commandCenter.pauseCommand.addTarget { [weak self] _ in
            self?.pause()
            return .success
        }
    }
    
    func play() {
        player?.play()
        isPlaying = true
    }
    
    func pause() {
        player?.pause()
        isPlaying = false
    }
    
    func playNext() {
        Task {
            try? await queueManager.nextTrack()
        }
    }
    
    func playPrevious() {
        Task {
            try? await queueManager.previousTrack()
        }
    }
    
    private func setupAudioSession() {
        do {
            try AVAudioSession.sharedInstance().setCategory(.playback, mode: .default)
            try AVAudioSession.sharedInstance().setActive(true)
        } catch {
            print("Failed to setup audio session: \(error)")
        }
    }
    
    deinit {
        if let observer = timeObserver {
            player?.removeTimeObserver(observer)
        }
    }
}
```

### Queue View Implementation

```swift
import SwiftUI

struct QueueView: View {
    @StateObject private var queueManager = QueueManager.shared
    @StateObject private var player = EnhancedAudioPlayer.shared
    
    var body: some View {
        NavigationView {
            VStack {
                if let queueState = queueManager.queueState {
                    QueueControlsView(queueState: queueState)
                    
                    List {
                        ForEach(Array(queueState.trackIds.enumerated()), id: \.offset) { index, trackId in
                            QueueTrackRow(
                                trackId: trackId,
                                index: index,
                                isCurrentTrack: index == queueState.currentIndex,
                                onTap: {
                                    Task {
                                        try? await queueManager.jumpToPosition(index)
                                    }
                                },
                                onRemove: {
                                    Task {
                                        try? await queueManager.removeTrack(at: index)
                                    }
                                }
                            )
                        }
                        .onMove(perform: moveTrack)
                    }
                } else if queueManager.isLoading {
                    ProgressView("Loading queue...")
                } else {
                    ContentUnavailableView(
                        "No Queue",
                        systemImage: "music.note.list",
                        description: Text("Add tracks to get started")
                    )
                }
            }
            .navigationTitle("Queue")
            .task {
                await queueManager.loadQueueState()
            }
            .refreshable {
                await queueManager.refresh()
            }
        }
    }
    
    private func moveTrack(from source: IndexSet, to destination: Int) {
        guard let queueState = queueManager.queueState,
              let sourceIndex = source.first,
              sourceIndex < queueState.trackIds.count else { return }
        
        let trackId = queueState.trackIds[sourceIndex]
        let newIndex = destination > sourceIndex ? destination - 1 : destination
        
        Task {
            try? await queueManager.reorderTrack(trackId: trackId, to: newIndex)
        }
    }
}

struct QueueControlsView: View {
    let queueState: QueueState
    @StateObject private var queueManager = QueueManager.shared
    
    var body: some View {
        VStack(spacing: 16) {
            HStack {
                Button(action: { toggleShuffle() }) {
                    Image(systemName: "shuffle")
                        .foregroundColor(queueState.isShuffled ? .blue : .gray)
                }
                
                Spacer()
                
                Button(action: { previousTrack() }) {
                    Image(systemName: "backward.fill")
                }
                .disabled(!queueManager.canPlayPrevious)
                
                Button(action: { nextTrack() }) {
                    Image(systemName: "forward.fill")
                }
                .disabled(!queueManager.canPlayNext)
                
                Spacer()
                
                Button(action: { cycleRepeatMode() }) {
                    Image(systemName: queueState.repeatMode.iconName)
                        .foregroundColor(queueState.repeatMode == .none ? .gray : .blue)
                }
            }
            
            HStack {
                Text("\(queueState.currentIndex + 1) of \(queueState.totalTracks)")
                    .font(.caption)
                    .foregroundColor(.secondary)
                
                Spacer()
                
                if let source = queueState.queueSource {
                    Text(source)
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
            }
        }
        .padding()
        .background(Color(.systemGray6))
        .cornerRadius(12)
        .padding(.horizontal)
    }
    
    private func toggleShuffle() {
        Task {
            try? await queueManager.toggleShuffle()
        }
    }
    
    private func cycleRepeatMode() {
        let nextMode: RepeatMode
        switch queueState.repeatMode {
        case .none: nextMode = .all
        case .all: nextMode = .one
        case .one: nextMode = .none
        }
        
        Task {
            try? await queueManager.setRepeatMode(nextMode)
        }
    }
    
    private func previousTrack() {
        Task {
            try? await queueManager.previousTrack()
        }
    }
    
    private func nextTrack() {
        Task {
            try? await queueManager.nextTrack()
        }
    }
}

struct QueueTrackRow: View {
    let trackId: String
    let index: Int
    let isCurrentTrack: Bool
    let onTap: () -> Void
    let onRemove: () -> Void
    
    // You would fetch track details here
    // This is simplified for the example
    
    var body: some View {
        HStack {
            Text("\(index + 1)")
                .font(.caption)
                .foregroundColor(.secondary)
                .frame(width: 20)
            
            VStack(alignment: .leading) {
                Text("Track \(index + 1)")
                    .font(.headline)
                    .foregroundColor(isCurrentTrack ? .blue : .primary)
                
                Text("Artist Name")
                    .font(.caption)
                    .foregroundColor(.secondary)
            }
            
            Spacer()
            
            if isCurrentTrack {
                Image(systemName: "speaker.wave.2")
                    .foregroundColor(.blue)
                    .font(.caption)
            }
        }
        .contentShape(Rectangle())
        .onTapGesture {
            onTap()
        }
        .contextMenu {
            Button("Remove from Queue") {
                onRemove()
            }
        }
    }
}
```

### Usage Examples

#### Adding Tracks to Queue
```swift
// Add tracks to end of queue
Task {
    try await QueueManager.shared.addTracks(
        ["track1", "track2"],
        source: "album:jazz_classics"
    )
}

// Add tracks to play next
Task {
    try await QueueManager.shared.addTracks(
        ["urgent_track"],
        playNext: true
    )
}
```

#### Managing Queue Settings
```swift
// Toggle shuffle
Task {
    try await QueueManager.shared.toggleShuffle()
}

// Set repeat mode
Task {
    try await QueueManager.shared.setRepeatMode(.all)
}

// Jump to specific track
Task {
    try await QueueManager.shared.jumpToPosition(5)
}
```

#### Replacing Entire Queue
```swift
// Replace queue with album tracks
Task {
    try await QueueManager.shared.replaceQueue(
        with: albumTrackIds,
        startIndex: 0,
        source: "album:\(albumId)"
    )
}
```

## Library Scanner

The Library Scanner allows you to scan and index audio files in your music library. This section shows how to implement scanner functionality with real-time progress monitoring via SignalR.

### Scanner Manager

```swift
import Foundation
import Combine

@MainActor
class ScannerManager: ObservableObject {
    static let shared = ScannerManager()
    
    @Published var isScanning = false
    @Published var scanProgress: ScanProgress?
    @Published var scanResult: ScanResult?
    @Published var errorMessage: String?
    @Published var supportedFormats: [String] = []
    
    private let apiClient = AudiarrAPIClient.shared
    private var signalRManager: ScannerSignalRManager?
    private var cancellables = Set<AnyCancellable>()
    
    init() {
        setupSignalRManager()
    }
    
    private func setupSignalRManager() {
        signalRManager = ScannerSignalRManager()
        
        // Subscribe to SignalR events
        signalRManager?.$scanProgress
            .assign(to: &$scanProgress)
        
        signalRManager?.$scanCompleted
            .sink { [weak self] result in
                if let result = result {
                    self?.scanResult = result
                    self?.isScanning = false
                }
            }
            .store(in: &cancellables)
        
        signalRManager?.$scanError
            .sink { [weak self] error in
                if let error = error {
                    self?.errorMessage = error
                    self?.isScanning = false
                }
            }
            .store(in: &cancellables)
    }
    
    // MARK: - Scanner Operations
    
    func loadSupportedFormats() async {
        do {
            let response = try await apiClient.getSupportedFormats()
            supportedFormats = response.formats
        } catch {
            errorMessage = "Failed to load supported formats: \(error.localizedDescription)"
        }
    }
    
    func startLibraryScan(libraryPath: String) async throws {
        guard !isScanning else {
            throw ScannerError.scanInProgress
        }
        
        // Connect to SignalR before starting scan
        await signalRManager?.connect()
        
        let request = ScanRequest(libraryPath: libraryPath)
        
        do {
            let response = try await apiClient.queueLibraryScan(request)
            isScanning = true
            scanProgress = nil
            scanResult = nil
            errorMessage = nil
            
            print("Scan queued: \(response.message)")
        } catch {
            await signalRManager?.disconnect()
            throw error
        }
    }
    
    func scanSingleFile(filePath: String) async throws -> ScanResult {
        // Validate file format first
        guard await isValidAudioFile(filePath) else {
            throw ScannerError.unsupportedFormat
        }
        
        return try await apiClient.scanSingleFile(filePath: filePath)
    }
    
    func isValidAudioFile(_ filePath: String) async -> Bool {
        if supportedFormats.isEmpty {
            await loadSupportedFormats()
        }
        
        return await apiClient.isAudioFile(filePath)
    }
    
    func stopScanning() async {
        isScanning = false
        await signalRManager?.disconnect()
    }
    
    // MARK: - Utility Methods
    
    func getFormattedFileSize(bytes: Int64) -> String {
        let formatter = ByteCountFormatter()
        formatter.allowedUnits = [.useKB, .useMB, .useGB]
        formatter.countStyle = .file
        return formatter.string(fromByteCount: bytes)
    }
    
    func validateLibraryPath(_ path: String) -> Bool {
        let fileManager = FileManager.default
        var isDirectory: ObjCBool = false
        
        return fileManager.fileExists(atPath: path, isDirectory: &isDirectory) && isDirectory.boolValue
    }
}

enum ScannerError: LocalizedError {
    case scanInProgress
    case unsupportedFormat
    case invalidPath
    
    var errorDescription: String? {
        switch self {
        case .scanInProgress:
            return "A scan is already in progress"
        case .unsupportedFormat:
            return "Unsupported audio format"
        case .invalidPath:
            return "Invalid library path"
        }
    }
}
```

### SignalR Scanner Manager

```swift
import Foundation
import SignalRClient

@MainActor
class ScannerSignalRManager: ObservableObject {
    private var hubConnection: HubConnection?
    
    @Published var isConnected = false
    @Published var scanProgress: ScanProgress?
    @Published var scanCompleted: ScanResult?
    @Published var scanError: String?
    
    private let hubURL = "http://your-server:8080/hubs/scan"
    
    func connect() async {
        guard hubConnection == nil else { return }
        
        hubConnection = HubConnectionBuilder(url: URL(string: hubURL)!)
            .withLogging(minLogLevel: .info)
            .withAutoReconnect()
            .withHubConnectionDelegate(self)
            .build()
        
        setupEventHandlers()
        
        do {
            try await hubConnection?.start()
        } catch {
            print("Failed to connect to SignalR: \(error)")
        }
    }
    
    func disconnect() async {
        await hubConnection?.stop()
        hubConnection = nil
        isConnected = false
    }
    
    private func setupEventHandlers() {
        // Listen for scan progress events
        hubConnection?.on(method: "ScanProgress") { [weak self] (event: ScanProgressEvent) in
            Task { @MainActor in
                self?.scanProgress = ScanProgress(
                    processedFiles: event.processed,
                    totalFiles: event.total,
                    currentFile: event.message
                )
            }
        }
        
        // Listen for scan completion events
        hubConnection?.on(method: "ScanComplete") { [weak self] (event: ScanCompleteEvent) in
            Task { @MainActor in
                self?.scanCompleted = ScanResult(
                    totalFiles: event.totalFiles,
                    processedFiles: event.totalFiles - event.errors,
                    newTracks: event.newTracks,
                    updatedTracks: event.updatedTracks,
                    errors: event.errors,
                    errorMessages: [],
                    startTime: event.completedAt.addingTimeInterval(-3600), // Approximate
                    endTime: event.completedAt
                )
            }
        }
        
        // Listen for scan error events
        hubConnection?.on(method: "ScanError") { [weak self] (error: String) in
            Task { @MainActor in
                self?.scanError = error
            }
        }
    }
}

extension ScannerSignalRManager: HubConnectionDelegate {
    func connectionDidOpen(hubConnection: HubConnection) {
        print("Scanner SignalR connected")
        isConnected = true
    }
    
    func connectionDidFailToOpen(error: Error) {
        print("Scanner SignalR failed to connect: \(error)")
        isConnected = false
    }
    
    func connectionDidClose(error: Error?) {
        print("Scanner SignalR disconnected")
        isConnected = false
    }
}
```

### Scanner UI Implementation

```swift
import SwiftUI
import UniformTypeIdentifiers

struct LibraryScannerView: View {
    @StateObject private var scannerManager = ScannerManager.shared
    @State private var selectedLibraryPath = ""
    @State private var showingFilePicker = false
    @State private var showingScanResult = false
    
    var body: some View {
        NavigationView {
            VStack(spacing: 20) {
                // Library Path Selection
                VStack(alignment: .leading, spacing: 8) {
                    Text("Library Path")
                        .font(.headline)
                    
                    HStack {
                        TextField("Select music library folder", text: $selectedLibraryPath)
                            .textFieldStyle(RoundedBorderTextFieldStyle())
                        
                        Button("Browse") {
                            showingFilePicker = true
                        }
                        .buttonStyle(.bordered)
                    }
                }
                
                // Supported Formats
                if !scannerManager.supportedFormats.isEmpty {
                    VStack(alignment: .leading, spacing: 8) {
                        Text("Supported Formats")
                            .font(.headline)
                        
                        LazyVGrid(columns: Array(repeating: GridItem(.flexible()), count: 4), spacing: 8) {
                            ForEach(scannerManager.supportedFormats, id: \.self) { format in
                                Text(format)
                                    .font(.caption)
                                    .padding(.horizontal, 8)
                                    .padding(.vertical, 4)
                                    .background(Color.blue.opacity(0.1))
                                    .foregroundColor(.blue)
                                    .cornerRadius(4)
                            }
                        }
                    }
                }
                
                // Scan Progress
                if scannerManager.isScanning {
                    ScanProgressView(progress: scannerManager.scanProgress)
                } else {
                    // Scan Controls
                    VStack(spacing: 12) {
                        Button("Start Library Scan") {
                            startLibraryScan()
                        }
                        .buttonStyle(.borderedProminent)
                        .disabled(selectedLibraryPath.isEmpty || scannerManager.isScanning)
                        
                        Button("Scan Single File") {
                            // Implement single file picker
                        }
                        .buttonStyle(.bordered)
                        .disabled(scannerManager.isScanning)
                    }
                }
                
                // Error Display
                if let error = scannerManager.errorMessage {
                    Text(error)
                        .foregroundColor(.red)
                        .padding()
                        .background(Color.red.opacity(0.1))
                        .cornerRadius(8)
                }
                
                Spacer()
            }
            .padding()
            .navigationTitle("Library Scanner")
            .task {
                await scannerManager.loadSupportedFormats()
            }
            .sheet(isPresented: $showingScanResult) {
                if let result = scannerManager.scanResult {
                    ScanResultView(result: result)
                }
            }
            .fileImporter(
                isPresented: $showingFilePicker,
                allowedContentTypes: [.folder],
                allowsMultipleSelection: false
            ) { result in
                switch result {
                case .success(let urls):
                    if let url = urls.first {
                        selectedLibraryPath = url.path
                    }
                case .failure(let error):
                    scannerManager.errorMessage = "Failed to select folder: \(error.localizedDescription)"
                }
            }
        }
    }
    
    private func startLibraryScan() {
        guard scannerManager.validateLibraryPath(selectedLibraryPath) else {
            scannerManager.errorMessage = "Invalid library path"
            return
        }
        
        Task {
            do {
                try await scannerManager.startLibraryScan(libraryPath: selectedLibraryPath)
            } catch {
                scannerManager.errorMessage = error.localizedDescription
            }
        }
    }
}

struct ScanProgressView: View {
    let progress: ScanProgress?
    
    var body: some View {
        VStack(spacing: 16) {
            Text("Scanning Library...")
                .font(.headline)
            
            if let progress = progress {
                VStack(spacing: 8) {
                    ProgressView(value: progress.percentComplete, total: 100)
                        .progressViewStyle(LinearProgressViewStyle())
                    
                    HStack {
                        Text("\(progress.processedFiles) / \(progress.totalFiles)")
                            .font(.caption)
                        
                        Spacer()
                        
                        Text("\(Int(progress.percentComplete))%")
                            .font(.caption)
                            .fontWeight(.medium)
                    }
                    
                    if !progress.currentFile.isEmpty {
                        Text("Processing: \(URL(fileURLWithPath: progress.currentFile).lastPathComponent)")
                            .font(.caption)
                            .foregroundColor(.secondary)
                            .lineLimit(1)
                    }
                }
            } else {
                ProgressView()
                    .progressViewStyle(CircularProgressViewStyle())
            }
        }
        .padding()
        .background(Color(.systemGray6))
        .cornerRadius(12)
    }
}

struct ScanResultView: View {
    let result: ScanResult
    @Environment(\.dismiss) private var dismiss
    
    var body: some View {
        NavigationView {
            VStack(spacing: 20) {
                // Summary Cards
                LazyVGrid(columns: Array(repeating: GridItem(.flexible()), count: 2), spacing: 16) {
                    ResultCard(title: "Total Files", value: "\(result.totalFiles)", color: .blue)
                    ResultCard(title: "New Tracks", value: "\(result.newTracks)", color: .green)
                    ResultCard(title: "Updated", value: "\(result.updatedTracks)", color: .orange)
                    ResultCard(title: "Errors", value: "\(result.errors)", color: .red)
                }
                
                // Duration
                VStack {
                    Text("Scan Duration")
                        .font(.headline)
                    Text(result.formattedDuration)
                        .font(.title2)
                        .fontWeight(.medium)
                }
                
                // Error Messages
                if !result.errorMessages.isEmpty {
                    VStack(alignment: .leading, spacing: 8) {
                        Text("Errors (\(result.errorMessages.count))")
                            .font(.headline)
                        
                        ScrollView {
                            LazyVStack(alignment: .leading, spacing: 4) {
                                ForEach(result.errorMessages, id: \.self) { error in
                                    Text(error)
                                        .font(.caption)
                                        .foregroundColor(.red)
                                }
                            }
                        }
                        .frame(maxHeight: 200)
                        .background(Color(.systemGray6))
                        .cornerRadius(8)
                    }
                }
                
                Spacer()
            }
            .padding()
            .navigationTitle("Scan Results")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("Done") {
                        dismiss()
                    }
                }
            }
        }
    }
}

struct ResultCard: View {
    let title: String
    let value: String
    let color: Color
    
    var body: some View {
        VStack(spacing: 8) {
            Text(value)
                .font(.title)
                .fontWeight(.bold)
                .foregroundColor(color)
            
            Text(title)
                .font(.caption)
                .foregroundColor(.secondary)
        }
        .padding()
        .background(Color(.systemGray6))
        .cornerRadius(12)
    }
}
```

### Usage Examples

#### Starting a Library Scan
```swift
// Start scanning a library directory
Task {
    do {
        try await ScannerManager.shared.startLibraryScan(
            libraryPath: "/Users/username/Music"
        )
        print("Library scan started")
    } catch {
        print("Failed to start scan: \(error)")
    }
}
```

#### Scanning a Single File
```swift
// Scan a single audio file
Task {
    do {
        let result = try await ScannerManager.shared.scanSingleFile(
            filePath: "/path/to/song.mp3"
        )
        print("Scan completed: \(result.newTracks) new tracks")
    } catch {
        print("Failed to scan file: \(error)")
    }
}
```

#### Checking File Format Support
```swift
// Check if a file format is supported
Task {
    let isSupported = await ScannerManager.shared.isValidAudioFile("/path/to/file.flac")
    if isSupported {
        print("File format is supported")
    } else {
        print("Unsupported file format")
    }
}
```

## Database Diagnostics

The Database Diagnostics functionality provides comprehensive health monitoring and data quality assessment for your music library. This helps administrators understand the current state of their library and identify potential issues.

### Diagnostic Models

```swift
// MARK: - Database Diagnostic Models

struct DatabaseDiagnosticResponse: Codable {
    let totalCounts: TotalCountsInfo
    let duplicateArtists: [DuplicateArtistInfo]
    let duplicateAlbums: [DuplicateAlbumInfo]
    let sampleArtists: [SampleArtistInfo]
}

struct TotalCountsInfo: Codable {
    let artistCount: Int
    let albumCount: Int
    let trackCount: Int
}

struct DuplicateArtistInfo: Codable {
    let name: String
    let count: Int
    let ids: [Int]
}

struct DuplicateAlbumInfo: Codable {
    let title: String
    let artistId: Int
    let count: Int
    let ids: [Int]
}

struct SampleArtistInfo: Codable {
    let id: Int
    let name: String
    let albumCount: Int
    let trackCount: Int
}
```

### Diagnostic Manager

```swift
class DiagnosticManager: ObservableObject {
    private let apiClient: APIClient
    
    @Published var isLoading = false
    @Published var lastDiagnostic: DatabaseDiagnosticResponse?
    @Published var error: APIError?
    
    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }
    
    @MainActor
    func performDatabaseCheck() async {
        isLoading = true
        error = nil
        
        do {
            let diagnostic = try await apiClient.performDatabaseDiagnostic()
            lastDiagnostic = diagnostic
        } catch {
            self.error = error as? APIError ?? .unknown(error.localizedDescription)
        }
        
        isLoading = false
    }
    
    // MARK: - Helper Properties
    
    var hasDuplicates: Bool {
        guard let diagnostic = lastDiagnostic else { return false }
        return !diagnostic.duplicateArtists.isEmpty || !diagnostic.duplicateAlbums.isEmpty
    }
    
    var totalDuplicateCount: Int {
        guard let diagnostic = lastDiagnostic else { return 0 }
        return diagnostic.duplicateArtists.count + diagnostic.duplicateAlbums.count
    }
}
```

### API Client Extension

```swift
extension APIClient {
    // MARK: - Diagnostics
    
    func performDatabaseDiagnostic() async throws -> DatabaseDiagnosticResponse {
        let url = baseURL.appendingPathComponent("api/v2/diagnostic/data-check")
        
        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        
        let (data, response) = try await URLSession.shared.data(for: request)
        
        if let httpResponse = response as? HTTPURLResponse,
           !(200...299).contains(httpResponse.statusCode) {
            throw APIError.serverError(httpResponse.statusCode)
        }
        
        return try JSONDecoder().decode(DatabaseDiagnosticResponse.self, from: data)
    }
}
```

### Diagnostic View

```swift
struct DiagnosticView: View {
    @StateObject private var diagnosticManager: DiagnosticManager
    
    init(apiClient: APIClient) {
        _diagnosticManager = StateObject(wrappedValue: DiagnosticManager(apiClient: apiClient))
    }
    
    var body: some View {
        NavigationView {
            VStack(spacing: 20) {
                if diagnosticManager.isLoading {
                    ProgressView("Running diagnostic...")
                } else if let diagnostic = diagnosticManager.lastDiagnostic {
                    ScrollView {
                        LazyVStack(spacing: 16) {
                            libraryOverviewCard(diagnostic.totalCounts)
                            
                            if !diagnostic.duplicateArtists.isEmpty {
                                duplicateArtistsCard(diagnostic.duplicateArtists)
                            }
                            
                            if !diagnostic.duplicateAlbums.isEmpty {
                                duplicateAlbumsCard(diagnostic.duplicateAlbums)
                            }
                            
                            sampleArtistsCard(diagnostic.sampleArtists)
                        }
                        .padding()
                    }
                } else {
                    VStack(spacing: 16) {
                        Image(systemName: "chart.bar.doc.horizontal")
                            .font(.system(size: 64))
                            .foregroundColor(.secondary)
                        
                        Text("No diagnostic data available")
                            .font(.headline)
                        
                        Text("Run a database check to analyze your music library")
                            .foregroundColor(.secondary)
                            .multilineTextAlignment(.center)
                        
                        Button("Run Diagnostic") {
                            Task {
                                await diagnosticManager.performDatabaseCheck()
                            }
                        }
                        .buttonStyle(.borderedProminent)
                    }
                    .padding()
                }
                
                Spacer()
            }
            .navigationTitle("Database Diagnostics")
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("Refresh") {
                        Task {
                            await diagnosticManager.performDatabaseCheck()
                        }
                    }
                    .disabled(diagnosticManager.isLoading)
                }
            }
            .alert("Error", isPresented: .constant(diagnosticManager.error != nil)) {
                Button("OK") {
                    diagnosticManager.error = nil
                }
            } message: {
                Text(diagnosticManager.error?.localizedDescription ?? "Unknown error")
            }
        }
    }
    
    // MARK: - View Components
    
    @ViewBuilder
    private func libraryOverviewCard(_ counts: TotalCountsInfo) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Image(systemName: "chart.pie.fill")
                    .foregroundColor(.blue)
                Text("Library Overview")
                    .font(.headline)
                Spacer()
            }
            
            HStack(spacing: 20) {
                statItem("Artists", value: counts.artistCount, color: .purple)
                statItem("Albums", value: counts.albumCount, color: .orange)
                statItem("Tracks", value: counts.trackCount, color: .green)
            }
        }
        .padding()
        .background(Color(.systemBackground))
        .cornerRadius(12)
        .shadow(radius: 2)
    }
    
    @ViewBuilder
    private func statItem(_ title: String, value: Int, color: Color) -> some View {
        VStack(spacing: 4) {
            Text("\(value)")
                .font(.title2)
                .fontWeight(.bold)
                .foregroundColor(color)
            Text(title)
                .font(.caption)
                .foregroundColor(.secondary)
        }
        .frame(maxWidth: .infinity)
    }
    
    @ViewBuilder
    private func duplicateArtistsCard(_ duplicates: [DuplicateArtistInfo]) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Image(systemName: "person.2.fill")
                    .foregroundColor(.red)
                Text("Duplicate Artists")
                    .font(.headline)
                Spacer()
                Text("\(duplicates.count)")
                    .font(.caption)
                    .padding(.horizontal, 8)
                    .padding(.vertical, 2)
                    .background(Color.red.opacity(0.1))
                    .foregroundColor(.red)
                    .cornerRadius(4)
            }
            
            LazyVStack(alignment: .leading, spacing: 8) {
                ForEach(duplicates.prefix(5), id: \.name) { duplicate in
                    HStack {
                        Text(duplicate.name)
                            .font(.subheadline)
                        Spacer()
                        Text("\(duplicate.count) copies")
                            .font(.caption)
                            .foregroundColor(.secondary)
                    }
                }
                
                if duplicates.count > 5 {
                    Text("+ \(duplicates.count - 5) more...")
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
            }
        }
        .padding()
        .background(Color(.systemBackground))
        .cornerRadius(12)
        .shadow(radius: 2)
    }
    
    @ViewBuilder
    private func duplicateAlbumsCard(_ duplicates: [DuplicateAlbumInfo]) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Image(systemName: "opticaldisc.fill")
                    .foregroundColor(.orange)
                Text("Duplicate Albums")
                    .font(.headline)
                Spacer()
                Text("\(duplicates.count)")
                    .font(.caption)
                    .padding(.horizontal, 8)
                    .padding(.vertical, 2)
                    .background(Color.orange.opacity(0.1))
                    .foregroundColor(.orange)
                    .cornerRadius(4)
            }
            
            LazyVStack(alignment: .leading, spacing: 8) {
                ForEach(duplicates.prefix(5), id: \.title) { duplicate in
                    HStack {
                        Text(duplicate.title)
                            .font(.subheadline)
                        Spacer()
                        Text("\(duplicate.count) copies")
                            .font(.caption)
                            .foregroundColor(.secondary)
                    }
                }
                
                if duplicates.count > 5 {
                    Text("+ \(duplicates.count - 5) more...")
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
            }
        }
        .padding()
        .background(Color(.systemBackground))
        .cornerRadius(12)
        .shadow(radius: 2)
    }
    
    @ViewBuilder
    private func sampleArtistsCard(_ artists: [SampleArtistInfo]) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Image(systemName: "music.note.list")
                    .foregroundColor(.blue)
                Text("Sample Artists")
                    .font(.headline)
                Spacer()
            }
            
            LazyVStack(alignment: .leading, spacing: 8) {
                ForEach(artists, id: \.id) { artist in
                    HStack {
                        VStack(alignment: .leading, spacing: 2) {
                            Text(artist.name)
                                .font(.subheadline)
                                .fontWeight(.medium)
                            Text("\(artist.albumCount) albums • \(artist.trackCount) tracks")
                                .font(.caption)
                                .foregroundColor(.secondary)
                        }
                        Spacer()
                    }
                }
            }
        }
        .padding()
        .background(Color(.systemBackground))
        .cornerRadius(12)
        .shadow(radius: 2)
    }
}
```

### Usage in Admin Dashboard

```swift
struct AdminDashboardView: View {
    @StateObject private var diagnosticManager: DiagnosticManager
    
    init(apiClient: APIClient) {
        _diagnosticManager = StateObject(wrappedValue: DiagnosticManager(apiClient: apiClient))
    }
    
    var body: some View {
        NavigationView {
            List {
                Section("System Health") {
                    NavigationLink {
                        DiagnosticView(apiClient: diagnosticManager.apiClient)
                    } label: {
                        HStack {
                            Image(systemName: "chart.bar.doc.horizontal")
                                .foregroundColor(.blue)
                            VStack(alignment: .leading) {
                                Text("Database Diagnostics")
                                if diagnosticManager.hasDuplicates {
                                    Text("\(diagnosticManager.totalDuplicateCount) duplicates found")
                                        .font(.caption)
                                        .foregroundColor(.orange)
                                }
                            }
                        }
                    }
                }
                
                // Other admin sections...
            }
            .navigationTitle("Admin Dashboard")
            .task {
                await diagnosticManager.performDatabaseCheck()
            }
        }
    }
}
```

**Best Practices:**
1. **Regular Monitoring**: Run diagnostics after library imports or changes
2. **Performance Awareness**: Diagnostics can be resource-intensive on large libraries
3. **Security**: Restrict diagnostic access to admin users only
4. **UI Feedback**: Show loading states and handle errors gracefully
5. **Data Analysis**: Use duplicate information to plan cleanup operations

## Data Cleanup

The Data Cleanup functionality helps maintain database integrity by identifying and merging duplicate entries. This is essential for keeping your music library organized and optimized.

### Cleanup Manager

```swift
import Foundation

@MainActor
class CleanupManager: ObservableObject {
    static let shared = CleanupManager()
    
    @Published var isPerformingCleanup = false
    @Published var lastCleanupResult: ComprehensiveCleanupResponse?
    @Published var errorMessage: String?
    
    private let apiClient = AudiarrAPIClient.shared
    
    // MARK: - Individual Cleanup Operations
    
    func mergeDuplicateArtists() async throws -> ArtistCleanupResponse {
        guard !isPerformingCleanup else {
            throw CleanupError.operationInProgress
        }
        
        isPerformingCleanup = true
        errorMessage = nil
        
        defer { isPerformingCleanup = false }
        
        do {
            let result = try await apiClient.mergeDuplicateArtists()
            print("Artist cleanup completed: \(result.message)")
            return result
        } catch {
            errorMessage = error.localizedDescription
            throw error
        }
    }
    
    func mergeDuplicateAlbums() async throws -> AlbumCleanupResponse {
        guard !isPerformingCleanup else {
            throw CleanupError.operationInProgress
        }
        
        isPerformingCleanup = true
        errorMessage = nil
        
        defer { isPerformingCleanup = false }
        
        do {
            let result = try await apiClient.mergeDuplicateAlbums()
            print("Album cleanup completed: \(result.message)")
            return result
        } catch {
            errorMessage = error.localizedDescription
            throw error
        }
    }
    
    func performComprehensiveCleanup() async throws -> ComprehensiveCleanupResponse {
        guard !isPerformingCleanup else {
            throw CleanupError.operationInProgress
        }
        
        isPerformingCleanup = true
        errorMessage = nil
        
        defer { isPerformingCleanup = false }
        
        do {
            let result = try await apiClient.cleanAllData()
            lastCleanupResult = result
            print("Comprehensive cleanup completed: \(result.summary)")
            return result
        } catch {
            errorMessage = error.localizedDescription
            throw error
        }
    }
    
    // MARK: - Batch Operations
    
    func performCleanupOperation(_ operation: CleanupOperation) async throws -> Any {
        switch operation {
        case .artists:
            return try await mergeDuplicateArtists()
        case .albums:
            return try await mergeDuplicateAlbums()
        case .all:
            return try await performComprehensiveCleanup()
        }
    }
    
    // MARK: - Utility Methods
    
    func shouldRecommendCleanup() -> Bool {
        // Recommend cleanup if it's been more than a week since last cleanup
        guard let lastResult = lastCleanupResult else { return true }
        
        // This is a simplified check - in a real app you might store the last cleanup date
        return lastResult.totalMerged > 0
    }
    
    func getRecommendedCleanupFrequency() -> String {
        "We recommend running cleanup weekly for active libraries"
    }
}

enum CleanupError: LocalizedError {
    case operationInProgress
    case networkError
    case serverError
    
    var errorDescription: String? {
        switch self {
        case .operationInProgress:
            return "A cleanup operation is already in progress"
        case .networkError:
            return "Network error during cleanup"
        case .serverError:
            return "Server error during cleanup"
        }
    }
}
```

### Cleanup UI Implementation

```swift
import SwiftUI

struct DataCleanupView: View {
    @StateObject private var cleanupManager = CleanupManager.shared
    @State private var selectedOperation: CleanupOperation = .all
    @State private var showingConfirmation = false
    @State private var showingResults = false
    @State private var lastResult: Any?
    
    var body: some View {
        NavigationView {
            VStack(spacing: 24) {
                // Header Section
                VStack(spacing: 8) {
                    Image(systemName: "sparkles")
                        .font(.system(size: 48))
                        .foregroundColor(.blue)
                    
                    Text("Database Cleanup")
                        .font(.title)
                        .fontWeight(.bold)
                    
                    Text("Maintain your library by merging duplicate entries")
                        .font(.subheadline)
                        .foregroundColor(.secondary)
                        .multilineTextAlignment(.center)
                }
                .padding(.top)
                
                // Operation Selection
                VStack(alignment: .leading, spacing: 16) {
                    Text("Cleanup Options")
                        .font(.headline)
                    
                    ForEach(CleanupOperation.allCases, id: \.self) { operation in
                        CleanupOptionRow(
                            operation: operation,
                            isSelected: selectedOperation == operation,
                            onSelect: {
                                selectedOperation = operation
                            }
                        )
                    }
                }
                
                // Last Cleanup Results
                if let lastResult = cleanupManager.lastCleanupResult {
                    VStack(alignment: .leading, spacing: 8) {
                        Text("Last Cleanup")
                            .font(.headline)
                        
                        CleanupResultCard(result: lastResult)
                    }
                }
                
                Spacer()
                
                // Action Button
                VStack(spacing: 12) {
                    if cleanupManager.isPerformingCleanup {
                        ProgressView("Performing cleanup...")
                            .frame(maxWidth: .infinity)
                    } else {
                        Button("Start Cleanup") {
                            showingConfirmation = true
                        }
                        .buttonStyle(.borderedProminent)
                        .disabled(cleanupManager.isPerformingCleanup)
                    }
                    
                    Text("⚠️ Important: Backup your database before running cleanup")
                        .font(.caption)
                        .foregroundColor(.orange)
                        .multilineTextAlignment(.center)
                }
                
                // Error Display
                if let error = cleanupManager.errorMessage {
                    Text(error)
                        .foregroundColor(.red)
                        .padding()
                        .background(Color.red.opacity(0.1))
                        .cornerRadius(8)
                }
            }
            .padding()
            .navigationTitle("Data Cleanup")
            .navigationBarTitleDisplayMode(.inline)
            .confirmationDialog(
                "Confirm Cleanup",
                isPresented: $showingConfirmation,
                titleVisibility: .visible
            ) {
                Button("Start \(selectedOperation.rawValue) Cleanup") {
                    performCleanup()
                }
                Button("Cancel", role: .cancel) { }
            } message: {
                Text("This operation will merge duplicate \(selectedOperation.rawValue.lowercased()) and cannot be undone. Make sure you have a backup!")
            }
            .sheet(isPresented: $showingResults) {
                if let result = lastResult {
                    CleanupResultsView(result: result)
                }
            }
        }
    }
    
    private func performCleanup() {
        Task {
            do {
                let result = try await cleanupManager.performCleanupOperation(selectedOperation)
                await MainActor.run {
                    lastResult = result
                    showingResults = true
                }
            } catch {
                // Error handling is managed by CleanupManager
                print("Cleanup failed: \(error)")
            }
        }
    }
}

struct CleanupOptionRow: View {
    let operation: CleanupOperation
    let isSelected: Bool
    let onSelect: () -> Void
    
    var body: some View {
        HStack {
            Image(systemName: operation.iconName)
                .font(.title2)
                .foregroundColor(isSelected ? .white : .blue)
                .frame(width: 24)
            
            VStack(alignment: .leading, spacing: 4) {
                Text(operation.rawValue)
                    .font(.headline)
                    .foregroundColor(isSelected ? .white : .primary)
                
                Text(operation.description)
                    .font(.caption)
                    .foregroundColor(isSelected ? .white.opacity(0.8) : .secondary)
            }
            
            Spacer()
            
            if isSelected {
                Image(systemName: "checkmark.circle.fill")
                    .foregroundColor(.white)
            }
        }
        .padding()
        .background(isSelected ? Color.blue : Color(.systemGray6))
        .cornerRadius(12)
        .onTapGesture {
            onSelect()
        }
    }
}

struct CleanupResultCard: View {
    let result: ComprehensiveCleanupResponse
    
    var body: some View {
        VStack(spacing: 12) {
            HStack {
                Image(systemName: "checkmark.circle.fill")
                    .foregroundColor(.green)
                
                Text(result.summary)
                    .font(.subheadline)
                    .fontWeight(.medium)
                
                Spacer()
            }
            
            HStack {
                ResultMetric(
                    title: "Artists",
                    value: "\(result.artistsMerged)",
                    color: .blue
                )
                
                Spacer()
                
                ResultMetric(
                    title: "Albums",
                    value: "\(result.albumsMerged)",
                    color: .orange
                )
                
                Spacer()
                
                ResultMetric(
                    title: "Total",
                    value: "\(result.totalMerged)",
                    color: .green
                )
            }
        }
        .padding()
        .background(Color(.systemGray6))
        .cornerRadius(12)
    }
}

struct ResultMetric: View {
    let title: String
    let value: String
    let color: Color
    
    var body: some View {
        VStack(spacing: 4) {
            Text(value)
                .font(.title2)
                .fontWeight(.bold)
                .foregroundColor(color)
            
            Text(title)
                .font(.caption)
                .foregroundColor(.secondary)
        }
    }
}

struct CleanupResultsView: View {
    let result: Any
    @Environment(\.dismiss) private var dismiss
    
    var body: some View {
        NavigationView {
            VStack(spacing: 20) {
                Image(systemName: "checkmark.circle.fill")
                    .font(.system(size: 64))
                    .foregroundColor(.green)
                
                Text("Cleanup Complete!")
                    .font(.title)
                    .fontWeight(.bold)
                
                if let comprehensiveResult = result as? ComprehensiveCleanupResponse {
                    ComprehensiveResultView(result: comprehensiveResult)
                } else if let artistResult = result as? ArtistCleanupResponse {
                    SimpleResultView(message: artistResult.message, groups: artistResult.duplicateGroupsFound)
                } else if let albumResult = result as? AlbumCleanupResponse {
                    SimpleResultView(message: albumResult.message, groups: albumResult.duplicateGroupsFound)
                }
                
                Spacer()
            }
            .padding()
            .navigationTitle("Results")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("Done") {
                        dismiss()
                    }
                }
            }
        }
    }
}

struct ComprehensiveResultView: View {
    let result: ComprehensiveCleanupResponse
    
    var body: some View {
        VStack(spacing: 16) {
            Text(result.message)
                .font(.headline)
                .multilineTextAlignment(.center)
            
            LazyVGrid(columns: Array(repeating: GridItem(.flexible()), count: 2), spacing: 16) {
                MetricCard(title: "Artists Merged", value: "\(result.artistsMerged)", color: .blue)
                MetricCard(title: "Albums Merged", value: "\(result.albumsMerged)", color: .orange)
                MetricCard(title: "Artist Groups", value: "\(result.duplicateArtistGroupsFound)", color: .purple)
                MetricCard(title: "Album Groups", value: "\(result.duplicateAlbumGroupsFound)", color: .cyan)
            }
        }
    }
}

struct SimpleResultView: View {
    let message: String
    let groups: Int
    
    var body: some View {
        VStack(spacing: 16) {
            Text(message)
                .font(.headline)
                .multilineTextAlignment(.center)
            
            MetricCard(title: "Duplicate Groups Found", value: "\(groups)", color: .blue)
        }
    }
}

struct MetricCard: View {
    let title: String
    let value: String
    let color: Color
    
    var body: some View {
        VStack(spacing: 8) {
            Text(value)
                .font(.title)
                .fontWeight(.bold)
                .foregroundColor(color)
            
            Text(title)
                .font(.caption)
                .foregroundColor(.secondary)
                .multilineTextAlignment(.center)
        }
        .padding()
        .background(Color(.systemGray6))
        .cornerRadius(12)
    }
}
```

### Usage Examples

#### Performing Individual Cleanup Operations
```swift
// Merge duplicate artists only
Task {
    do {
        let result = try await CleanupManager.shared.mergeDuplicateArtists()
        print("Merged \(result.duplicateGroupsFound) artist groups")
    } catch {
        print("Artist cleanup failed: \(error)")
    }
}

// Merge duplicate albums only
Task {
    do {
        let result = try await CleanupManager.shared.mergeDuplicateAlbums()
        print("Merged \(result.duplicateGroupsFound) album groups")
    } catch {
        print("Album cleanup failed: \(error)")
    }
}
```

#### Comprehensive Cleanup
```swift
// Perform complete database cleanup
Task {
    do {
        let result = try await CleanupManager.shared.performComprehensiveCleanup()
        print("Cleanup completed: \(result.summary)")
        print("Artists: \(result.artistsMerged), Albums: \(result.albumsMerged)")
    } catch {
        print("Comprehensive cleanup failed: \(error)")
    }
}
```

#### Scheduled Cleanup
```swift
// Schedule periodic cleanup (example using background tasks)
func schedulePeriodicCleanup() {
    Task {
        // Run cleanup weekly
        while true {
            try await Task.sleep(nanoseconds: 7 * 24 * 60 * 60 * 1_000_000_000) // 7 days
            
            do {
                let result = try await CleanupManager.shared.performComprehensiveCleanup()
                print("Scheduled cleanup completed: \(result.summary)")
            } catch {
                print("Scheduled cleanup failed: \(error)")
            }
        }
    }
}
```

### Important Considerations

1. **Data Safety**: Always backup your database before running cleanup operations
2. **Network Timing**: Cleanup operations may take time for large libraries
3. **User Experience**: Show progress indicators during cleanup operations
4. **Error Handling**: Implement robust error handling for network and server issues
5. **Frequency**: Regular cleanup (weekly) helps maintain data quality
6. **Authentication**: Note that cleanup endpoints currently don't require authentication

## Audio Streaming

```swift
import AVFoundation
import MediaPlayer
import Combine

class AudioPlayer: NSObject, ObservableObject {
    static let shared = AudioPlayer()
    
    private var player: AVPlayer?
    private var playerItem: AVPlayerItem?
    private var timeObserver: Any?
    
    @Published var isPlaying = false
    @Published var currentTrack: Track?
    @Published var currentTime: TimeInterval = 0
    @Published var duration: TimeInterval = 0
    @Published var isBuffering = false
    
    private var cancellables = Set<AnyCancellable>()
    
    override init() {
        super.init()
        setupAudioSession()
        setupRemoteControls()
    }
    
    // MARK: - Setup
    
    private func setupAudioSession() {
        do {
            try AVAudioSession.sharedInstance().setCategory(.playback, mode: .default)
            try AVAudioSession.sharedInstance().setActive(true)
        } catch {
            print("Failed to setup audio session: \(error)")
        }
    }
    
    private func setupRemoteControls() {
        let commandCenter = MPRemoteCommandCenter.shared()
        
        commandCenter.playCommand.addTarget { _ in
            self.play()
            return .success
        }
        
        commandCenter.pauseCommand.addTarget { _ in
            self.pause()
            return .success
        }
        
        commandCenter.nextTrackCommand.addTarget { _ in
            self.playNext()
            return .success
        }
        
        commandCenter.previousTrackCommand.addTarget { _ in
            self.playPrevious()
            return .success
        }
        
        commandCenter.changePlaybackPositionCommand.addTarget { event in
            if let event = event as? MPChangePlaybackPositionCommandEvent {
                self.seek(to: event.positionTime)
                return .success
            }
            return .commandFailed
        }
    }
    
    // MARK: - Playback Control
    
    func play(track: Track) {
        currentTrack = track
        
        let url = AudiarrAPIClient.shared.getStreamURL(for: track.id)
        playerItem = AVPlayerItem(url: url)
        
        // Observe player item status
        playerItem?.publisher(for: \.status)
            .sink { [weak self] status in
                switch status {
                case .readyToPlay:
                    self?.isBuffering = false
                    self?.duration = self?.playerItem?.duration.seconds ?? 0
                case .failed:
                    print("Player item failed")
                case .unknown:
                    self?.isBuffering = true
                @unknown default:
                    break
                }
            }
            .store(in: &cancellables)
        
        player = AVPlayer(playerItem: playerItem)
        
        // Add time observer
        let interval = CMTime(seconds: 1, preferredTimescale: 1)
        timeObserver = player?.addPeriodicTimeObserver(forInterval: interval, queue: .main) { [weak self] time in
            self?.currentTime = time.seconds
            self?.updateNowPlaying()
        }
        
        player?.play()
        isPlaying = true
        
        updateNowPlaying()
        
        // Update play count
        Task {
            try? await AudiarrAPIClient.shared.updatePlayCount(trackId: track.id)
        }
    }
    
    func play() {
        player?.play()
        isPlaying = true
    }
    
    func pause() {
        player?.pause()
        isPlaying = false
    }
    
    func togglePlayPause() {
        if isPlaying {
            pause()
        } else {
            play()
        }
    }
    
    func seek(to time: TimeInterval) {
        let cmTime = CMTime(seconds: time, preferredTimescale: 1)
        player?.seek(to: cmTime)
    }
    
    func playNext() {
        // Implement queue logic
    }
    
    func playPrevious() {
        // Implement queue logic
    }
    
    // MARK: - Now Playing Info
    
    private func updateNowPlaying() {
        guard let track = currentTrack else { return }
        
        var info = [String: Any]()
        info[MPMediaItemPropertyTitle] = track.title
        info[MPMediaItemPropertyArtist] = track.artistName
        info[MPMediaItemPropertyAlbumTitle] = track.albumTitle
        info[MPMediaItemPropertyPlaybackDuration] = duration
        info[MPNowPlayingInfoPropertyElapsedPlaybackTime] = currentTime
        info[MPNowPlayingInfoPropertyPlaybackRate] = isPlaying ? 1.0 : 0.0
        
        // Load album artwork
        if let albumId = track.albumId {
            let coverURL = AudiarrAPIClient.shared.getAlbumCoverURL(for: albumId)
            URLSession.shared.dataTask(with: coverURL) { data, _, _ in
                if let data = data, let image = UIImage(data: data) {
                    DispatchQueue.main.async {
                        info[MPMediaItemPropertyArtwork] = MPMediaItemArtwork(boundsSize: image.size) { _ in image }
                        MPNowPlayingInfoCenter.default().nowPlayingInfo = info
                    }
                }
            }.resume()
        }
        
        MPNowPlayingInfoCenter.default().nowPlayingInfo = info
    }
    
    deinit {
        if let observer = timeObserver {
            player?.removeTimeObserver(observer)
        }
    }
}

// MARK: - Queue Manager

class QueueManager: ObservableObject {
    @Published var queue: [Track] = []
    @Published var currentIndex: Int = 0
    @Published var shuffleEnabled = false
    @Published var repeatMode: RepeatMode = .none
    
    enum RepeatMode {
        case none, one, all
    }
    
    func add(_ track: Track) {
        queue.append(track)
    }
    
    func addNext(_ track: Track) {
        queue.insert(track, at: min(currentIndex + 1, queue.count))
    }
    
    func remove(at index: Int) {
        guard index < queue.count else { return }
        queue.remove(at: index)
        if index < currentIndex {
            currentIndex -= 1
        }
    }
    
    func clear() {
        queue.removeAll()
        currentIndex = 0
    }
    
    func playNext() -> Track? {
        switch repeatMode {
        case .one:
            return queue[safe: currentIndex]
        case .all:
            currentIndex = (currentIndex + 1) % queue.count
            return queue[safe: currentIndex]
        case .none:
            guard currentIndex + 1 < queue.count else { return nil }
            currentIndex += 1
            return queue[safe: currentIndex]
        }
    }
    
    func playPrevious() -> Track? {
        guard currentIndex > 0 else { return nil }
        currentIndex -= 1
        return queue[safe: currentIndex]
    }
}

extension Array {
    subscript(safe index: Int) -> Element? {
        return indices.contains(index) ? self[index] : nil
    }
}
```

## SignalR Integration

```swift
import Foundation
import SignalRClient

class SignalRManager: ObservableObject {
    private var hubConnection: HubConnection?
    
    @Published var scanProgress: ScanProgress?
    @Published var isConnected = false
    
    init() {
        setupConnection()
    }
    
    private func setupConnection() {
        let url = URL(string: "http://your-server:8080/hubs/scan")!
        
        hubConnection = HubConnectionBuilder(url: url)
            .withLogging(minLogLevel: .debug)
            .withAutoReconnect()
            .withHubConnectionDelegate(self)
            .build()
        
        // Register event handlers
        hubConnection?.on(method: "ScanProgress") { (progress: ScanProgress) in
            DispatchQueue.main.async {
                self.scanProgress = progress
            }
        }
        
        hubConnection?.on(method: "ScanComplete") { (result: ScanResult) in
            DispatchQueue.main.async {
                print("Scan complete: \(result)")
                // Handle scan completion
            }
        }
        
        hubConnection?.on(method: "ScanError") { (error: String) in
            DispatchQueue.main.async {
                print("Scan error: \(error)")
                // Handle error
            }
        }
    }
    
    func connect() {
        hubConnection?.start()
    }
    
    func disconnect() {
        hubConnection?.stop()
    }
}

// MARK: - HubConnectionDelegate

extension SignalRManager: HubConnectionDelegate {
    func connectionDidOpen(hubConnection: HubConnection) {
        print("SignalR connected")
        DispatchQueue.main.async {
            self.isConnected = true
        }
    }
    
    func connectionDidFailToOpen(error: Error) {
        print("SignalR failed to connect: \(error)")
    }
    
    func connectionDidClose(error: Error?) {
        print("SignalR disconnected")
        DispatchQueue.main.async {
            self.isConnected = false
        }
    }
}

// MARK: - SignalR Models

struct ScanProgress: Codable {
    let processed: Int
    let total: Int
    let message: String
    let percentComplete: Double
}

struct ScanResult: Codable {
    let totalFiles: Int
    let newTracks: Int
    let updatedTracks: Int
    let errors: Int
    let completedAt: Date
}
```

## Offline Support

```swift
import CoreData

class OfflineManager {
    private let container: NSPersistentContainer
    
    init() {
        container = NSPersistentContainer(name: "AudiarrCache")
        container.loadPersistentStores { _, error in
            if let error = error {
                print("Core Data failed to load: \(error)")
            }
        }
    }
    
    // MARK: - Caching
    
    func cacheTrack(_ track: Track, audioData: Data) {
        let context = container.viewContext
        
        let cachedTrack = CachedTrack(context: context)
        cachedTrack.id = track.id
        cachedTrack.title = track.title
        cachedTrack.artistName = track.artistName
        cachedTrack.albumTitle = track.albumTitle
        cachedTrack.audioData = audioData
        cachedTrack.cachedDate = Date()
        
        try? context.save()
    }
    
    func getCachedTrack(id: String) -> (track: Track, audioData: Data)? {
        let request: NSFetchRequest<CachedTrack> = CachedTrack.fetchRequest()
        request.predicate = NSPredicate(format: "id == %@", id)
        
        guard let cachedTrack = try? container.viewContext.fetch(request).first,
              let audioData = cachedTrack.audioData else {
            return nil
        }
        
        let track = Track(
            id: cachedTrack.id!,
            title: cachedTrack.title!,
            artistId: "",
            artistName: cachedTrack.artistName!,
            albumId: nil,
            albumTitle: cachedTrack.albumTitle,
            trackNumber: nil,
            discNumber: nil,
            durationMs: 0,
            genre: nil,
            year: nil,
            fileSize: nil,
            bitrate: nil,
            codec: nil,
            filePath: nil
        )
        
        return (track, audioData)
    }
    
    func clearCache() {
        let request: NSFetchRequest<NSFetchRequestResult> = CachedTrack.fetchRequest()
        let deleteRequest = NSBatchDeleteRequest(fetchRequest: request)
        
        try? container.viewContext.execute(deleteRequest)
    }
}
```

## UI Components

### Track List View
```swift
import SwiftUI
import SDWebImageSwiftUI

struct TrackListView: View {
    @StateObject private var viewModel = TrackListViewModel()
    @StateObject private var player = AudioPlayer.shared
    
    var body: some View {
        List {
            ForEach(viewModel.tracks) { track in
                TrackRow(track: track)
                    .onTapGesture {
                        player.play(track: track)
                    }
            }
            
            if viewModel.hasMorePages {
                ProgressView()
                    .frame(maxWidth: .infinity)
                    .onAppear {
                        viewModel.loadMoreTracks()
                    }
            }
        }
        .navigationTitle("Tracks")
        .refreshable {
            await viewModel.refresh()
        }
        .task {
            await viewModel.loadTracks()
        }
    }
}

struct TrackRow: View {
    let track: Track
    
    var body: some View {
        HStack {
            if let albumId = track.albumId {
                WebImage(url: AudiarrAPIClient.shared.getAlbumCoverURL(for: albumId))
                    .resizable()
                    .placeholder {
                        Rectangle()
                            .foregroundColor(.gray.opacity(0.3))
                    }
                    .frame(width: 50, height: 50)
                    .cornerRadius(4)
            }
            
            VStack(alignment: .leading, spacing: 4) {
                Text(track.title)
                    .font(.headline)
                    .lineLimit(1)
                
                Text("\(track.artistName) • \(track.albumTitle ?? "Unknown Album")")
                    .font(.caption)
                    .foregroundColor(.secondary)
                    .lineLimit(1)
            }
            
            Spacer()
            
            Text(formatDuration(track.durationMs))
                .font(.caption)
                .foregroundColor(.secondary)
        }
        .padding(.vertical, 4)
    }
    
    private func formatDuration(_ milliseconds: Int) -> String {
        let seconds = milliseconds / 1000
        let minutes = seconds / 60
        let remainingSeconds = seconds % 60
        return String(format: "%d:%02d", minutes, remainingSeconds)
    }
}

@MainActor
class TrackListViewModel: ObservableObject {
    @Published var tracks: [Track] = []
    @Published var isLoading = false
    @Published var hasMorePages = true
    
    private var currentPage = 1
    private let pageSize = 50
    
    func loadTracks() async {
        guard !isLoading else { return }
        
        isLoading = true
        
        do {
            let response = try await AudiarrAPIClient.shared.getTracks(
                page: currentPage,
                limit: pageSize
            )
            
            tracks.append(contentsOf: response.data)
            hasMorePages = currentPage < response.totalPages
            currentPage += 1
        } catch {
            print("Failed to load tracks: \(error)")
        }
        
        isLoading = false
    }
    
    func loadMoreTracks() {
        Task {
            await loadTracks()
        }
    }
    
    func refresh() async {
        currentPage = 1
        tracks.removeAll()
        hasMorePages = true
        await loadTracks()
    }
}
```

### Now Playing View
```swift
import SwiftUI
import SDWebImageSwiftUI

struct NowPlayingView: View {
    @StateObject private var player = AudioPlayer.shared
    @State private var isDraggingSlider = false
    @State private var draggedTime: TimeInterval = 0
    
    var body: some View {
        VStack(spacing: 20) {
            // Album Art
            if let track = player.currentTrack,
               let albumId = track.albumId {
                WebImage(url: AudiarrAPIClient.shared.getAlbumCoverURL(for: albumId))
                    .resizable()
                    .aspectRatio(contentMode: .fit)
                    .cornerRadius(12)
                    .shadow(radius: 10)
                    .padding(.horizontal, 40)
            } else {
                Rectangle()
                    .fill(Color.gray.opacity(0.3))
                    .aspectRatio(1, contentMode: .fit)
                    .cornerRadius(12)
                    .padding(.horizontal, 40)
            }
            
            // Track Info
            VStack(spacing: 8) {
                Text(player.currentTrack?.title ?? "Not Playing")
                    .font(.title2)
                    .fontWeight(.semibold)
                    .lineLimit(1)
                
                Text(player.currentTrack?.artistName ?? "")
                    .font(.title3)
                    .foregroundColor(.secondary)
                    .lineLimit(1)
            }
            .padding(.horizontal)
            
            // Progress Bar
            VStack(spacing: 8) {
                Slider(
                    value: isDraggingSlider ? $draggedTime : $player.currentTime,
                    in: 0...max(player.duration, 1),
                    onEditingChanged: { editing in
                        isDraggingSlider = editing
                        if !editing {
                            player.seek(to: draggedTime)
                        }
                    }
                )
                
                HStack {
                    Text(formatTime(isDraggingSlider ? draggedTime : player.currentTime))
                        .font(.caption)
                        .foregroundColor(.secondary)
                    
                    Spacer()
                    
                    Text(formatTime(player.duration))
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
            }
            .padding(.horizontal)
            
            // Playback Controls
            HStack(spacing: 40) {
                Button(action: { player.playPrevious() }) {
                    Image(systemName: "backward.fill")
                        .font(.title)
                }
                
                Button(action: { player.togglePlayPause() }) {
                    Image(systemName: player.isPlaying ? "pause.circle.fill" : "play.circle.fill")
                        .font(.system(size: 64))
                }
                
                Button(action: { player.playNext() }) {
                    Image(systemName: "forward.fill")
                        .font(.title)
                }
            }
            .foregroundColor(.primary)
            
            Spacer()
        }
        .padding()
    }
    
    private func formatTime(_ seconds: TimeInterval) -> String {
        guard !seconds.isNaN && !seconds.isInfinite else { return "0:00" }
        
        let minutes = Int(seconds) / 60
        let seconds = Int(seconds) % 60
        return String(format: "%d:%02d", minutes, seconds)
    }
}
```

### Playlist Views

#### Playlist List View
```swift
import SwiftUI

struct PlaylistListView: View {
    @StateObject private var playlistManager = PlaylistManager.shared
    @State private var showingCreateSheet = false
    @State private var includePublic = false
    
    var body: some View {
        NavigationView {
            List {
                ForEach(playlistManager.playlists) { playlist in
                    NavigationLink(destination: PlaylistDetailView(playlistId: playlist.id)) {
                        PlaylistRow(playlist: playlist)
                    }
                }
                .onDelete(perform: deletePlaylists)
                
                if playlistManager.isLoading {
                    ProgressView()
                        .frame(maxWidth: .infinity)
                }
            }
            .navigationTitle("Playlists")
            .toolbar {
                ToolbarItem(placement: .navigationBarLeading) {
                    Toggle("Public", isOn: $includePublic)
                        .onChange(of: includePublic) { _ in
                            Task {
                                await playlistManager.refresh()
                            }
                        }
                }
                
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("Add") {
                        showingCreateSheet = true
                    }
                }
            }
            .refreshable {
                await playlistManager.refresh()
            }
            .task {
                await playlistManager.loadPlaylists(includePublic: includePublic)
            }
            .sheet(isPresented: $showingCreateSheet) {
                CreatePlaylistView()
            }
        }
    }
    
    private func deletePlaylists(at offsets: IndexSet) {
        for index in offsets {
            let playlist = playlistManager.playlists[index]
            Task {
                try? await playlistManager.deletePlaylist(playlist)
            }
        }
    }
}

struct PlaylistRow: View {
    let playlist: Playlist
    
    var body: some View {
        HStack {
            if let imagePath = playlist.imagePath {
                AsyncImage(url: URL(string: imagePath)) { image in
                    image
                        .resizable()
                        .aspectRatio(contentMode: .fill)
                } placeholder: {
                    Rectangle()
                        .foregroundColor(.gray.opacity(0.3))
                }
                .frame(width: 50, height: 50)
                .cornerRadius(8)
            } else {
                Rectangle()
                    .foregroundColor(.gray.opacity(0.3))
                    .frame(width: 50, height: 50)
                    .cornerRadius(8)
                    .overlay(
                        Image(systemName: "music.note.list")
                            .foregroundColor(.gray)
                    )
            }
            
            VStack(alignment: .leading, spacing: 4) {
                Text(playlist.name)
                    .font(.headline)
                    .lineLimit(1)
                
                HStack {
                    Text("\(playlist.trackCount) tracks")
                        .font(.caption)
                        .foregroundColor(.secondary)
                    
                    if playlist.isPublic {
                        Image(systemName: "globe")
                            .font(.caption)
                            .foregroundColor(.blue)
                    }
                }
                
                if let description = playlist.description, !description.isEmpty {
                    Text(description)
                        .font(.caption)
                        .foregroundColor(.secondary)
                        .lineLimit(2)
                }
            }
            
            Spacer()
            
            VStack(alignment: .trailing) {
                Text(playlist.userName)
                    .font(.caption)
                    .foregroundColor(.secondary)
                
                Text(formatDate(playlist.updatedAt))
                    .font(.caption2)
                    .foregroundColor(.secondary)
            }
        }
        .padding(.vertical, 4)
    }
    
    private func formatDate(_ date: Date) -> String {
        let formatter = RelativeDateTimeFormatter()
        formatter.unitsStyle = .abbreviated
        return formatter.localizedString(for: date, relativeTo: Date())
    }
}
```

#### Create Playlist View
```swift
struct CreatePlaylistView: View {
    @Environment(\.dismiss) private var dismiss
    @StateObject private var playlistManager = PlaylistManager.shared
    
    @State private var name = ""
    @State private var description = ""
    @State private var isPublic = false
    @State private var isCreating = false
    @State private var errorMessage: String?
    
    var body: some View {
        NavigationView {
            Form {
                Section("Details") {
                    TextField("Playlist Name", text: $name)
                    TextField("Description (optional)", text: $description, axis: .vertical)
                        .lineLimit(3...6)
                    Toggle("Public Playlist", isOn: $isPublic)
                }
                
                if let errorMessage = errorMessage {
                    Section {
                        Text(errorMessage)
                            .foregroundColor(.red)
                    }
                }
            }
            .navigationTitle("New Playlist")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarLeading) {
                    Button("Cancel") {
                        dismiss()
                    }
                }
                
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("Create") {
                        createPlaylist()
                    }
                    .disabled(name.isEmpty || isCreating)
                }
            }
        }
    }
    
    private func createPlaylist() {
        isCreating = true
        errorMessage = nil
        
        Task {
            do {
                _ = try await playlistManager.createPlaylist(
                    name: name,
                    description: description.isEmpty ? nil : description,
                    isPublic: isPublic
                )
                
                await MainActor.run {
                    dismiss()
                }
            } catch {
                await MainActor.run {
                    errorMessage = error.localizedDescription
                    isCreating = false
                }
            }
        }
    }
}
```

#### Playlist Detail View
```swift
struct PlaylistDetailView: View {
    let playlistId: String
    
    @StateObject private var viewModel: PlaylistDetailViewModel
    @StateObject private var player = AudioPlayer.shared
    @State private var showingEditSheet = false
    @State private var showingAddTracksSheet = false
    
    init(playlistId: String) {
        self.playlistId = playlistId
        self._viewModel = StateObject(wrappedValue: PlaylistDetailViewModel(playlistId: playlistId))
    }
    
    var body: some View {
        Group {
            if let playlist = viewModel.playlist {
                List {
                    PlaylistHeaderView(playlist: playlist)
                        .listRowInsets(EdgeInsets())
                        .listRowSeparator(.hidden)
                    
                    Section("Tracks") {
                        ForEach(Array(playlist.tracks.enumerated()), id: \.element.id) { index, track in
                            PlaylistTrackRow(
                                track: track,
                                position: index + 1,
                                onPlay: {
                                    // Convert PlaylistTrack to Track for playback
                                    let playableTrack = Track(
                                        id: track.trackId,
                                        title: track.title,
                                        artistId: track.artistId,
                                        artistName: track.artistName,
                                        albumId: track.albumId,
                                        albumTitle: track.albumTitle,
                                        trackNumber: track.trackNumber,
                                        discNumber: track.discNumber,
                                        durationMs: track.durationMs,
                                        genre: track.genre,
                                        year: track.year,
                                        fileSize: nil,
                                        bitrate: nil,
                                        codec: nil,
                                        filePath: track.filePath
                                    )
                                    player.play(track: playableTrack)
                                }
                            )
                        }
                        .onDelete(perform: deleteTracks)
                        .onMove(perform: moveTracks)
                    }
                }
            } else if viewModel.isLoading {
                ProgressView("Loading playlist...")
            } else if let errorMessage = viewModel.errorMessage {
                ContentUnavailableView(
                    "Error Loading Playlist",
                    systemImage: "exclamationmark.triangle",
                    description: Text(errorMessage)
                )
            }
        }
        .navigationTitle(viewModel.playlist?.name ?? "Playlist")
        .navigationBarTitleDisplayMode(.large)
        .toolbar {
            ToolbarItem(placement: .navigationBarTrailing) {
                Menu {
                    Button("Edit Playlist") {
                        showingEditSheet = true
                    }
                    
                    Button("Add Tracks") {
                        showingAddTracksSheet = true
                    }
                    
                    Button("Copy Playlist") {
                        copyPlaylist()
                    }
                } label: {
                    Image(systemName: "ellipsis.circle")
                }
            }
        }
        .task {
            await viewModel.loadPlaylist()
        }
        .refreshable {
            await viewModel.loadPlaylist()
        }
        .sheet(isPresented: $showingEditSheet) {
            if let playlist = viewModel.playlist {
                EditPlaylistView(playlist: playlist) {
                    await viewModel.loadPlaylist()
                }
            }
        }
        .sheet(isPresented: $showingAddTracksSheet) {
            AddTracksToPlaylistView(playlistId: playlistId) {
                await viewModel.loadPlaylist()
            }
        }
    }
    
    private func deleteTracks(at offsets: IndexSet) {
        guard let playlist = viewModel.playlist else { return }
        
        let tracksToRemove = offsets.map { playlist.tracks[$0].trackId }
        
        Task {
            try? await viewModel.removeTracks(tracksToRemove)
        }
    }
    
    private func moveTracks(from source: IndexSet, to destination: Int) {
        guard let sourceIndex = source.first else { return }
        
        let destinationIndex = destination > sourceIndex ? destination - 1 : destination
        
        Task {
            try? await viewModel.reorderTracks(from: sourceIndex, to: destinationIndex)
        }
    }
    
    private func copyPlaylist() {
        guard let playlist = viewModel.playlist else { return }
        
        Task {
            try? await PlaylistManager.shared.copyPlaylist(
                // Convert PlaylistDetails to Playlist
                Playlist(
                    id: playlist.id,
                    name: playlist.name,
                    description: playlist.description,
                    isPublic: playlist.isPublic,
                    userId: playlist.userId,
                    userName: playlist.userName,
                    trackCount: playlist.trackCount,
                    createdAt: playlist.createdAt,
                    updatedAt: playlist.updatedAt,
                    imagePath: playlist.imagePath
                ),
                newName: "Copy of \(playlist.name)"
            )
        }
    }
}

struct PlaylistHeaderView: View {
    let playlist: PlaylistDetails
    
    var body: some View {
        VStack(spacing: 16) {
            if let imagePath = playlist.imagePath {
                AsyncImage(url: URL(string: imagePath)) { image in
                    image
                        .resizable()
                        .aspectRatio(contentMode: .fill)
                } placeholder: {
                    Rectangle()
                        .foregroundColor(.gray.opacity(0.3))
                        .overlay(
                            Image(systemName: "music.note.list")
                                .font(.system(size: 60))
                                .foregroundColor(.gray)
                        )
                }
                .frame(width: 200, height: 200)
                .cornerRadius(12)
            } else {
                Rectangle()
                    .foregroundColor(.gray.opacity(0.3))
                    .frame(width: 200, height: 200)
                    .cornerRadius(12)
                    .overlay(
                        Image(systemName: "music.note.list")
                            .font(.system(size: 60))
                            .foregroundColor(.gray)
                    )
            }
            
            VStack(spacing: 8) {
                Text(playlist.name)
                    .font(.title)
                    .fontWeight(.bold)
                    .multilineTextAlignment(.center)
                
                if let description = playlist.description, !description.isEmpty {
                    Text(description)
                        .font(.body)
                        .foregroundColor(.secondary)
                        .multilineTextAlignment(.center)
                }
                
                HStack {
                    Text("By \(playlist.userName)")
                        .font(.subheadline)
                        .foregroundColor(.secondary)
                    
                    if playlist.isPublic {
                        Image(systemName: "globe")
                            .font(.subheadline)
                            .foregroundColor(.blue)
                    }
                }
                
                Text("\(playlist.trackCount) tracks")
                    .font(.subheadline)
                    .foregroundColor(.secondary)
            }
        }
        .padding()
        .frame(maxWidth: .infinity)
    }
}

struct PlaylistTrackRow: View {
    let track: PlaylistTrack
    let position: Int
    let onPlay: () -> Void
    
    var body: some View {
        HStack {
            Text("\(position)")
                .font(.caption)
                .foregroundColor(.secondary)
                .frame(width: 20)
            
            if !track.albumId.isEmpty {
                AsyncImage(url: AudiarrAPIClient.shared.getAlbumCoverURL(for: track.albumId)) { image in
                    image
                        .resizable()
                        .aspectRatio(contentMode: .fill)
                } placeholder: {
                    Rectangle()
                        .foregroundColor(.gray.opacity(0.3))
                }
                .frame(width: 40, height: 40)
                .cornerRadius(4)
            }
            
            VStack(alignment: .leading, spacing: 4) {
                Text(track.title)
                    .font(.headline)
                    .lineLimit(1)
                
                Text("\(track.artistName) • \(track.albumTitle)")
                    .font(.caption)
                    .foregroundColor(.secondary)
                    .lineLimit(1)
            }
            
            Spacer()
            
            Text(formatDuration(track.durationMs))
                .font(.caption)
                .foregroundColor(.secondary)
        }
        .contentShape(Rectangle())
        .onTapGesture {
            onPlay()
        }
        .padding(.vertical, 4)
    }
    
    private func formatDuration(_ milliseconds: Int) -> String {
        let seconds = milliseconds / 1000
        let minutes = seconds / 60
        let remainingSeconds = seconds % 60
        return String(format: "%d:%02d", minutes, remainingSeconds)
    }
}
```

## Best Practices

### 1. Network Handling
- Implement retry logic with exponential backoff
- Cache responses where appropriate
- Handle offline mode gracefully
- Show loading states in UI

### 2. Security
- Use HTTPS in production
- Store tokens in Keychain
- Implement certificate pinning
- Never log sensitive data

### 3. Performance
- Lazy load images
- Implement pagination
- Cache album artwork
- Prefetch next track for gapless playback

### 4. User Experience
- Show buffering indicators
- Implement background audio
- Support AirPlay
- Handle interruptions (calls, etc.)

### 5. Error Handling
```swift
class ErrorHandler {
    static func handle(_ error: Error, in viewController: UIViewController) {
        let alert = UIAlertController(
            title: "Error",
            message: error.localizedDescription,
            preferredStyle: .alert
        )
        
        alert.addAction(UIAlertAction(title: "OK", style: .default))
        
        if let authError = error as? AuthError {
            switch authError {
            case .notAuthenticated, .refreshFailed:
                alert.addAction(UIAlertAction(title: "Login", style: .default) { _ in
                    // Navigate to login screen
                })
            default:
                break
            }
        }
        
        viewController.present(alert, animated: true)
    }
}
```

### 6. App Transport Security
For production, configure proper ATS exceptions:
```xml
<key>NSAppTransportSecurity</key>
<dict>
    <key>NSExceptionDomains</key>
    <dict>
        <key>your-server.com</key>
        <dict>
            <key>NSExceptionMinimumTLSVersion</key>
            <string>TLSv1.2</string>
            <key>NSExceptionRequiresForwardSecrecy</key>
            <true/>
            <key>NSIncludesSubdomains</key>
            <true/>
        </dict>
    </dict>
</dict>
```

## Testing

### Unit Tests
```swift
import XCTest
@testable import AudiarrClient

class AuthenticationTests: XCTestCase {
    func testLoginSuccess() async throws {
        let auth = AuthenticationManager(baseURL: "http://test-server")
        
        try await auth.login(username: "test", password: "password")
        
        XCTAssertTrue(auth.isAuthenticated)
        XCTAssertNotNil(auth.currentUser)
    }
    
    func testTokenRefresh() async throws {
        // Test token refresh logic
    }
}
```

### UI Tests
```swift
import XCTest

class AudiarrUITests: XCTestCase {
    func testLoginFlow() throws {
        let app = XCUIApplication()
        app.launch()
        
        app.textFields["Username"].tap()
        app.textFields["Username"].typeText("testuser")
        
        app.secureTextFields["Password"].tap()
        app.secureTextFields["Password"].typeText("password")
        
        app.buttons["Login"].tap()
        
        XCTAssertTrue(app.navigationBars["Library"].exists)
    }
}
```

## Deployment Checklist

- [ ] Replace development server URL with production URL
- [ ] Enable HTTPS and certificate validation
- [ ] Configure proper ATS settings
- [ ] Implement analytics
- [ ] Add crash reporting (e.g., Sentry)
- [ ] Test on various iOS versions
- [ ] Test on different device sizes
- [ ] Optimize for iPad if universal app
- [ ] Submit for App Store review