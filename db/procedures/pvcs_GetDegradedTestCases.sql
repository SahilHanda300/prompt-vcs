-- Returns test cases that scored below threshold for a given eval run.
-- Used by the SPA QA failures panel to show what broke and why.
CREATE OR REPLACE FUNCTION pvcs_GetDegradedTestCases(
    p_eval_id UUID
) RETURNS TABLE (
    resultid        UUID,
    testcaseindex   INT,
    inputtext       TEXT,
    expectedoutput  TEXT,
    actualoutput    TEXT,
    goldenscore     NUMERIC,
    judgescore      NUMERIC,
    judgereasoning  TEXT,
    createdat       TIMESTAMPTZ
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT
        tcr.ResultId,
        tcr.TestCaseIndex,
        tcr.InputText,
        tcr.ExpectedOutput,
        tcr.ActualOutput,
        tcr.GoldenScore,
        tcr.JudgeScore,
        tcr.JudgeReasoning,
        tcr.CreatedAt
    FROM TestCaseResults tcr
    WHERE tcr.EvalId     = p_eval_id
      AND tcr.JudgeScore < 3
    ORDER BY tcr.JudgeScore ASC;
END;
$$;
