"""Triggers the next GitHub Actions workflow via repository_dispatch."""
import json
import os
import sys

import httpx


def trigger_next_workflow(event_type: str, prompt_name: str, prompt_hash: str) -> None:
    token = os.environ.get("DISPATCH_TOKEN")
    repo = os.environ.get("GITHUB_REPOSITORY")

    if not token or not repo:
        print("WARNING: DISPATCH_TOKEN or GITHUB_REPOSITORY not set — skipping dispatch.", file=sys.stderr)
        return

    url = f"https://api.github.com/repos/{repo}/dispatches"
    payload = {
        "event_type": event_type,
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

    response = httpx.post(url, headers=headers, content=json.dumps(payload), timeout=15)
    if response.status_code not in (200, 204):
        print(
            f"ERROR: repository_dispatch failed: {response.status_code} {response.text}",
            file=sys.stderr,
        )
        sys.exit(1)

    print(f"Dispatched '{event_type}' for {prompt_name}@{prompt_hash[:8]}")
