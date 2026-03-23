-- Migration: Move ProfileCTA settings from appsettings.json to InstanceDescription table
ALTER TABLE InstanceDescription ADD COLUMN ProfileCTAType TEXT NOT NULL DEFAULT '';
ALTER TABLE InstanceDescription ADD COLUMN ProfileCTAUrl TEXT NOT NULL DEFAULT '';
