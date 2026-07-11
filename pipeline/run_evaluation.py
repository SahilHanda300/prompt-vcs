"""
Evaluation pipeline entry point.

Called by GitHub Actions:
  python pipeline/run_evaluation.py --stage DEV_TO_QA
  python pipeline/run_evaluation.py --stage QA_TO_PROD --sample 10

Required environment variables:
  SQL_CONNECTION   — Supabase connection string
  GEMINI_API_KEY   — Google AI Studio free API key
  PROMPT_NAME      — ref name (injected by GitHub Actions)
  PROMPT_HASH      — SHA-256 hash (injected by GitHub Actions)
  GITHUB_TOKEN     — auto-provided by GitHub Actions
  GITHUB_REPOSITORY — auto-provided by GitHub Actions
  EVAL_MODEL       — optional override (default: gemini-1.5-flash)
"""
import argparse
import json
import os
import random
import sys

from dotenv import load_dotenv

load_dotenv()

from db import get_connection
from procedures import (
    get_prompt_by_hash,
    get_dataset_by_name,
    get_baseline_scores,
    write_eval_result,
    write_test_case_result,
    promote_to_qa,
    promote_to_prod,
)
from golden_runner import run_golden_tests
from judge import run_judge
from statistics import compute_significance
from promotion_reason import build_promotion_reason, check_minimum_thresholds
from github_dispatch import trigger_next_workflow


def main() -> None:
    parser = argparse.ArgumentParser(description="PromptVCS evaluation pipeline")
    parser.add_argument(
        "--stage",
        required=True,
        choices=["DEV_TO_QA", "QA_TO_PROD"],
        help="Evaluation stage",
    )
    parser.add_argument(
        "--sample",
        type=int,
        default=None,
        help="Randomly sample N test cases (used for QA_TO_PROD sanity check)",
    )
    args = parser.parse_args()

    prompt_name = os.environ["PROMPT_NAME"]
    prompt_hash = os.environ["PROMPT_HASH"]
    stage = args.stage

    print(f"[pipeline] stage={stage} prompt={prompt_name} hash={prompt_hash[:12]}...")

    exit_code = 0
    promote_after = False

    with get_connection() as conn:
        # ── 1. Fetch prompt ──────────────────────────────────────────────────
        prompt = get_prompt_by_hash(conn, prompt_hash)
        if not prompt:
            print(f"ERROR: Prompt hash '{prompt_hash}' not found in database.", file=sys.stderr)
            sys.exit(1)

        # ── 2. Fetch dataset ─────────────────────────────────────────────────
        dataset = get_dataset_by_name(conn, prompt_name)
        if not dataset:
            print(
                f"ERROR: Dataset '{prompt_name}' not found. "
                f"Create a dataset with DatasetName='{prompt_name}' in the Datasets table.",
                file=sys.stderr,
            )
            sys.exit(1)

        test_cases: list[dict] = json.loads(dataset["testcases"])

        if args.sample and args.sample < len(test_cases):
            test_cases = random.sample(test_cases, args.sample)

        print(f"[pipeline] Running {len(test_cases)} test cases.")

        try:
            # ── 3. Run golden tests ──────────────────────────────────────────
            golden_results = run_golden_tests(
                system_template=prompt["systemtemplate"],
                user_template=prompt["usertemplate"],
                model_params=json.loads(prompt["modelparams"]),
                test_cases=test_cases,
            )

            # ── 4. Run judge ─────────────────────────────────────────────────
            judge_results = run_judge(test_results=golden_results, stage=stage)

            # ── 5. Aggregate scores ──────────────────────────────────────────
            golden_scores = [r["golden_score"] for r in golden_results]
            judge_scores = [r["judge_score"] for r in judge_results]
            mean_golden = round(sum(golden_scores) / len(golden_scores), 4)
            mean_judge = round(sum(judge_scores) / len(judge_scores), 4)

            print(f"[pipeline] GoldenScore={mean_golden:.4f}  JudgeScore={mean_judge:.4f}")

            # ── 6. Statistical significance against QA baseline ──────────────
            p_value = 1.0
            cohens_d = 0.0
            regression_flag = False
            baseline_golden_mean: float | None = None

            baseline_rows = get_baseline_scores(conn, prompt_name)
            if baseline_rows:
                baseline_golden = [float(r["goldenscore"]) for r in baseline_rows]
                n = min(len(baseline_golden), len(golden_scores))
                bl = baseline_golden[:n]
                curr = golden_scores[:n]
                p_value, cohens_d, regression_flag = compute_significance(bl, curr)
                baseline_golden_mean = round(sum(bl) / len(bl), 4)
                print(
                    f"[pipeline] Baseline={baseline_golden_mean:.4f}  "
                    f"p={p_value:.4f}  d={cohens_d:.4f}  regression={regression_flag}"
                )
            else:
                print("[pipeline] No QA baseline found — skipping statistical test.")

            # ── 7. Build PromotionReason ─────────────────────────────────────
            # Minimum thresholds only apply at DEV_TO_QA — the first gate.
            # QA_TO_PROD relies on regression detection vs baseline only.
            threshold_failure = check_minimum_thresholds(mean_golden, mean_judge) if stage == "DEV_TO_QA" else None
            if threshold_failure:
                regression_flag = True
                print(f"[pipeline] BLOCKED — {threshold_failure}")

            promotion_reason = build_promotion_reason(
                stage=stage,
                golden_score=mean_golden,
                judge_score=mean_judge,
                p_value=p_value,
                cohens_d=cohens_d,
                regression_flag=regression_flag,
                baseline_golden=baseline_golden_mean,
                threshold_failure=threshold_failure,
            )
            print(f"[pipeline] {promotion_reason}")

            # ── 8. Persist EvalResult ────────────────────────────────────────
            eval_id = write_eval_result(
                conn,
                prompt_hash=prompt_hash,
                dataset_id=dataset["datasetid"],
                golden_score=mean_golden,
                judge_score=mean_judge,
                regression_flag=regression_flag,
                stage=stage,
                promotion_reason=promotion_reason,
            )

            # ── 9. Persist per-test-case results ─────────────────────────────
            for idx, (gr, jr) in enumerate(zip(golden_results, judge_results)):
                write_test_case_result(
                    conn,
                    eval_id=eval_id,
                    prompt_hash=prompt_hash,
                    test_case_index=idx,
                    input_text=gr["input"],
                    expected_output=gr["expected"],
                    actual_output=gr["actual"],
                    golden_score=gr["golden_score"],
                    judge_score=jr["judge_score"],
                    judge_reasoning=jr["reasoning"],
                )

            # ── 10. Gate: fail if regression or below threshold ──────────────
            if regression_flag:
                print(f"[pipeline] BLOCKED — hash stays in current stage.")
                exit_code = 1
            else:
                # ── 11. Promote ───────────────────────────────────────────────
                if stage == "DEV_TO_QA":
                    promote_to_qa(conn, prompt_name, prompt_hash)
                    print(f"[pipeline] Promoted {prompt_name} to QA.")
                else:
                    promote_to_prod(conn, prompt_name, prompt_hash)
                    print(f"[pipeline] Promoted {prompt_name} to PROD.")
                promote_after = True

        except Exception as e:
            error_reason = f"Pipeline error during {stage}: {type(e).__name__}: {e}"
            print(f"ERROR: {error_reason}", file=sys.stderr)
            write_eval_result(
                conn,
                prompt_hash=prompt_hash,
                dataset_id=dataset["datasetid"],
                golden_score=0.0,
                judge_score=0.0,
                regression_flag=True,
                stage=stage,
                promotion_reason=error_reason,
            )
            exit_code = 1

    # with block ends normally here → conn.commit() is called → writes persist

    # ── 12. Trigger next workflow (outside transaction) ───────────────────────
    if promote_after and stage == "DEV_TO_QA":
        trigger_next_workflow("qa-promoted", prompt_name, prompt_hash)

    print("[pipeline] Done.")
    sys.exit(exit_code)


if __name__ == "__main__":
    main()
