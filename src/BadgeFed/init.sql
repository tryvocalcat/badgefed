-- SQLITE DATABASE Schema

-- tables that need ownership

CREATE TABLE Actor (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL CHECK(length(Name) >= 2 AND length(Name) <= 100),
    Summary TEXT CHECK(length(Summary) <= 500),
    AvatarPath TEXT,
    InformationUri TEXT,
    Uri TEXT,
    Domain TEXT,
    Username TEXT,
    PublicKeyPem TEXT,
    PrivateKeyPem TEXT,
    Featured BOOLEAN DEFAULT TRUE,
    LinkedInOrganizationId TEXT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    IsMain BOOLEAN DEFAULT FALSE,
    OwnerId TEXT NOT NULL UNIQUE
);

CREATE TRIGGER UpdateActorTimestamp
AFTER UPDATE ON Actor
FOR EACH ROW
BEGIN
    UPDATE Actor SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = OLD.Id;
END;

CREATE TABLE Badge (
    Id INTEGER PRIMARY KEY,
    Title TEXT NOT NULL CHECK(length(Title) >= 2 AND length(Title) <= 100),
    Description TEXT CHECK(length(Description) <= 500),
    IssuedBy INTEGER NOT NULL,
    Image TEXT,
    ImageAltText TEXT CHECK(length(ImageAltText) <= 1500),
    EarningCriteria TEXT CHECK(length(EarningCriteria) <= 500),
    BadgeType TEXT NOT NULL CHECK(BadgeType IN ('Achievement', 'Badge', 'Credential', 'Recognition', 'Milestone', 'Honor', 'Certification', 'Distinction')),
    Hashtags TEXT,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (IssuedBy) REFERENCES Actor(Id)
);

CREATE TRIGGER UpdateBadgeTimestamp
AFTER UPDATE ON Badge
FOR EACH ROW
BEGIN
    UPDATE Badge SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = OLD.Id;
END;

CREATE TABLE RecentActivityLog (
    Id INTEGER PRIMARY KEY,
    Title TEXT NOT NULL CHECK(length(Title) >= 2 AND length(Title) <= 100),
    Description TEXT CHECK(length(Description) <= 500),
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE BadgeRecord (
    Id INTEGER PRIMARY KEY,
    Title TEXT NOT NULL CHECK(length(Title) >= 2 AND length(Title) <= 100),
    IssuedBy TEXT NOT NULL,
    Description TEXT CHECK(length(Description) <= 500),
    Image TEXT,
    ImageAltText TEXT CHECK(length(ImageAltText) <= 1500),
    EarningCriteria TEXT CHECK(length(EarningCriteria) <= 500),
    Hashtags TEXT,
    IssuedOn DATETIME NOT NULL,
    IssuedToEmail TEXT NOT NULL,
    IssuedToName TEXT NOT NULL,
    IssuedToSubjectUri TEXT NOT NULL,
    AcceptedOn DATETIME,
    LastUpdated DATETIME DEFAULT CURRENT_TIMESTAMP,
    FingerPrint TEXT NULL,
    AcceptKey TEXT NULL,
    BadgeId INTEGER NULL DEFAULT 0,
    IsExternal BOOLEAN DEFAULT FALSE,
    Visibility TEXT NOT NULL CHECK(Visibility IN ('Public', 'Private', 'Unlisted')) DEFAULT 'Public',
    NotifiedOfGrant BOOLEAN DEFAULT FALSE,
    NoteId TEXT NULL,
    FOREIGN KEY (BadgeId) REFERENCES BadgeId(Id)
);

CREATE TRIGGER UpdateBadgeRecordTimestamp
AFTER UPDATE ON BadgeRecord
FOR EACH ROW
BEGIN
    UPDATE BadgeRecord SET LastUpdated = CURRENT_TIMESTAMP WHERE Id = OLD.Id;
END;

CREATE TABLE BadgeRecordComment (
    Id INTEGER PRIMARY KEY,
    BadgeRecordId INTEGER NOT NULL,
    NoteId TEXT NOT NULL,
    AuthorId TEXT NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (BadgeRecordId) REFERENCES BadgeRecord(Id)
);

CREATE UNIQUE INDEX IX_BadgeRecordComment_BadgeRecordId_NoteId ON BadgeRecordComment(BadgeRecordId, NoteId);

CREATE INDEX IX_BadgeRecordComment_BadgeRecordId ON BadgeRecordComment(BadgeRecordId);

CREATE TRIGGER UpdateBadgeRecordCommentTimestamp
AFTER UPDATE ON BadgeRecordComment
FOR EACH ROW
BEGIN
    UPDATE BadgeRecordComment SET CreatedAt = CURRENT_TIMESTAMP WHERE Id = OLD.Id;
END;

CREATE TABLE FollowedIssuer (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL CHECK(length(Name) >= 2 AND length(Name) <= 100),
    Url TEXT NOT NULL,
    Inbox TEXT NOT NULL,
    Outbox TEXT NOT NULL,
    ActorId INTEGER NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    AvatarUri TEXT,
    FOREIGN KEY (ActorId) REFERENCES Actor(Id)
);


CREATE UNIQUE INDEX IX_FollowedIssuer_Url_ActorId ON FollowedIssuer(Url, ActorId);

CREATE TRIGGER UpdateFollowedIssuerTimestamp
AFTER UPDATE ON FollowedIssuer
FOR EACH ROW
BEGIN
    UPDATE FollowedIssuer SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = OLD.Id;
END;

CREATE TABLE BadgeComments (
    Id INTEGER PRIMARY KEY,
    BadgeUri TEXT NOT NULL,
    NoteId TEXT NOT NULL,
    UNIQUE(BadgeUri, NoteId)
);

CREATE TABLE IF NOT EXISTS DbVersion (Version INTEGER);

INSERT INTO DbVersion (Version) 
SELECT 1
WHERE NOT EXISTS (SELECT 1 FROM DbVersion);

CREATE TABLE Recipient (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL CHECK(length(Name) >= 2 AND length(Name) <= 100),
    Email TEXT CHECK(length(Email) <= 100),
    ProfileUri TEXT NOT NULL UNIQUE,
    IsActivityPubActor BOOLEAN DEFAULT FALSE,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TRIGGER UpdateRecipientTimestamp
AFTER UPDATE ON Recipient
FOR EACH ROW
BEGIN
    UPDATE Recipient SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = OLD.Id;
END;

CREATE TABLE Follower (
    FollowerUri TEXT NOT NULL CHECK(length(FollowerUri) <= 300),
    Domain TEXT NOT NULL CHECK(length(Domain) <= 100),
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    ActorId INTEGER NOT NULL,
    AvatarUri TEXT,
    DisplayName TEXT,
    PRIMARY KEY (FollowerUri, ActorId),
    FOREIGN KEY (ActorId) REFERENCES Actor(Id)
);

-------- 6/6/2025 

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

INSERT INTO Users (id, email, givenName, surname, provider, role)
VALUES ('hachyderm.io_mapache', '', 'mapache', '', 'hachyderm.io', 'manager');

-- ALTER TABLE Actor ADD COLUMN OwnerId TEXT NOT NULL UNIQUE;
-- ALTER TABLE Badge ADD COLUMN OwnerId TEXT NOT NULL UNIQUE;