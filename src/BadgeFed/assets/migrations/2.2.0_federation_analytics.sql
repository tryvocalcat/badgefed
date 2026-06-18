CREATE TABLE IF NOT EXISTS FederationEvent (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EventType TEXT NOT NULL,
    ActorUri TEXT,
    ObjectUri TEXT,
    TargetUri TEXT,
    RemoteHost TEXT,
    RequestIp TEXT,
    UserAgent TEXT,
    GroupId TEXT,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS idx_federation_event_type ON FederationEvent(EventType);
CREATE INDEX IF NOT EXISTS idx_federation_event_created ON FederationEvent(CreatedAt);
CREATE INDEX IF NOT EXISTS idx_federation_event_actor ON FederationEvent(ActorUri);
CREATE INDEX IF NOT EXISTS idx_federation_event_group ON FederationEvent(GroupId);
