-- Migration 1.3.0: Server Discovery System
-- This migration adds the server discovery system for following other BadgeFed servers

CREATE TABLE IF NOT EXISTS DiscoveredServers (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    url TEXT NOT NULL UNIQUE,
    description TEXT NOT NULL DEFAULT '',
    categories TEXT NOT NULL DEFAULT '[]',
    admin TEXT NOT NULL DEFAULT '',
    actor TEXT NOT NULL,
    isFollowed BOOLEAN DEFAULT FALSE,
    followedAt DATETIME NULL,
    createdAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    updatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS idx_discovered_servers_url ON DiscoveredServers(url);
CREATE INDEX IF NOT EXISTS idx_discovered_servers_is_followed ON DiscoveredServers(isFollowed);
CREATE INDEX IF NOT EXISTS idx_discovered_servers_created_at ON DiscoveredServers(createdAt);
