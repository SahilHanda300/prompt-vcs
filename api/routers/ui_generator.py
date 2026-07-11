import os
from groq import Groq

_PROMPT = """You are an expert web developer. Generate a complete, self-contained HTML application based on the description below.

Requirements:
- Single HTML file with ALL CSS and JavaScript embedded inline
- Modern, clean dark design (#1a1a2e background, white text, accent colours)
- Fully functional — every feature must work with pure JavaScript
- Zero external dependencies — no CDN links, no external fonts, no API calls
- Mobile-responsive layout
- Return ONLY the raw HTML starting with <!DOCTYPE html>. No explanation, no markdown, no code fences.

Description: {description}

HTML:"""


def generate_ui(description: str) -> str:
    client = Groq(api_key=os.environ["GEMINI_API_KEY"])
    response = client.chat.completions.create(
        model=os.environ.get("EVAL_MODEL", "llama-3.1-8b-instant"),
        messages=[{"role": "user", "content": _PROMPT.format(description=description)}],
        temperature=0.2,
        max_tokens=4096,
    )
    html = response.choices[0].message.content.strip()
    if html.startswith("```"):
        lines = html.splitlines()
        html = "\n".join(lines[1:-1] if lines[-1].strip() == "```" else lines[1:])
    if not html.lower().startswith("<!doctype") and not html.lower().startswith("<html"):
        raise ValueError("Model did not return valid HTML.")
    return html
