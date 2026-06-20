CREATE OR REPLACE FUNCTION pvcs_GetPromptByEnvironment(
    p_ref_name    TEXT,
    p_environment TEXT
) RETURNS TABLE (
    promptid        UUID,
    contenthash     TEXT,
    systemtemplate  TEXT,
    usertemplate    TEXT,
    modelparams     TEXT,
    submittedby     TEXT,
    country         TEXT,
    commitmessage   TEXT,
    parenthash      TEXT,
    prompttype      TEXT,
    inputlabel      TEXT,
    inputplaceholder TEXT,
    outputlabel     TEXT,
    createdat       TIMESTAMPTZ,
    environment     TEXT,
    refname         TEXT,
    updatedat       TIMESTAMPTZ
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT
        p.PromptId,
        p.ContentHash,
        p.SystemTemplate,
        p.UserTemplate,
        p.ModelParams,
        p.SubmittedBy,
        p.Country,
        p.CommitMessage,
        p.ParentHash,
        p.PromptType,
        p.InputLabel,
        p.InputPlaceholder,
        p.OutputLabel,
        p.CreatedAt,
        r.Environment,
        r.RefName,
        r.UpdatedAt
    FROM Prompts p
    INNER JOIN Refs r ON p.ContentHash = r.TargetHash
    WHERE r.RefName    = p_ref_name
      AND r.Environment = p_environment;
END;
$$;
