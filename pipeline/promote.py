"""Calls the appropriate promotion procedure based on the stage."""
from sqlalchemy.engine import Connection
from procedures import promote_to_qa, promote_to_prod


def promote_prompt(conn: Connection, ref_name: str, prompt_hash: str, stage: str) -> None:
    if stage == "DEV_TO_QA":
        promote_to_qa(conn, ref_name, prompt_hash)
    elif stage == "QA_TO_PROD":
        promote_to_prod(conn, ref_name, prompt_hash)
    else:
        raise ValueError(f"Unknown stage: {stage}")
