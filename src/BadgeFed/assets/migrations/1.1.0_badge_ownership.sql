-- Migration 1.1.0: Badge ownership
-- This migration adds proper ownership tracking
CREATE TABLE IF NOT EXISTS Users (
    id TEXT PRIMARY KEY,
    email TEXT NOT NULL,
    givenName TEXT NOT NULL,
    surname TEXT NOT NULL,
    createdAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    provider TEXT NOT NULL,
    role TEXT NOT NULL DEFAULT 'user',
    isActive BOOLEAN DEFAULT TRUE
);

-- Ensure all badges have an OwnerId
UPDATE Badge SET OwnerId = 'system' WHERE OwnerId IS NULL OR OwnerId = '';

-- Ensure all actors have an OwnerId  
UPDATE Actor SET OwnerId = 'system' WHERE OwnerId IS NULL OR OwnerId = '';
