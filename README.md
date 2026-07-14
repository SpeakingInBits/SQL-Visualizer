# SQL Visualizer

An interactive web app for teaching SQL. Open a SQLite database directly in your browser, write queries, and watch **sorting**, **filtering**, and **joins** animate step by step — no server required.

Built with [Blazor WebAssembly](https://learn.microsoft.com/en-us/aspnet/core/blazor/), runs entirely in the browser as a static site.

---

## Features

- **Connection manager** — open any SQLite `.db` file via the file picker, or load a built-in sample database
- **Schema browser** — tree view of tables → columns with PK / FK indicators
- **Query editor** — Monaco (VS Code) editor with SQL syntax highlighting; `Ctrl+Enter` to run
- **Run** any statement — SELECT, INSERT, UPDATE, DELETE; DML shows rows-affected feedback
- **Visualize** SELECT queries in a zoomable, pannable **node-graph canvas** (opens in a full-screen modal):
  - **Simple query** — a row-by-row table scan
  - `LIMIT` — rows beyond the limit are visibly cut
  - `ORDER BY` — input rows flow into their sorted positions, one connector at a time
  - `WHERE` — rows are scanned, each AND condition evaluated on its own card (TRUE/FALSE), matches collected into an output table
  - `WHERE + ORDER BY` — filter stage followed by a sort stage
  - `JOIN` — one or **multiple** joins laid out left-to-right, connectors between matched rows, then the merged result
- **Navigable space** — scroll to zoom, drag to pan, plus zoom / fit buttons; playback controls (Prev / Next / Play / Reset / Speed) stay as a fixed 2D toolbar for teacher-led demos
- **Script library** — upload `.sql` files, save them to browser storage, and re-run on demand

---

## Prerequisites

| Requirement | Notes |
|---|---|
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | Preview or later |
| `wasm-tools` workload | Run `dotnet workload install wasm-tools` once after installing the SDK |

No Node.js, Python, or database server required.

---

## Quick start

```bash
cd blazor
dotnet run
```

Then open [http://localhost:5000](http://localhost:5000) in your browser.

---

## Deploy as a static site

```bash
cd blazor
dotnet publish -c Release -o publish
```

Copy the contents of `publish/wwwroot/` to any static host (GitHub Pages, Netlify, Azure Static Web Apps, etc.). No server-side component is needed.

---

## Project structure

```
SQL-Visualizer/
└── blazor/
    ├── SqlVisualizer.csproj
    ├── Program.cs                    # DI registration + WASM host setup
    ├── _Imports.razor                # Global using directives
    ├── App.razor / Layout/           # Root component and shell layout
    ├── Models/
    │   └── Models.cs                 # Shared C# record types (results, schema, scripts)
    ├── Services/
    │   ├── SqliteConnectionService.cs   # Open SQLite from file bytes or in-memory
    │   ├── SampleDatabaseService.cs     # Seeds school.db and store.db in-memory
    │   ├── SchemaService.cs             # PRAGMA-based table/column introspection
    │   ├── QueryExecutorService.cs      # RunStatement + VisualizeQuery (all viz types)
    │   ├── ScriptRunnerService.cs       # Multi-statement script execution
    │   └── ScriptStoreService.cs        # Script persistence via browser localStorage
    ├── Components/
    │   ├── ConnectionPanel.razor
    │   ├── SchemaBrowser.razor
    │   ├── QueryEditor.razor
    │   ├── ScriptLibrary.razor
    │   └── Visualizer/
    │       ├── Visualizer.razor          # Dispatches to the correct sub-visualizer
    │       ├── SortVisualizer.razor      # ORDER BY step animation
    │       ├── FilterVisualizer.razor    # WHERE scan + optional ORDER BY sort
    │       └── JoinVisualizer.razor      # JOIN with SVG connector lines
    ├── Pages/
    │   └── Home.razor                # Single-page shell
    └── wwwroot/
        ├── index.html
        ├── css/app.css
        └── js/sqlvis.js              # JS interop helpers (file picker, localStorage)
```

---

## Sample databases

Two in-memory sample databases are included and require no external files:

| Name | Tables | Good for |
|---|---|---|
| `school.db` | `students`, `courses`, `enrollments` | ORDER BY, WHERE, INNER JOIN demos |
| `store.db` | `categories`, `products`, `orders` | Aggregates, JOINs, multi-condition WHERE |

Select either from the connection panel — no file picker needed.


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
