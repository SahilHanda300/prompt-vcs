"""
Structural diff: sentence/paragraph-level changes and instruction analysis.
Uses spaCy for sentence boundary detection.
"""
from __future__ import annotations
from dataclasses import dataclass

try:
    import spacy
    _nlp = spacy.load("en_core_web_sm")
except OSError:
    _nlp = None


@dataclass
class StructuralChange:
    section: str          # "system" | "user"
    change_type: str      # "added" | "removed" | "modified" | "unchanged"
    before_sentences: list[str]
    after_sentences: list[str]


def _sentences(text: str) -> list[str]:
    if _nlp is None:
        return [s.strip() for s in text.split(".") if s.strip()]
    doc = _nlp(text)
    return [sent.text.strip() for sent in doc.sents]


def compute_structural_diff(
    before_system: str,
    after_system: str,
    before_user: str,
    after_user: str,
) -> list[StructuralChange]:
    changes: list[StructuralChange] = []
    for section, b, a in [("system", before_system, after_system), ("user", before_user, after_user)]:
        b_sents = _sentences(b)
        a_sents = _sentences(a)
        if b_sents == a_sents:
            changes.append(StructuralChange(section, "unchanged", b_sents, a_sents))
        else:
            changes.append(StructuralChange(section, "modified", b_sents, a_sents))
    return changes


def instruction_count_delta(before: str, after: str) -> int:
    """Rough heuristic: count bullet-point or numbered-list instructions."""
    def count(text: str) -> int:
        return sum(
            1 for line in text.splitlines()
            if line.strip().startswith(("-", "*", "•")) or (len(line) > 2 and line.strip()[0].isdigit() and line.strip()[1] in (".", ")"))
        )
    return count(after) - count(before)
