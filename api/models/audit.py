from pydantic import BaseModel
from uuid import UUID
from datetime import datetime


class CliAuditLogItem(BaseModel):
    logid: UUID
    executedby: str
    command: str
    arguments: str | None
    promptname: str | None
    prompthash: str | None
    environment: str | None
    outcome: str
    errormessage: str | None
    cliversion: str
    executedat: datetime


class PromotionAuditItem(BaseModel):
    promptname: str | None
    stage: str
    goldenscore: float | None
    judgescore: float | None
    regressionflag: bool
    promotionreason: str | None
    runat: datetime
    submittedby: str
    commitmessage: str


class RollbackRequest(BaseModel):
    ref_name: str
    environment: str
    target_hash: str
    executed_by: str


class EnvironmentStatusItem(BaseModel):
    refname: str
    environment: str
    targethash: str
    promotedfrom: str | None
    updatedat: datetime
    submittedby: str
    commitmessage: str
    createdat: datetime
