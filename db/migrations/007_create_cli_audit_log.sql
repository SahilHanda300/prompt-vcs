CREATE TABLE IF NOT EXISTS CliAuditLog (
    LogId        UUID          DEFAULT gen_random_uuid() PRIMARY KEY,
    ExecutedBy   TEXT          NOT NULL,
    Command      TEXT          NOT NULL,
    Arguments    TEXT          NULL,
    PromptName   TEXT          NULL,
    PromptHash   CHAR(64)      NULL,
    Environment  TEXT          NULL,
    Outcome      TEXT          NOT NULL,
    ErrorMessage TEXT          NULL,
    CliVersion   TEXT          NOT NULL,
    ExecutedAt   TIMESTAMPTZ   DEFAULT NOW()
);

ALTER TABLE CliAuditLog
    ADD CONSTRAINT chk_cli_audit_outcome
    CHECK (Outcome IN ('SUCCESS', 'FAILED', 'DENIED'));

ALTER TABLE CliAuditLog
    ADD CONSTRAINT chk_cli_audit_environment
    CHECK (Environment IS NULL OR Environment IN ('DEV', 'QA', 'PROD'));

CREATE INDEX IF NOT EXISTS idx_cli_audit_executed_by  ON CliAuditLog(ExecutedBy);
CREATE INDEX IF NOT EXISTS idx_cli_audit_command      ON CliAuditLog(Command);
CREATE INDEX IF NOT EXISTS idx_cli_audit_executed_at  ON CliAuditLog(ExecutedAt DESC);
