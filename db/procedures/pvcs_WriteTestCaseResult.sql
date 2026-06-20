CREATE OR REPLACE PROCEDURE pvcs_WriteTestCaseResult(
    IN p_eval_id         UUID,
    IN p_prompt_hash     TEXT,
    IN p_test_case_index INT,
    IN p_input_text      TEXT,
    IN p_expected_output TEXT,
    IN p_actual_output   TEXT,
    IN p_golden_score    NUMERIC,
    IN p_judge_score     NUMERIC,
    IN p_judge_reasoning TEXT
) LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO TestCaseResults (
        EvalId, PromptHash, TestCaseIndex, InputText,
        ExpectedOutput, ActualOutput, GoldenScore, JudgeScore, JudgeReasoning
    ) VALUES (
        p_eval_id, p_prompt_hash, p_test_case_index, p_input_text,
        p_expected_output, p_actual_output, p_golden_score, p_judge_score, p_judge_reasoning
    );
EXCEPTION
    WHEN OTHERS THEN RAISE;
END;
$$;
