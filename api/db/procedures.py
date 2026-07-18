from uuid import UUID
from typing import Any
from sqlalchemy import text
from sqlalchemy.engine import Connection


# ── Write procedures (CALL) ───────────────────────────────────────────────────

def submit_prompt(
    conn: Connection,
    content_hash: str,
    system_template: str,
    user_template: str,
    model_params: str,
    submitted_by: str,
    country: str,
    commit_message: str,
    parent_hash: str | None,
    prompt_type: str,
    input_label: str | None,
    input_placeholder: str | None,
    output_label: str | None,
    ref_name: str,
) -> None:
    conn.execute(
        text("CALL pvcs_SubmitPrompt(:hash, :system, :user, :params, :by, :country, :msg, :parent, :type, :label, :placeholder, :outlabel, :ref)"),
        {
            "hash": content_hash, "system": system_template, "user": user_template,
            "params": model_params, "by": submitted_by, "country": country,
            "msg": commit_message, "parent": parent_hash, "type": prompt_type,
            "label": input_label, "placeholder": input_placeholder,
            "outlabel": output_label, "ref": ref_name,
        },
    )


def write_eval_result(
    conn: Connection,
    prompt_hash: str,
    dataset_id: UUID,
    golden_score: float,
    judge_score: float,
    regression_flag: bool,
    stage: str,
    promotion_reason: str,
) -> UUID:
    result = conn.execute(
        text("CALL pvcs_WriteEvalResult(:hash, :dataset, :golden, :judge, :flag, :stage, :reason, NULL)"),
        {
            "hash": prompt_hash, "dataset": str(dataset_id),
            "golden": golden_score, "judge": judge_score,
            "flag": regression_flag, "stage": stage, "reason": promotion_reason,
        },
    )
    row = result.fetchone()
    return row[0]


def write_test_case_result(
    conn: Connection,
    eval_id: UUID,
    prompt_hash: str,
    test_case_index: int,
    input_text: str,
    expected_output: str,
    actual_output: str,
    golden_score: float,
    judge_score: float,
    judge_reasoning: str | None,
) -> None:
    conn.execute(
        text("CALL pvcs_WriteTestCaseResult(:eval_id, :hash, :idx, :input, :expected, :actual, :golden, :judge, :reasoning)"),
        {
            "eval_id": str(eval_id), "hash": prompt_hash, "idx": test_case_index,
            "input": input_text, "expected": expected_output, "actual": actual_output,
            "golden": golden_score, "judge": judge_score, "reasoning": judge_reasoning,
        },
    )


def promote_to_qa(conn: Connection, ref_name: str, prompt_hash: str) -> None:
    conn.execute(
        text("CALL pvcs_PromoteToQA(:ref, :hash)"),
        {"ref": ref_name, "hash": prompt_hash},
    )


def promote_to_prod(conn: Connection, ref_name: str, prompt_hash: str) -> None:
    conn.execute(
        text("CALL pvcs_PromoteToPROD(:ref, :hash)"),
        {"ref": ref_name, "hash": prompt_hash},
    )


def rollback(
    conn: Connection,
    ref_name: str,
    environment: str,
    target_hash: str,
    executed_by: str,
    owner_username: str,
) -> None:
    conn.execute(
        text("CALL pvcs_Rollback(:ref, :env, :hash, :by, :owner)"),
        {
            "ref": ref_name, "env": environment, "hash": target_hash,
            "by": executed_by, "owner": owner_username,
        },
    )


def log_chat(
    conn: Connection,
    session_id: UUID,
    prompt_hash: str,
    user_message: str,
    bot_response: str,
    response_ms: int,
    username: str | None = None,
    refname: str | None = None,
) -> None:
    conn.execute(
        text("CALL pvcs_LogChat(:session, :hash, :user_msg, :bot_resp, :ms, :username, :refname)"),
        {
            "session": str(session_id), "hash": prompt_hash,
            "user_msg": user_message, "bot_resp": bot_response, "ms": response_ms,
            "username": username, "refname": refname,
        },
    )


def get_chat_history(conn: Connection, username: str, refname: str) -> list[dict[str, Any]]:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetChatHistory(:username, :refname)"),
        {"username": username, "refname": refname},
    )
    return [dict(row) for row in result.mappings().all()]


def log_cli_command(
    conn: Connection,
    executed_by: str,
    command: str,
    arguments: str | None,
    prompt_name: str | None,
    prompt_hash: str | None,
    environment: str | None,
    outcome: str,
    error_message: str | None,
    cli_version: str,
) -> None:
    conn.execute(
        text("CALL pvcs_LogCliCommand(:by, :cmd, :args, :name, :hash, :env, :outcome, :err, :ver)"),
        {
            "by": executed_by, "cmd": command, "args": arguments,
            "name": prompt_name, "hash": prompt_hash, "env": environment,
            "outcome": outcome, "err": error_message, "ver": cli_version,
        },
    )


# ── Read functions (SELECT * FROM) ────────────────────────────────────────────

def get_prompt_by_environment(
    conn: Connection, ref_name: str, environment: str
) -> dict[str, Any] | None:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetPromptByEnvironment(:ref, :env)"),
        {"ref": ref_name, "env": environment},
    )
    row = result.mappings().first()
    return dict(row) if row else None


def get_prompt_by_hash(conn: Connection, content_hash: str) -> dict[str, Any] | None:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetPromptByHash(:hash)"),
        {"hash": content_hash},
    )
    row = result.mappings().first()
    return dict(row) if row else None


def get_prompt_history(conn: Connection, ref_name: str) -> list[dict[str, Any]]:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetPromptHistory(:ref)"),
        {"ref": ref_name},
    )
    return [dict(row) for row in result.mappings().all()]


def get_prompt_history_with_identity(conn: Connection, ref_name: str) -> list[dict[str, Any]]:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetPromptHistoryWithIdentity(:ref)"),
        {"ref": ref_name},
    )
    return [dict(row) for row in result.mappings().all()]


def get_baseline_scores(conn: Connection, ref_name: str) -> list[dict[str, Any]]:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetBaselineScores(:ref)"),
        {"ref": ref_name},
    )
    return [dict(row) for row in result.mappings().all()]


def get_degraded_test_cases(conn: Connection, eval_id: UUID) -> list[dict[str, Any]]:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetDegradedTestCases(:eval_id)"),
        {"eval_id": str(eval_id)},
    )
    return [dict(row) for row in result.mappings().all()]


def get_all_prod_sites(conn: Connection, submitted_by: str | None = None) -> list[dict[str, Any]]:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetAllPRODSites(:by)"),
        {"by": submitted_by},
    )
    return [dict(row) for row in result.mappings().all()]


def get_prod_dashboard(conn: Connection, submitted_by: str | None = None) -> list[dict[str, Any]]:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetPRODDashboard(:by)"),
        {"by": submitted_by},
    )
    return [dict(row) for row in result.mappings().all()]


def get_qa_failures(conn: Connection, submitted_by: str | None = None) -> list[dict[str, Any]]:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetQAFailures(:by)"),
        {"by": submitted_by},
    )
    return [dict(row) for row in result.mappings().all()]


def get_prod_outputs(conn: Connection, ref_name: str) -> list[dict[str, Any]]:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetProdOutputs(:ref)"),
        {"ref": ref_name},
    )
    return [dict(row) for row in result.mappings().all()]


def get_environment_status(
    conn: Connection, ref_name: str | None = None
) -> list[dict[str, Any]]:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetEnvironmentStatus(:ref)"),
        {"ref": ref_name},
    )
    return [dict(row) for row in result.mappings().all()]


def get_cli_audit_log(
    conn: Connection,
    executed_by: str | None = None,
    command: str | None = None,
    prompt_name: str | None = None,
) -> list[dict[str, Any]]:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetCliAuditLog(:by, :cmd, :name)"),
        {"by": executed_by, "cmd": command, "name": prompt_name},
    )
    return [dict(row) for row in result.mappings().all()]


def get_promotion_audit_trail(
    conn: Connection, ref_name: str | None = None
) -> list[dict[str, Any]]:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetPromotionAuditTrail(:ref)"),
        {"ref": ref_name},
    )
    return [dict(row) for row in result.mappings().all()]


def get_latest_eval_by_hash(conn: Connection, prompt_hash: str) -> dict[str, Any] | None:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetLatestEvalByHash(:hash)"),
        {"hash": prompt_hash},
    )
    row = result.mappings().first()
    return dict(row) if row else None


def get_dataset_by_name(conn: Connection, dataset_name: str) -> dict[str, Any] | None:
    result = conn.execute(
        text("SELECT * FROM pvcs_GetDatasetByName(:name)"),
        {"name": dataset_name},
    )
    row = result.mappings().first()
    return dict(row) if row else None


def create_dataset(
    conn: Connection,
    dataset_name: str,
    test_cases: str,
    owner: str,
) -> str:
    result = conn.execute(
        text("SELECT pvcs_CreateDataset(:name, :cases, :owner)"),
        {"name": dataset_name, "cases": test_cases, "owner": owner},
    )
    row = result.fetchone()
    return str(row[0])
