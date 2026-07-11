CREATE OR REPLACE PROCEDURE pvcs_LogCliCommand(
    IN p_executed_by   TEXT,
    IN p_command       TEXT,
    IN p_arguments     TEXT,
    IN p_prompt_name   TEXT,
    IN p_prompt_hash   TEXT,
    IN p_environment   TEXT,
    IN p_outcome       TEXT,
    IN p_error_message TEXT,
    IN p_cli_version   TEXT
) LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO CliAuditLog (
        ExecutedBy, Command, Arguments, PromptName, PromptHash,
        Environment, Outcome, ErrorMessage, CliVersion
    ) VALUES (
        p_executed_by, p_command, p_arguments, p_prompt_name, p_prompt_hash,
        p_environment, p_outcome, p_error_message, p_cli_version
    );
EXCEPTION
    WHEN OTHERS THEN RAISE;
END;
$$;
