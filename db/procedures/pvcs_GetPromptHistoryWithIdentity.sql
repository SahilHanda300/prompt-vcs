-- Returns full version history for a site — every version ever evaluated
-- or currently pointed to by a ref, with environment pointers and eval scores.
CREATE OR REPLACE FUNCTION pvcs_GetPromptHistoryWithIdentity(
    p_ref_name TEXT
) RETURNS TABLE (
    promptid        UUID,
    contenthash     TEXT,
    systemtemplate  TEXT,
    usertemplate    TEXT,
    submittedby     TEXT,
    country         TEXT,
    commitmessage   TEXT,
    parenthash      TEXT,
    prompttype      TEXT,
    createdat       TIMESTAMPTZ,
    dev_current     BOOLEAN,
    qa_current      BOOLEAN,
    prod_current    BOOLEAN,
    goldenscore     NUMERIC,
    judgescore      NUMERIC,
    regressionflag  BOOLEAN,
    evalstage       TEXT
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    WITH site_prompts AS (
        -- All prompts ever evaluated against this site's dataset
        SELECT DISTINCT p.PromptId, p.ContentHash, p.SystemTemplate, p.UserTemplate,
               p.SubmittedBy, p.Country, p.CommitMessage, p.ParentHash, p.PromptType, p.CreatedAt
        FROM Prompts p
        INNER JOIN EvalResults e ON p.ContentHash = e.PromptHash
        INNER JOIN Datasets d ON e.DatasetId = d.DatasetId
        WHERE d.DatasetName = p_ref_name

        UNION

        -- Plus any version currently pointed to by a ref (covers pending/unevaluated)
        SELECT DISTINCT p.PromptId, p.ContentHash, p.SystemTemplate, p.UserTemplate,
               p.SubmittedBy, p.Country, p.CommitMessage, p.ParentHash, p.PromptType, p.CreatedAt
        FROM Prompts p
        INNER JOIN Refs r ON p.ContentHash = r.TargetHash
        WHERE r.RefName = p_ref_name
    )
    SELECT
        sp.PromptId,
        sp.ContentHash,
        sp.SystemTemplate,
        sp.UserTemplate,
        sp.SubmittedBy,
        sp.Country,
        sp.CommitMessage,
        sp.ParentHash,
        sp.PromptType,
        sp.CreatedAt,
        EXISTS (SELECT 1 FROM Refs r WHERE r.RefName = p_ref_name AND r.Environment = 'DEV'  AND r.TargetHash = sp.ContentHash) AS dev_current,
        EXISTS (SELECT 1 FROM Refs r WHERE r.RefName = p_ref_name AND r.Environment = 'QA'   AND r.TargetHash = sp.ContentHash) AS qa_current,
        EXISTS (SELECT 1 FROM Refs r WHERE r.RefName = p_ref_name AND r.Environment = 'PROD' AND r.TargetHash = sp.ContentHash) AS prod_current,
        e.GoldenScore,
        e.JudgeScore,
        e.RegressionFlag,
        e.Stage AS EvalStage
    FROM site_prompts sp
    LEFT JOIN LATERAL (
        SELECT ev.GoldenScore, ev.JudgeScore, ev.RegressionFlag, ev.Stage
        FROM EvalResults ev
        WHERE ev.PromptHash = sp.ContentHash
        ORDER BY ev.RunAt DESC
        LIMIT 1
    ) e ON TRUE
    ORDER BY sp.CreatedAt DESC;
END;
$$;
