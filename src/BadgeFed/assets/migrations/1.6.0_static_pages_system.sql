-- Add new columns to InstanceDescription table for static page management
ALTER TABLE InstanceDescription ADD COLUMN LandingPageType TEXT NOT NULL DEFAULT 'default';
ALTER TABLE InstanceDescription ADD COLUMN StaticPageFilename TEXT NOT NULL DEFAULT '';

-- Create StaticPages table for managing uploaded HTML files
CREATE TABLE IF NOT EXISTS StaticPages (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Filename TEXT NOT NULL UNIQUE,
    Title TEXT NOT NULL DEFAULT '',
    Description TEXT NOT NULL DEFAULT '',
    FileSize INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
