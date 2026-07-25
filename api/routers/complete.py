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

_MAX_COMPLETION_WORDS = 4

# Words that leave a truncated phrase feeling unfinished if they're the last
# thing shown — trimmed off the end after capping to _MAX_COMPLETION_WORDS.
_DANGLING_TRAILING_WORDS = {
    "a", "an", "the", "and", "or", "but", "nor", "as", "that", "this",
    "to", "of", "in", "on", "at", "for", "with", "from", "by", "is", "are",
}


def _trim_to_understandable_end(completion: str) -> str:
    words = completion.split()[:_MAX_COMPLETION_WORDS]
    while words and words[-1].strip(",;:").lower() in _DANGLING_TRAILING_WORDS:
        words.pop()
    return " ".join(words)

_PROMPT = """Finish this sentence. {hint}
Stay strictly on the same topic and continue the exact idea being described — do not change subject or introduce anything new. \
Keep it VERY short: at most 3-4 words, a single short phrase — never a list, never multiple clauses joined by commas or "and"/"as well as". \
Output ONLY the missing rest of the sentence: the exact text that comes right after what's given, \
with no repetition of it, no quotes, no markdown, no explanation, and nothing after the sentence ends. \
If it already reads as a complete sentence, output nothing.

Sentence so far: "{partial}"

Rest of the sentence (3-4 words max):"""

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
            max_tokens=8,
            stop=["\n", "."],
        )
        completion = (response.choices[0].message.content or "").strip()
    except Exception:
        return CompleteResponse(completion="")

    completion = completion.strip('"').strip("'")
    # Hard cap regardless of what the model actually returned — the prompt
    # asks for 3-4 words but generation length isn't guaranteed to comply —
    # then trim any trailing filler word so it ends on an understandable note.
    completion = _trim_to_understandable_end(completion)
    return CompleteResponse(completion=completion)
