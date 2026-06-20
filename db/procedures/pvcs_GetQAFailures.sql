CREATE OR REPLACE FUNCTION pvcs_GetQAFailures()
RETURNS TABLE (
    evalid          UUID,
    prompthash      TEXT,
    refname         TEXT,
    goldenscore     NUMERIC,
    judgescore      NUMERIC,
    stage           TEXT,
    promotionreason TEXT,
    runat           TIMESTAMPTZ,
    submittedby     TEXT,
    commitmessage   TEXT
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT
        e.EvalId,
        e.PromptHash,
        r.RefName,
        e.GoldenScore,
        e.JudgeScore,
        e.Stage,
        e.PromotionReason,
        e.RunAt,
        p.SubmittedBy,
        p.CommitMessage
    FROM EvalResults e
    INNER JOIN Prompts p ON e.PromptHash = p.ContentHash
    LEFT JOIN Refs r ON e.PromptHash = r.TargetHash AND r.Environment = 'DEV'
    WHERE e.RegressionFlag = TRUE
    ORDER BY e.RunAt DESC;
END;
$$;
