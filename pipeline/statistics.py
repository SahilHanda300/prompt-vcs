"""Paired t-test + Cohen's d for regression detection."""
import numpy as np
from scipy import stats


def compute_significance(
    baseline_scores: list[float],
    current_scores: list[float],
) -> tuple[float, float, bool]:
    """
    Returns (p_value, cohens_d, regression_flag).

    regression_flag is True when:
      - p < 0.05 (statistically significant difference)
      - |d| >= 0.5 (medium or larger effect)
      - current mean < baseline mean (scores declined)

    Both conditions required per project specification.
    """
    if len(baseline_scores) < 2 or len(baseline_scores) != len(current_scores):
        return 1.0, 0.0, False

    baseline = np.array(baseline_scores, dtype=float)
    current = np.array(current_scores, dtype=float)
    diff = current - baseline

    _, p_value = stats.ttest_rel(current, baseline)

    std_diff = float(np.std(diff, ddof=1))
    cohens_d = float(np.mean(diff) / std_diff) if std_diff > 0.0 else 0.0

    regression_flag = (
        p_value < 0.05
        and abs(cohens_d) >= 0.5
        and float(np.mean(diff)) < 0.0
    )

    return round(float(p_value), 6), round(cohens_d, 4), regression_flag
