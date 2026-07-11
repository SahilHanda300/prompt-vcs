CREATE OR REPLACE FUNCTION pvcs_GetChatHistory(
    p_username TEXT,
    p_refname  TEXT
) RETURNS TABLE (
    usermessage TEXT,
    botresponse TEXT,
    createdat   TIMESTAMPTZ
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT cl.UserMessage, cl.BotResponse, cl.CreatedAt
    FROM ChatLogs cl
    WHERE cl.Username = p_username
      AND cl.RefName  = p_refname
    ORDER BY cl.CreatedAt ASC;
END;
$$;
