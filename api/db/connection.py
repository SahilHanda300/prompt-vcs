import os
from sqlalchemy import create_engine
from sqlalchemy.engine import Connection
from dotenv import load_dotenv

load_dotenv()

engine = create_engine(
    os.environ["SQL_CONNECTION"],
    pool_pre_ping=True,
    pool_size=5,
    max_overflow=10,
)


def get_db():
    with engine.connect() as conn:
        yield conn
        conn.commit()
