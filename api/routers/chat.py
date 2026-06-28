import os
import time
from uuid import UUID
from fastapi import APIRouter, Depends, HTTPException
from fastapi.responses import StreamingResponse
from sqlalchemy.engine import Connection
import anthropic
from db.connection import get_db
from db import procedures as db
from models.chat import ChatRequest

router = APIRouter(prefix="/chat", tags=["chat"])

_client = anthropic.Anthropic(api_key=os.environ["ANTHROPIC_API_KEY"])


@router.post("/{name}")
def chat(
    name: str,
    body: ChatRequest,
    conn: Connection = Depends(get_db),
) -> StreamingResponse:
    prompt = db.get_prompt_by_environment(conn, name, "PROD")
    if not prompt:
        raise HTTPException(status_code=404, detail=f"No PROD prompt found for '{name}'.")

    import json
    try:
        model_params = json.loads(prompt["modelparams"])
    except Exception:
        model_params = {}

    system = prompt["systemtemplate"]
    full_response: list[str] = []
    start_ms = int(time.time() * 1000)

    def generate():
        with _client.messages.stream(
            model="claude-haiku-4-5-20251001",
            max_tokens=model_params.get("max_tokens", 1024),
            system=system,
            messages=[{"role": "user", "content": body.user_message}],
        ) as stream:
            for chunk in stream.text_stream:
                full_response.append(chunk)
                yield chunk

        response_text = "".join(full_response)
        elapsed_ms = int(time.time() * 1000) - start_ms

        with _client:
            pass

        try:
            import threading

            def log():
                from db.connection import engine
                from sqlalchemy import text
                with engine.connect() as log_conn:
                    db.log_chat(
                        log_conn,
                        session_id=body.session_id,
                        prompt_hash=prompt["contenthash"],
                        user_message=body.user_message,
                        bot_response=response_text,
                        response_ms=elapsed_ms,
                    )
                    log_conn.commit()

            threading.Thread(target=log, daemon=True).start()
        except Exception:
            pass

    return StreamingResponse(generate(), media_type="text/plain")
