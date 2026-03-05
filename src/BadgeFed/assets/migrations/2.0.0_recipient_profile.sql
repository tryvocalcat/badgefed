-- Migration 2.0.0: Recipient Profile Enhancement + OAuth Identity Linking
-- Adds profile customization fields and multi-provider identity linking

-- Profile fields on Recipient
ALTER TABLE Recipient ADD COLUMN DisplayName TEXT;
ALTER TABLE Recipient ADD COLUMN Bio TEXT;
ALTER TABLE Recipient ADD COLUMN AvatarPath TEXT;
ALTER TABLE Recipient ADD COLUMN Slug TEXT;
ALTER TABLE Recipient ADD COLUMN ProfileTemplate TEXT DEFAULT 'professional';
ALTER TABLE Recipient ADD COLUMN Theme TEXT;
ALTER TABLE Recipient ADD COLUMN ProfileLinks TEXT;
ALTER TABLE Recipient ADD COLUMN CustomHeadline TEXT;
ALTER TABLE Recipient ADD COLUMN IsPublic BOOLEAN DEFAULT 1;
ALTER TABLE Recipient ADD COLUMN PrimaryRecipientId INTEGER REFERENCES Recipient(Id);

CREATE UNIQUE INDEX IF NOT EXISTS idx_recipient_slug ON Recipient(Slug);

-- OAuth identity linking table
CREATE TABLE IF NOT EXISTS RecipientIdentity (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RecipientId INTEGER NOT NULL,
    Provider TEXT NOT NULL,
    ProviderUserId TEXT NOT NULL,
    ProviderUsername TEXT,
    ProviderHostname TEXT,
    ProviderProfileUrl TEXT,
    ProviderEmail TEXT,
    ProviderAvatarUrl TEXT,
    ProviderDisplayName TEXT,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    LastLoginAt DATETIME,
    FOREIGN KEY (RecipientId) REFERENCES Recipient(Id),
    UNIQUE(Provider, ProviderUserId)
);

CREATE INDEX IF NOT EXISTS idx_recipient_identity_recipient ON RecipientIdentity(RecipientId);
CREATE INDEX IF NOT EXISTS idx_recipient_identity_profile_url ON RecipientIdentity(ProviderProfileUrl);
CREATE INDEX IF NOT EXISTS idx_recipient_identity_email ON RecipientIdentity(ProviderEmail);
