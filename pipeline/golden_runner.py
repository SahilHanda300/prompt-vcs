"""Runs each test case through the prompt and scores with exact match + ROUGE-L."""
import os
from typing import Any

from groq import Groq
from rouge_score import rouge_scorer

_scorer = rouge_scorer.RougeScorer(["rougeL"], use_stemmer=True)


def _call_prompt(
    client: Groq,
    system_template: str,
    user_template: str,
    model_params: dict[str, Any],
    input_text: str,
    model: str,
) -> str:
    user_message = user_template.replace("{input}", input_text)
    response = client.chat.completions.create(
        model=model,
        messages=[
            {"role": "system", "content": system_template},
            {"role": "user", "content": user_message},
        ],
        max_tokens=model_params.get("max_tokens", 1024),
        temperature=model_params.get("temperature", 0.7),
    )
    return response.choices[0].message.content.strip()


def _golden_score(actual: str, expected: str) -> float:
    exact = 1.0 if actual.strip().lower() == expected.strip().lower() else 0.0
    rouge = _scorer.score(expected, actual)["rougeL"].fmeasure
    return round(0.5 * exact + 0.5 * rouge, 4)


def run_golden_tests(
    system_template: str,
    user_template: str,
    model_params: dict[str, Any],
    test_cases: list[dict[str, str]],
    model: str | None = None,
) -> list[dict[str, Any]]:
    """
    Returns a list of dicts with keys:
    input, expected, actual, golden_score
    """
    effective_model = model or os.environ.get("EVAL_MODEL", "llama-3.1-8b-instant")
    client = Groq(api_key=os.environ["GEMINI_API_KEY"])

    results: list[dict[str, Any]] = []
    for tc in test_cases:
        input_text = tc["input"]
        expected = tc["expected_output"]
        actual = _call_prompt(client, system_template, user_template, model_params, input_text, effective_model)
        score = _golden_score(actual, expected)
        results.append({
            "input": input_text,
            "expected": expected,
            "actual": actual,
            "golden_score": score,
        })

    return results
