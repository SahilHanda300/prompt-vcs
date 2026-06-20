CREATE OR REPLACE FUNCTION pvcs_GetPromptByHash(
    p_content_hash TEXT
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
    createdat       TIMESTAMPTZ
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
        p.CreatedAt
    FROM Prompts p
    WHERE p.ContentHash = p_content_hash;
END;
$$;
