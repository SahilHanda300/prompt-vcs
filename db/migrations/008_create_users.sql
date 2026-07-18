-- Users table for PromptVCS authentication
CREATE TABLE IF NOT EXISTS Users (
    UserId    UUID        DEFAULT gen_random_uuid() PRIMARY KEY,
    Name      TEXT        NOT NULL,
    Username  TEXT        NOT NULL,
    PassHash  TEXT        NOT NULL,
    CreatedAt TIMESTAMPTZ DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_users_username ON Users (LOWER(Username));
