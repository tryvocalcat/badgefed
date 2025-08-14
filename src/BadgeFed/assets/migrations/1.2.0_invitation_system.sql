-- Migration 1.2.0: User Invitation System
-- This migration adds the invitation system for user signups

CREATE TABLE IF NOT EXISTS Invitations (
    id TEXT PRIMARY KEY,
    email TEXT NOT NULL,
    invitedBy TEXT NOT NULL,
    createdAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    expiresAt DATETIME NOT NULL,
    acceptedBy TEXT NULL,
    acceptedAt DATETIME NULL,   
    isActive BOOLEAN DEFAULT TRUE,
    role TEXT NOT NULL DEFAULT 'manager',
    notes TEXT NULL,
    FOREIGN KEY (invitedBy) REFERENCES Users(id),
    FOREIGN KEY (acceptedBy) REFERENCES Users(id)
);

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS idx_invitations_email ON Invitations(email);
CREATE INDEX IF NOT EXISTS idx_invitations_invited_by ON Invitations(invitedBy);
CREATE INDEX IF NOT EXISTS idx_invitations_expires_at ON Invitations(expiresAt);
CREATE INDEX IF NOT EXISTS idx_invitations_is_active ON Invitations(isActive);
