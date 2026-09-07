# SQL Visualizer

**Learn SQL by watching it run.** SQL Visualizer is a free, browser-based app for students and teachers. Write a query, press Run, and watch **filtering**, **sorting**, and **joins** play out row by row on an animated canvas.

**Try it now:** [speakinginbits.github.io/SQL-Visualizer](https://speakinginbits.github.io/SQL-Visualizer/)

Nothing to install and no account needed. The whole app, including the SQLite engine, runs inside your browser as a static site built with [Blazor WebAssembly](https://learn.microsoft.com/en-us/aspnet/core/blazor/).

---

## Two ways to use it

### Learn

A guided course of seven units, each with a short reading, live query cells you can edit and run, and graded practice problems. Your progress is saved in your browser.

| # | Unit | Practice database |
|---|---|---|
| 1 | Select Queries | `movies.db` |
| 2 | Joins & Set Operators | `school.db` |
| 3 | Summary Queries | `sales.db` |
| 4 | Subqueries | `company.db` |
| 5 | Insert, Update & Delete | `library.db` |
| 6 | DDL: Creating Tables & Schemas | `sandbox.db` |
| 7 | Database Design Fundamentals | Knowledge check (quiz) |

Every practice problem has a hint, a reveal-solution button, a schema peek, and a **Visualize** button so you can see what your query actually did.

### Playground

A free-form workspace for experimenting, demos, and homework.

- **Connection manager** — pick a built-in sample database or open any SQLite `.db` file from your computer
- **Schema browser** — tree view of tables and columns with primary-key and foreign-key markers
- **Query editor** — Monaco (the VS Code editor) with SQL highlighting; `Ctrl+Enter` runs the query
- **Run anything** — SELECT, INSERT, UPDATE, DELETE, and CREATE all work; changes show a rows-affected count
- **Visualize** — animate any SELECT in a zoomable, pannable node-graph:
  - table scans and `LIMIT` cut-offs
  - `WHERE` with each condition evaluated on its own card
  - `ORDER BY` with rows flowing into their sorted positions
  - single and multi-table `JOIN`s with connectors between matched rows
  - playback controls (Prev / Next / Play / Reset / Speed) for teacher-led walkthroughs
- **Script library** — upload `.sql` files, keep them in browser storage, and re-run them later
- **Guided tour** — a skippable first-run walkthrough, replayable any time from the **? Tour** button

---

## Sample databases

All sample databases are built in memory when you pick them. Nothing is downloaded and nothing you change is permanent, so feel free to break things.

| Database | What's inside | Good for |
|---|---|---|
| `movies.db` | A film catalog | SELECT, WHERE, ORDER BY, LIMIT |
| `school.db` | Students, courses, enrollments, alumni | Joins and set operators |
| `store.db` | Categories, products, orders | Multi-condition filters and joins |
| `sales.db` | Customers, orders, order items | Aggregates, GROUP BY, HAVING |
| `company.db` | Employees, departments, projects | Subqueries |
| `library.db` | Books, members, loans | INSERT, UPDATE, DELETE |
| `sandbox.db` | Empty | CREATE TABLE and other DDL |

---

## For teachers

- **Project it.** The visualizer opens full-screen with step-by-step playback controls, so it works well on a classroom display.
- **Bring your own data.** Any SQLite file opens directly in the browser, so you can hand students a `.db` that matches your course.
- **No setup for students.** Share the link above. It works on school laptops and Chromebooks without an install.

---

## Run it locally

You only need this if you want to change the code. Otherwise, use the hosted link above.

### Prerequisites

| Requirement | Notes |
|---|---|
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | Preview or later |
| `wasm-tools` workload | Run `dotnet workload install wasm-tools` once after installing the SDK |

No Node.js, Python, or database server is required.

### Start the app

```bash
cd blazor
dotnet run
```

Then open [http://localhost:5229](http://localhost:5229). The port comes from `blazor/Properties/launchSettings.json`.

### Build a static release

```bash
cd blazor
dotnet publish -c Release -o publish
```

Copy the contents of `publish/wwwroot/` to any static host. There is no server-side component.

The hosted copy is published automatically to GitHub Pages by the workflow in `.github/workflows/deploy.yml` on every push to `main`.

---