from uuid import UUID
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.engine import Connection
from db.connection import get_db
from db import procedures as db
from models.eval_result import (
    WriteEvalResultRequest,
    WriteTestCaseResultRequest,
    QAFailureItem,
    DegradedTestCase,
)

router = APIRouter(prefix="/eval", tags=["eval"])


@router.post("/results", status_code=201)
def write_eval_result(
    body: WriteEvalResultRequest, conn: Connection = Depends(get_db)
) -> dict:
    eval_id = db.write_eval_result(
        conn,
        prompt_hash=body.prompt_hash,
        dataset_id=body.dataset_id,
        golden_score=body.golden_score,
        judge_score=body.judge_score,
        regression_flag=body.regression_flag,
        stage=body.stage,
        promotion_reason=body.promotion_reason,
    )
    return {"eval_id": str(eval_id)}


@router.post("/test-cases", status_code=201)
def write_test_case_result(
    body: WriteTestCaseResultRequest, conn: Connection = Depends(get_db)
) -> dict:
    db.write_test_case_result(
        conn,
        eval_id=body.eval_id,
        prompt_hash=body.prompt_hash,
        test_case_index=body.test_case_index,
        input_text=body.input_text,
        expected_output=body.expected_output,
        actual_output=body.actual_output,
        golden_score=body.golden_score,
        judge_score=body.judge_score,
        judge_reasoning=body.judge_reasoning,
    )
    return {"status": "ok"}


@router.get("/failures", response_model=list[QAFailureItem])
def get_qa_failures(conn: Connection = Depends(get_db)) -> list[QAFailureItem]:
    rows = db.get_qa_failures(conn)
    return [QAFailureItem(**r) for r in rows]


@router.get("/failures/{eval_id}/cases", response_model=list[DegradedTestCase])
def get_degraded_test_cases(
    eval_id: UUID, conn: Connection = Depends(get_db)
) -> list[DegradedTestCase]:
    rows = db.get_degraded_test_cases(conn, eval_id)
    return [DegradedTestCase(**r) for r in rows]
