import os
from contextlib import contextmanager
from sqlalchemy import create_engine
from sqlalchemy.engine import Connection

_engine = create_engine(os.environ["SQL_CONNECTION"], pool_pre_ping=True)


@contextmanager
def get_connection():
    conn = _engine.connect()
    try:
        yield conn
        conn.commit()
    except Exception:
        conn.rollback()
        raise
    finally:
        conn.close()
