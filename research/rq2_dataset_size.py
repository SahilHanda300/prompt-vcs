"""
RQ2 — Minimum Dataset Size for Reliable Regression Detection

Method: Bootstrap resampling at N = 2, 5, 10, 25, 50, 100, 200
Metric: Statistical power to detect a 10% accuracy drop (p < 0.05, paired t-test)
        + 95% CI width of the mean score estimate
Success: 80% power AND CI width < 0.10 at minimum N

Run: python research/rq2_dataset_size.py
"""
import json
import sys
import numpy as np
from scipy import stats
from db import get_connection, query

SAMPLE_SIZES   = [2, 5, 10, 25, 50, 100, 200]
N_BOOTSTRAP    = 2000
EFFECT_SIZE    = 0.10   # simulate a 10% accuracy drop
ALPHA          = 0.05
POWER_TARGET   = 0.80
CI_WIDTH_MAX   = 0.10
RNG_SEED       = 42


def bootstrap_power_and_ci(scores: np.ndarray, n: int) -> tuple[float, float]:
    """Return (mean_ci_width, power) for sample size n via bootstrap."""
    rng = np.random.default_rng(RNG_SEED)
    degraded_pool = np.clip(scores - EFFECT_SIZE, 0.0, 1.0)

    detected  = 0
    ci_widths = []

    for _ in range(N_BOOTSTRAP):
        current  = rng.choice(scores,        size=n, replace=True)
        degraded = rng.choice(degraded_pool, size=n, replace=True)

        _, p = stats.ttest_rel(current, degraded)
        if p < ALPHA:
            detected += 1

        sem = stats.sem(current)
        if sem > 0 and n > 1:
            lo, hi = stats.t.interval(0.95, df=n - 1, loc=np.mean(current), scale=sem)
            ci_widths.append(hi - lo)
        else:
            ci_widths.append(0.0)

    return float(np.mean(ci_widths)), detected / N_BOOTSTRAP


def main() -> None:
    with get_connection() as conn:
        rows = query(conn, """
            SELECT tcr.goldenscore
            FROM testcaseresults tcr
            JOIN evalresults e ON tcr.evalid = e.evalid
            WHERE e.stage = 'DEV_TO_QA'
            ORDER BY e.runat DESC
        """)

    if not rows:
        print("No TestCaseResults found. Submit some prompts and run the pipeline first.")
        sys.exit(1)

    scores = np.array([float(r["goldenscore"]) for r in rows])
    print(f"Total individual test-case scores: {len(scores)}")
    print(f"Mean={np.mean(scores):.4f}  Std={np.std(scores):.4f}  "
          f"Min={np.min(scores):.4f}  Max={np.max(scores):.4f}")
    print(f"\nEffect size simulated: -{EFFECT_SIZE} (10% accuracy drop)")
    print(f"Bootstrap iterations : {N_BOOTSTRAP}")
    print()

    header = f"{'N':>6} | {'CI Width':>10} | {'Power':>8} | {'Meets criteria':>14}"
    print(header)
    print("-" * len(header))

    results = []
    first_passing_n = None

    for n in SAMPLE_SIZES:
        ci_width, power = bootstrap_power_and_ci(scores, n)
        meets = power >= POWER_TARGET and ci_width <= CI_WIDTH_MAX
        if meets and first_passing_n is None:
            first_passing_n = n
        mark = "✓" if meets else ""
        print(f"{n:>6} | {ci_width:>10.4f} | {power:>8.2%} | {mark:>14}")
        results.append({"n": n, "ci_width": round(ci_width, 6), "power": round(power, 6), "meets": meets})

    print()
    if first_passing_n:
        print(f"Minimum dataset size meeting criteria: {first_passing_n} test cases per evaluation")
    else:
        print("No sample size meets criteria — more real evaluation data needed.")

    out = {
        "n_scores_collected": len(scores),
        "mean_score": float(np.mean(scores)),
        "std_score":  float(np.std(scores)),
        "effect_size": EFFECT_SIZE,
        "n_bootstrap": N_BOOTSTRAP,
        "minimum_n": first_passing_n,
        "results": results,
    }
    with open("research/results/rq2_dataset_size.json", "w") as f:
        json.dump(out, f, indent=2)
    print("Saved → research/results/rq2_dataset_size.json")


if __name__ == "__main__":
    main()
