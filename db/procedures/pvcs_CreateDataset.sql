CREATE OR REPLACE FUNCTION pvcs_CreateDataset(
    p_dataset_name TEXT,
    p_test_cases   TEXT,
    p_owner        TEXT
)
RETURNS UUID
LANGUAGE plpgsql
AS $$
DECLARE
    v_dataset_id UUID;
BEGIN
    INSERT INTO Datasets (DatasetId, DatasetName, TestCases, Owner)
    VALUES (gen_random_uuid(), p_dataset_name, p_test_cases, p_owner)
    RETURNING DatasetId INTO v_dataset_id;

    RETURN v_dataset_id;
EXCEPTION
    WHEN OTHERS THEN
        RAISE;
END;
$$;
