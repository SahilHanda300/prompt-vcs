CREATE TABLE IF NOT EXISTS Refs (
    RefName       TEXT          NOT NULL,
    Environment   TEXT          NOT NULL,
    TargetHash    CHAR(64)      NOT NULL,
    RefType       TEXT          NOT NULL DEFAULT 'branch',
    PromotedFrom  CHAR(64)      NULL,
    UpdatedAt     TIMESTAMPTZ   DEFAULT NOW(),
    PRIMARY KEY (RefName, Environment)
);

ALTER TABLE Refs
    ADD CONSTRAINT chk_refs_environment
    CHECK (Environment IN ('DEV', 'QA', 'PROD'));

ALTER TABLE Refs
    ADD CONSTRAINT chk_refs_ref_type
    CHECK (RefType IN ('branch', 'tag'));

CREATE INDEX IF NOT EXISTS idx_refs_environment   ON Refs(Environment);
CREATE INDEX IF NOT EXISTS idx_refs_target_hash   ON Refs(TargetHash);
