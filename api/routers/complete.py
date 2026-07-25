import os
from fastapi import APIRouter
from groq import Groq
from models.complete import CompleteRequest, CompleteResponse

router = APIRouter(prefix="/complete", tags=["complete"])

_client = Groq(api_key=os.environ["GEMINI_API_KEY"])

_CONTEXT_HINTS = {
    "ui-description": (
        "The sentence describes a software application someone wants built "
        "(for example a calculator, todo list, quiz app, or expense tracker) — "
        "stay specifically on what that app does."
    ),
    "chat-prompt": (
        "The sentence is a system prompt defining the role and behavior of an "
        "AI chat assistant — stay specifically on that role and behavior."
    ),
}

_PROMPT = """Finish this sentence. {hint}
Stay strictly on the same topic and continue the exact idea being described — do not change subject or introduce anything new. \
Output ONLY the missing rest of the sentence: the exact text that comes right after what's given, \
with no repetition of it, no quotes, no markdown, no explanation, and nothing after the sentence ends. \
If it already reads as a complete sentence, output nothing.

Sentence so far: "{partial}"

Rest of the sentence:"""


@router.post("", response_model=CompleteResponse)
def complete(body: CompleteRequest) -> CompleteResponse:
    text = body.text.strip()
    if not text:
        return CompleteResponse(completion="")

    hint = _CONTEXT_HINTS.get(body.context or "", "")

    try:
        response = _client.chat.completions.create(
            model=os.environ.get("EVAL_MODEL", "llama-3.1-8b-instant"),
            messages=[{"role": "user", "content": _PROMPT.format(hint=hint, partial=text)}],
            temperature=0.2,
            max_tokens=25,
            stop=["\n", "."],
        )
        completion = (response.choices[0].message.content or "").strip()
    except Exception:
        return CompleteResponse(completion="")

    completion = completion.strip('"').strip("'")
    return CompleteResponse(completion=completion)
