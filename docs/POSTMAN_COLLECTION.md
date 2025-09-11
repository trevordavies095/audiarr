# Audiarr Postman Collection

## Installation

1. Download [Postman](https://www.postman.com/downloads/)
2. Open Postman and click "Import"
3. Select "Raw text" and paste the JSON below
4. Click "Import"

## Environment Setup

Create a new environment with these variables:
- `baseUrl`: `http://localhost:8080`
- `accessToken`: (leave empty, will be set after login)
- `refreshToken`: (leave empty, will be set after login)
- `trackId`: (leave empty, will be set by requests)
- `albumId`: (leave empty, will be set by requests)
- `artistId`: (leave empty, will be set by requests)
- `playlistId`: (leave empty, will be set by requests)
- `queueId`: (leave empty, will be set by requests)
- `scanRequestId`: (leave empty, will be set by requests)

## Collection JSON

```json
{
  "info": {
    "name": "Audiarr API v2",
    "description": "Complete API collection for Audiarr music streaming server",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "auth": {
    "type": "bearer",
    "bearer": [
      {
        "key": "token",
        "value": "{{accessToken}}",
        "type": "string"
      }
    ]
  },
  "item": [
    {
      "name": "Authentication",
      "item": [
        {
          "name": "Login",
          "event": [
            {
              "listen": "test",
              "script": {
                "exec": [
                  "if (pm.response.code === 200) {",
                  "    const response = pm.response.json();",
                  "    pm.environment.set('accessToken', response.accessToken);",
                  "    pm.environment.set('refreshToken', response.refreshToken);",
                  "    console.log('Login successful, tokens saved');",
                  "}"
                ],
                "type": "text/javascript"
              }
            }
          ],
          "request": {
            "method": "POST",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"username\": \"admin\",\n  \"password\": \"admin\"\n}"
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/auth/login",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "auth", "login"]
            }
          }
        },
        {
          "name": "Refresh Token",
          "event": [
            {
              "listen": "test",
              "script": {
                "exec": [
                  "if (pm.response.code === 200) {",
                  "    const response = pm.response.json();",
                  "    pm.environment.set('accessToken', response.accessToken);",
                  "    pm.environment.set('refreshToken', response.refreshToken);",
                  "}"
                ],
                "type": "text/javascript"
              }
            }
          ],
          "request": {
            "method": "POST",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"refreshToken\": \"{{refreshToken}}\"\n}"
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/auth/refresh",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "auth", "refresh"]
            }
          }
        },
        {
          "name": "Get Current User",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/auth/me",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "auth", "me"]
            }
          }
        },
        {
          "name": "Logout",
          "request": {
            "method": "POST",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"refreshToken\": \"{{refreshToken}}\"\n}"
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/auth/logout",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "auth", "logout"]
            }
          }
        },
        {
          "name": "Get Current User",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/auth/me",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "auth", "me"]
            }
          }
        },
        {
          "name": "Change Password",
          "event": [
            {
              "listen": "test",
              "script": {
                "exec": [
                  "pm.test('Status code is 200', function () {",
                  "    pm.response.to.have.status(200);",
                  "});",
                  "",
                  "if (pm.response.code === 200) {",
                  "    const response = pm.response.json();",
                  "    console.log('Password changed successfully:', response.message);",
                  "}"
                ],
                "type": "text/javascript"
              }
            }
          ],
          "request": {
            "method": "POST",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"currentPassword\": \"admin\",\n  \"newPassword\": \"NewSecurePass123!\"\n}"
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/auth/change-password",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "auth", "change-password"]
            }
          }
        }
      ]
    },
    {
      "name": "Artists",
      "item": [
        {
          "name": "Get All Artists",
          "event": [
            {
              "listen": "test",
              "script": {
                "exec": [
                  "if (pm.response.code === 200) {",
                  "    const response = pm.response.json();",
                  "    if (response.data && response.data.length > 0) {",
                  "        pm.environment.set('artistId', response.data[0].id);",
                  "    }",
                  "}"
                ],
                "type": "text/javascript"
              }
            }
          ],
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/artists?page=1&limit=50",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "artists"],
              "query": [
                { "key": "page", "value": "1" },
                { "key": "limit", "value": "50" }
              ]
            }
          }
        },
        {
          "name": "Get Artist by ID",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/artists/{{artistId}}",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "artists", "{{artistId}}"]
            }
          }
        },
        {
          "name": "Get Artist Albums",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/artists/{{artistId}}/albums",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "artists", "{{artistId}}", "albums"]
            }
          }
        },
        {
          "name": "Get Artist Tracks",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/artists/{{artistId}}/tracks",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "artists", "{{artistId}}", "tracks"]
            }
          }
        }
      ]
    },
    {
      "name": "Albums",
      "item": [
        {
          "name": "Get All Albums",
          "event": [
            {
              "listen": "test",
              "script": {
                "exec": [
                  "if (pm.response.code === 200) {",
                  "    const response = pm.response.json();",
                  "    if (response.data && response.data.length > 0) {",
                  "        pm.environment.set('albumId', response.data[0].id);",
                  "    }",
                  "}"
                ],
                "type": "text/javascript"
              }
            }
          ],
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/albums?page=1&limit=50",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "albums"],
              "query": [
                { "key": "page", "value": "1" },
                { "key": "limit", "value": "50" }
              ]
            }
          }
        },
        {
          "name": "Get Album by ID",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/albums/{{albumId}}",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "albums", "{{albumId}}"]
            }
          }
        },
        {
          "name": "Get Album Tracks",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/albums/{{albumId}}/tracks",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "albums", "{{albumId}}", "tracks"]
            }
          }
        },
        {
          "name": "Get Album Cover",
          "request": {
            "auth": { "type": "noauth" },
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/albums/{{albumId}}/cover",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "albums", "{{albumId}}", "cover"]
            }
          }
        },
        {
          "name": "Get Recent Albums",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/albums/recent?limit=20",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "albums", "recent"],
              "query": [{ "key": "limit", "value": "20" }]
            }
          }
        }
      ]
    },
    {
      "name": "Tracks",
      "item": [
        {
          "name": "Get All Tracks",
          "event": [
            {
              "listen": "test",
              "script": {
                "exec": [
                  "if (pm.response.code === 200) {",
                  "    const response = pm.response.json();",
                  "    if (response.data && response.data.length > 0) {",
                  "        pm.environment.set('trackId', response.data[0].id);",
                  "    }",
                  "}"
                ],
                "type": "text/javascript"
              }
            }
          ],
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/tracks?page=1&limit=50",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "tracks"],
              "query": [
                { "key": "page", "value": "1" },
                { "key": "limit", "value": "50" }
              ]
            }
          }
        },
        {
          "name": "Get Track by ID",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/tracks/{{trackId}}",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "tracks", "{{trackId}}"]
            }
          }
        },
        {
          "name": "Stream Track",
          "request": {
            "auth": { "type": "noauth" },
            "method": "GET",
            "header": [
              {
                "key": "Range",
                "value": "bytes=0-1048575",
                "disabled": true
              }
            ],
            "url": {
              "raw": "{{baseUrl}}/api/v2/tracks/{{trackId}}/stream",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "tracks", "{{trackId}}", "stream"]
            }
          }
        },
        {
          "name": "Download Track",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/tracks/{{trackId}}/download",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "tracks", "{{trackId}}", "download"]
            }
          }
        },
        {
          "name": "Update Play Count",
          "request": {
            "method": "POST",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/tracks/{{trackId}}/play",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "tracks", "{{trackId}}", "play"]
            }
          }
        },
        {
          "name": "Get Popular Tracks",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/tracks/popular?limit=50",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "tracks", "popular"],
              "query": [{ "key": "limit", "value": "50" }]
            }
          }
        },
        {
          "name": "Get Recently Played",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/tracks/recent?limit=20",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "tracks", "recent"],
              "query": [{ "key": "limit", "value": "20" }]
            }
          }
        }
      ]
    },
    {
      "name": "Search",
      "item": [
        {
          "name": "Basic Search",
          "request": {
            "auth": { "type": "noauth" },
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/search?q=pink&limit=5",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "search"],
              "query": [
                { "key": "q", "value": "pink" },
                { "key": "limit", "value": "5" }
              ]
            }
          }
        },
        {
          "name": "Advanced Search",
          "request": {
            "method": "POST",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"title\": \"money\",\n  \"artist\": \"pink\",\n  \"genre\": \"rock\",\n  \"yearFrom\": 1970,\n  \"yearTo\": 1980,\n  \"page\": 1,\n  \"pageSize\": 50\n}"
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/search/advanced",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "search", "advanced"]
            }
          }
        },
        {
          "name": "Search Suggestions",
          "request": {
            "auth": { "type": "noauth" },
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/search/suggestions?q=pin",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "search", "suggestions"],
              "query": [{ "key": "q", "value": "pin" }]
            }
          }
        }
      ]
    },
    {
      "name": "System",
      "item": [
        {
          "name": "Health Check",
          "request": {
            "auth": { "type": "noauth" },
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/health",
              "host": ["{{baseUrl}}"],
              "path": ["health"]
            }
          }
        },
        {
          "name": "API Info",
          "request": {
            "auth": { "type": "noauth" },
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/info",
              "host": ["{{baseUrl}}"],
              "path": ["api", "info"]
            }
          }
        }
      ]
    },
    {
      "name": "Playlists",
      "item": [
        {
          "name": "Get User Playlists",
          "event": [
            {
              "listen": "test",
              "script": {
                "exec": [
                  "if (pm.response.code === 200) {",
                  "    const response = pm.response.json();",
                  "    if (response.data && response.data.length > 0) {",
                  "        pm.environment.set('playlistId', response.data[0].id);",
                  "        console.log('Saved playlist ID:', response.data[0].id);",
                  "    }",
                  "}"
                ]
              }
            }
          ],
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/playlists?page=1&limit=10",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "playlists"],
              "query": [
                { "key": "page", "value": "1" },
                { "key": "limit", "value": "10" },
                { "key": "includePublic", "value": "false" }
              ]
            }
          }
        },
        {
          "name": "Get Playlist Details",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/playlists/{{playlistId}}",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "playlists", "{{playlistId}}"]
            }
          }
        },
        {
          "name": "Create Playlist",
          "event": [
            {
              "listen": "test",
              "script": {
                "exec": [
                  "if (pm.response.code === 201) {",
                  "    const response = pm.response.json();",
                  "    pm.environment.set('playlistId', response.id);",
                  "    console.log('Created playlist ID:', response.id);",
                  "}"
                ]
              }
            }
          ],
          "request": {
            "method": "POST",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"name\": \"Test Playlist\",\n  \"description\": \"Created via Postman\",\n  \"isPublic\": false,\n  \"initialTrackIds\": [\"{{trackId}}\"]\n}"
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/playlists",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "playlists"]
            }
          }
        },
        {
          "name": "Update Playlist",
          "request": {
            "method": "PUT",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"name\": \"Updated Test Playlist\",\n  \"description\": \"Updated via Postman\",\n  \"isPublic\": true\n}"
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/playlists/{{playlistId}}",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "playlists", "{{playlistId}}"]
            }
          }
        },
        {
          "name": "Add Tracks to Playlist",
          "request": {
            "method": "POST",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"trackIds\": [\"{{trackId}}\"],\n  \"position\": 0\n}"
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/playlists/{{playlistId}}/tracks",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "playlists", "{{playlistId}}", "tracks"]
            }
          }
        },
        {
          "name": "Remove Tracks from Playlist",
          "request": {
            "method": "DELETE",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"trackIds\": [\"{{trackId}}\"]\n}"
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/playlists/{{playlistId}}/tracks",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "playlists", "{{playlistId}}", "tracks"]
            }
          }
        },
        {
          "name": "Reorder Playlist Tracks",
          "request": {
            "method": "PUT",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"tracks\": [\n    {\n      \"trackId\": \"{{trackId}}\",\n      \"newPosition\": 1.5\n    }\n  ]\n}"
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/playlists/{{playlistId}}/tracks/reorder",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "playlists", "{{playlistId}}", "tracks", "reorder"]
            }
          }
        },
        {
          "name": "Get Public Playlists",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/playlists/public?page=1&limit=10",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "playlists", "public"],
              "query": [
                { "key": "page", "value": "1" },
                { "key": "limit", "value": "10" }
              ]
            }
          }
        },
        {
          "name": "Delete Playlist",
          "request": {
            "method": "DELETE",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/playlists/{{playlistId}}",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "playlists", "{{playlistId}}"]
            }
          }
        },
        {
          "name": "Get Playlist Play Context",
          "event": [
            {
              "listen": "test",
              "script": {
                "exec": [
                  "pm.test('Status code is 200', function () {",
                  "    pm.response.to.have.status(200);",
                  "});",
                  "",
                  "if (pm.response.code === 200) {",
                  "    const response = pm.response.json();",
                  "    console.log('Playlist:', response.playlist.name);",
                  "    console.log('Track count:', response.tracks.length);",
                  "    console.log('Total duration (ms):', response.totalDurationMs);",
                  "    ",
                  "    // Validate response structure",
                  "    pm.test('Response has playlist object', function () {",
                  "        pm.expect(response).to.have.property('playlist');",
                  "        pm.expect(response.playlist).to.have.property('id');",
                  "        pm.expect(response.playlist).to.have.property('name');",
                  "    });",
                  "    ",
                  "    pm.test('Response has tracks array', function () {",
                  "        pm.expect(response).to.have.property('tracks');",
                  "        pm.expect(response.tracks).to.be.an('array');",
                  "    });",
                  "    ",
                  "    if (response.tracks.length > 0) {",
                  "        pm.test('First track has required fields', function () {",
                  "            const track = response.tracks[0];",
                  "            pm.expect(track).to.have.property('id');",
                  "            pm.expect(track).to.have.property('streamUrl');",
                  "            pm.expect(track).to.have.property('position');",
                  "        });",
                  "    }",
                  "}"
                ],
                "type": "text/javascript"
              }
            }
          ],
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/playlists/{{playlistId}}/play",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "playlists", "{{playlistId}}", "play"]
            }
          }
        }
      ]
    },
    {
      "name": "Queue",
      "item": [
        {
          "name": "Get Queue State",
          "event": [
            {
              "listen": "test",
              "script": {
                "exec": [
                  "if (pm.response.code === 200) {",
                  "    const response = pm.response.json();",
                  "    pm.environment.set('queueId', response.queueId);",
                  "    console.log('Queue ID saved:', response.queueId);",
                  "}"
                ],
                "type": "text/javascript"
              }
            }
          ],
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/queue",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "queue"]
            }
          }
        },
        {
          "name": "Add Tracks to Queue",
          "request": {
            "method": "POST",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"trackIds\": [\"{{trackId}}\"],\n  \"source\": \"postman_test\",\n  \"playNext\": false\n}"
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/queue/tracks",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "queue", "tracks"]
            }
          }
        },
        {
          "name": "Remove Track from Queue",
          "request": {
            "method": "DELETE",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/queue/tracks/0",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "queue", "tracks", "0"]
            }
          }
        },
        {
          "name": "Clear Queue",
          "request": {
            "method": "DELETE",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/queue/clear?keepCurrentTrack=false",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "queue", "clear"],
              "query": [
                {
                  "key": "keepCurrentTrack",
                  "value": "false"
                }
              ]
            }
          }
        },
        {
          "name": "Reorder Queue",
          "request": {
            "method": "PUT",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"trackId\": \"{{trackId}}\",\n  \"newIndex\": 0\n}"
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/queue/reorder",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "queue", "reorder"]
            }
          }
        },
        {
          "name": "Update Queue Settings",
          "request": {
            "method": "PUT",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"repeatMode\": 2,\n  \"isShuffled\": true,\n  \"currentIndex\": 0\n}"
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/queue/settings",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "queue", "settings"]
            }
          }
        },
        {
          "name": "Replace Queue",
          "request": {
            "method": "POST",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"trackIds\": [\"{{trackId}}\"],\n  \"startIndex\": 0,\n  \"source\": \"postman_replace\"\n}"
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/queue/replace",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "queue", "replace"]
            }
          }
        },
        {
          "name": "Next Track",
          "request": {
            "method": "POST",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/queue/next",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "queue", "next"]
            }
          }
        },
        {
          "name": "Previous Track",
          "request": {
            "method": "POST",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/queue/previous",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "queue", "previous"]
            }
          }
        },
        {
          "name": "Jump to Position",
          "request": {
            "method": "PUT",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/queue/position/1",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "queue", "position", "1"]
            }
          }
        }
      ]
    },
    {
      "name": "Library Scanner",
      "item": [
        {
          "name": "Get Supported Formats",
          "request": {
            "method": "GET",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/scanner/supported-formats",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "scanner", "supported-formats"]
            }
          }
        },
        {
          "name": "Queue Library Scan",
          "event": [
            {
              "listen": "test",
              "script": {
                "exec": [
                  "if (pm.response.code === 202) {",
                  "    const response = pm.response.json();",
                  "    pm.environment.set('scanRequestId', response.requestId);",
                  "    console.log('Scan queued, request ID:', response.requestId);",
                  "}"
                ],
                "type": "text/javascript"
              }
            }
          ],
          "request": {
            "method": "POST",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": "{\n  \"libraryPath\": \"/path/to/music/library\",\n  \"requestId\": \"scan_{{$randomUUID}}\"\n}"
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/scanner/scan",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "scanner", "scan"]
            }
          }
        },
        {
          "name": "Scan Single File",
          "request": {
            "method": "POST",
            "header": [],
            "url": {
              "raw": "{{baseUrl}}/api/v2/scanner/scan/single?filePath=/path/to/song.mp3",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "scanner", "scan", "single"],
              "query": [
                {
                  "key": "filePath",
                  "value": "/path/to/song.mp3",
                  "description": "Full path to the audio file to scan"
                }
              ]
            }
          }
        }
      ]
    },
    {
      "name": "Diagnostics",
      "item": [
        {
          "name": "Database Data Check",
          "event": [
            {
              "listen": "test",
              "script": {
                "exec": [
                  "if (pm.response.code === 200) {",
                  "    const response = pm.response.json();",
                  "    console.log('Database diagnostic completed');",
                  "    console.log('Total artists:', response.totalCounts.artistCount);",
                  "    console.log('Total albums:', response.totalCounts.albumCount);",
                  "    console.log('Total tracks:', response.totalCounts.trackCount);",
                  "    console.log('Duplicate artists found:', response.duplicateArtists.length);",
                  "    console.log('Duplicate albums found:', response.duplicateAlbums.length);",
                  "    ",
                  "    // Log duplicate details if any found",
                  "    if (response.duplicateArtists.length > 0) {",
                  "        console.log('--- Duplicate Artists ---');",
                  "        response.duplicateArtists.forEach(dup => {",
                  "            console.log(`${dup.name}: ${dup.count} duplicates (IDs: ${dup.ids.join(', ')})`);",
                  "        });",
                  "    }",
                  "    ",
                  "    if (response.duplicateAlbums.length > 0) {",
                  "        console.log('--- Duplicate Albums ---');",
                  "        response.duplicateAlbums.forEach(dup => {",
                  "            console.log(`${dup.title} (Artist ID ${dup.artistId}): ${dup.count} duplicates`);",
                  "        });",
                  "    }",
                  "}"
                ],
                "type": "text/javascript"
              }
            }
          ],
          "request": {
            "method": "GET",
            "header": [
              {
                "key": "Accept",
                "value": "application/json"
              }
            ],
            "url": {
              "raw": "{{baseUrl}}/api/v2/diagnostic/data-check",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "diagnostic", "data-check"]
            }
          },
          "response": []
        }
      ]
    },
    {
      "name": "Data Cleanup",
      "item": [
        {
          "name": "Merge Duplicate Artists",
          "event": [
            {
              "listen": "test",
              "script": {
                "exec": [
                  "if (pm.response.code === 200) {",
                  "    const response = pm.response.json();",
                  "    console.log('Artist cleanup completed:', response.message);",
                  "    console.log('Duplicate groups found:', response.duplicateGroupsFound);",
                  "}"
                ],
                "type": "text/javascript"
              }
            }
          ],
          "request": {
            "method": "POST",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": ""
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/cleanup/merge-duplicate-artists",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "cleanup", "merge-duplicate-artists"]
            }
          }
        },
        {
          "name": "Merge Duplicate Albums",
          "event": [
            {
              "listen": "test",
              "script": {
                "exec": [
                  "if (pm.response.code === 200) {",
                  "    const response = pm.response.json();",
                  "    console.log('Album cleanup completed:', response.message);",
                  "    console.log('Duplicate groups found:', response.duplicateGroupsFound);",
                  "}"
                ],
                "type": "text/javascript"
              }
            }
          ],
          "request": {
            "method": "POST",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": ""
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/cleanup/merge-duplicate-albums",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "cleanup", "merge-duplicate-albums"]
            }
          }
        },
        {
          "name": "Clean All Data",
          "event": [
            {
              "listen": "test",
              "script": {
                "exec": [
                  "if (pm.response.code === 200) {",
                  "    const response = pm.response.json();",
                  "    console.log('Comprehensive cleanup completed:', response.message);",
                  "    console.log('Artists merged:', response.artistsMerged);",
                  "    console.log('Albums merged:', response.albumsMerged);",
                  "    console.log('Artist groups found:', response.duplicateArtistGroupsFound);",
                  "    console.log('Album groups found:', response.duplicateAlbumGroupsFound);",
                  "}"
                ],
                "type": "text/javascript"
              }
            }
          ],
          "request": {
            "method": "POST",
            "header": [
              {
                "key": "Content-Type",
                "value": "application/json"
              }
            ],
            "body": {
              "mode": "raw",
              "raw": ""
            },
            "url": {
              "raw": "{{baseUrl}}/api/v2/cleanup/clean-all",
              "host": ["{{baseUrl}}"],
              "path": ["api", "v2", "cleanup", "clean-all"]
            }
          }
        }
      ]
    }
  ]
}
```

## Usage Instructions

1. **First Time Setup**
   - Import the collection into Postman
   - Create and select the environment
   - Set the `baseUrl` variable to your server address

2. **Authentication Flow**
   - Start with the "Login" request in the Authentication folder
   - The test script will automatically save tokens to environment variables
   - All authenticated requests will use the saved access token

3. **Testing Workflow**
   - Login first to get tokens
   - Run "Get All Artists" - this saves the first artist ID
   - Run "Get All Albums" - this saves the first album ID
   - Run "Get All Tracks" - this saves the first track ID
   - Now you can test all ID-based endpoints

4. **Database Diagnostics**
   - Use "Database Data Check" in the Diagnostics folder to get library overview
   - Review console output for detailed duplicate analysis
   - No authentication required (but should be restricted in production)
   - Helpful for planning cleanup operations

5. **Token Refresh**
   - When your access token expires (after 60 minutes)
   - Run the "Refresh Token" request
   - New tokens will be automatically saved

5. **Streaming Audio**
   - The "Stream Track" request returns audio data
   - Enable the Range header to test seeking
   - Use Postman's "Send and Download" to save audio files

## Advanced Features

### Pre-request Scripts
Add this to collection pre-request scripts for automatic token refresh:

```javascript
const accessToken = pm.environment.get("accessToken");
const tokenExpiry = pm.environment.get("tokenExpiry");

if (accessToken && tokenExpiry) {
    const now = new Date().getTime();
    const expiry = new Date(tokenExpiry).getTime();
    
    if (expiry - now < 60000) { // Less than 1 minute left
        console.log("Token expiring soon, consider refreshing");
    }
}
```

### Test Assertions
Add to any request's test script:

```javascript
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});

pm.test("Response time is less than 500ms", function () {
    pm.expect(pm.response.responseTime).to.be.below(500);
});

pm.test("Response has correct content type", function () {
    pm.expect(pm.response.headers.get("Content-Type")).to.include("application/json");
});
```

### Environment Variables for Different Servers

Create multiple environments:
- **Local**: `baseUrl = http://localhost:8080`
- **Docker**: `baseUrl = http://audiarr:8080`
- **Production**: `baseUrl = https://music.yourdomain.com`

Switch between environments using the environment dropdown in Postman.