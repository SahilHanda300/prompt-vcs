CREATE TABLE IF NOT EXISTS Datasets (
    DatasetId    UUID          DEFAULT gen_random_uuid() PRIMARY KEY,
    DatasetName  TEXT          NOT NULL,
    TestCases    TEXT          NOT NULL,
    Owner        TEXT          NOT NULL,
    CreatedAt    TIMESTAMPTZ   DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_datasets_owner ON Datasets(Owner);
