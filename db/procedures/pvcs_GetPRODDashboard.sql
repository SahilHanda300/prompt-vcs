-- Returns full PROD metadata for the dashboard in a single call.
CREATE OR REPLACE FUNCTION pvcs_GetPRODDashboard()
RETURNS TABLE (
    refname         TEXT,
    targethash      TEXT,
    prompttype      TEXT,
    submittedby     TEXT,
    country         TEXT,
    commitmessage   TEXT,
    createdat       TIMESTAMPTZ,
    updatedat       TIMESTAMPTZ,
    goldenscore     NUMERIC,
    judgescore      NUMERIC,
    regressionflag  BOOLEAN,
    promotionstage  TEXT,
    promotionreason TEXT,
    runat           TIMESTAMPTZ
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT
        r.RefName,
        r.TargetHash::TEXT,
        p.PromptType,
        p.SubmittedBy,
        p.Country,
        p.CommitMessage,
        p.CreatedAt,
        r.UpdatedAt,
        e.GoldenScore,
        e.JudgeScore,
        e.RegressionFlag,
        e.Stage,
        e.PromotionReason,
        e.RunAt
    FROM Refs r
    INNER JOIN Prompts p ON r.TargetHash = p.ContentHash
    LEFT JOIN LATERAL (
        SELECT ev.GoldenScore, ev.JudgeScore, ev.RegressionFlag, ev.Stage, ev.PromotionReason, ev.RunAt
        FROM EvalResults ev
        WHERE ev.PromptHash = r.TargetHash
          AND ev.Stage = 'QA_TO_PROD'
        ORDER BY ev.RunAt DESC
        LIMIT 1
    ) e ON TRUE
    WHERE r.Environment = 'PROD'
    ORDER BY r.UpdatedAt DESC;
END;
$$;
