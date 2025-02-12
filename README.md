# Audiarr

Audiarr is a selfhostable, lightweight music server that enables users to listen to their music from anywhere in the world. The primary goal of this project is to provide a robust backend that can efficiently manage and stream your personal music library. While the focus is on backend functionality, a very basic client has been developed to test the core features. In the future, a more polished and fully featured UI may be added.

## Features

- **Selfhostable & Lightweight:**  
  Designed to run on your own hardware, Audiarr is optimized for performance and minimal resource usage. Docker to eventually come if I can figure out a path for this project. 

- **Music Library Management:**  
  The server scans a specified directory for audio files, extracts metadata (such as track title, artist, album, release year, genre, track number, duration, and more), and stores the information in a SQLite database. It also supports synchronizing changes by adding new tracks and removing deleted ones.

- **Streaming:**  
  Users can stream music files over HTTP using range processing to support seeking.

- **Basic Client for Testing:**  
  A very basic [web client](https://github.com/trevordavies095/audiarr-client) is included to test and demonstrate the backend functionality. This client currently allows you to:
  - Connect to the server (locally or remotely).
  - View your music library in a simple table format.
  - Play tracks by double-clicking on them.
  - Initiate a scan to synchronize changes.

## Current Status

- **Backend First:**  
  The primary focus is on building and refining the backend functionality. The server is built with ASP.NET Core and uses Entity Framework Core with SQLite for data storage.
  
- **Basic Client:**  
  A rudimentary React-based [client](https://github.com/trevordavies095/audiarr-client) is available for testing purposes. This client allows you to interact with the server and validate its features. A more fully featured, polished UI may be developed in the future, but for now, the emphasis remains on the server’s core capabilities.

## Get Started

1. Run the Server
Use Docker Compose to start the Audiarr server:
```bash
docker-compose up -d
```

2. Scan Your Music Library
After starting the server, initiate a library scan to index your music files:
```bash
curl -X POST http://localhost:5279/api/library/scan
```

3. Connect to the Server (Optional: Using Audiarr Client)
If you're using Audiarr Client:
- Navigate to the client in your browser
- Input the server URL (e.g., http://localhost:5279)
- Start browsing and playing your music!

## API Endpoints

You can view endpoints by navigating to `http://localhost:5279/Swagger`
