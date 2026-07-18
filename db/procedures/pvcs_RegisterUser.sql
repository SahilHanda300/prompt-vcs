-- Insert a new user. Password hashing is done in Python before calling this.
-- Returns the new UserId.
CREATE OR REPLACE FUNCTION pvcs_RegisterUser(
    p_name     TEXT,
    p_username TEXT,
    p_passhash TEXT
) RETURNS UUID LANGUAGE plpgsql AS $$
DECLARE
    v_id UUID;
BEGIN
    INSERT INTO Users (Name, Username, PassHash)
    VALUES (p_name, LOWER(TRIM(p_username)), p_passhash)
    RETURNING UserId INTO v_id;

    RETURN v_id;
EXCEPTION
    WHEN unique_violation THEN
        RAISE EXCEPTION 'USERNAME_EXISTS';
END;
$$;
