-- Migration 1.5.0: Badge Boosted Tracking
-- This migration adds tracking for when badge records are boosted/announced to followers

-- Add BoostedOn column to BadgeRecord table to track when a badge was boosted
ALTER TABLE BadgeRecord ADD COLUMN BoostedOn DATETIME NULL;

-- Add index for efficient querying of boosted badges
CREATE INDEX IX_BadgeRecord_BoostedOn ON BadgeRecord(BoostedOn);
