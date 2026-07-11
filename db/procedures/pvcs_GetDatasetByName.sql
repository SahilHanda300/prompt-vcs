CREATE OR REPLACE FUNCTION pvcs_GetDatasetByName(
    p_dataset_name TEXT
) RETURNS TABLE (
    datasetid   UUID,
    datasetname TEXT,
    testcases   TEXT,
    owner       TEXT,
    createdat   TIMESTAMPTZ
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT
        d.DatasetId,
        d.DatasetName,
        d.TestCases,
        d.Owner,
        d.CreatedAt
    FROM Datasets d
    WHERE d.DatasetName = p_dataset_name
    LIMIT 1;
END;
$$;
