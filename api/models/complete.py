from pydantic import BaseModel


class CompleteRequest(BaseModel):
    text: str
    context: str | None = None


class CompleteResponse(BaseModel):
    completion: str
