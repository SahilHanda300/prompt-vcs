CREATE OR REPLACE FUNCTION pvcs_GetLatestEvalByHash(p_prompt_hash TEXT)
RETURNS TABLE (
    stage           TEXT,
    goldenscore     NUMERIC,
    judgescore      NUMERIC,
    regressionflag  BOOLEAN,
    promotionreason TEXT,
    runat           TIMESTAMPTZ
) LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT e.Stage, e.GoldenScore, e.JudgeScore, e.RegressionFlag, e.PromotionReason, e.RunAt
    FROM EvalResults e
    WHERE e.PromptHash = p_prompt_hash
    ORDER BY e.RunAt DESC
    LIMIT 1;
END;
$$;
