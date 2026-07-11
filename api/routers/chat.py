import json
import os
import time
from fastapi import APIRouter, Depends, HTTPException
from fastapi.responses import StreamingResponse
from sqlalchemy.engine import Connection
from groq import Groq
from db.connection import get_db
from db import procedures as db
from models.chat import ChatRequest

router = APIRouter(prefix="/chat", tags=["chat"])

_client = Groq(api_key=os.environ["GEMINI_API_KEY"])


@router.get("/{name}/history/{username}")
def get_chat_history(
    name: str,
    username: str,
    conn: Connection = Depends(get_db),
) -> list[dict]:
    rows = db.get_chat_history(conn, username=username, refname=name)
    messages = []
    for r in rows:
        messages.append({"role": "user",      "content": r["usermessage"]})
        messages.append({"role": "assistant", "content": r["botresponse"]})
    return messages


@router.post("/{name}")
def chat(
    name: str,
    body: ChatRequest,
    conn: Connection = Depends(get_db),
) -> StreamingResponse:
    prompt = db.get_prompt_by_environment(conn, name, "PROD")
    if not prompt:
        raise HTTPException(status_code=404, detail=f"No PROD prompt found for '{name}'.")

    try:
        model_params = json.loads(prompt["modelparams"])
    except Exception:
        model_params = {}

    system = prompt["systemtemplate"]
    full_response: list[str] = []
    start_ms = int(time.time() * 1000)

    def generate():
        stream = _client.chat.completions.create(
            model="llama-3.1-8b-instant",
            messages=[
                {"role": "system", "content": system},
                {"role": "user",   "content": body.user_message},
            ],
            max_tokens=model_params.get("max_tokens", 1024),
            temperature=model_params.get("temperature", 0.7),
            stream=True,
        )
        for chunk in stream:
            delta = chunk.choices[0].delta.content
            if delta:
                full_response.append(delta)
                yield delta

        response_text = "".join(full_response)
        elapsed_ms = int(time.time() * 1000) - start_ms

        try:
            import threading

            def log():
                from db.connection import engine
                with engine.connect() as log_conn:
                    db.log_chat(
                        log_conn,
                        session_id=body.session_id,
                        prompt_hash=prompt["contenthash"],
                        user_message=body.user_message,
                        bot_response=response_text,
                        response_ms=elapsed_ms,
                        username=body.username,
                        refname=name,
                    )
                    log_conn.commit()

            threading.Thread(target=log, daemon=True).start()
        except Exception:
            pass

    return StreamingResponse(generate(), media_type="text/plain")
