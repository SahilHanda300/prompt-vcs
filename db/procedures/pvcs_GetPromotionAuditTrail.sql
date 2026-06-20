-- EU AI Act Article 12 compliance.
-- Returns every automated promotion decision with PromotionReason.
-- NULL p_ref_name returns all prompts.
CREATE OR REPLACE FUNCTION pvcs_GetPromotionAuditTrail(
    p_ref_name TEXT DEFAULT NULL
) RETURNS TABLE (
    promptname      TEXT,
    stage           TEXT,
    goldenscore     NUMERIC,
    judgescore      NUMERIC,
    regressionflag  BOOLEAN,
    promotionreason TEXT,
    runat           TIMESTAMPTZ,
    submittedby     TEXT,
    commitmessage   TEXT
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT
        r.RefName,
        e.Stage,
        e.GoldenScore,
        e.JudgeScore,
        e.RegressionFlag,
        e.PromotionReason,
        e.RunAt,
        p.SubmittedBy,
        p.CommitMessage
    FROM EvalResults e
    INNER JOIN Prompts p ON e.PromptHash = p.ContentHash
    LEFT JOIN Refs r ON p.ContentHash = r.TargetHash AND r.Environment = 'DEV'
    WHERE (p_ref_name IS NULL OR r.RefName = p_ref_name)
    ORDER BY e.RunAt DESC;
END;
$$;
