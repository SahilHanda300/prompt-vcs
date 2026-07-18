-- Returns PROD prompts for the SPA sidebar, filtered by submitter.
CREATE OR REPLACE FUNCTION pvcs_GetAllPRODSites(
    p_submitted_by TEXT DEFAULT NULL
)
RETURNS TABLE (
    refname          TEXT,
    targethash       TEXT,
    prompttype       TEXT,
    systemtemplate   TEXT,
    commitmessage    TEXT,
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
        r.TargetHash::TEXT,
        p.PromptType,
        p.SystemTemplate,
        p.CommitMessage,
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
        SELECT ev.GoldenScore, ev.JudgeScore, ev.PromotionReason
        FROM EvalResults ev
        WHERE ev.PromptHash = r.TargetHash
          AND ev.Stage = 'QA_TO_PROD'
        ORDER BY ev.RunAt DESC
        LIMIT 1
    ) e ON TRUE
    WHERE r.Environment = 'PROD'
      AND (p_submitted_by IS NULL OR p.SubmittedBy = p_submitted_by)
    ORDER BY r.UpdatedAt DESC;
END;
$$;
