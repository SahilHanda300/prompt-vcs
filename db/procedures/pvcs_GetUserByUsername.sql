-- Fetch a user by username for login verification.
-- Python compares the returned PassHash using bcrypt.
CREATE OR REPLACE FUNCTION pvcs_GetUserByUsername(
    p_username TEXT
) RETURNS TABLE (
    userid   UUID,
    name     TEXT,
    username TEXT,
    passhash TEXT
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT u.UserId, u.Name, u.Username, u.PassHash
    FROM Users u
    WHERE u.Username = LOWER(TRIM(p_username));
END;
$$;
