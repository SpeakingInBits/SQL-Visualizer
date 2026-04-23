"""
SQL Visualizer — FastAPI entry point.
Serves the React frontend as static files in production.
In development the Vite dev server is used instead.
"""
from __future__ import annotations

import os
import webbrowser
from contextlib import asynccontextmanager
from pathlib import Path

import uvicorn
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles

import seed
from routes import connection, schema, query, scripts


STATIC_DIR = Path(__file__).parent / "static"


@asynccontextmanager
async def lifespan(app: FastAPI):
    seed.ensure_samples()
    # Open the browser only when serving the built frontend.
    if STATIC_DIR.exists():
        webbrowser.open("http://localhost:8000")
    yield


app = FastAPI(title="SQL Visualizer", lifespan=lifespan)

# Allow the Vite dev-server (port 5173) to call the API during development.
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:5173", "http://127.0.0.1:5173"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(connection.router, prefix="/api")
app.include_router(schema.router, prefix="/api")
app.include_router(query.router, prefix="/api")
app.include_router(scripts.router, prefix="/api")

# Serve built React app when the static folder is present.
if STATIC_DIR.exists():
    app.mount("/", StaticFiles(directory=STATIC_DIR, html=True), name="static")


if __name__ == "__main__":
    uvicorn.run("main:app", host="127.0.0.1", port=8000, reload=not STATIC_DIR.exists())
