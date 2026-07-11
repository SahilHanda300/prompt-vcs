"""
RQ4 — Change Pattern Risk Taxonomy

Method:
  1. For each prompt version that has a parent, use Groq to classify the change type
  2. Compute regression rate per category
  3. ANOVA across categories
  4. Tukey HSD post-hoc (if ≥ 3 categories with data)

Change categories:
  tone_change        — same intent, different formality/style
  instruction_added  — new constraints or rules added
  instruction_removed— constraints removed or softened
  length_change      — significantly shorter or longer
  format_change      — output format spec changed
  context_added      — more background or examples added
  other              — doesn't clearly fit above

Run: python research/rq4_change_taxonomy.py

Requires GEMINI_API_KEY (holds Groq key) in .env
"""
import json
import os
import sys
from collections import defaultdict

import numpy as np
from scipy import stats
from groq import Groq

from db import get_connection, query

RESULTS_PATH = "research/results/rq4_change_taxonomy.json"

CATEGORIES = [
    "tone_change",
    "instruction_added",
    "instruction_removed",
    "length_change",
    "format_change",
    "context_added",
    "other",
]

_groq_client = None


def groq_client() -> Groq:
    global _groq_client
    if _groq_client is None:
        _groq_client = Groq(api_key=os.environ["GEMINI_API_KEY"])
    return _groq_client


def classify_change(old_prompt: str, new_prompt: str) -> str:
    """Ask Groq to classify what type of change was made between two prompt versions."""
    resp = groq_client().chat.completions.create(
        model="llama-3.1-8b-instant",
        max_tokens=64,
        temperature=0,
        messages=[{
            "role": "user",
            "content": f"""Classify the change between these two AI prompt versions into exactly one category.

CATEGORIES (pick one):
- tone_change: same intent, different formality or style
- instruction_added: new rules, constraints, or requirements added
- instruction_removed: rules or constraints removed or softened
- length_change: significantly shorter or longer, little else changed
- format_change: output format specification changed
- context_added: more background, examples, or persona added
- other: doesn't clearly fit any above

OLD PROMPT:
{old_prompt[:800]}

NEW PROMPT:
{new_prompt[:800]}

Reply with only the category name, nothing else.""",
        }],
    )
    category = resp.choices[0].message.content.strip().lower()
    return category if category in CATEGORIES else "other"


def tukey_hsd(groups: dict[str, list[float]]) -> list[dict]:
    """Simple Tukey HSD post-hoc test between all category pairs."""
    from itertools import combinations
    from scipy.stats import t as t_dist

    keys = list(groups.keys())
    all_data = [v for v in groups.values()]
    grand_mean = np.mean([x for v in all_data for x in v])
    n_total = sum(len(v) for v in all_data)
    k = len(keys)
    df_error = n_total - k
    ms_error = sum(sum((x - np.mean(v)) ** 2 for x in v) for v in all_data) / df_error if df_error > 0 else 1

    results = []
    for a, b in combinations(keys, 2):
        na, nb = len(groups[a]), len(groups[b])
        if na < 2 or nb < 2:
            continue
        mean_diff = abs(np.mean(groups[a]) - np.mean(groups[b]))
        se = np.sqrt(ms_error * (1 / na + 1 / nb) / 2)
        if se == 0:
            continue
        q = mean_diff / se
        # approximate p-value via t-distribution with df_error
        p = 2 * (1 - t_dist.cdf(q, df=df_error))
        results.append({
            "pair": f"{a} vs {b}",
            "mean_diff": round(mean_diff, 4),
            "p_value": round(p, 4),
            "significant": p < 0.05,
        })
    return sorted(results, key=lambda x: x["p_value"])


def main() -> None:
    with get_connection() as conn:
        # Fetch prompt pairs (child + parent) with their regression outcome
        pairs = query(conn, """
            SELECT
                p.contenthash  AS child_hash,
                p.parenthash   AS parent_hash,
                p.systemtemplate AS child_prompt,
                par.systemtemplate AS parent_prompt,
                e.regressionflag
            FROM prompts p
            JOIN prompts par ON p.parenthash = par.contenthash
            LEFT JOIN evalresults e
                ON e.prompthash = p.contenthash AND e.stage = 'DEV_TO_QA'
            WHERE p.parenthash IS NOT NULL
            ORDER BY p.createdat DESC
        """)

    if not pairs:
        print("No prompt pairs with parent found.")
        print("Submit a second version of an existing site to create pairs.")
        sys.exit(1)

    print(f"Prompt pairs to classify: {len(pairs)}")

    category_regressions: dict[str, list[int]] = defaultdict(list)

    for i, pair in enumerate(pairs, 1):
        if pair["parent_prompt"] is None:
            continue
        category = classify_change(pair["parent_prompt"], pair["child_prompt"])
        regressed = int(pair["regressionflag"] or False)
        category_regressions[category].append(regressed)
        print(f"  [{i}/{len(pairs)}] {category} → {'FAIL' if regressed else 'PASS'}")

    print()
    print("=" * 55)
    print("RQ4 Results — Change Pattern Risk Taxonomy")
    print("=" * 55)
    print(f"{'Category':<22} | {'N':>4} | {'Regression Rate':>15}")
    print("-" * 55)

    category_rates: dict[str, list[float]] = {}
    summary = []

    for cat in CATEGORIES:
        outcomes = category_regressions.get(cat, [])
        if not outcomes:
            continue
        rate = np.mean(outcomes)
        category_rates[cat] = [float(x) for x in outcomes]
        summary.append({"category": cat, "n": len(outcomes), "regression_rate": round(rate, 4)})
        print(f"{cat:<22} | {len(outcomes):>4} | {rate:>15.2%}")

    print()

    # One-way ANOVA
    groups_with_data = {k: v for k, v in category_rates.items() if len(v) >= 2}

    anova_result = None
    tukey_results = []

    if len(groups_with_data) >= 2:
        f_stat, p_value = stats.f_oneway(*groups_with_data.values())
        anova_result = {"f_stat": round(f_stat, 4), "p_value": round(p_value, 4)}
        print(f"One-way ANOVA: F={f_stat:.4f}  p={p_value:.4f}  "
              f"{'✓ significant' if p_value < 0.05 else '✗ not significant'}")

        if p_value < 0.05 and len(groups_with_data) >= 3:
            tukey_results = tukey_hsd(groups_with_data)
            print()
            print("Tukey HSD post-hoc:")
            for r in tukey_results[:10]:
                sig = "✓" if r["significant"] else " "
                print(f"  {sig} {r['pair']:<40}  Δ={r['mean_diff']:.4f}  p={r['p_value']:.4f}")
    else:
        print("Not enough categories with data for ANOVA (need ≥ 2 groups with ≥ 2 samples each).")

    out = {
        "n_pairs": len(pairs),
        "categories": summary,
        "anova": anova_result,
        "tukey_hsd": tukey_results,
    }
    os.makedirs("research/results", exist_ok=True)
    with open(RESULTS_PATH, "w") as f:
        json.dump(out, f, indent=2)
    print(f"\nSaved → {RESULTS_PATH}")


if __name__ == "__main__":
    main()
