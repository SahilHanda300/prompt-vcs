"""
RQ1 — LLM-as-Judge Reliability

Two phases:
  Phase 1 (export): Pull Groq-scored responses → CSV for human annotators
  Phase 2 (analyse): Load CSV with human scores filled in → Cohen's Kappa, F1, agreement %

Usage:
  python research/rq1_judge_reliability.py --export          # generates rq1_annotation_task.csv
  python research/rq1_judge_reliability.py --analyse         # reads rq1_annotations_filled.csv

Annotators must fill in columns: human1_score, human2_score, human3_score (1–5 integer each)
"""
import argparse
import csv
import json
import sys
from pathlib import Path

import numpy as np
from sklearn.metrics import cohen_kappa_score, f1_score

from db import get_connection, query

EXPORT_PATH = Path("research/results/rq1_annotation_task.csv")
FILLED_PATH = Path("research/results/rq1_annotations_filled.csv")
RESULTS_PATH = Path("research/results/rq1_judge_reliability.json")

PASS_THRESHOLD = 4   # scores >= 4 → pass; < 4 → fail (binary classification)


# ── Phase 1: Export ───────────────────────────────────────────────────────────

def export_annotation_task() -> None:
    with get_connection() as conn:
        rows = query(conn, """
            SELECT
                tcr.resultid,
                tcr.prompthash,
                tcr.testcaseindex,
                tcr.inputtext,
                tcr.expectedoutput,
                tcr.actualoutput,
                tcr.judgescore      AS groq_score,
                tcr.judgereasoning  AS groq_reasoning
            FROM testcaseresults tcr
            JOIN evalresults e ON tcr.evalid = e.evalid
            WHERE e.stage = 'DEV_TO_QA'
            ORDER BY e.runat DESC
        """)

    if not rows:
        print("No TestCaseResults found. Run the pipeline on some prompts first.")
        sys.exit(1)

    EXPORT_PATH.parent.mkdir(parents=True, exist_ok=True)

    fieldnames = [
        "result_id", "prompt_hash", "test_case_index",
        "input", "expected_output", "actual_output",
        "groq_score", "groq_reasoning",
        "human1_score", "human2_score", "human3_score",
    ]

    with open(EXPORT_PATH, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        for r in rows:
            writer.writerow({
                "result_id":       str(r["resultid"]),
                "prompt_hash":     r["prompthash"][:12] + "...",
                "test_case_index": r["testcaseindex"],
                "input":           r["inputtext"],
                "expected_output": r["expectedoutput"],
                "actual_output":   r["actualoutput"],
                "groq_score":      r["groq_score"],
                "groq_reasoning":  r["groq_reasoning"] or "",
                "human1_score":    "",
                "human2_score":    "",
                "human3_score":    "",
            })

    print(f"Exported {len(rows)} cases → {EXPORT_PATH}")
    print()
    print("Next steps:")
    print(f"  1. Share {EXPORT_PATH} with 2–3 human annotators")
    print("  2. Each annotator fills in their column with a score 1–5")
    print(f"  3. Save completed file as {FILLED_PATH}")
    print("  4. Run: python research/rq1_judge_reliability.py --analyse")


# ── Phase 2: Analyse ──────────────────────────────────────────────────────────

def fleiss_kappa(ratings: np.ndarray) -> float:
    """Fleiss' Kappa for multiple raters. ratings shape: (n_items, n_raters)."""
    n, r = ratings.shape
    n_categories = 5   # scores 1–5
    # count matrix: (n_items, n_categories)
    counts = np.zeros((n, n_categories))
    for i in range(n):
        for j in range(r):
            score = int(ratings[i, j]) - 1   # 0-indexed
            counts[i, score] += 1

    p_j = counts.sum(axis=0) / (n * r)           # category proportions
    P_i = (np.sum(counts ** 2, axis=1) - r) / (r * (r - 1))   # per-item agreement
    P_bar = P_i.mean()
    P_e   = (p_j ** 2).sum()
    if P_e == 1.0:
        return 1.0
    return (P_bar - P_e) / (1.0 - P_e)


def analyse() -> None:
    if not FILLED_PATH.exists():
        print(f"File not found: {FILLED_PATH}")
        print("Run --export first, fill in human scores, then run --analyse.")
        sys.exit(1)

    rows = []
    with open(FILLED_PATH, newline="", encoding="utf-8") as f:
        for row in csv.DictReader(f):
            try:
                groq   = float(row["groq_score"])
                h1     = int(row["human1_score"])
                h2     = int(row["human2_score"])
                h3     = int(row["human3_score"])
            except (ValueError, KeyError):
                continue   # skip rows not yet annotated
            rows.append({"groq": groq, "h1": h1, "h2": h2, "h3": h3})

    if not rows:
        print("No annotated rows found. Fill in human1_score/human2_score/human3_score columns.")
        sys.exit(1)

    print(f"Annotated cases: {len(rows)}")

    groq_scores  = np.array([r["groq"] for r in rows])
    human_matrix = np.array([[r["h1"], r["h2"], r["h3"]] for r in rows])
    human_mean   = human_matrix.mean(axis=1)

    # Binarise for classification metrics (>=4 = pass, <4 = fail)
    groq_binary  = (groq_scores  >= PASS_THRESHOLD).astype(int)
    human_binary = (human_mean   >= PASS_THRESHOLD).astype(int)

    # Cohen's Kappa (Groq vs human consensus)
    kappa = cohen_kappa_score(human_binary, groq_binary)

    # Fleiss' Kappa across all four raters (Groq rounded + 3 humans)
    all_raters = np.column_stack([np.round(groq_scores).astype(int), human_matrix])
    fk = fleiss_kappa(all_raters)

    # F1 score
    f1 = f1_score(human_binary, groq_binary)

    # Agreement %
    agreement = (groq_binary == human_binary).mean()

    # Inter-annotator Kappa (humans only)
    ia_kappas = []
    for i in range(3):
        for j in range(i + 1, 3):
            bi = (human_matrix[:, i] >= PASS_THRESHOLD).astype(int)
            bj = (human_matrix[:, j] >= PASS_THRESHOLD).astype(int)
            ia_kappas.append(cohen_kappa_score(bi, bj))
    inter_annotator_kappa = float(np.mean(ia_kappas))

    print()
    print("=" * 50)
    print("RQ1 Results — LLM-as-Judge Reliability")
    print("=" * 50)
    print(f"Cohen's Kappa (Groq vs human consensus) : {kappa:.4f}  {'✓' if kappa > 0.7 else '✗'} (target > 0.70)")
    print(f"Fleiss' Kappa (all 4 raters)            : {fk:.4f}")
    print(f"Inter-annotator Kappa (humans only)     : {inter_annotator_kappa:.4f}")
    print(f"F1 Score (Groq vs human)                : {f1:.4f}")
    print(f"Agreement %                             : {agreement:.2%}")
    print(f"Pass threshold used                     : >= {PASS_THRESHOLD}/5")
    print()

    if kappa > 0.7:
        print("→ Substantial agreement. Groq judge is reliable for regression detection.")
    elif kappa > 0.4:
        print("→ Moderate agreement. Groq judge is usable but with caveats.")
    else:
        print("→ Poor agreement. Groq judge cannot be trusted for this use case.")

    out = {
        "n": len(rows),
        "pass_threshold": PASS_THRESHOLD,
        "cohen_kappa_groq_vs_human": round(kappa, 6),
        "fleiss_kappa_all_raters":   round(fk, 6),
        "inter_annotator_kappa":     round(inter_annotator_kappa, 6),
        "f1_score":                  round(float(f1), 6),
        "agreement_pct":             round(float(agreement), 6),
    }
    RESULTS_PATH.write_text(json.dumps(out, indent=2))
    print(f"Saved → {RESULTS_PATH}")


# ── Entry point ───────────────────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(description="RQ1: LLM-as-Judge Reliability")
    group  = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--export",  action="store_true", help="Export annotation task CSV")
    group.add_argument("--analyse", action="store_true", help="Analyse filled annotations")
    args = parser.parse_args()

    if args.export:
        export_annotation_task()
    else:
        analyse()


if __name__ == "__main__":
    main()
