"""
Lexical diff: token-level changes between two prompt texts.
Returns a structured diff suitable for the DiffViewer in the SPA.
"""
import difflib
from dataclasses import dataclass


@dataclass
class LexicalChange:
    change_type: str          # "added" | "removed" | "unchanged"
    tokens: list[str]


def tokenise(text: str) -> list[str]:
    return text.split()


def compute_lexical_diff(before: str, after: str) -> list[LexicalChange]:
    before_tokens = tokenise(before)
    after_tokens = tokenise(after)

    matcher = difflib.SequenceMatcher(None, before_tokens, after_tokens)
    changes: list[LexicalChange] = []

    for tag, i1, i2, j1, j2 in matcher.get_opcodes():
        if tag == "equal":
            changes.append(LexicalChange("unchanged", before_tokens[i1:i2]))
        elif tag == "replace":
            changes.append(LexicalChange("removed", before_tokens[i1:i2]))
            changes.append(LexicalChange("added", after_tokens[j1:j2]))
        elif tag == "delete":
            changes.append(LexicalChange("removed", before_tokens[i1:i2]))
        elif tag == "insert":
            changes.append(LexicalChange("added", after_tokens[j1:j2]))

    return changes


def lexical_similarity(before: str, after: str) -> float:
    """Returns 0.0–1.0 character-level similarity ratio."""
    return difflib.SequenceMatcher(None, before, after).ratio()
