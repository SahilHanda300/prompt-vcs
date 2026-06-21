from fastapi import APIRouter, Depends, Query
from sqlalchemy.engine import Connection
from api.db.connection import get_db
from api.db import procedures as db
from api.models.audit import CliAuditLogItem, EnvironmentStatusItem

router = APIRouter(prefix="/audit", tags=["audit"])


@router.get("/cli", response_model=list[CliAuditLogItem])
def get_cli_audit_log(
    executed_by: str | None = Query(default=None),
    command: str | None = Query(default=None),
    prompt_name: str | None = Query(default=None),
    conn: Connection = Depends(get_db),
) -> list[CliAuditLogItem]:
    rows = db.get_cli_audit_log(conn, executed_by, command, prompt_name)
    return [CliAuditLogItem(**r) for r in rows]


@router.post("/cli", status_code=201)
def log_cli_command(body: dict, conn: Connection = Depends(get_db)) -> dict:
    db.log_cli_command(
        conn,
        executed_by=body["executed_by"],
        command=body["command"],
        arguments=body.get("arguments"),
        prompt_name=body.get("prompt_name"),
        prompt_hash=body.get("prompt_hash"),
        environment=body.get("environment"),
        outcome=body["outcome"],
        error_message=body.get("error_message"),
        cli_version=body["cli_version"],
    )
    return {"status": "ok"}


@router.get("/env", response_model=list[EnvironmentStatusItem])
def get_environment_status(
    ref_name: str | None = Query(default=None),
    conn: Connection = Depends(get_db),
) -> list[EnvironmentStatusItem]:
    rows = db.get_environment_status(conn, ref_name)
    return [EnvironmentStatusItem(**r) for r in rows]
