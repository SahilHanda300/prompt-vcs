import hashlib
import json
import threading
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.engine import Connection
from db.connection import get_db
from db import procedures as db
from models.prompt import SubmitPromptRequest, PromptResponse, PromptHistoryItem
from dispatch import trigger_evaluation
from routers.dataset_generator import generate_test_cases, generate_ui_test_cases
from routers.ui_generator import generate_ui

router = APIRouter(prefix="/prompts", tags=["prompts"])


@router.post("/submit", status_code=201)
def submit_prompt(body: SubmitPromptRequest, conn: Connection = Depends(get_db)) -> dict:
    model_params_str = json.dumps(body.model_params)
    content = body.system_template + body.user_template + model_params_str
    content_hash = hashlib.sha256(content.encode()).hexdigest()

    existing = db.get_prompt_by_hash(conn, content_hash)
    if existing:
        raise HTTPException(status_code=409, detail="Identical prompt already exists.")

    # For generated-ui: description → HTML (description stored in commit_message)
    if body.prompt_type == 'generated-ui':
        description = body.system_template
        try:
            html = generate_ui(description)
        except ValueError as e:
            raise HTTPException(status_code=422, detail=str(e))
        body = body.model_copy(update={
            'system_template': html,
            'commit_message': description,
        })
        content = body.system_template + body.user_template + model_params_str
        content_hash = hashlib.sha256(content.encode()).hexdigest()

    # Auto-generate dataset if this is a new site
    existing_dataset = db.get_dataset_by_name(conn, body.ref_name)
    if not existing_dataset:
        try:
            if body.prompt_type == 'generated-ui':
                cases = generate_ui_test_cases(body.commit_message, body.system_template)
            else:
                cases = generate_test_cases(body.system_template)
            db.create_dataset(conn, body.ref_name, json.dumps(cases), body.submitted_by)
        except ValueError as e:
            raise HTTPException(status_code=422, detail=str(e))

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
    threading.Thread(
        target=trigger_evaluation,
        args=(body.ref_name, content_hash),
        daemon=True,
    ).start()

    return {"content_hash": content_hash, "ref_name": body.ref_name}


@router.get("/eval-status/{content_hash}")
def get_eval_status(content_hash: str, conn: Connection = Depends(get_db)) -> dict:
    row = db.get_latest_eval_by_hash(conn, content_hash)
    if not row:
        return {"status": "pending"}
    return {
        "status": "failed" if row["regressionflag"] else "passed",
        "stage": row["stage"],
        "reason": row["promotionreason"],
    }


@router.get("/hash/{content_hash}", response_model=PromptResponse)
def get_prompt_by_hash(
    content_hash: str, conn: Connection = Depends(get_db)
) -> PromptResponse:
    row = db.get_prompt_by_hash(conn, content_hash)
    if not row:
        raise HTTPException(status_code=404, detail="Prompt not found.")
    return PromptResponse(**row)


@router.get("/{ref_name}/history/identity", response_model=list[PromptHistoryItem])
def get_prompt_history_with_identity(
    ref_name: str, conn: Connection = Depends(get_db)
) -> list[PromptHistoryItem]:
    rows = db.get_prompt_history_with_identity(conn, ref_name)
    return [PromptHistoryItem(**r) for r in rows]


@router.get("/{ref_name}/history", response_model=list[PromptHistoryItem])
def get_prompt_history(
    ref_name: str, conn: Connection = Depends(get_db)
) -> list[PromptHistoryItem]:
    rows = db.get_prompt_history(conn, ref_name)
    return [PromptHistoryItem(**r) for r in rows]


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
