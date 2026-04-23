"""
build.py — one-shot build + package script.

Usage:
    python build.py            # build React + package with PyInstaller
    python build.py --dev      # start both dev servers (no packaging)
"""
from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).parent
FRONTEND = ROOT / "frontend"
BACKEND = ROOT / "backend"
STATIC_SRC = FRONTEND / "dist"
STATIC_DST = BACKEND / "static"

# On Windows, npm/npx are .cmd scripts and need shell=True (or the .cmd suffix)
NPM = "npm.cmd" if sys.platform == "win32" else "npm"


def run(cmd: list[str], cwd: Path | None = None) -> None:
    print(f"\n>>> {' '.join(cmd)}")
    result = subprocess.run(cmd, cwd=cwd)
    if result.returncode != 0:
        sys.exit(result.returncode)


def build_frontend() -> None:
    print("\n=== Building React frontend ===")
    run([NPM, "run", "build"], cwd=FRONTEND)


def copy_static() -> None:
    print("\n=== Copying frontend/dist → backend/static ===")
    if STATIC_DST.exists():
        shutil.rmtree(STATIC_DST)
    shutil.copytree(STATIC_SRC, STATIC_DST)
    print(f"Copied to {STATIC_DST}")


def package_pyinstaller() -> None:
    print("\n=== Packaging with PyInstaller ===")
    sep = ";" if sys.platform == "win32" else ":"
    run(
        [
            sys.executable, "-m", "PyInstaller",
            "--onefile",
            "--name", "sql-visualizer",
            f"--add-data", f"{STATIC_DST}{sep}static",
            "--hidden-import", "uvicorn.logging",
            "--hidden-import", "uvicorn.loops.auto",
            "--hidden-import", "uvicorn.protocols.http.auto",
            "--hidden-import", "uvicorn.lifespan.on",
            "main.py",
        ],
        cwd=BACKEND,
    )
    dist = BACKEND / "dist" / "sql-visualizer"
    exe = dist.with_suffix(".exe") if sys.platform == "win32" else dist
    if exe.exists():
        print(f"\n✓ Executable: {exe}")
    else:
        print(f"\n✓ Build complete. Check {BACKEND / 'dist'}/")


def start_dev() -> None:
    """Launch both backend and frontend dev servers concurrently."""
    import threading

    def run_backend():
        subprocess.run(
            [sys.executable, "-m", "uvicorn", "main:app", "--reload", "--port", "8000"],
            cwd=BACKEND,
        )

    def run_frontend():
        subprocess.run([NPM, "run", "dev"], cwd=FRONTEND)

    print("Starting backend on http://127.0.0.1:8000")
    print("Starting frontend on http://localhost:5173")
    t1 = threading.Thread(target=run_backend, daemon=True)
    t2 = threading.Thread(target=run_frontend, daemon=True)
    t1.start()
    t2.start()
    try:
        t1.join()
        t2.join()
    except KeyboardInterrupt:
        print("\nStopped.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="SQL Visualizer build tool")
    parser.add_argument("--dev", action="store_true", help="Start dev servers instead of building")
    args = parser.parse_args()

    if args.dev:
        start_dev()
    else:
        build_frontend()
        copy_static()
        package_pyinstaller()
