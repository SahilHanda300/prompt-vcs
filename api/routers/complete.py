import os
from fastapi import APIRouter
from groq import Groq
from models.complete import CompleteRequest, CompleteResponse

router = APIRouter(prefix="/complete", tags=["complete"])

_client = Groq(api_key=os.environ["GEMINI_API_KEY"])

_PROMPT = """You are autocompleting a short app description for a no-code app builder. \
Continue the partial text below with a natural, concise continuation (a few words up to one short sentence). \
Output ONLY the continuation — the exact text that comes right after the partial text, with no repetition of it, \
no quotes, no markdown, no explanation. If the partial text already reads as a complete description, output nothing.

Partial text: "{partial}"

Continuation:"""


@router.post("", response_model=CompleteResponse)
def complete(body: CompleteRequest) -> CompleteResponse:
    text = body.text.strip()
    if not text:
        return CompleteResponse(completion="")

    try:
        response = _client.chat.completions.create(
            model=os.environ.get("EVAL_MODEL", "llama-3.1-8b-instant"),
            messages=[{"role": "user", "content": _PROMPT.format(partial=text)}],
            temperature=0.3,
            max_tokens=40,
        )
        completion = (response.choices[0].message.content or "").strip()
    except Exception:
        return CompleteResponse(completion="")

    completion = completion.strip('"').strip("'")
    return CompleteResponse(completion=completion)
