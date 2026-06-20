-- SubmittedBy is the name typed on the submission form (no SSO).
-- This function extends GetPromptHistory with environment pointers
-- so the caller can see which version is currently in DEV / QA / PROD.
CREATE OR REPLACE FUNCTION pvcs_GetPromptHistoryWithIdentity(
    p_ref_name TEXT
) RETURNS TABLE (
    promptid      UUID,
    contenthash   TEXT,
    submittedby   TEXT,
    country       TEXT,
    commitmessage TEXT,
    parenthash    TEXT,
    prompttype    TEXT,
    createdat     TIMESTAMPTZ,
    dev_current   BOOLEAN,
    qa_current    BOOLEAN,
    prod_current  BOOLEAN
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    WITH RECURSIVE history AS (
        SELECT
            p.PromptId, p.ContentHash, p.SubmittedBy, p.Country,
            p.CommitMessage, p.ParentHash, p.PromptType, p.CreatedAt
        FROM Prompts p
        INNER JOIN Refs r ON p.ContentHash = r.TargetHash
        WHERE r.RefName = p_ref_name
          AND r.Environment = 'DEV'

        UNION ALL

        SELECT
            p.PromptId, p.ContentHash, p.SubmittedBy, p.Country,
            p.CommitMessage, p.ParentHash, p.PromptType, p.CreatedAt
        FROM Prompts p
        INNER JOIN history h ON p.ContentHash = h.ParentHash
        WHERE h.ParentHash IS NOT NULL
    )
    SELECT
        h.PromptId,
        h.ContentHash,
        h.SubmittedBy,
        h.Country,
        h.CommitMessage,
        h.ParentHash,
        h.PromptType,
        h.CreatedAt,
        EXISTS (SELECT 1 FROM Refs r WHERE r.RefName = p_ref_name AND r.Environment = 'DEV'  AND r.TargetHash = h.ContentHash) AS dev_current,
        EXISTS (SELECT 1 FROM Refs r WHERE r.RefName = p_ref_name AND r.Environment = 'QA'   AND r.TargetHash = h.ContentHash) AS qa_current,
        EXISTS (SELECT 1 FROM Refs r WHERE r.RefName = p_ref_name AND r.Environment = 'PROD' AND r.TargetHash = h.ContentHash) AS prod_current
    FROM history h
    ORDER BY h.CreatedAt DESC;
END;
$$;
