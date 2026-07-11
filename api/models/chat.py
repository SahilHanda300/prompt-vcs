from pydantic import BaseModel
from uuid import UUID


class ChatRequest(BaseModel):
    session_id: UUID
    user_message: str
    username: str | None = None


class ChatLogRequest(BaseModel):
    session_id: UUID
    prompt_hash: str
    user_message: str
    bot_response: str
    response_ms: int
