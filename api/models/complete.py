from pydantic import BaseModel


class CompleteRequest(BaseModel):
    text: str


class CompleteResponse(BaseModel):
    completion: str
