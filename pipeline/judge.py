"""Groq-as-judge: scores each actual output 1–5 with one-sentence reasoning."""
import json
import os
from typing import Any

from groq import Groq

_JUDGE_PROMPT = """\
You are an impartial evaluator assessing the quality of an AI response.

Expected output:
{expected}

Actual output:
{actual}

Rate the actual output on a scale of 1 to 5 based on accuracy, completeness, and relevance \
compared to the expected output.

1 = completely wrong or missing
2 = mostly incorrect or incomplete
3 = partially correct
4 = mostly correct with minor issues
5 = correct and complete

Reply with JSON only — no extra text:
{{"score": <integer 1-5>, "reasoning": "<one sentence>"}}"""


def _judge_one(
    client: Groq,
    expected: str,
    actual: str,
    model: str,
) -> tuple[float, str]:
    prompt = _JUDGE_PROMPT.format(expected=expected, actual=actual)
    response = client.chat.completions.create(
        model=model,
        messages=[{"role": "user", "content": prompt}],
        max_tokens=256,
        temperature=0.0,
    )
    raw = response.choices[0].message.content.strip()
    if raw.startswith("```"):
        parts = raw.split("```")
        raw = parts[1].lstrip("json").strip() if len(parts) > 1 else raw
    try:
        parsed = json.loads(raw)
        score = float(parsed["score"])
        reasoning = str(parsed["reasoning"])
    except Exception:
        score = 3.0
        reasoning = "Could not parse judge response."
    return round(min(max(score, 1.0), 5.0), 4), reasoning


def run_judge(
    test_results: list[dict[str, Any]],
    stage: str,
    model: str | None = None,
) -> list[dict[str, Any]]:
    """
    Takes golden_runner output list and adds judge scores.
    Returns list of dicts with keys: judge_score, reasoning
    """
    effective_model = model or os.environ.get("EVAL_MODEL", "llama-3.1-8b-instant")
    client = Groq(api_key=os.environ["GEMINI_API_KEY"])

    results: list[dict[str, Any]] = []
    for r in test_results:
        score, reasoning = _judge_one(client, r["expected"], r["actual"], effective_model)
        results.append({"judge_score": score, "reasoning": reasoning})

    return results
