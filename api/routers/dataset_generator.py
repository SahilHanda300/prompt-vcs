import os
import json
from groq import Groq

_CHAT_PROMPT = """You are a test case generator for AI assistants.

Given the system prompt below, generate exactly 5 realistic test cases.
Each test case must have:
- "input": a realistic user message this assistant would receive
- "expected_output": a good, complete response the assistant should give

Return ONLY a valid JSON array with exactly 5 objects. No explanation, no markdown, no code fences.

System prompt:
{system_template}

JSON array:"""

_UI_PROMPT = """You are a test case generator for generated web UIs.

Given the description of what was built and the generated HTML below, generate exactly 5 test cases
that verify the HTML correctly implements the described functionality.

Each test case must have:
- "input": a question about whether a specific feature or element exists in the HTML
- "expected_output": "Yes" followed by one sentence confirming it is present and working

Return ONLY a valid JSON array with exactly 5 objects. No explanation, no markdown, no code fences.

Description: {description}

HTML (first 2000 chars):
{html_preview}

JSON array:"""


def _call_groq(prompt: str) -> list[dict]:
    client = Groq(api_key=os.environ["GEMINI_API_KEY"])
    response = client.chat.completions.create(
        model=os.environ.get("EVAL_MODEL", "llama-3.1-8b-instant"),
        messages=[{"role": "user", "content": prompt}],
        temperature=0.4,
        max_tokens=1024,
    )
    raw = response.choices[0].message.content.strip()
    if raw.startswith("```"):
        lines = raw.splitlines()
        raw = "\n".join(lines[1:-1] if lines[-1].strip() == "```" else lines[1:])
    cases = json.loads(raw)
    if not isinstance(cases, list) or not all(
        isinstance(c, dict) and "input" in c and "expected_output" in c for c in cases
    ):
        raise ValueError("Response is not a valid list of {input, expected_output} objects.")
    return cases


def generate_test_cases(system_template: str) -> list[dict]:
    prompt = _CHAT_PROMPT.format(system_template=system_template)
    last_error: Exception = Exception("Unknown error")
    for _ in range(2):
        try:
            return _call_groq(prompt)
        except (json.JSONDecodeError, ValueError, KeyError) as e:
            last_error = e
    raise ValueError(
        f"Could not auto-generate test cases after 2 attempts. "
        f"Make sure your prompt clearly describes what the assistant should do. "
        f"Detail: {last_error}"
    )


def generate_ui_test_cases(description: str, html: str) -> list[dict]:
    prompt = _UI_PROMPT.format(description=description, html_preview=html[:2000])
    last_error: Exception = Exception("Unknown error")
    for _ in range(2):
        try:
            return _call_groq(prompt)
        except (json.JSONDecodeError, ValueError, KeyError) as e:
            last_error = e
    raise ValueError(
        f"Could not auto-generate UI test cases after 2 attempts. Detail: {last_error}"
    )
