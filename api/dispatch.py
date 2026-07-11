"""Triggers GitHub Actions repository_dispatch from the API (Render → GitHub)."""
import json
import os
import sys

import httpx


def trigger_evaluation(prompt_name: str, prompt_hash: str) -> None:
    """
    Fires the 'prompt-submitted' repository_dispatch event, which starts the
    DEV → QA evaluation workflow.

    Requires env vars:
      GITHUB_DISPATCH_TOKEN  — Personal Access Token with repo scope
      GITHUB_REPOSITORY      — e.g. "owner/promptvcs"
    """
    token = os.environ.get("DISPATCH_TOKEN")
    repo = os.environ.get("REPOSITORY", "")

    if not token or not repo:
        print(
            "WARNING: DISPATCH_TOKEN or REPOSITORY not configured — "
            "evaluation workflow will not be triggered automatically.",
            file=sys.stderr,
        )
        return

    # Accept full GitHub URL or owner/repo format
    if repo.startswith("http"):
        repo = repo.rstrip("/").removeprefix("https://github.com/")

    url = f"https://api.github.com/repos/{repo}/dispatches"
    payload = {
        "event_type": "prompt-submitted",
        "client_payload": {
            "prompt_name": prompt_name,
            "prompt_hash": prompt_hash,
        },
    }
    headers = {
        "Authorization": f"Bearer {token}",
        "Accept": "application/vnd.github.v3+json",
        "Content-Type": "application/json",
    }

    try:
        response = httpx.post(url, headers=headers, content=json.dumps(payload), timeout=10)
        if response.status_code not in (200, 204):
            print(
                f"WARNING: repository_dispatch returned {response.status_code}: {response.text}",
                file=sys.stderr,
            )
    except httpx.RequestError as exc:
        print(f"WARNING: repository_dispatch request failed: {exc}", file=sys.stderr)
