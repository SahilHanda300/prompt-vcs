"""
Semantic diff: cosine similarity between prompt embeddings.
Used by RQ3 (diff-based regression prediction).
"""
from __future__ import annotations

import numpy as np

try:
    from sentence_transformers import SentenceTransformer
    _model = SentenceTransformer("all-MiniLM-L6-v2")
except Exception:
    _model = None


def _embed(text: str) -> np.ndarray:
    if _model is None:
        raise RuntimeError("sentence-transformers not available.")
    return _model.encode(text, normalize_embeddings=True)


def semantic_similarity(before: str, after: str) -> float:
    """Returns cosine similarity (0.0–1.0) between two prompt texts."""
    emb_before = _embed(before)
    emb_after = _embed(after)
    return float(np.dot(emb_before, emb_after))


def compute_semantic_diff(
    before_system: str,
    after_system: str,
    before_user: str,
    after_user: str,
) -> dict[str, float]:
    """Returns similarity scores per section and combined."""
    system_sim = semantic_similarity(before_system, after_system)
    user_sim = semantic_similarity(before_user, after_user)
    combined = (before_system + " " + before_user, after_system + " " + after_user)
    overall_sim = semantic_similarity(*combined)
    return {
        "system_similarity": round(system_sim, 4),
        "user_similarity": round(user_sim, 4),
        "overall_similarity": round(overall_sim, 4),
        "semantic_distance": round(1.0 - overall_sim, 4),
    }
