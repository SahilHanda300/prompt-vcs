"""
RQ3 — Diff-Based Regression Prediction

Can we predict pipeline failure just by looking at how the prompt changed?

Method:
  1. Extract lexical diff features between prompt v_parent → v_child
  2. Label: did the new version fail (regressionflag = True)?
  3. Train logistic regression + random forest with cross-validation
  4. Report F1, AUC-ROC per model

Features extracted (no heavy NLP — pure lexical):
  - word_count_delta       : words added (positive) or removed (negative)
  - word_count_ratio       : new / old word count
  - jaccard_similarity     : word overlap between versions
  - sentence_count_delta   : sentence count change
  - unique_words_added     : new unique words not in parent
  - unique_words_removed   : parent unique words dropped in child
  - char_count_delta       : character count change

Success criteria: F1 > 0.75, AUC-ROC > 0.80

Run: python research/rq3_diff_predictor.py
"""
import json
import re
import sys
import os

import numpy as np
from sklearn.ensemble import RandomForestClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import f1_score, roc_auc_score
from sklearn.model_selection import StratifiedKFold, cross_val_predict
from sklearn.preprocessing import StandardScaler
from sklearn.pipeline import Pipeline

from db import get_connection, query

RESULTS_PATH = "research/results/rq3_diff_predictor.json"
RANDOM_SEED  = 42


def tokenize(text: str) -> list[str]:
    return re.findall(r"\b\w+\b", text.lower())


def sentence_count(text: str) -> int:
    return max(1, len(re.split(r"[.!?]+", text.strip())))


def extract_features(old: str, new: str) -> dict[str, float]:
    old_tokens = tokenize(old)
    new_tokens = tokenize(new)
    old_set = set(old_tokens)
    new_set = set(new_tokens)

    union = old_set | new_set
    jaccard = len(old_set & new_set) / len(union) if union else 1.0

    return {
        "word_count_delta":    len(new_tokens) - len(old_tokens),
        "word_count_ratio":    len(new_tokens) / max(len(old_tokens), 1),
        "jaccard_similarity":  jaccard,
        "sentence_count_delta": sentence_count(new) - sentence_count(old),
        "unique_words_added":  len(new_set - old_set),
        "unique_words_removed": len(old_set - new_set),
        "char_count_delta":    len(new) - len(old),
    }


FEATURE_NAMES = [
    "word_count_delta", "word_count_ratio", "jaccard_similarity",
    "sentence_count_delta", "unique_words_added", "unique_words_removed",
    "char_count_delta",
]


def main() -> None:
    with get_connection() as conn:
        pairs = query(conn, """
            SELECT
                p.contenthash  AS child_hash,
                p.systemtemplate AS child_prompt,
                par.systemtemplate AS parent_prompt,
                e.regressionflag
            FROM prompts p
            JOIN prompts par ON p.parenthash = par.contenthash
            LEFT JOIN evalresults e
                ON e.prompthash = p.contenthash AND e.stage = 'DEV_TO_QA'
            WHERE p.parenthash IS NOT NULL
              AND e.regressionflag IS NOT NULL
            ORDER BY p.createdat DESC
        """)

    if not pairs:
        print("No prompt pairs with eval results found.")
        print("Submit multiple versions of the same site and run the pipeline.")
        sys.exit(1)

    print(f"Prompt pairs available: {len(pairs)}")

    X_rows, y = [], []
    for pair in pairs:
        feats = extract_features(pair["parent_prompt"], pair["child_prompt"])
        X_rows.append([feats[f] for f in FEATURE_NAMES])
        y.append(int(pair["regressionflag"]))

    X = np.array(X_rows)
    y = np.array(y)

    n_pos = y.sum()
    n_neg = len(y) - n_pos
    print(f"Regressions (FAIL): {n_pos}   No-regression (PASS): {n_neg}")

    if len(y) < 10:
        print("\nWarning: fewer than 10 pairs — results will have high variance.")
        print("Submit more prompt versions to improve reliability.")

    # Choose CV folds
    n_splits = min(5, n_pos, n_neg) if min(n_pos, n_neg) >= 2 else 2
    cv = StratifiedKFold(n_splits=n_splits, shuffle=True, random_state=RANDOM_SEED)

    models = {
        "Logistic Regression": Pipeline([
            ("scaler", StandardScaler()),
            ("clf", LogisticRegression(random_state=RANDOM_SEED, max_iter=1000)),
        ]),
        "Random Forest": Pipeline([
            ("clf", RandomForestClassifier(n_estimators=100, random_state=RANDOM_SEED)),
        ]),
    }

    print()
    print("=" * 55)
    print("RQ3 Results — Diff-Based Regression Prediction")
    print("=" * 55)
    print(f"{'Model':<22} | {'F1':>8} | {'AUC-ROC':>10} | {'Criteria met':>12}")
    print("-" * 55)

    results_out = {"n_pairs": len(pairs), "n_features": len(FEATURE_NAMES), "models": []}

    for name, model in models.items():
        try:
            y_pred = cross_val_predict(model, X, y, cv=cv, method="predict")
            y_prob = cross_val_predict(model, X, y, cv=cv, method="predict_proba")[:, 1]

            f1  = f1_score(y, y_pred, zero_division=0)
            auc = roc_auc_score(y, y_prob) if len(np.unique(y)) > 1 else 0.0
            met = "✓" if f1 > 0.75 and auc > 0.80 else " "
            print(f"{name:<22} | {f1:>8.4f} | {auc:>10.4f} | {met:>12}")
            results_out["models"].append({"model": name, "f1": round(f1, 6), "auc_roc": round(auc, 6)})
        except Exception as e:
            print(f"{name:<22} | ERROR: {e}")

    # Feature importance from Random Forest
    rf_model = models["Random Forest"]
    rf_model.fit(X, y)
    importances = rf_model.named_steps["clf"].feature_importances_
    feat_importance = sorted(
        zip(FEATURE_NAMES, importances), key=lambda x: x[1], reverse=True
    )

    print()
    print("Feature importances (Random Forest):")
    for feat, imp in feat_importance:
        bar = "█" * int(imp * 40)
        print(f"  {feat:<25} {imp:.4f} {bar}")

    results_out["feature_importances"] = [
        {"feature": f, "importance": round(float(i), 6)} for f, i in feat_importance
    ]

    os.makedirs("research/results", exist_ok=True)
    with open(RESULTS_PATH, "w") as f:
        json.dump(results_out, f, indent=2)
    print(f"\nSaved → {RESULTS_PATH}")


if __name__ == "__main__":
    main()
