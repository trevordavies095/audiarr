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

4. **Token Refresh**
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