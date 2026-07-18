from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import text
from sqlalchemy.engine import Connection
from pydantic import BaseModel
import bcrypt
from db.connection import get_db

router = APIRouter(prefix="/auth", tags=["auth"])


class RegisterRequest(BaseModel):
    name: str
    username: str
    password: str


class LoginRequest(BaseModel):
    username: str
    password: str


class AuthResponse(BaseModel):
    user_id: str
    name: str
    username: str


@router.post("/register", response_model=AuthResponse)
def register(body: RegisterRequest, db: Connection = Depends(get_db)):
    if len(body.name.strip()) < 2:
        raise HTTPException(400, "Name must be at least 2 characters.")
    if len(body.username.strip()) < 3:
        raise HTTPException(400, "Username must be at least 3 characters.")
    if len(body.password) < 6:
        raise HTTPException(400, "Password must be at least 6 characters.")

    pass_hash = bcrypt.hashpw(body.password.encode(), bcrypt.gensalt()).decode()

    try:
        row = db.execute(
            text("SELECT pvcs_RegisterUser(:name, :username, :hash)"),
            {"name": body.name.strip(), "username": body.username.strip(), "hash": pass_hash},
        ).fetchone()
    except Exception as e:
        if "USERNAME_EXISTS" in str(e):
            raise HTTPException(409, "That username is already taken.")
        raise HTTPException(500, "Registration failed.")

    return AuthResponse(user_id=str(row[0]), name=body.name.strip(), username=body.username.strip().lower())


@router.post("/login", response_model=AuthResponse)
def login(body: LoginRequest, db: Connection = Depends(get_db)):
    row = db.execute(
        text("SELECT * FROM pvcs_GetUserByUsername(:username)"),
        {"username": body.username},
    ).fetchone()

    if not row:
        raise HTTPException(401, "No account found with that username.")

    if not bcrypt.checkpw(body.password.encode(), row.passhash.encode()):
        raise HTTPException(401, "Incorrect password.")

    return AuthResponse(user_id=str(row.userid), name=row.name, username=row.username)
