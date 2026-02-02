-- Migration 1.9.6: Recent Activity Entity Tracking
-- This migration adds entity and entityId fields to RecentActivityLog for better tracking and linking

ALTER TABLE RecentActivityLog ADD COLUMN Entity TEXT;
ALTER TABLE RecentActivityLog ADD COLUMN EntityId TEXT;