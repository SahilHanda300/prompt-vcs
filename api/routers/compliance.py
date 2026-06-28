from fastapi import APIRouter, Depends, Query
from sqlalchemy.engine import Connection
from db.connection import get_db
from db import procedures as db
from models.audit import PromotionAuditItem

router = APIRouter(prefix="/compliance", tags=["compliance"])


@router.get("/audit-trail", response_model=list[PromotionAuditItem])
def get_promotion_audit_trail(
    ref_name: str | None = Query(default=None),
    conn: Connection = Depends(get_db),
) -> list[PromotionAuditItem]:
    rows = db.get_promotion_audit_trail(conn, ref_name)
    return [PromotionAuditItem(**r) for r in rows]
