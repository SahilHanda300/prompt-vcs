CREATE OR REPLACE FUNCTION pvcs_GetPromptHistory(
    p_ref_name TEXT
) RETURNS TABLE (
    promptid      UUID,
    contenthash   TEXT,
    submittedby   TEXT,
    country       TEXT,
    commitmessage TEXT,
    parenthash    TEXT,
    prompttype    TEXT,
    createdat     TIMESTAMPTZ
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    WITH RECURSIVE history AS (
        -- Anchor: current DEV version for this ref
        SELECT
            p.PromptId, p.ContentHash::TEXT, p.SubmittedBy, p.Country,
            p.CommitMessage, p.ParentHash::TEXT, p.PromptType, p.CreatedAt
        FROM Prompts p
        INNER JOIN Refs r ON p.ContentHash = r.TargetHash
        WHERE r.RefName = p_ref_name
          AND r.Environment = 'DEV'

        UNION ALL

        -- Traverse parent chain backwards
        SELECT
            p.PromptId, p.ContentHash::TEXT, p.SubmittedBy, p.Country,
            p.CommitMessage, p.ParentHash::TEXT, p.PromptType, p.CreatedAt
        FROM Prompts p
        INNER JOIN history h ON p.ContentHash = h.ParentHash
        WHERE h.ParentHash IS NOT NULL
    )
    SELECT * FROM history ORDER BY CreatedAt DESC;
END;
$$;
