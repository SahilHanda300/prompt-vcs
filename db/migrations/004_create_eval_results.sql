CREATE TABLE IF NOT EXISTS EvalResults (
    EvalId            UUID          DEFAULT gen_random_uuid() PRIMARY KEY,
    PromptHash        CHAR(64)      NOT NULL,
    DatasetId         UUID          NOT NULL,
    GoldenScore       NUMERIC(5,4)  NULL,
    JudgeScore        NUMERIC(5,4)  NULL,
    RegressionFlag    BOOLEAN       DEFAULT FALSE,
    Stage             TEXT          NOT NULL,
    PromotionReason   TEXT          NULL,
    RunAt             TIMESTAMPTZ   DEFAULT NOW()
);

ALTER TABLE EvalResults
    ADD CONSTRAINT chk_eval_results_stage
    CHECK (Stage IN ('DEV_TO_QA', 'QA_TO_PROD'));

CREATE INDEX IF NOT EXISTS idx_eval_results_prompt_hash ON EvalResults(PromptHash);
CREATE INDEX IF NOT EXISTS idx_eval_results_stage       ON EvalResults(Stage);
CREATE INDEX IF NOT EXISTS idx_eval_results_run_at      ON EvalResults(RunAt DESC);

-- History table replacing SQL Server Temporal Tables
-- Populated by trigger on every INSERT/UPDATE/DELETE
CREATE TABLE IF NOT EXISTS EvalResults_History (
    HistoryId         UUID          DEFAULT gen_random_uuid() PRIMARY KEY,
    EvalId            UUID          NOT NULL,
    PromptHash        CHAR(64)      NOT NULL,
    DatasetId         UUID          NOT NULL,
    GoldenScore       NUMERIC(5,4)  NULL,
    JudgeScore        NUMERIC(5,4)  NULL,
    RegressionFlag    BOOLEAN,
    Stage             TEXT          NOT NULL,
    PromotionReason   TEXT          NULL,
    RunAt             TIMESTAMPTZ,
    Operation         CHAR(1)       NOT NULL,  -- I=insert, U=update, D=delete
    ChangedAt         TIMESTAMPTZ   DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_eval_history_eval_id    ON EvalResults_History(EvalId);
CREATE INDEX IF NOT EXISTS idx_eval_history_changed_at ON EvalResults_History(ChangedAt DESC);

CREATE OR REPLACE FUNCTION fn_eval_results_history()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        INSERT INTO EvalResults_History (
            EvalId, PromptHash, DatasetId, GoldenScore, JudgeScore,
            RegressionFlag, Stage, PromotionReason, RunAt, Operation
        ) VALUES (
            NEW.EvalId, NEW.PromptHash, NEW.DatasetId, NEW.GoldenScore, NEW.JudgeScore,
            NEW.RegressionFlag, NEW.Stage, NEW.PromotionReason, NEW.RunAt, 'I'
        );
    ELSIF TG_OP = 'UPDATE' THEN
        INSERT INTO EvalResults_History (
            EvalId, PromptHash, DatasetId, GoldenScore, JudgeScore,
            RegressionFlag, Stage, PromotionReason, RunAt, Operation
        ) VALUES (
            OLD.EvalId, OLD.PromptHash, OLD.DatasetId, OLD.GoldenScore, OLD.JudgeScore,
            OLD.RegressionFlag, OLD.Stage, OLD.PromotionReason, OLD.RunAt, 'U'
        );
    ELSIF TG_OP = 'DELETE' THEN
        INSERT INTO EvalResults_History (
            EvalId, PromptHash, DatasetId, GoldenScore, JudgeScore,
            RegressionFlag, Stage, PromotionReason, RunAt, Operation
        ) VALUES (
            OLD.EvalId, OLD.PromptHash, OLD.DatasetId, OLD.GoldenScore, OLD.JudgeScore,
            OLD.RegressionFlag, OLD.Stage, OLD.PromotionReason, OLD.RunAt, 'D'
        );
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_eval_results_history ON EvalResults;

CREATE TRIGGER trg_eval_results_history
AFTER INSERT OR UPDATE OR DELETE ON EvalResults
FOR EACH ROW EXECUTE FUNCTION fn_eval_results_history();
