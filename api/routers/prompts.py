import hashlib
import json
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.engine import Connection
from api.db.connection import get_db
from api.db import procedures as db
from api.models.prompt import SubmitPromptRequest, PromptResponse, PromptHistoryItem

router = APIRouter(prefix="/prompts", tags=["prompts"])


@router.post("/submit", status_code=201)
def submit_prompt(body: SubmitPromptRequest, conn: Connection = Depends(get_db)) -> dict:
    model_params_str = json.dumps(body.model_params)
    content = body.system_template + body.user_template + model_params_str
    content_hash = hashlib.sha256(content.encode()).hexdigest()

    existing = db.get_prompt_by_hash(conn, content_hash)
    if existing:
        raise HTTPException(status_code=409, detail="Identical prompt already exists.")

    db.submit_prompt(
        conn,
        content_hash=content_hash,
        system_template=body.system_template,
        user_template=body.user_template,
        model_params=model_params_str,
        submitted_by=body.submitted_by,
        country=body.country,
        commit_message=body.commit_message,
        parent_hash=body.parent_hash,
        prompt_type=body.prompt_type,
        input_label=body.input_label,
        input_placeholder=body.input_placeholder,
        output_label=body.output_label,
        ref_name=body.ref_name,
    )
    return {"content_hash": content_hash, "ref_name": body.ref_name}


@router.get("/{ref_name}/{environment}", response_model=PromptResponse)
def get_prompt_by_environment(
    ref_name: str,
    environment: str,
    conn: Connection = Depends(get_db),
) -> PromptResponse:
    row = db.get_prompt_by_environment(conn, ref_name, environment.upper())
    if not row:
        raise HTTPException(status_code=404, detail="Prompt not found.")
    return PromptResponse(**row)


@router.get("/hash/{content_hash}", response_model=PromptResponse)
def get_prompt_by_hash(
    content_hash: str, conn: Connection = Depends(get_db)
) -> PromptResponse:
    row = db.get_prompt_by_hash(conn, content_hash)
    if not row:
        raise HTTPException(status_code=404, detail="Prompt not found.")
    return PromptResponse(**row)


@router.get("/{ref_name}/history", response_model=list[PromptHistoryItem])
def get_prompt_history(
    ref_name: str, conn: Connection = Depends(get_db)
) -> list[PromptHistoryItem]:
    rows = db.get_prompt_history(conn, ref_name)
    return [PromptHistoryItem(**r) for r in rows]


@router.get("/{ref_name}/history/identity", response_model=list[PromptHistoryItem])
def get_prompt_history_with_identity(
    ref_name: str, conn: Connection = Depends(get_db)
) -> list[PromptHistoryItem]:
    rows = db.get_prompt_history_with_identity(conn, ref_name)
    return [PromptHistoryItem(**r) for r in rows]
