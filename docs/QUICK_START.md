# Audiarr Quick Start Guide

Get up and running with Audiarr in 5 minutes!

## Prerequisites

- Docker installed on your system
- A folder with music files (MP3, FLAC, etc.)
- 5 minutes of your time

## 1. Start the Server (2 minutes)

### Option A: Using Docker (Recommended)

```bash
# Pull the latest image
docker pull ghcr.io/yourusername/audiarr:latest

# Run the container
docker run -d \
  --name audiarr \
  -p 8080:8080 \
  -v /path/to/your/music:/music:ro \
  -v audiarr_data:/data \
  ghcr.io/yourusername/audiarr:latest
```

### Option B: Using Docker Compose

Create a `docker-compose.yml`:

```yaml
services:
  audiarr:
    image: ghcr.io/yourusername/audiarr:latest
    ports:
      - "8080:8080"
    volumes:
      - /path/to/your/music:/music:ro
      - audiarr_data:/data
    restart: unless-stopped

volumes:
  audiarr_data:
```

Then run:
```bash
docker-compose up -d
```

## 2. Verify Server is Running (30 seconds)

Check the server is healthy:

```bash
curl http://localhost:8080/health
# Should return: OK

curl http://localhost:8080/api/info
# Should return API information
```

Or open in browser: http://localhost:8080

## 3. Login and Get Token (1 minute)

Default credentials:
- Username: `admin`
- Password: `admin`

### Using curl:

```bash
# Login
curl -X POST http://localhost:8080/api/v2/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin"}'

# Save the accessToken from the response
```

### Using HTTPie (if installed):

```bash
http POST localhost:8080/api/v2/auth/login \
  username=admin password=admin
```

### Response:
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "d2f8c3a9-...",
  "expiresAt": "2024-01-15T10:30:00Z",
  "user": {
    "id": "user_id",
    "username": "admin",
    "email": "admin@example.com",
    "role": "Admin"
  }
}
```

## 4. Scan Your Music Library (1 minute)

Start scanning your music:

```bash
# Replace TOKEN with your accessToken
curl -X POST http://localhost:8080/api/v2/scan/start \
  -H "Authorization: Bearer TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"path":"/music"}'
```

Check scan status:

```bash
curl http://localhost:8080/api/v2/scan/status \
  -H "Authorization: Bearer TOKEN"
```

## 5. Play Your First Track (30 seconds)

### Get all tracks:

```bash
curl http://localhost:8080/api/v2/tracks \
  -H "Authorization: Bearer TOKEN"
```

### Stream a track:

```bash
# Get track ID from previous response
# No authentication needed for streaming
curl http://localhost:8080/api/v2/tracks/TRACK_ID/stream \
  --output song.mp3
```

Or open in your browser:
```
http://localhost:8080/api/v2/tracks/TRACK_ID/stream
```

## Complete Example Script

Save this as `audiarr-test.sh`:

```bash
#!/bin/bash

BASE_URL="http://localhost:8080"

# Health check
echo "Checking server health..."
curl -s "$BASE_URL/health"
echo ""

# Login
echo "Logging in..."
RESPONSE=$(curl -s -X POST "$BASE_URL/api/v2/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin"}')

TOKEN=$(echo $RESPONSE | grep -o '"accessToken":"[^"]*' | cut -d'"' -f4)
echo "Got token: ${TOKEN:0:20}..."

# Start scan
echo "Starting library scan..."
curl -s -X POST "$BASE_URL/api/v2/scan/start" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"path":"/music"}'

# Wait a bit
sleep 5

# Get tracks
echo "Getting tracks..."
curl -s "$BASE_URL/api/v2/tracks?limit=5" \
  -H "Authorization: Bearer $TOKEN" | jq '.data[].title'

echo "Setup complete!"
```

Run it:
```bash
chmod +x audiarr-test.sh
./audiarr-test.sh
```

## Quick API Examples

### Search for Music

```bash
# Search across all entities
curl "http://localhost:8080/api/v2/search?q=rock"

# Get search suggestions
curl "http://localhost:8080/api/v2/search/suggestions?q=beat"
```

### Browse Your Library

```bash
# Get all artists
curl http://localhost:8080/api/v2/artists \
  -H "Authorization: Bearer TOKEN"

# Get albums by artist
curl http://localhost:8080/api/v2/artists/ARTIST_ID/albums \
  -H "Authorization: Bearer TOKEN"

# Get album with tracks
curl http://localhost:8080/api/v2/albums/ALBUM_ID \
  -H "Authorization: Bearer TOKEN"
```

### Stream Music

```bash
# Stream with range support (for seeking)
curl http://localhost:8080/api/v2/tracks/TRACK_ID/stream \
  -H "Range: bytes=0-1048575" \
  --output partial.mp3

# Get album cover art
curl http://localhost:8080/api/v2/albums/ALBUM_ID/cover \
  --output cover.jpg
```

## Python Quick Start

```python
import requests
import json

# Configuration
BASE_URL = "http://localhost:8080"
USERNAME = "admin"
PASSWORD = "admin"

# Login
def login():
    response = requests.post(
        f"{BASE_URL}/api/v2/auth/login",
        json={"username": USERNAME, "password": PASSWORD}
    )
    return response.json()["accessToken"]

# Get tracks
def get_tracks(token):
    headers = {"Authorization": f"Bearer {token}"}
    response = requests.get(f"{BASE_URL}/api/v2/tracks", headers=headers)
    return response.json()

# Stream a track
def stream_track(track_id, output_file):
    response = requests.get(f"{BASE_URL}/api/v2/tracks/{track_id}/stream")
    with open(output_file, 'wb') as f:
        f.write(response.content)

# Main
if __name__ == "__main__":
    token = login()
    print(f"Logged in! Token: {token[:20]}...")
    
    tracks = get_tracks(token)
    print(f"Found {tracks['total']} tracks")
    
    if tracks['data']:
        first_track = tracks['data'][0]
        print(f"First track: {first_track['title']} by {first_track['artistName']}")
        
        # Download it
        stream_track(first_track['id'], "first_track.mp3")
        print("Downloaded first track to first_track.mp3")
```

## JavaScript/Node.js Quick Start

```javascript
const axios = require('axios');
const fs = require('fs');

const BASE_URL = 'http://localhost:8080';
let accessToken = '';

// Login
async function login() {
    const response = await axios.post(`${BASE_URL}/api/v2/auth/login`, {
        username: 'admin',
        password: 'admin'
    });
    accessToken = response.data.accessToken;
    console.log('Logged in!');
}

// Get tracks
async function getTracks() {
    const response = await axios.get(`${BASE_URL}/api/v2/tracks`, {
        headers: { Authorization: `Bearer ${accessToken}` }
    });
    return response.data;
}

// Stream track
async function streamTrack(trackId, outputFile) {
    const response = await axios.get(
        `${BASE_URL}/api/v2/tracks/${trackId}/stream`,
        { responseType: 'stream' }
    );
    response.data.pipe(fs.createWriteStream(outputFile));
}

// Main
async function main() {
    await login();
    
    const tracks = await getTracks();
    console.log(`Found ${tracks.total} tracks`);
    
    if (tracks.data.length > 0) {
        const firstTrack = tracks.data[0];
        console.log(`First track: ${firstTrack.title}`);
        
        await streamTrack(firstTrack.id, 'first_track.mp3');
        console.log('Downloaded first track');
    }
}

main().catch(console.error);
```

## Web Player Example

Create an `index.html`:

```html
<!DOCTYPE html>
<html>
<head>
    <title>Audiarr Player</title>
</head>
<body>
    <h1>Audiarr Quick Player</h1>
    
    <div>
        <input type="text" id="username" placeholder="Username" value="admin">
        <input type="password" id="password" placeholder="Password" value="admin">
        <button onclick="login()">Login</button>
    </div>
    
    <div id="player" style="display:none">
        <h2>Tracks</h2>
        <ul id="tracks"></ul>
        <audio id="audio" controls></audio>
    </div>

    <script>
        const BASE_URL = 'http://localhost:8080';
        let accessToken = '';

        async function login() {
            const username = document.getElementById('username').value;
            const password = document.getElementById('password').value;
            
            const response = await fetch(`${BASE_URL}/api/v2/auth/login`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ username, password })
            });
            
            const data = await response.json();
            accessToken = data.accessToken;
            
            document.getElementById('player').style.display = 'block';
            loadTracks();
        }

        async function loadTracks() {
            const response = await fetch(`${BASE_URL}/api/v2/tracks`, {
                headers: { Authorization: `Bearer ${accessToken}` }
            });
            
            const data = await response.json();
            const tracksList = document.getElementById('tracks');
            
            data.data.forEach(track => {
                const li = document.createElement('li');
                li.innerHTML = `
                    <a href="#" onclick="playTrack('${track.id}'); return false;">
                        ${track.title} - ${track.artistName}
                    </a>
                `;
                tracksList.appendChild(li);
            });
        }

        function playTrack(trackId) {
            const audio = document.getElementById('audio');
            audio.src = `${BASE_URL}/api/v2/tracks/${trackId}/stream`;
            audio.play();
        }
    </script>
</body>
</html>
```

Open this file in your browser and you have a working music player!

## Troubleshooting

### Server won't start
- Check port 8080 is not in use: `lsof -i :8080`
- Check Docker logs: `docker logs audiarr`

### Can't login
- Default credentials: admin/admin
- Check server is running: `curl http://localhost:8080/health`

### No music after scan
- Check music folder is mounted correctly
- Check scan status: `/api/v2/scan/status`
- Check for scan errors in logs

### Playback issues
- Ensure track exists: `/api/v2/tracks/{id}`
- Check file permissions in music folder
- Try downloading instead of streaming first

## Next Steps

1. **Change default password**: Use `/api/v2/auth/change-password`
2. **Configure HTTPS**: Set up a reverse proxy with SSL
3. **Read full documentation**: Check `/docs` folder
4. **Build a client**: Use the API to build your own music player
5. **Join the community**: Report issues and contribute on GitHub

## Useful Commands

```bash
# Stop the server
docker stop audiarr

# Start the server
docker start audiarr

# View logs
docker logs -f audiarr

# Update to latest version
docker pull ghcr.io/yourusername/audiarr:latest
docker stop audiarr
docker rm audiarr
# Then run the docker run command again

# Backup data
docker run --rm -v audiarr_data:/data -v $(pwd):/backup alpine tar czf /backup/audiarr-backup.tar.gz /data

# Restore data
docker run --rm -v audiarr_data:/data -v $(pwd):/backup alpine tar xzf /backup/audiarr-backup.tar.gz -C /
```

## That's It!

You now have a working Audiarr server with:
- ✅ Server running
- ✅ Music library scanned
- ✅ API authentication working
- ✅ Music streaming enabled

Time to build something awesome! 🎵