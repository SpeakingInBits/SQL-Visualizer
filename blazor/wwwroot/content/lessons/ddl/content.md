# DDL: Creating Tables & Schemas

Everything so far assumed the tables already existed. **DDL** (Data Definition
Language) is how they come to exist: `CREATE`, `ALTER`, and `DROP` define the
*structure* of the database rather than its contents.

This lesson runs in the **`sandbox`** database — completely empty, all yours.
(As in the last lesson, **✓ Check** in the practice section re-seeds the sandbox
back to empty before testing your answer.)

## CREATE TABLE

A table definition lists columns, each with a name and a type:

```sql
CREATE TABLE pets (
    id      INTEGER PRIMARY KEY,
    name    TEXT NOT NULL,
    species TEXT,
    weight  REAL
);
```

Check what you built — this query reads the database's own catalog:

```sql
SELECT name, sql FROM sqlite_master WHERE type = 'table';
```

SQLite keeps types simple: `INTEGER`, `REAL`, `TEXT`, `BLOB` (other databases add
`VARCHAR(n)`, `DATE`, `DECIMAL`, and more — SQLite happily accepts those names and
maps them onto its own). Dates are usually stored as `TEXT` in `YYYY-MM-DD` form.

## Constraints — rules the data must follow

- `PRIMARY KEY` — uniquely identifies each row; an `INTEGER PRIMARY KEY` also
  auto-numbers itself when you insert without an id.
- `NOT NULL` — the column must always have a value.
- `UNIQUE` — no two rows may share a value.
- `DEFAULT x` — value used when an INSERT omits the column.
- `CHECK (condition)` — every row must satisfy the condition.

```sql
CREATE TABLE accounts (
    id       INTEGER PRIMARY KEY,
    email    TEXT NOT NULL UNIQUE,
    status   TEXT NOT NULL DEFAULT 'active',
    balance  REAL CHECK (balance >= 0)
);
```

Test the rules — the first insert works, the second violates the CHECK:

```sql
INSERT INTO accounts (email, balance) VALUES ('ada@example.com', 100);
INSERT INTO accounts (email, balance) VALUES ('bad@example.com', -5);
```

(Read the error, then re-run with a valid balance. Auto-numbering gave Ada id 1.)

### Foreign keys

A `FOREIGN KEY` declares that a column's values must point at rows of another
table — this is what turns separate tables into a *relational* schema:

```sql
CREATE TABLE owners (
    id   INTEGER PRIMARY KEY,
    name TEXT NOT NULL
);
CREATE TABLE dogs (
    id       INTEGER PRIMARY KEY,
    name     TEXT NOT NULL,
    owner_id INTEGER REFERENCES owners(id)
);
```

### Composite primary keys

When no single column identifies a row, combine two — the classic case is a
link table:

```sql
CREATE TABLE student_courses (
    student_id INTEGER,
    course_id  INTEGER,
    PRIMARY KEY (student_id, course_id)
);
```

## ALTER TABLE — changing an existing table

```sql
CREATE TABLE books (id INTEGER PRIMARY KEY, title TEXT);
ALTER TABLE books ADD COLUMN pages INTEGER;
ALTER TABLE books RENAME COLUMN title TO book_title;
ALTER TABLE books RENAME TO catalog;
```

`ALTER TABLE … DROP COLUMN x` removes a column. For bigger surgery (changing a
column's type, adding a constraint), the standard recipe is: rename the old table,
`CREATE` the new shape, `INSERT … SELECT` the data across, `DROP` the old one.

## DROP — removing objects

```sql
DROP TABLE catalog;
```

`DROP TABLE IF EXISTS x;` won't error when the table is already gone — handy in
scripts. Dropping a table deletes its data *and* its definition. There is no undo.

## Indexes

An **index** is a lookup structure that makes searches on a column fast, at the
cost of some write speed and storage. Unique indexes also enforce uniqueness:

```sql
CREATE TABLE events (id INTEGER PRIMARY KEY, name TEXT, event_date TEXT);
CREATE INDEX idx_events_date ON events(event_date);
CREATE UNIQUE INDEX idx_events_name ON events(name);
```

```sql
SELECT name FROM sqlite_master WHERE type = 'index';
```

Rule of thumb: index the columns you constantly filter or join on — foreign keys
first. Don't index everything; each index slows every INSERT and UPDATE a bit.

## Views

A **view** is a saved query that behaves like a read-only table:

```sql
CREATE TABLE tasks (id INTEGER PRIMARY KEY, title TEXT, status TEXT DEFAULT 'todo');
INSERT INTO tasks (title) VALUES ('write schema'), ('add indexes');
CREATE VIEW open_tasks AS SELECT id, title FROM tasks WHERE status = 'todo';
SELECT * FROM open_tasks;
```

## CREATE TABLE AS SELECT

Snapshot a query's result straight into a new table (columns and data, but no
constraints come along):

```sql
CREATE TABLE task_backup AS SELECT * FROM tasks;
```

## Tips

- `INTEGER PRIMARY KEY` in SQLite auto-numbers on its own — you rarely need the
  `AUTOINCREMENT` keyword.
- Name things consistently: singular-or-plural table names (pick one and stick to
  it), `snake_case` columns, `fk`-style names like `owner_id`, index names like
  `idx_table_column`.
- Put `NOT NULL` on everything that should always exist — NULLs you allow today
  are NULLs you'll be handling in every query for years.
- The `sqlite_master` table and `pragma_table_info('name')` let you inspect any
  schema — the practice checker uses exactly those.
