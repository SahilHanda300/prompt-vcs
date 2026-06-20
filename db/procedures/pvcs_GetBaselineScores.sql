-- Returns per-test-case scores for the current QA version.
-- Used by the evaluation pipeline for paired t-test against DEV candidate.
CREATE OR REPLACE FUNCTION pvcs_GetBaselineScores(
    p_ref_name TEXT
) RETURNS TABLE (
    testcaseindex INT,
    goldenscore   NUMERIC,
    judgescore    NUMERIC
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT
        tcr.TestCaseIndex,
        tcr.GoldenScore,
        tcr.JudgeScore
    FROM TestCaseResults tcr
    INNER JOIN Refs r ON tcr.PromptHash = r.TargetHash
    WHERE r.RefName     = p_ref_name
      AND r.Environment = 'QA'
    ORDER BY tcr.TestCaseIndex;
END;
$$;
