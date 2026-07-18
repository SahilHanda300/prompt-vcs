from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from routers import prompts, sites, chat, eval, audit, compliance, rollback, auth

app = FastAPI(title="PromptVCS API", version="1.0.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(prompts.router)
app.include_router(sites.router)
app.include_router(chat.router)
app.include_router(eval.router)
app.include_router(audit.router)
app.include_router(compliance.router)
app.include_router(rollback.router)
app.include_router(auth.router)


@app.get("/health")
def health() -> dict:
    return {"status": "ok"}
