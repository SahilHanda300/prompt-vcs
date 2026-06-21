from pydantic import BaseModel
from uuid import UUID
from datetime import datetime


class SubmitPromptRequest(BaseModel):
    system_template: str
    user_template: str
    model_params: dict
    submitted_by: str
    country: str
    commit_message: str
    parent_hash: str | None = None
    prompt_type: str = "chat"
    input_label: str | None = None
    input_placeholder: str | None = None
    output_label: str | None = None
    ref_name: str


class PromptResponse(BaseModel):
    promptid: UUID
    contenthash: str
    systemtemplate: str
    usertemplate: str
    modelparams: str
    submittedby: str
    country: str
    commitmessage: str
    parenthash: str | None
    prompttype: str
    inputlabel: str | None
    inputplaceholder: str | None
    outputlabel: str | None
    createdat: datetime
    environment: str | None = None
    refname: str | None = None
    updatedat: datetime | None = None


class PromptHistoryItem(BaseModel):
    promptid: UUID
    contenthash: str
    submittedby: str
    country: str
    commitmessage: str
    parenthash: str | None
    prompttype: str
    createdat: datetime
    dev_current: bool | None = None
    qa_current: bool | None = None
    prod_current: bool | None = None
