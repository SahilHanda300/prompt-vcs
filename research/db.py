"""Shared DB connection for research scripts (direct SQL — analytics only)."""
import os
from contextlib import contextmanager
from sqlalchemy import create_engine, text
from dotenv import load_dotenv

load_dotenv()

_engine = create_engine(os.environ["SQL_CONNECTION"], pool_pre_ping=True)


@contextmanager
def get_connection():
    conn = _engine.connect()
    try:
        yield conn
    finally:
        conn.close()


def query(conn, sql: str, params: dict = None) -> list[dict]:
    result = conn.execute(text(sql), params or {})
    return [dict(row) for row in result.mappings().all()]
