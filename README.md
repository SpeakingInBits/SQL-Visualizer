# SQL Visualizer

An interactive desktop app for teaching SQL. Connect to a local SQL Server or SQLite database, write queries, and watch **sorting**, **filtering**, and **joins** animate step by step.

---

## Features

- **Connection manager** — SQL Server (SQL auth or Windows Authentication) and SQLite
- **Schema browser** — tree view of databases → tables → columns with PK / FK indicators
- **Query editor** — Monaco (VS Code) editor with SQL syntax highlighting
- **Run** any statement — SELECT, INSERT, UPDATE, DELETE; DML shows rows-affected feedback
- **Visualize** SELECT queries:
  - `ORDER BY` — rows slide into their sorted positions (FLIP animation)
  - `WHERE` — non-matching rows fade and slide out; matching rows highlight
  - `JOIN` — animated SVG connectors between matched rows, then merged result fades in
- **Step-through controls** — Prev / Next / Reset for teacher-led demos
- **Script library** — upload `.sql` files, save them, and re-run to recreate databases on demand

---

## Prerequisites

| Requirement | Notes |
|---|---|
| Python 3.11+ | [python.org](https://www.python.org/downloads/) |
| Node.js 20+ | [nodejs.org](https://nodejs.org/) |
| ODBC Driver 17 or 18 for SQL Server | [Microsoft docs](https://learn.microsoft.com/en-us/sql/connect/odbc/download-odbc-driver-for-sql-server) — only needed for SQL Server connections |

---

## Quick start — development mode

Both the FastAPI backend and the Vite dev server run concurrently. The frontend proxies all `/api` requests to the backend automatically.

**1. Install dependencies (first time only)**

```bash
# Python backend
cd backend
pip install -r requirements.txt
cd ..

# React frontend
cd frontend
npm install
cd ..
```

**2. Start both servers**

```bash
python build.py --dev
```

- Backend → [http://127.0.0.1:8000](http://127.0.0.1:8000)
- Frontend → [http://localhost:5173](http://localhost:5173)

Open [http://localhost:5173](http://localhost:5173) in your browser.

> Alternatively, start them separately:
> ```bash
> # Terminal 1
> cd backend && uvicorn main:app --reload --port 8000
>
> # Terminal 2
> cd frontend && npm run dev
> ```

---

## Package as a standalone executable

The build script compiles the React app, bundles it into the backend, then wraps everything into a single executable with PyInstaller.

**1. Install PyInstaller (first time only)**

```bash
pip install pyinstaller
```

**2. Run the build**

```bash
python build.py
```

This does three things:
1. `npm run build` inside `frontend/` → produces `frontend/dist/`
2. Copies `frontend/dist/` → `backend/static/`
3. Runs PyInstaller `--onefile` on `backend/main.py`

**Output:** `backend/dist/sql-visualizer` (or `sql-visualizer.exe` on Windows)

**3. Run the executable**

```bash
# Windows
backend\dist\sql-visualizer.exe

# macOS / Linux
./backend/dist/sql-visualizer
```

The app starts the server on port 8000 and automatically opens your default browser.

> **Note:** The executable is platform-specific. Build on each OS separately (or use CI) to produce Windows, macOS, and Linux binaries.

---

## Project structure

```
SQL-Visualizer/
├── backend/
│   ├── main.py               # FastAPI entry point + static file serving
│   ├── db/
│   │   ├── connector.py      # SQL Server + SQLite connection manager
│   │   ├── executor.py       # Query execution + animation step extraction
│   │   ├── schema.py         # Schema introspection (databases, tables, columns, FKs)
│   │   └── script_store.py   # .sql script persistence (app_data/scripts.json)
│   ├── routes/
│   │   ├── connection.py     # POST /connect/*, DELETE /disconnect, GET /status
│   │   ├── schema.py         # GET /databases, /tables, /columns
│   │   ├── query.py          # POST /query/run, /query/visualize
│   │   └── scripts.py        # GET|POST|DELETE /scripts, POST /scripts/{id}/run
│   ├── app_data/             # Created at runtime — stores saved .sql scripts
│   └── requirements.txt
├── frontend/
│   └── src/
│       ├── api/client.ts         # Typed API wrappers (axios)
│       ├── types.ts              # Shared TypeScript types
│       ├── App.tsx               # Two-panel shell layout
│       └── components/
│           ├── ConnectionPanel/  # Connection form
│           ├── SchemaBrowser/    # Schema tree
│           ├── QueryEditor/      # Monaco editor + results
│           ├── ScriptLibrary/    # Script upload / run / delete
│           └── Visualizer/
│               ├── SortVisualizer.tsx    # ORDER BY animation
│               ├── FilterVisualizer.tsx  # WHERE animation
│               └── JoinVisualizer.tsx    # JOIN animation (D3 SVG)
├── build.py                  # Build + package script
└── README.md
```

---

## API reference

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/connect/sqlserver` | Connect to SQL Server |
| `POST` | `/api/connect/sqlite` | Connect to a SQLite file |
| `DELETE` | `/api/disconnect` | Close active connection |
| `GET` | `/api/status` | Connection status |
| `GET` | `/api/databases` | List databases |
| `GET` | `/api/tables?database=` | List tables |
| `GET` | `/api/columns?table=&schema=` | List columns + foreign keys |
| `POST` | `/api/query/run` | Execute any SQL statement |
| `POST` | `/api/query/visualize` | Execute SELECT + return animation step data |
| `GET` | `/api/scripts` | List saved scripts |
| `POST` | `/api/scripts/upload` | Upload a `.sql` file |
| `POST` | `/api/scripts/{id}/run` | Run a saved script |
| `DELETE` | `/api/scripts/{id}` | Delete a saved script |

Interactive API docs are available at [http://127.0.0.1:8000/docs](http://127.0.0.1:8000/docs) when running in dev mode.
