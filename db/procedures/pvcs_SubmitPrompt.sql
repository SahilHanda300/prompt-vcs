CREATE OR REPLACE PROCEDURE pvcs_SubmitPrompt(
    IN p_content_hash      TEXT,
    IN p_system_template   TEXT,
    IN p_user_template     TEXT,
    IN p_model_params      TEXT,
    IN p_submitted_by      TEXT,
    IN p_country           TEXT,
    IN p_commit_message    TEXT,
    IN p_parent_hash       TEXT,
    IN p_prompt_type       TEXT,
    IN p_input_label       TEXT,
    IN p_input_placeholder TEXT,
    IN p_output_label      TEXT,
    IN p_ref_name          TEXT
) LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO Prompts (
        ContentHash, SystemTemplate, UserTemplate, ModelParams,
        SubmittedBy, Country, CommitMessage, ParentHash,
        PromptType, InputLabel, InputPlaceholder, OutputLabel
    ) VALUES (
        p_content_hash, p_system_template, p_user_template, p_model_params,
        p_submitted_by, p_country, p_commit_message, p_parent_hash,
        p_prompt_type, p_input_label, p_input_placeholder, p_output_label
    );

    INSERT INTO Refs (RefName, Environment, TargetHash, RefType, PromotedFrom, UpdatedAt)
    VALUES (p_ref_name, 'DEV', p_content_hash, 'branch', NULL, NOW())
    ON CONFLICT (RefName, Environment) DO UPDATE
        SET TargetHash   = EXCLUDED.TargetHash,
            PromotedFrom = Refs.TargetHash,
            UpdatedAt    = NOW();
EXCEPTION
    WHEN OTHERS THEN RAISE;
END;
$$;
