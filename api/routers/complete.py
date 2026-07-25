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

_MAX_SUMMARY_WORDS = 5

_COMMIT_SUMMARY_PROMPT = """Read the following AI prompt / system instructions and write a short \
git-commit-style label describing what it does or the change it represents. \
2-5 words, Title Case, no ending punctuation, no quotes, no markdown, no explanation. \
Examples of the style: "Fixed Hallucination", "Added Stricter Tone", "Initial Version", "Simplified Refund Flow".

Prompt:
\"\"\"{text}\"\"\"

Short summary (2-5 words):"""


def _complete_commit_summary(text: str) -> CompleteResponse:
    try:
        response = _client.chat.completions.create(
            model=os.environ.get("EVAL_MODEL", "llama-3.1-8b-instant"),
            messages=[{"role": "user", "content": _COMMIT_SUMMARY_PROMPT.format(text=text)}],
            temperature=0.3,
            max_tokens=12,
            stop=["\n"],
        )
        completion = (response.choices[0].message.content or "").strip()
    except Exception:
        return CompleteResponse(completion="")

    completion = completion.strip('"').strip("'")
    completion = " ".join(completion.split()[:_MAX_SUMMARY_WORDS])
    return CompleteResponse(completion=completion)

_PROMPT_QUALITY_PROMPT = """You are assessing a draft AI prompt before it's submitted and tested — judge how \
likely it is to produce clear, correct, on-topic responses once evaluated, based only on how well-specified \
the instructions are (clarity, specificity, completeness). This may be a chat assistant's system prompt or a \
description of a UI app to generate — judge either kind on the same criteria.

Reply in EXACTLY this format and nothing else, one line, no markdown:
SCORE|one short sentence of feedback (max 12 words)

Prompt:
\"\"\"{text}\"\"\"

Reply:"""


def _complete_prompt_quality(text: str) -> CompleteResponse:
    try:
        response = _client.chat.completions.create(
            model=os.environ.get("EVAL_MODEL", "llama-3.1-8b-instant"),
            messages=[{"role": "user", "content": _PROMPT_QUALITY_PROMPT.format(text=text)}],
            temperature=0.2,
            max_tokens=40,
            stop=["\n"],
        )
        completion = (response.choices[0].message.content or "").strip()
    except Exception:
        return CompleteResponse(completion="")

    return CompleteResponse(completion=completion)

_PROMPT = """Finish this sentence. {hint}
Stay strictly on the same topic and continue the exact idea being described — do not change subject or introduce anything new. \
Keep it VERY short: at most 3-4 words, a single short phrase — never a list, never multiple clauses joined by commas or "and"/"as well as". \
Output ONLY the missing rest of the sentence: the exact text that comes right after what's given, \
with no repetition of it, no quotes, no markdown, no explanation, and nothing after the sentence ends. \
If it already reads as a complete sentence, output nothing.
{gap_hint}
Sentence so far: "{partial}"

Rest of the sentence (3-4 words max):"""

# When the caller passes along the current quality-score feedback (the thing
# a reviewer said is still missing), fold it in so the suggestion closes that
# specific gap instead of just extending the sentence generically — that's
# what makes accepting suggestions actually move the readiness score, rather
# than giving the user endless generic text to tab through.
_GAP_HINT = 'A reviewer said this prompt still needs: "{related}". ' \
    "If the next few words can address that, prefer that direction — otherwise continue naturally.\n"


@router.post("", response_model=CompleteResponse)
def complete(body: CompleteRequest) -> CompleteResponse:
    text = body.text.strip()
    if not text:
        return CompleteResponse(completion="")

    if body.context == "commit-summary":
        return _complete_commit_summary(text)

    if body.context == "prompt-quality":
        return _complete_prompt_quality(text)

    hint = _CONTEXT_HINTS.get(body.context or "", "")
    gap_hint = _GAP_HINT.format(related=body.related.strip()) if body.related and body.related.strip() else ""

    try:
        response = _client.chat.completions.create(
            model=os.environ.get("EVAL_MODEL", "llama-3.1-8b-instant"),
            messages=[{"role": "user", "content": _PROMPT.format(hint=hint, partial=text, gap_hint=gap_hint)}],
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
