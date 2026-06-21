import os
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.engine import Connection
from sqlalchemy.exc import ProgrammingError
from api.db.connection import get_db
from api.db import procedures as db
from api.models.audit import RollbackRequest

router = APIRouter(prefix="/rollback", tags=["rollback"])


@router.post("")
def rollback(body: RollbackRequest, conn: Connection = Depends(get_db)) -> dict:
    owner_username = os.environ["OWNER_USERNAME"]
    try:
        db.rollback(
            conn,
            ref_name=body.ref_name,
            environment=body.environment.upper(),
            target_hash=body.target_hash,
            executed_by=body.executed_by,
            owner_username=owner_username,
        )
    except ProgrammingError as e:
        raise HTTPException(status_code=403, detail=str(e.orig))
    return {"status": "ok", "environment": body.environment, "target_hash": body.target_hash}
