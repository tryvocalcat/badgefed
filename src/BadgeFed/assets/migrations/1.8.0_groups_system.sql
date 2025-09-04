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

-- Create the default user group
INSERT OR IGNORE INTO UserGroups (id, name, description) 
VALUES ('system', 'Default User Group', 'Default user group for all users');

-- Add UserGroupId to Users table
ALTER TABLE Users ADD COLUMN groupId TEXT REFERENCES UserGroups(id) DEFAULT 'system';

-- Update existing users to be in the default user group
UPDATE Users SET groupId = 'system' WHERE groupId IS NULL;

ALTER TABLE Invitations ADD COLUMN groupId TEXT REFERENCES UserGroups(id) DEFAULT 'system';