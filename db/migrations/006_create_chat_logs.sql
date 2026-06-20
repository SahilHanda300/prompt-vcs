CREATE TABLE IF NOT EXISTS ChatLogs (
    LogId        UUID          DEFAULT gen_random_uuid() PRIMARY KEY,
    SessionId    UUID          NOT NULL,
    PromptHash   CHAR(64)      NOT NULL,
    UserMessage  TEXT          NOT NULL,
    BotResponse  TEXT          NOT NULL,
    ResponseMs   INT           NOT NULL,
    CreatedAt    TIMESTAMPTZ   DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_chat_logs_session_id   ON ChatLogs(SessionId);
CREATE INDEX IF NOT EXISTS idx_chat_logs_prompt_hash  ON ChatLogs(PromptHash);
CREATE INDEX IF NOT EXISTS idx_chat_logs_created_at   ON ChatLogs(CreatedAt DESC);
