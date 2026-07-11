"""Pipeline DB wrappers — mirrors api/db/procedures.py for pipeline-only calls."""
from typing import Any
from uuid import UUID
from sqlalchemy import text
from sqlalchemy.engine import Connection


def get_prompt_by_hash(conn: Connection, content_hash: str) -> dict[str, Any] | None:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetPromptByHash(:hash)"),
        {"hash": content_hash},
    )
    row = result.mappings().first()
    return dict(row) if row else None


def get_dataset_by_name(conn: Connection, dataset_name: str) -> dict[str, Any] | None:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetDatasetByName(:name)"),
        {"name": dataset_name},
    )
    row = result.mappings().first()
    return dict(row) if row else None


def get_baseline_scores(conn: Connection, ref_name: str) -> list[dict[str, Any]]:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetBaselineScores(:ref)"),
        {"ref": ref_name},
    )
    return [dict(row) for row in result.mappings().all()]


def write_eval_result(
    conn: Connection,
    prompt_hash: str,
    dataset_id: UUID,
    golden_score: float,
    judge_score: float,
    regression_flag: bool,
    stage: str,
    promotion_reason: str,
) -> UUID:
    result = conn.execute(
        text("CALL pvcs_WriteEvalResult(:hash, :dataset, :golden, :judge, :flag, :stage, :reason, NULL)"),
        {
            "hash": prompt_hash,
            "dataset": str(dataset_id),
            "golden": golden_score,
            "judge": judge_score,
            "flag": bool(regression_flag),
            "stage": stage,
            "reason": promotion_reason,
        },
    )
    row = result.fetchone()
    return row[0]


def write_test_case_result(
    conn: Connection,
    eval_id: UUID,
    prompt_hash: str,
    test_case_index: int,
    input_text: str,
    expected_output: str,
    actual_output: str,
    golden_score: float,
    judge_score: float,
    judge_reasoning: str | None,
) -> None:
    conn.execute(
        text("CALL pvcs_WriteTestCaseResult(:eval_id, :hash, :idx, :input, :expected, :actual, :golden, :judge, :reasoning)"),
        {
            "eval_id": str(eval_id),
            "hash": prompt_hash,
            "idx": test_case_index,
            "input": input_text,
            "expected": expected_output,
            "actual": actual_output,
            "golden": golden_score,
            "judge": judge_score,
            "reasoning": judge_reasoning,
        },
    )


def promote_to_qa(conn: Connection, ref_name: str, prompt_hash: str) -> None:
    conn.execute(
        text("CALL pvcs_PromoteToQA(:ref, :hash)"),
        {"ref": ref_name, "hash": prompt_hash},
    )


def promote_to_prod(conn: Connection, ref_name: str, prompt_hash: str) -> None:
    conn.execute(
        text("CALL pvcs_PromoteToPROD(:ref, :hash)"),
        {"ref": ref_name, "hash": prompt_hash},
    )
