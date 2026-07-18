from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy.engine import Connection
from db.connection import get_db
from db import procedures as db
from models.site import SiteListItem, SiteDashboardItem
from models.eval_result import ProdOutputItem, DegradedTestCase

router = APIRouter(prefix="/sites", tags=["sites"])


@router.get("", response_model=list[SiteListItem])
def get_all_prod_sites(
    username: str | None = Query(default=None),
    conn: Connection = Depends(get_db),
) -> list[SiteListItem]:
    rows = db.get_all_prod_sites(conn, submitted_by=username)
    return [SiteListItem(**r) for r in rows]


@router.get("/dashboard", response_model=list[SiteDashboardItem])
def get_prod_dashboard(
    username: str | None = Query(default=None),
    conn: Connection = Depends(get_db),
) -> list[SiteDashboardItem]:
    rows = db.get_prod_dashboard(conn, submitted_by=username)
    return [SiteDashboardItem(**r) for r in rows]


@router.get("/{name}/outputs", response_model=list[ProdOutputItem])
def get_prod_outputs(
    name: str, conn: Connection = Depends(get_db)
) -> list[ProdOutputItem]:
    rows = db.get_prod_outputs(conn, name)
    return [ProdOutputItem(**r) for r in rows]
