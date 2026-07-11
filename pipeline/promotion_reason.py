"""Builds the human-readable PromotionReason string stored on every EvalResult."""

MIN_GOLDEN_SCORE = 0.10   # absolute floor — below this always fails
MIN_JUDGE_SCORE  = 3.0    # out of 5 — below this always fails


def check_minimum_thresholds(golden_score: float, judge_score: float) -> str | None:
    """Returns a developer-friendly failure message if below floor, None if acceptable."""
    if golden_score < MIN_GOLDEN_SCORE and judge_score < MIN_JUDGE_SCORE:
        return (
            f"Your prompt's responses were too inaccurate (accuracy: {golden_score:.0%}) "
            f"and rated too low by the quality reviewer ({judge_score:.1f}/5, minimum 4.0). "
            f"Try writing a more specific and detailed prompt."
        )
    if golden_score < MIN_GOLDEN_SCORE:
        return (
            f"Your prompt's responses didn't match expected outputs closely enough "
            f"(accuracy: {golden_score:.0%}, minimum 10%). "
            f"Try being more specific about what the assistant should say."
        )
    if judge_score < MIN_JUDGE_SCORE:
        return (
            f"The quality reviewer rated your prompt's responses {judge_score:.1f}/5 "
            f"— minimum required is 4.0/5. "
            f"Try refining your prompt with clearer instructions and examples."
        )
    return None


def build_promotion_reason(
    stage: str,
    golden_score: float,
    judge_score: float,
    p_value: float,
    cohens_d: float,
    regression_flag: bool,
    baseline_golden: float | None = None,
    threshold_failure: str | None = None,
) -> str:
    if threshold_failure:
        return f"Failed {stage}: {threshold_failure}"
    if not regression_flag:
        baseline_text = f" vs baseline {baseline_golden:.2f}" if baseline_golden is not None else ""
        return (
            f"Passed {stage}: GoldenScore={golden_score:.2f}{baseline_text}, "
            f"JudgeScore={judge_score:.1f}/5, p={p_value:.3f}, d={cohens_d:.2f}. "
            f"Quality checks passed — no regression detected."
        )
    return (
        f"Failed {stage}: This update performed significantly worse than the current live version "
        f"(accuracy dropped from {baseline_golden:.2f} to {golden_score:.2f}, "
        f"judge score {judge_score:.1f}/5). "
        f"Review your changes and try again."
        if baseline_golden is not None else
        f"Failed {stage}: GoldenScore={golden_score:.2f}, JudgeScore={judge_score:.1f}/5. "
        f"Regression detected."
    )
