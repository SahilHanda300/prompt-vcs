CREATE OR REPLACE FUNCTION pvcs_GetEnvironmentStatus(
    p_ref_name TEXT DEFAULT NULL
) RETURNS TABLE (
    refname     TEXT,
    environment TEXT,
    targethash  TEXT,
    promotedfrom TEXT,
    updatedat   TIMESTAMPTZ,
    submittedby TEXT,
    commitmessage TEXT,
    createdat   TIMESTAMPTZ
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT
        r.RefName,
        r.Environment,
        r.TargetHash,
        r.PromotedFrom,
        r.UpdatedAt,
        p.SubmittedBy,
        p.CommitMessage,
        p.CreatedAt
    FROM Refs r
    INNER JOIN Prompts p ON r.TargetHash = p.ContentHash
    WHERE (p_ref_name IS NULL OR r.RefName = p_ref_name)
    ORDER BY r.RefName, r.Environment;
END;
$$;
