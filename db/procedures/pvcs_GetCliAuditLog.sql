CREATE OR REPLACE FUNCTION pvcs_GetCliAuditLog(
    p_executed_by TEXT DEFAULT NULL,
    p_command     TEXT DEFAULT NULL,
    p_prompt_name TEXT DEFAULT NULL
) RETURNS TABLE (
    logid        UUID,
    executedby   TEXT,
    command      TEXT,
    arguments    TEXT,
    promptname   TEXT,
    prompthash   TEXT,
    environment  TEXT,
    outcome      TEXT,
    errormessage TEXT,
    cliversion   TEXT,
    executedat   TIMESTAMPTZ
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT
        c.LogId,
        c.ExecutedBy,
        c.Command,
        c.Arguments,
        c.PromptName,
        c.PromptHash::TEXT,
        c.Environment,
        c.Outcome,
        c.ErrorMessage,
        c.CliVersion,
        c.ExecutedAt
    FROM CliAuditLog c
    WHERE (p_executed_by IS NULL OR c.ExecutedBy = p_executed_by)
      AND (p_command     IS NULL OR c.Command    = p_command)
      AND (p_prompt_name IS NULL OR c.PromptName = p_prompt_name)
    ORDER BY c.ExecutedAt DESC;
END;
$$;
