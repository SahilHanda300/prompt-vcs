CREATE OR REPLACE PROCEDURE pvcs_LogChat(
    IN p_session_id   UUID,
    IN p_prompt_hash  TEXT,
    IN p_user_message TEXT,
    IN p_bot_response TEXT,
    IN p_response_ms  INT
) LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO ChatLogs (SessionId, PromptHash, UserMessage, BotResponse, ResponseMs)
    VALUES (p_session_id, p_prompt_hash, p_user_message, p_bot_response, p_response_ms);
EXCEPTION
    WHEN OTHERS THEN RAISE;
END;
$$;
