-- SQLITE DATABASE Schema

CREATE TABLE Actor (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL CHECK(length(Name) >= 2 AND length(Name) <= 100),
    Summary TEXT CHECK(length(Summary) <= 500),
    AvatarPath TEXT,
    InformationUri TEXT,
    Uri TEXT,
    Domain TEXT,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE Actor ADD COLUMN Username TEXT;
ALTER TABLE Actor ADD COLUMN PublicKeyPem TEXT;

CREATE TRIGGER UpdateActorTimestamp
AFTER UPDATE ON Actor
FOR EACH ROW
BEGIN
    UPDATE Actor SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = OLD.Id;
END;

CREATE TABLE BadgeDefinition (
    Id INTEGER PRIMARY KEY,
    Title TEXT NOT NULL CHECK(length(Title) >= 2 AND length(Title) <= 100),
    Description TEXT CHECK(length(Description) <= 500),
    IssuedBy INTEGER NOT NULL,
    Image TEXT,
    EarningCriteria TEXT CHECK(length(EarningCriteria) <= 500),
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (IssuedBy) REFERENCES Actor(Id)
);

CREATE TRIGGER UpdateBadgeDefinitionTimestamp
AFTER UPDATE ON BadgeDefinition
FOR EACH ROW
BEGIN
    UPDATE BadgeDefinition SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = OLD.Id;
END;

CREATE TABLE Badge (
    Id INTEGER PRIMARY KEY,
    Title TEXT NOT NULL CHECK(length(Title) >= 2 AND length(Title) <= 100),
    IssuedBy TEXT NOT NULL,
    Description TEXT CHECK(length(Description) <= 500),
    Image TEXT,
    EarningCriteria TEXT CHECK(length(EarningCriteria) <= 500),
    IssuedUsing TEXT,
    IssuedOn DATETIME NOT NULL,
    IssuedTo INTEGER NOT NULL,
    AcceptedOn DATETIME,
    LastUpdated DATETIME DEFAULT CURRENT_TIMESTAMP,
    FingerPrint TEXT NOT NULL,
    BadgeDefinitionId INTEGER NOT NULL,
    FOREIGN KEY (BadgeDefinitionId) REFERENCES BadgeDefinition(Id),
    FOREIGN KEY (IssuedTo) REFERENCES Recipient(Id)
);

CREATE TRIGGER UpdateBadgeTimestamp
AFTER UPDATE ON Badge
FOR EACH ROW
BEGIN
    UPDATE Badge SET LastUpdated = CURRENT_TIMESTAMP WHERE Id = OLD.Id;
END;

CREATE TABLE Recipient (
    Id INTEGER PRIMARY KEY,
    FullName TEXT NOT NULL CHECK(length(FullName) >= 2 AND length(FullName) <= 100),
    Email TEXT NOT NULL CHECK(length(Email) <= 100),
    FediverseHandle TEXT CHECK(length(FediverseHandle) <= 200),
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
    FollowerUri TEXT NOT NULL CHECK(length(FollowerUri) <= 300) PRIMARY KEY,
    Domain TEXT NOT NULL CHECK(length(Domain) <= 100),
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    ActorId INTEGER NOT NULL,
    FOREIGN KEY (ActorId) REFERENCES Actor(Id)
);

-- Insert test data

INSERT INTO Actor (Name, Summary, InformationUri, Domain, Username)
VALUES ('VocalCat', 'An example badges actor', 'https://vocalcat.com/', 'vocalcat.com', 'badges');
    

INSERT INTO BadgeDefinition (Title, Description, IssuedBy, Image, EarningCriteria)
VALUES ('First Badge', 'This is the first badge', 1, '/images/first_badge.png', 'Complete the first task'),
    ('Second Badge', 'This is the second badge', 2, '/images/second_badge.png', 'Complete the second task');

INSERT INTO Recipient (FullName, Email, FediverseHandle)
VALUES ('Charlie', 'charlie@example.com', '@charlie@fediverse.example'),
    ('Dana', 'dana@example.com', '@dana@fediverse.example');

INSERT INTO Badge (Title, IssuedBy, Description, Image, EarningCriteria, IssuedUsing, IssuedOn, IssuedTo, FingerPrint, BadgeDefinitionId)
VALUES ('First Badge', 'Alice', 'This is the first badge issued', '/images/first_badge.png', 'Complete the first task', 'System', '2023-01-01 10:00:00', 1, 'abc123', 1),
    ('Second Badge', 'Bob', 'This is the second badge issued', '/images/second_badge.png', 'Complete the second task', 'System', '2023-01-02 11:00:00', 2, 'def456', 2);