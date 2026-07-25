import os
from fastapi import APIRouter
from groq import Groq
from models.complete import CompleteRequest, CompleteResponse

router = APIRouter(prefix="/complete", tags=["complete"])

_client = Groq(api_key=os.environ["GEMINI_API_KEY"])

_PROMPT = """Finish this sentence. Stay strictly on the same topic and continue the exact idea \
being described — do not change subject or introduce anything new. \
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

    try:
        response = _client.chat.completions.create(
            model=os.environ.get("EVAL_MODEL", "llama-3.1-8b-instant"),
            messages=[{"role": "user", "content": _PROMPT.format(partial=text)}],
            temperature=0.2,
            max_tokens=25,
            stop=["\n", "."],
        )
        completion = (response.choices[0].message.content or "").strip()
    except Exception:
        return CompleteResponse(completion="")

    completion = completion.strip('"').strip("'")
    return CompleteResponse(completion=completion)
