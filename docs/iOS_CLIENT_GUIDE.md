# Audiarr iOS Client Development Guide

## Table of Contents
1. [Project Setup](#project-setup)
2. [Authentication Manager](#authentication-manager)
3. [API Client](#api-client)
4. [Models](#models)
5. [Audio Streaming](#audio-streaming)
6. [SignalR Integration](#signalr-integration)
7. [Offline Support](#offline-support)
8. [UI Components](#ui-components)
9. [Best Practices](#best-practices)

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
```

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