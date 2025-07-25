-- Migration 1.4.0: Actor Theme System
-- This migration adds theme support to actors for customizable profile styling

-- Add Theme column to Actors table
ALTER TABLE Actor ADD COLUMN Theme TEXT DEFAULT 'default';

-- Update all existing actors to use the default theme
UPDATE Actor SET Theme = 'default' WHERE Theme IS NULL;
