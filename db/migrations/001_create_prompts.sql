CREATE TABLE IF NOT EXISTS Prompts (
    PromptId          UUID          DEFAULT gen_random_uuid() PRIMARY KEY,
    ContentHash       CHAR(64)      NOT NULL UNIQUE,
    SystemTemplate    TEXT          NOT NULL,
    UserTemplate      TEXT          NOT NULL,
    ModelParams       TEXT          NOT NULL,
    SubmittedBy       TEXT          NOT NULL,
    Country           TEXT          NOT NULL,
    CommitMessage     TEXT          NOT NULL,
    ParentHash        CHAR(64)      NULL,
    PromptType        TEXT          NOT NULL DEFAULT 'chat',
    InputLabel        TEXT          NULL,
    InputPlaceholder  TEXT          NULL,
    OutputLabel       TEXT          NULL,
    CreatedAt         TIMESTAMPTZ   DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_prompts_content_hash ON Prompts(ContentHash);
CREATE INDEX IF NOT EXISTS idx_prompts_submitted_by ON Prompts(SubmittedBy);
CREATE INDEX IF NOT EXISTS idx_prompts_created_at   ON Prompts(CreatedAt DESC);

ALTER TABLE Prompts
    ADD CONSTRAINT chk_prompts_prompt_type
    CHECK (PromptType IN ('chat', 'transform', 'analyse', 'generate', 'qa'));
