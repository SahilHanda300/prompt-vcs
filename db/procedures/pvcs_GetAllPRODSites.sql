-- Returns all PROD prompts for the SPA sidebar (polled every 30s).
CREATE OR REPLACE FUNCTION pvcs_GetAllPRODSites()
RETURNS TABLE (
    refname          TEXT,
    targethash       TEXT,
    prompttype       TEXT,
    submittedby      TEXT,
    country          TEXT,
    inputlabel       TEXT,
    inputplaceholder TEXT,
    outputlabel      TEXT,
    updatedat        TIMESTAMPTZ,
    goldenscore      NUMERIC,
    judgescore       NUMERIC,
    promotionreason  TEXT
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT
        r.RefName,
        r.TargetHash,
        p.PromptType,
        p.SubmittedBy,
        p.Country,
        p.InputLabel,
        p.InputPlaceholder,
        p.OutputLabel,
        r.UpdatedAt,
        e.GoldenScore,
        e.JudgeScore,
        e.PromotionReason
    FROM Refs r
    INNER JOIN Prompts p ON r.TargetHash = p.ContentHash
    LEFT JOIN LATERAL (
        SELECT GoldenScore, JudgeScore, PromotionReason
        FROM EvalResults
        WHERE PromptHash = r.TargetHash
          AND Stage = 'QA_TO_PROD'
        ORDER BY RunAt DESC
        LIMIT 1
    ) e ON TRUE
    WHERE r.Environment = 'PROD'
    ORDER BY r.UpdatedAt DESC;
END;
$$;
