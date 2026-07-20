# Database Design Fundamentals

You can now write any query — but queries are only as good as the tables under
them. **Database design** is deciding what tables exist, what columns they hold,
and how they relate. Get it right and the data practically keeps itself
consistent; get it wrong and every query becomes a workaround.

This unit is theory + a graded knowledge check at the bottom (no database needed).

## Entities become tables

Start by naming the **entities** — the kinds of *things* your system tracks:
customers, orders, products, courses. Each entity becomes a table; each occurrence
of it becomes a row; each fact you store about it becomes a column.

Two rules of thumb:

- **One table per entity type.** A `pets` table, not a `dogs` table plus a
  `cats` table with identical columns.
- **One value per cell.** A `phones` column holding `"555-1234, 555-9876"` is a
  list crammed into a cell — the moment you need "find by phone number" it hurts.

## Primary keys

Every table needs a **primary key** — a column (or columns) whose value uniquely
identifies each row, never changes, and is never NULL.

- A **surrogate key** is a meaningless auto-number (`id INTEGER PRIMARY KEY`).
  It's the default choice: cheap, stable, and never embarrassing.
- A **natural key** is a real-world value like an email or ISBN. Tempting, but
  real-world values change (people change emails) and recycle — which is exactly
  what a key must never do.
- A **composite key** combines columns — `(student_id, course_id)` in a link
  table is the classic case.

## Relationships

Three shapes cover nearly everything:

- **One-to-many** — a customer has many orders; each order has one customer.
  Implemented with a **foreign key on the "many" side**: `orders.customer_id`.
- **Many-to-many** — a student takes many courses; a course has many students.
  No pair of foreign keys can express this — you need a third **junction table**
  (`enrollments`) with a foreign key to each side.
- **One-to-one** — rare; usually the two tables should just be one, unless you're
  splitting off optional or sensitive columns.

The **foreign key** is the glue: a declared promise that `orders.customer_id`
always points at a real `customers.id`. Declare it, and the database refuses
orphan rows.

## Normalization

Normalization is the discipline of **storing every fact exactly once**. The
symptoms it cures are duplication (the same customer address typed on 40 order
rows) and the *update anomalies* that follow (fix the address on 39 of them,
and your data now lies).

The first three normal forms, informally:

- **1NF** — every cell holds a single value; no repeating groups like
  `item1, item2, item3` columns.
- **2NF** — no column depends on just *part* of a composite key. If
  `(order_id, product_id)` is the key, `product_name` belongs in `products`, not
  in the order-items table.
- **3NF** — no column depends on another **non-key** column. If `dept_name` is
  determined by `dept_id`, storing both on every employee duplicates the fact —
  `dept_name` belongs in a `departments` table.

The classic summary: *every non-key column depends on the key, the whole key, and
nothing but the key.*

**Denormalization** — deliberately re-duplicating data for read speed — is a real
technique, but it's a performance optimization made *after* a clean design, never
a starting point. One honest exception you've already seen: `order_items` storing
`unit_price` isn't a 3NF violation, because the price *at the time of sale* is a
different fact than the product's current price.

## Choosing types and NULLs

- Pick the narrowest honest type: numbers as numbers, dates as dates (ISO text in
  SQLite), never money as floating point in serious systems.
- `NOT NULL` by default; allow NULL only when "unknown/absent" is genuinely
  meaningful for that column.
- Name consistently: `snake_case`, foreign keys as `<entity>_id`, and pick
  singular or plural table names project-wide.

## Indexes, briefly

Primary keys are indexed automatically. Add indexes on **foreign keys** and on
columns you filter or sort by constantly. Every index costs write speed — index
deliberately, not reflexively.

## A worked example

A bookstore needs: books, authors (a book can have several), customers, and
purchases. A solid 3NF design:

```text
authors:       id, name
books:         id, title, year, price
book_authors:  book_id, author_id          (junction, composite PK, 2 FKs)
customers:     id, name, email
purchases:     id, customer_id, book_id, purchase_date, unit_price
```

Notice: no author names inside `books`, no book titles inside `purchases`, and a
junction table for the many-to-many. Every fact lives in exactly one place.

Ready? Take the knowledge check below — 70% passes, and you can retake it freely.
