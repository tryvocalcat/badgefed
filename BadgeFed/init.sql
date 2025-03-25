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
ALTER TABLE Actor ADD COLUMN PrivateKeyPem TEXT;

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

CREATE TABLE BadgeRecord (
    Id INTEGER PRIMARY KEY,
    Title TEXT NOT NULL CHECK(length(Title) >= 2 AND length(Title) <= 100),
    IssuedBy TEXT NOT NULL,
    Description TEXT CHECK(length(Description) <= 500),
    Image TEXT,
    ImageAltText TEXT CHECK(length(Description) <= 1500),
    EarningCriteria TEXT CHECK(length(EarningCriteria) <= 500),
    IssuedOn DATETIME NOT NULL,
    IssuedToEmail TEXT NOT NULL,
    IssuedToName TEXT NOT NULL,
    IssuedToSubjectUri TEXT NOT NULL,
    AcceptedOn DATETIME,
    LastUpdated DATETIME DEFAULT CURRENT_TIMESTAMP,
    FingerPrint TEXT NULL,
    AcceptKey TEXT NULL,
    BadgeId INTEGER NOT NULL,
    FOREIGN KEY (BadgeId) REFERENCES BadgeId(Id)
);

CREATE TRIGGER UpdateBadgeRecordTimestamp
AFTER UPDATE ON BadgeRecord
FOR EACH ROW
BEGIN
    UPDATE BadgeRecord SET LastUpdated = CURRENT_TIMESTAMP WHERE Id = OLD.Id;
END;

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
    FollowerUri TEXT NOT NULL CHECK(length(FollowerUri) <= 300) PRIMARY KEY,
    Domain TEXT NOT NULL CHECK(length(Domain) <= 100),
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    ActorId INTEGER NOT NULL,
    FOREIGN KEY (ActorId) REFERENCES Actor(Id)
);

-- Insert test data

INSERT INTO Actor (Name, Summary, AvatarPath, InformationUri, Uri, Domain, Username, PublicKeyPem, PrivateKeyPem)
VALUES ('VocalCat', 'An example badges actor', '/avatars/vocalcat.png', 'https://vocalcat.com/', 
'https://badgefedtest.azurewebsites.net/actors/badgefedtest.azurewebsites.net/poc', 
'badgefedtest.azurewebsites.net', 'poc', '-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAwuex1DXcBjEUx7bKV4Kq\nb9175RypfzcJNFUG5y58LbQIcYDAD8uaCL3h/oNbDBMh6XLxR7BfBW16GtHPQr6D\nIm28OhpfDMwo/AYSfhBHVKdhm0cF2I9emyScPVlS+6SXO0mnjGktalpndeb681Yh\n5hb0VYLmMmQ/Od92NgS5x8Bi2W1FvyH6ZhWBimspvXz6iF2AahTVFEPpBMAU7yEG\ngMhOIEc1nQ3Yj8+H/md9b9zKM7m9TkPqQKFnHhtaMOTdJvmD6bABLNK7eeOJ5E7g\n3ZZexb5uLaKjVGCKorrpCWWjC0ycvkXo5h+NO1WdxZwBq3QfazBeU98etUxgUmfV\nVQIDAQAB\n-----END PUBLIC KEY-----\n', '-----BEGIN PRIVATE KEY-----\nMIIEvAIBADANBgkqhkiG9w0BAQEFAASCBKYwggSiAgEAAoIBAQDC57HUNdwGMRTH\ntspXgqpv3XvlHKl/Nwk0VQbnLnwttAhxgMAPy5oIveH+g1sMEyHpcvFHsF8FbXoa\n0c9CvoMibbw6Gl8MzCj8BhJ+EEdUp2GbRwXYj16bJJw9WVL7pJc7SaeMaS1qWmd1\n5vrzViHmFvRVguYyZD8533Y2BLnHwGLZbUW/IfpmFYGKaym9fPqIXYBqFNUUQ+kE\nwBTvIQaAyE4gRzWdDdiPz4f+Z31v3Mozub1OQ+pAoWceG1ow5N0m+YPpsAEs0rt5\n44nkTuDdll7Fvm4toqNUYIqiuukJZaMLTJy+RejmH407VZ3FnAGrdB9rMF5T3x61\nTGBSZ9VVAgMBAAECggEAITiyckBC8mg7yUXiK/Po/rvTIiyWRImaP0oN7OpavsSM\n2uHt+tNCvGI3SvVTV9XMBjlyIHzS2X89XKRuDkugWQY42vODR5BmHP0g+k1mhyqn\no7rBp5Xac5nUKs3Wq+90WqX1keScmd/yeiGGnWMNUKWRfKDdAXJeZ4wsH02DaCVZ\nzflNdeE+lmplbaPABasUK+YYCea008lcGigGX3mUW/FpXDEM8XUJeRyAL+ERLGam\nsMZ+jB1PQwVlNUxM7AXHxdd0nytrtsyIbRgUHGxzw8PvujJywfXiiKuHD0xR7+xT\nW81GDfKWkhB9ZPVpC+147ngZ3IWKavCLM2SaJX1fSQKBgQDolNR6v9nPfARxayHH\nHPxKJko+0W+zfyE6kAKi3aW8RYIpSN8t0/+Fx3+Z8WW2eK+AkQVcOhuZSvkimuO8\nzNfxkelws8yQPlHsJTimbWo5GoeUVlm5HbmIxFk+3sWE7DZLtM+rj3QuYQ5MTnMz\n8wUzZ3mkhMbOpAEEveyKF6ezCQKBgQDWh7ICRkefxSdKUbBBhP6Hpg7haexSxLCO\nsVRjPyl3+SzZGqiIbgNOwFxSNlfnXvL2OCo9WUFoT2cBmKOCOg5hUj45l/XzA9Jf\nPaW116W0KSjqkro0tMcimu+CMoNMlYn4rphaqLSrX7eQLGi5WSm35OgTbVDCwJG4\nux/XGSXm7QKBgGCudYEtPTK13/bxzNHDZ9C/CAAC+ccI4txAPwhK0Plpf4j/5N5d\nEQwgeReaNGjc1D/CiRLkiNJ5SwUqk97I3D9sIzkZVMDlVxKuClWMiCqCr7dnCdcc\n1yJWVK8A1eTCeHOSDv3HHUmmSNZJijQfIptSuUs9cpM1s8Kv3KMu4CRZAoGAA+sU\nkEASXNOwBQZ67qdsMrQQv4M8wsI+60xH20wzCLLvY8O94kgIHW8cAJBniJ1OWLrr\n4pT3wdz5Z6kPC3jd/F8RLeIdpuOh+wVYOnsG1sSNr8MgTYgjvvkPeNRNW5+7lmQx\n+i3sptintKVrAD+lqGsw5fHwMK5tuu8IBNi7vX0CgYBaMKBXAVcd8nIGnrS9wNFe\nURJrAGs2Sy868reEdSIsugjZy0VJ1ONAkXqIcVg58CDOuOhwDjXVOkv6PmcgG98n\nVXzvI7cJyG4sj6aqDazYuVvoAKPMFMX4AAgbowYUQP9XSlTY7z5m9b5tNvrFS4Ew\nBCRockGuo0/SUZD11j1Kfw==\n-----END PRIVATE KEY-----\n');

INSERT INTO BadgeDefinition (Title, Description, IssuedBy, Image, ImageAltText, EarningCriteria)
VALUES ('First Badge', 'This is the first badge', 1, '/images/first_badge.png', 'Image of badge', 'Complete the first task'),
    ('Second Badge', 'This is the second badge', 2, '/images/second_badge.png', 'Image of badge', 'Complete the second task');

INSERT INTO Recipient (FullName, Email, FediverseHandle)
VALUES ('Charlie', 'charlie@example.com', '@charlie@fediverse.example'),
    ('Dana', 'dana@example.com', '@dana@fediverse.example');

INSERT INTO Badge (Title, IssuedBy, Description, Image, ImageAltText, EarningCriteria, IssuedUsing, IssuedOn, IssuedTo, FingerPrint, BadgeDefinitionId)
VALUES ('First Badge', 'Alice', 'This is the first badge issued', '/images/first_badge.png', 'Image of badge', 'Complete the first task', 'System', '2023-01-01 10:00:00', 1, 'abc123', 1),
    ('Second Badge', 'Bob', 'This is the second badge issued', '/images/second_badge.png', 'Image of badge', 'Complete the second task', 'System', '2023-01-02 11:00:00', 2, 'def456', 2);