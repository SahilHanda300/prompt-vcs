# PromptVCS

Version control for prompts — submit a prompt once, and a fully automatic
`DEV → QA → PROD` pipeline takes it the rest of the way, publishing a live,
self-contained site or app generated from it. No manual "promote" step, no
manual QA gate, no mode-picking between "content site" and "functional app."

- **Live app:** [prompt-vcs-web.onrender.com](https://prompt-vcs-web.onrender.com)

## What it does

1. You submit a prompt (e.g. *"a landing page for a coffee shop"* or *"a
   scientific calculator"*) through the web app.
2. The pipeline runs automatically, with no further action from you:
   - **DEV** — the new prompt version is recorded.
   - **QA** — four checks run: local validation, a content-safety check, a
     feasibility/trial-generation check, and a checkpoint recording the
     result. The two Claude Code checks are combined into a single call for
     speed.
   - **PROD** — once QA passes, Claude Code generates (or, for an edit,
     diff-aware-updates) a complete, self-contained HTML/CSS/JS artifact.
3. You get a working link to the generated site/app, plus full QA and build
   history, and can diff any two prompt versions against each other.

## Architecture

```
                Web (PromptVcs.Web)
   dashboard + browser-based terminal, one app
                        |
                        | MCP tool calls over HTTP
                        v
              MCP server (mcp-server, Render)
        owns the store (MongoDB), the pipeline, QA, and
        publish-rules engine — decides *how* to build/update
        a site, not just a pass-through
                        |
                        | dispatches Claude Code jobs over SignalR
                        v
        Runner agent (PromptVcs.Runner — on your own machine)
        the only component that ever invokes Claude Code, under
        your own Claude Pro login
```

Four C# projects, tied together by `PromptVCS.slnx`:

| Project | Role |
|---|---|
| `PromptVcs.Core` | Shared server + runner domain logic: pipeline, QA, publish rules, artifact sanitizing, Claude Code invocation. |
| `mcp-server` (`PromptVcs.McpServer`) | ASP.NET Core app hosting the MCP tools, the Runner's SignalR hub, and the MongoDB-backed store/auth. Also serves generated artifacts as static files, and a browser-based terminal. |
| `PromptVcs.Runner` | Console app that connects to the server's SignalR hub and is the **only** process that ever runs real Claude Code, authenticated via your Claude Pro login. |
| `PromptVcs.Web` | ASP.NET Core Razor Pages web app — a thin MCP client with cookie-based auth, a dashboard, and the prompt detail/diff/edit pages. |

Web never talks to the Runner directly — both only ever talk to the MCP
server (hub-and-spoke).

## Data model

Two independent version histories per prompt:

1. **Prompt versions** — the prompt text itself, versioned on every
   submit/edit. `environments.dev/qa/prod` are pointers to a version number,
   advanced automatically in sequence as the pipeline runs.
2. **Build/artifact versions** — the generated output for a prompt,
   versioned independently per successful prod promotion. Each build
   records the prompt version that triggered it, its artifact location, a
   timestamp, and a status — so a bad generation is traceable without
   re-running the prompt through Claude Code.

Everything is stored in MongoDB — there are no local JSON files anywhere in
this project.

## Generation strategy

- **First run** (no existing build): the full prompt is sent to Claude Code
  in a single combined call that screens for safety/feasibility *and*
  generates the artifact if it passes.
- **Edits** (a prior build exists): the diff between the previous and new
  prompt content is sent along with the existing artifact, and Claude Code
  is asked for a targeted update rather than a full regeneration. The
  previous artifact remains available for rollback of the underlying data.

Every generated artifact is a single self-contained page — plain markup for
a content site, or real working JS state/interactivity for a functional
app — inferred by the model per-request, with no mode picked by the user.

## Running locally

### Prerequisites

- .NET 10 SDK
- A MongoDB connection string (e.g. a free MongoDB Atlas cluster)
- [Claude Code](https://claude.com/claude-code) installed and logged in
  with a Claude Pro/Max subscription (only needed on the machine running
  `PromptVcs.Runner`)

### 1. Start the MCP server

```powershell
$env:PROMPTVCS_MONGO_URI = "mongodb+srv://<user>:<password>@<cluster>.mongodb.net/promptvcs"
dotnet run --project mcp-server
```

Listens on `http://localhost:5279` by default (`/mcp` for tool calls,
`/site/<promptId>/` for generated artifacts, `/terminal` for a browser-based
CLI).

### 2. Start a Runner (on the machine with your Claude Pro login)

```powershell
$env:PROMPTVCS_RUNNER_HUB_URL = "http://localhost:5279/runnerhub"
dotnet run --project PromptVcs.Runner
```

Without a Runner connected, QA's content-safety/feasibility check and prod
generation both fail cleanly with "No runner connected" rather than hanging.

Set `PROMPTVCS_MOCK_CLAUDE=1` on the Runner to return canned output instead
of invoking real Claude Code — useful for exercising the pipeline without
spending real usage.

### 3. Use it

Run `dotnet run --project PromptVcs.Web`, then open `http://localhost:5285`.
Register an account (the first user ever registered becomes admin
automatically), then create/edit prompts from the dashboard. A
terminal-style CLI is also reachable in the browser via the MCP server's
`/terminal` page, if you'd rather type commands than click through the UI.

## Deployment

- **`mcp-server`** and **`PromptVcs.Web`** each deploy as their own Docker-based
  Render Web Service, both auto-deploying from `main`. The root `Dockerfile`
  builds `mcp-server` plus a bundled copy of the CLI (for `/terminal`); the
  web app has its own standalone `Dockerfile`.
- **`PromptVcs.Runner`** is never deployed — it only ever runs on a user's
  own machine, since that's where their Claude Pro login lives.
- Render's free tier has **no persistent disk**: generated artifacts on
  disk don't survive a redeploy, even though the MongoDB records referencing
  them do.

## Environment variables

| Variable | Used by | Purpose |
|---|---|---|
| `PROMPTVCS_MONGO_URI` | `mcp-server` | MongoDB Atlas connection string backing the store, `users`, and `sessions` collections. |
| `PROMPTVCS_MCP_PORT` / `PORT` | `mcp-server` | Listen port (Render injects `PORT`; `PROMPTVCS_MCP_PORT` is the local fallback, default `5279`). |
| `PROMPTVCS_RUNNER_TOKEN` | `mcp-server`, `PromptVcs.Runner` | Shared secret gating the Runner's SignalR connection. |
| `PROMPTVCS_MCP_URL` | `PromptVcs.Cli`, `PromptVcs.Web` | The MCP server endpoint to call, default `http://localhost:5279/mcp`. |
| `PROMPTVCS_RUNNER_HUB_URL` | `PromptVcs.Runner` | The SignalR hub to connect to, default `http://localhost:5279/runnerhub`. |
| `PROMPTVCS_MOCK_CLAUDE` | `PromptVcs.Runner` | Set to `1` to return canned output instead of invoking real Claude Code (test seam, not for production use). |
| `PROMPTVCS_SESSION_PATH` | `PromptVcs.Cli` | Optional override for where the CLI caches its login session token; used internally by the browser terminal to isolate concurrent sessions. |

## Tech stack

ASP.NET Core (Razor Pages + MCP server), the `ModelContextProtocol` /
`ModelContextProtocol.AspNetCore` NuGet packages (Streamable HTTP
transport), SignalR, MongoDB, DiffPlex for line diffing, Tailwind CSS (via
CDN, no Node build step) for the web app's UI, and Claude Code as the sole
generation engine. No Node code anywhere in this project.
