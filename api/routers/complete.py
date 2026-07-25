import os
import re
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

_SITE_NAME_PROMPT = """You are completing a short site identifier (lowercase words separated by hyphens, e.g. "my-calculator" \
or "customer-support-bot" — no spaces, no sentences). {app_hint}\
Continue the partial identifier below with a short, sensible continuation (1-3 words, hyphen-separated, nothing after it). \
Output ONLY the missing rest of the identifier — the exact characters that come right after what's given, \
no repetition of it, no quotes, no explanation. If it already reads as a complete, sensible name, output nothing.

Partial identifier: "{partial}"

Rest of the identifier:"""

_SLUG_DISALLOWED_RE = re.compile(r"[^a-z0-9-]")
_SLUG_REPEAT_HYPHEN_RE = re.compile(r"-{2,}")
_MAX_SLUG_COMPLETION_LEN = 24


def _sanitize_slug_completion(completion: str) -> str:
    text = completion.strip().lower().split("\n")[0]
    text = text.replace(" ", "-").replace("_", "-")
    text = _SLUG_DISALLOWED_RE.sub("", text)
    text = _SLUG_REPEAT_HYPHEN_RE.sub("-", text)
    return text[:_MAX_SLUG_COMPLETION_LEN].strip("-")


def _complete_site_name(text: str, related: str | None) -> CompleteResponse:
    app_hint = f'The app being built is described as: "{related.strip()}". ' if related and related.strip() else ""

    try:
        response = _client.chat.completions.create(
            model=os.environ.get("EVAL_MODEL", "llama-3.1-8b-instant"),
            messages=[{"role": "user", "content": _SITE_NAME_PROMPT.format(app_hint=app_hint, partial=text)}],
            temperature=0.3,
            max_tokens=10,
            stop=["\n"],
        )
        completion = (response.choices[0].message.content or "").strip()
    except Exception:
        return CompleteResponse(completion="")

    return CompleteResponse(completion=_sanitize_slug_completion(completion))


@router.post("", response_model=CompleteResponse)
def complete(body: CompleteRequest) -> CompleteResponse:
    text = body.text.strip()
    if not text:
        return CompleteResponse(completion="")

    if body.context == "site-name":
        return _complete_site_name(text, body.related)

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
