CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;
CREATE TABLE "MusicTracks" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_MusicTracks" PRIMARY KEY AUTOINCREMENT,
    "FilePath" TEXT NOT NULL,
    "TrackTitle" TEXT NOT NULL,
    "Artist" TEXT NOT NULL,
    "AlbumName" TEXT NOT NULL,
    "AlbumArtist" TEXT NOT NULL,
    "ReleaseYear" INTEGER NULL,
    "Genre" TEXT NOT NULL,
    "TrackNumber" INTEGER NULL,
    "Duration" TEXT NOT NULL,
    "FileFormat" TEXT NOT NULL,
    "Bitrate" INTEGER NOT NULL,
    "FileSize" INTEGER NOT NULL,
    "MusicBrainzId" TEXT NOT NULL,
    "ReleaseType" TEXT NOT NULL,
    "AlbumType" TEXT NOT NULL
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250205211814_InitialCreate', '9.0.1');

CREATE TABLE "ef_temp_MusicTracks" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_MusicTracks" PRIMARY KEY AUTOINCREMENT,
    "AlbumArtist" TEXT NULL,
    "AlbumName" TEXT NULL,
    "AlbumType" TEXT NULL,
    "Artist" TEXT NULL,
    "Bitrate" INTEGER NULL,
    "Duration" TEXT NULL,
    "FileFormat" TEXT NULL,
    "FilePath" TEXT NOT NULL,
    "FileSize" INTEGER NULL,
    "Genre" TEXT NULL,
    "MusicBrainzId" TEXT NULL,
    "ReleaseType" TEXT NULL,
    "ReleaseYear" INTEGER NULL,
    "TrackNumber" INTEGER NULL,
    "TrackTitle" TEXT NULL
);

INSERT INTO "ef_temp_MusicTracks" ("Id", "AlbumArtist", "AlbumName", "AlbumType", "Artist", "Bitrate", "Duration", "FileFormat", "FilePath", "FileSize", "Genre", "MusicBrainzId", "ReleaseType", "ReleaseYear", "TrackNumber", "TrackTitle")
SELECT "Id", "AlbumArtist", "AlbumName", "AlbumType", "Artist", "Bitrate", "Duration", "FileFormat", "FilePath", "FileSize", "Genre", "MusicBrainzId", "ReleaseType", "ReleaseYear", "TrackNumber", "TrackTitle"
FROM "MusicTracks";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "MusicTracks";

ALTER TABLE "ef_temp_MusicTracks" RENAME TO "MusicTracks";

COMMIT;

PRAGMA foreign_keys = 1;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250205230739_MakeReleaseTypeNullable', '9.0.1');

BEGIN TRANSACTION;
CREATE INDEX "IX_MusicTracks_AlbumName_TrackNumber" ON "MusicTracks" ("AlbumName", "TrackNumber");

CREATE INDEX "IX_MusicTracks_Artist" ON "MusicTracks" ("Artist");

CREATE INDEX "IX_MusicTracks_Artist_AlbumName" ON "MusicTracks" ("Artist", "AlbumName");

CREATE INDEX "IX_MusicTracks_FilePath" ON "MusicTracks" ("FilePath");

CREATE INDEX "IX_MusicTracks_Id" ON "MusicTracks" ("Id");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250206210034_AddIndexes', '9.0.1');

CREATE TABLE "ServerSettings" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ServerSettings" PRIMARY KEY AUTOINCREMENT,
    "ServerName" TEXT NOT NULL
);

INSERT INTO "ServerSettings" ("Id", "ServerName")
VALUES (1, 'Audiarr');
SELECT changes();


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250206214132_AddServerSettings', '9.0.1');

CREATE TABLE "Artists" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Artists" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "SortName" TEXT NOT NULL
);

CREATE TABLE "Albums" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Albums" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "ArtistId" INTEGER NOT NULL,
    "ReleaseYear" INTEGER NULL,
    "Genre" TEXT NOT NULL,
    "CoverArtUrl" TEXT NOT NULL,
    CONSTRAINT "FK_Albums_Artists_ArtistId" FOREIGN KEY ("ArtistId") REFERENCES "Artists" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Tracks" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Tracks" PRIMARY KEY AUTOINCREMENT,
    "Title" TEXT NOT NULL,
    "ArtistId" INTEGER NOT NULL,
    "AlbumId" INTEGER NOT NULL,
    "TrackNumber" INTEGER NOT NULL,
    "Duration" TEXT NOT NULL,
    "FileFormat" TEXT NOT NULL,
    "Bitrate" INTEGER NOT NULL,
    "FileSize" INTEGER NOT NULL,
    "FilePath" TEXT NOT NULL,
    CONSTRAINT "FK_Tracks_Albums_AlbumId" FOREIGN KEY ("AlbumId") REFERENCES "Albums" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Tracks_Artists_ArtistId" FOREIGN KEY ("ArtistId") REFERENCES "Artists" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Albums_ArtistId" ON "Albums" ("ArtistId");

CREATE UNIQUE INDEX "IX_Albums_Name_ArtistId" ON "Albums" ("Name", "ArtistId");

CREATE UNIQUE INDEX "IX_Artists_Name" ON "Artists" ("Name");

CREATE INDEX "IX_Tracks_AlbumId" ON "Tracks" ("AlbumId");

CREATE INDEX "IX_Tracks_ArtistId" ON "Tracks" ("ArtistId");

CREATE UNIQUE INDEX "IX_Tracks_Title_AlbumId_ArtistId" ON "Tracks" ("Title", "AlbumId", "ArtistId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250208145354_CreateMusicSchema', '9.0.1');

COMMIT;

