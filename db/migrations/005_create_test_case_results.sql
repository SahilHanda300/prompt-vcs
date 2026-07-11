CREATE TABLE IF NOT EXISTS TestCaseResults (
    ResultId        UUID          DEFAULT gen_random_uuid() PRIMARY KEY,
    EvalId          UUID          NOT NULL,
    PromptHash      CHAR(64)      NOT NULL,
    TestCaseIndex   INT           NOT NULL,
    InputText       TEXT          NOT NULL,
    ExpectedOutput  TEXT          NOT NULL,
    ActualOutput    TEXT          NOT NULL,
    GoldenScore     NUMERIC(5,4)  NOT NULL,
    JudgeScore      NUMERIC(5,4)  NOT NULL,
    JudgeReasoning  TEXT          NULL,
    CreatedAt       TIMESTAMPTZ   DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_tcr_eval_id     ON TestCaseResults(EvalId);
CREATE INDEX IF NOT EXISTS idx_tcr_prompt_hash ON TestCaseResults(PromptHash);
