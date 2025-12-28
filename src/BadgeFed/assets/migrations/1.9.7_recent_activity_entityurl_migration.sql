-- Migration: Recent Activity Entity URL Migration
-- This migration replaces entity and entityId fields with a single EntityUrl field in RecentActivityLog

-- Add the new EntityUrl column
ALTER TABLE RecentActivityLog ADD COLUMN EntityUrl TEXT;

-- Drop the old columns
ALTER TABLE RecentActivityLog DROP COLUMN Entity;
ALTER TABLE RecentActivityLog DROP COLUMN EntityId;