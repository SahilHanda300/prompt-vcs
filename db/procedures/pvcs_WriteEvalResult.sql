CREATE OR REPLACE PROCEDURE pvcs_WriteEvalResult(
    IN  p_prompt_hash      TEXT,
    IN  p_dataset_id       UUID,
    IN  p_golden_score     NUMERIC,
    IN  p_judge_score      NUMERIC,
    IN  p_regression_flag  BOOLEAN,
    IN  p_stage            TEXT,
    IN  p_promotion_reason TEXT,
    OUT p_eval_id          UUID
) LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO EvalResults (
        PromptHash, DatasetId, GoldenScore, JudgeScore,
        RegressionFlag, Stage, PromotionReason
    ) VALUES (
        p_prompt_hash, p_dataset_id, p_golden_score, p_judge_score,
        p_regression_flag, p_stage, p_promotion_reason
    )
    RETURNING EvalId INTO p_eval_id;
EXCEPTION
    WHEN OTHERS THEN RAISE;
END;
$$;
