# Insert, Update & Delete

Reading data is half the job — this lesson covers **DML** (Data Manipulation
Language): the statements that *change* what's in the tables. We're using the
**`library`** database:

- **`books`** — `id`, `title`, `author`, `year`, `available` (1 = on the shelf, 0 = checked out)
- **`members`** — `id`, `name`, `joined`
- **`loans`** — `id`, `book_id`, `member_id`, `loan_date`, `return_date` (NULL = still out)

Two things to know before you start:

1. These cells **really modify** the in-memory database. The **↺ Reset** button at
   the top re-seeds it fresh whenever you want a clean slate.
2. In the practice section, **✓ Check re-seeds the database first**, runs your SQL
   on the fresh copy, and inspects the outcome — so experiments here can't break
   the exercises.

## INSERT — adding rows

The full form names the table, the columns, then the values:

```sql
INSERT INTO books (id, title, author, year, available)
VALUES (9, 'The Left Hand of Darkness', 'Ursula K. Le Guin', 1969, 1);
```

Run it, then look at the table:

```sql
SELECT * FROM books;
```

Columns you leave out get their **default value** (or NULL). `available` defaults
to 1 here, so this works:

```sql
INSERT INTO books (id, title, author, year)
VALUES (10, 'Kindred', 'Octavia E. Butler', 1979);
```

One statement can insert **many rows** — just stack the value lists:

```sql
INSERT INTO members (id, name, joined) VALUES
    (6, 'Ada Quinn', '2024-06-01'),
    (7, 'Leo Faust', '2024-06-03');
```

And `INSERT … SELECT` copies rows produced by a query — no VALUES at all:

```sql
INSERT INTO loans (id, book_id, member_id, loan_date)
SELECT id + 100, id, 6, '2024-06-10'
FROM books
WHERE author = 'Jane Austen';
```

## UPDATE — changing rows

`UPDATE` sets new column values on every row that matches the `WHERE`:

```sql
UPDATE books SET available = 0 WHERE id = 4;
```

Set several columns at once, and compute values from the old ones:

```sql
UPDATE books
SET title = title || ' (Classic)',
    available = 1
WHERE year < 1900;
```

**⚠ The most feared bug in SQL:** an `UPDATE` (or `DELETE`) with **no WHERE clause
hits every row in the table.** Before running one, try selecting with the same
WHERE first to see exactly which rows you're about to touch.

`UPDATE` can use subqueries too — mark every book with an open loan as unavailable:

```sql
UPDATE books
SET available = 0
WHERE id IN (SELECT book_id FROM loans WHERE return_date IS NULL);
```

## DELETE — removing rows

`DELETE FROM` removes whole rows — there is no column list:

```sql
DELETE FROM loans WHERE id = 3;
```

Delete with any condition, including subqueries:

```sql
DELETE FROM loans
WHERE member_id = (SELECT id FROM members WHERE name = 'Theo Banks');
```

`DELETE FROM loans;` with no WHERE empties the whole table. It's occasionally what
you want. It's usually a resume-updating event. Reset the database if you tried it.

## Multi-statement changes

Real changes often take several statements — a loan means a new `loans` row *and*
flipping the book's `available` flag. Run them together:

```sql
INSERT INTO loans (id, book_id, member_id, loan_date)
VALUES (7, 7, 4, '2024-06-05');
UPDATE books SET available = 0 WHERE id = 7;
```

In production systems you'd wrap those in a **transaction** (`BEGIN` … `COMMIT`)
so they succeed or fail as a unit — worth reading about once you're comfortable
with the statements themselves.

## Tips

- `INSERT` adds rows; `UPDATE` changes columns of existing rows; `DELETE` removes
  rows. None of them change the table's *structure* — that's the next lesson.
- Always write the `WHERE` before the `SET`/`DELETE` part mentally — decide *which
  rows* first.
- Text values in single quotes; numbers bare; dates here are just `'YYYY-MM-DD'`
  strings.
- `SELECT` first, then reuse the same `WHERE` in your UPDATE/DELETE.
