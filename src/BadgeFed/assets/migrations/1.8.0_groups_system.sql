-- Migration 1.8.0: Workspace/Group system
-- This migration adds workspaces (groups) and assigns users to them
-- Workspaces become the ownership entities instead of individual users

-- Create UserGroups table (Groups)
CREATE TABLE IF NOT EXISTS UserGroups (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    createdAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS Users (
    id TEXT PRIMARY KEY,
    email TEXT NOT NULL UNIQUE,
    givenName TEXT,
    surname TEXT,
    createdAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    provider TEXT NOT NULL,
    role TEXT NOT NULL,
    isActive BOOLEAN DEFAULT 1
);

-- Create the default user group
INSERT OR IGNORE INTO UserGroups (id, name, description) 
VALUES ('system', 'Default User Group', 'Default user group for all users');

-- Add UserGroupId to Users table
ALTER TABLE Users ADD COLUMN groupId TEXT REFERENCES UserGroups(id) DEFAULT 'system';

-- Update existing users to be in the default user group
UPDATE Users SET groupId = 'system' WHERE groupId IS NULL;

ALTER TABLE Invitations ADD COLUMN groupId TEXT REFERENCES UserGroups(id) DEFAULT 'system';