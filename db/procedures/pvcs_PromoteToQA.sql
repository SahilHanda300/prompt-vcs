CREATE OR REPLACE PROCEDURE pvcs_PromoteToQA(
    IN p_ref_name    TEXT,
    IN p_prompt_hash TEXT
) LANGUAGE plpgsql AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM Refs
        WHERE RefName = p_ref_name AND Environment = 'DEV' AND TargetHash = p_prompt_hash
    ) THEN
        RAISE EXCEPTION 'Hash % is not the current DEV version for ref %', p_prompt_hash, p_ref_name;
    END IF;

    INSERT INTO Refs (RefName, Environment, TargetHash, RefType, PromotedFrom, UpdatedAt)
    VALUES (p_ref_name, 'QA', p_prompt_hash, 'branch', NULL, NOW())
    ON CONFLICT (RefName, Environment) DO UPDATE
        SET TargetHash   = EXCLUDED.TargetHash,
            PromotedFrom = Refs.TargetHash,
            UpdatedAt    = NOW();
EXCEPTION
    WHEN OTHERS THEN RAISE;
END;
$$;
