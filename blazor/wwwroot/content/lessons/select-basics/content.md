# Select Queries

Almost everything you do in SQL starts with a **SELECT query** — a request to read
rows out of a table. This lesson uses the **`movies`** database: a single table,
`movies`, with these columns:

| Column | Meaning |
| --- | --- |
| `id` | unique number for each movie |
| `title`, `genre`, `director` | text columns |
| `year`, `runtime_min` | whole numbers |
| `rating`, `gross_millions` | decimal numbers (`gross_millions` is NULL when unknown) |

Every gray cell below is live: edit it, hit **▶ Run** (or Ctrl+Enter), and try
**⚡ Visualize** to watch the query work row by row.

## SELECT and FROM

The simplest query grabs *every* column of *every* row. `*` means "all columns":

```sql
SELECT * FROM movies;
```

Usually you only want some columns — list them, separated by commas:

```sql
SELECT title, year, rating FROM movies;
```

Column order in the result follows *your* list, not the table. You can also rename
a column in the output with `AS` (an **alias**), and compute new columns with
expressions:

```sql
SELECT title AS movie,
       gross_millions * 1000000 AS gross_dollars
FROM movies;
```

`SELECT DISTINCT` collapses duplicate result rows — handy for "what values exist?"
questions:

```sql
SELECT DISTINCT genre FROM movies;
```

## WHERE — filtering rows

`WHERE` keeps only the rows that pass a condition. Comparison operators work the way
you'd expect: `=`, `<>` (not equal), `<`, `<=`, `>`, `>=`.

```sql
SELECT title, rating
FROM movies
WHERE rating >= 9.0;
```

Text values go in **single quotes**:

```sql
SELECT title, year FROM movies WHERE genre = 'Sci-Fi';
```

**Visualize** that one — you'll see every row tested against the condition, with
the matches surviving into the result.

### Combining conditions

Join conditions with `AND` / `OR`, and use parentheses when mixing them:

```sql
SELECT title, genre, year
FROM movies
WHERE genre = 'Drama' AND year >= 1994;
```

Three more operators cover very common patterns:

- `BETWEEN a AND b` — inclusive range
- `IN (a, b, c)` — matches any value in the list
- `LIKE` — text patterns, where `%` matches anything and `_` matches one character

```sql
SELECT title, year FROM movies WHERE year BETWEEN 1990 AND 1999;
```

```sql
SELECT title, genre FROM movies WHERE genre IN ('Action', 'Adventure');
```

```sql
SELECT title FROM movies WHERE title LIKE 'The %';
```

### NULL — the missing value

`NULL` means "unknown / not recorded". It is **not** equal to anything — even
another NULL — so `= NULL` never matches. Use `IS NULL` / `IS NOT NULL`:

```sql
SELECT title, gross_millions
FROM movies
WHERE gross_millions IS NULL;
```

## ORDER BY — sorting

Rows come back in no guaranteed order unless you ask. `ORDER BY` sorts ascending by
default; add `DESC` for descending. Multiple sort keys break ties left to right:

```sql
SELECT title, genre, rating
FROM movies
ORDER BY genre, rating DESC;
```

## LIMIT — top-N results

`LIMIT n` keeps only the first *n* rows — combined with `ORDER BY`, that's a
"top N" query. **Visualize** this one to watch the sort and the cut happen:

```sql
SELECT title, rating
FROM movies
ORDER BY rating DESC
LIMIT 5;
```

## The clause order

A select query's clauses must appear in this order — memorize it now, because every
later lesson builds on it:

```text
SELECT columns
FROM table
WHERE row filter
ORDER BY sort keys
LIMIT row count
```

## Tips

- Single quotes for text (`'Drama'`); double quotes are for column names.
- `LIKE` in SQLite ignores upper/lower case for plain letters.
- `WHERE` filters **rows**; the `SELECT` list picks **columns**. Keep the two jobs
  separate in your head and queries get much easier to write.
- Always give an `ORDER BY` when using `LIMIT` — without it, "first 5 rows" is
  whatever order the database feels like.
