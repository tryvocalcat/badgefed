-- Simple Message Queue for BadgeFed
-- Separate database to handle job queuing and prevent main database locks

-- Simple jobs table - very generic
CREATE TABLE Jobs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    JobType TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'pending', -- pending, processing, completed, failed
    Payload TEXT NULL, -- JSON data for the job
    
    EntityType TEXT NULL, -- e.g., Badge, User, etc.
    EntityId TEXT NULL, -- ID of the entity the job is related to
    Domain TEXT NULL, -- e.g., badge issuance, notification, etc.
    
    -- Retry and error handling
    MaxRetries INTEGER NOT NULL DEFAULT 3,
    CurrentRetry INTEGER NOT NULL DEFAULT 0,
    LastError TEXT NULL,

    Priority INTEGER NOT NULL DEFAULT 0, -- Higher number = higher priority
    
    -- Timestamps
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ScheduledFor DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, -- When job should be executed
    StartedAt DATETIME NULL, -- When job processing began
    CompletedAt DATETIME NULL, -- When job finished (success or failure)
    ProcessedAt DATETIME NULL,
    
    -- Additional metadata
    CreatedBy TEXT NULL, -- Source that created the job (controller, service, etc.)
    Notes TEXT NULL -- Additional context or metadata
);

-- Simple index for fetching jobs
CREATE INDEX IX_Jobs_Status_Created ON Jobs(Status, CreatedAt);
CREATE INDEX IX_Jobs_Status_Scheduled ON Jobs(Status, ScheduledFor);
CREATE INDEX IX_Jobs_Domain ON Jobs(Domain);

-- Job logs for debugging
CREATE TABLE JobLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    JobId INTEGER NOT NULL,
    Message TEXT NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (JobId) REFERENCES Jobs(Id)
);

CREATE INDEX IX_JobLogs_JobId ON JobLogs(JobId);

-- Migration tracking
CREATE TABLE IF NOT EXISTS QueueDbMigrations (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Version TEXT NOT NULL UNIQUE,
    Name TEXT NOT NULL,
    AppliedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO QueueDbMigrations (Version, Name) 
SELECT '1.0.0', 'Simple Queue Schema'
WHERE NOT EXISTS (SELECT 1 FROM QueueDbMigrations WHERE Version = '1.0.0');
