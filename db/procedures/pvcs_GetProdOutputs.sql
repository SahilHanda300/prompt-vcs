CREATE OR REPLACE FUNCTION pvcs_GetProdOutputs(
    p_ref_name TEXT
) RETURNS TABLE (
    resultid       UUID,
    testcaseindex  INT,
    inputtext      TEXT,
    expectedoutput TEXT,
    actualoutput   TEXT,
    goldenscore    NUMERIC,
    judgescore     NUMERIC,
    judgereasoning TEXT,
    createdat      TIMESTAMPTZ
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
    INNER JOIN Refs r ON tcr.PromptHash = r.TargetHash
    WHERE r.RefName     = p_ref_name
      AND r.Environment = 'PROD'
    ORDER BY tcr.TestCaseIndex;
END;
$$;
