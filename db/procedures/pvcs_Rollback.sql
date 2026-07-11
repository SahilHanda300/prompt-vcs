CREATE OR REPLACE PROCEDURE pvcs_Rollback(
    IN p_ref_name       TEXT,
    IN p_environment    TEXT,
    IN p_target_hash    TEXT,
    IN p_executed_by    TEXT,
    IN p_owner_username TEXT
) LANGUAGE plpgsql AS $$
BEGIN
    IF p_environment = 'PROD' AND p_executed_by != p_owner_username THEN
        RAISE EXCEPTION 'PROD rollback restricted to owner account.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM Prompts WHERE ContentHash = p_target_hash) THEN
        RAISE EXCEPTION 'Target hash % does not exist.', p_target_hash;
    END IF;

    UPDATE Refs
    SET TargetHash   = p_target_hash,
        PromotedFrom = TargetHash,
        UpdatedAt    = NOW()
    WHERE RefName = p_ref_name AND Environment = p_environment;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Ref % in environment % not found.', p_ref_name, p_environment;
    END IF;
EXCEPTION
    WHEN OTHERS THEN RAISE;
END;
$$;
