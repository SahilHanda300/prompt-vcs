-- Fetch a user by email for login verification.
-- Python compares the returned PassHash using bcrypt.
CREATE OR REPLACE FUNCTION pvcs_GetUserByEmail(
    p_email TEXT
) RETURNS TABLE (
    userid   UUID,
    name     TEXT,
    email    TEXT,
    passhash TEXT
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT u.UserId, u.Name, u.Email, u.PassHash
    FROM Users u
    WHERE u.Email = LOWER(TRIM(p_email));
END;
$$;
