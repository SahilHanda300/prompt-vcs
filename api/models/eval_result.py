from pydantic import BaseModel
from uuid import UUID
from datetime import datetime


class WriteEvalResultRequest(BaseModel):
    prompt_hash: str
    dataset_id: UUID
    golden_score: float
    judge_score: float
    regression_flag: bool
    stage: str
    promotion_reason: str


class WriteTestCaseResultRequest(BaseModel):
    eval_id: UUID
    prompt_hash: str
    test_case_index: int
    input_text: str
    expected_output: str
    actual_output: str
    golden_score: float
    judge_score: float
    judge_reasoning: str | None = None


class QAFailureItem(BaseModel):
    evalid: UUID
    prompthash: str
    refname: str | None
    goldenscore: float | None
    judgescore: float | None
    stage: str
    promotionreason: str | None
    runat: datetime
    submittedby: str
    commitmessage: str


class DegradedTestCase(BaseModel):
    resultid: UUID
    testcaseindex: int
    inputtext: str
    expectedoutput: str
    actualoutput: str
    goldenscore: float
    judgescore: float
    judgereasoning: str | None
    createdat: datetime


class ProdOutputItem(BaseModel):
    resultid: UUID
    testcaseindex: int
    inputtext: str
    expectedoutput: str
    actualoutput: str
    goldenscore: float
    judgescore: float
    judgereasoning: str | None
    createdat: datetime
