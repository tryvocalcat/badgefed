-- Create Registrations table for form-based user registrations
CREATE TABLE IF NOT EXISTS Registrations (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FormData TEXT NOT NULL, -- JSON serialized form data
    Email TEXT, -- Extracted email for easy querying
    Name TEXT, -- Extracted name for easy querying
    IpAddress TEXT NOT NULL DEFAULT '',
    UserAgent TEXT NOT NULL DEFAULT '',
    SubmittedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    IsReviewed BOOLEAN NOT NULL DEFAULT 0,
    IsApproved BOOLEAN NOT NULL DEFAULT 0,
    ReviewNotes TEXT,
    ReviewedAt DATETIME,
    ReviewedBy TEXT
);

-- Create indexes for efficient querying
CREATE INDEX IF NOT EXISTS idx_registrations_email ON Registrations(Email);
CREATE INDEX IF NOT EXISTS idx_registrations_submitted_at ON Registrations(SubmittedAt);
CREATE INDEX IF NOT EXISTS idx_registrations_reviewed ON Registrations(IsReviewed);
CREATE INDEX IF NOT EXISTS idx_registrations_approved ON Registrations(IsApproved);
