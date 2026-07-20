# Joins & Set Operators

Real databases spread information across several tables so nothing is repeated.
The **`school`** database (loaded for this lesson) has four:

- **`students`** — `id`, `name`, `grade`, `major`, `mentor_id` (another student's id)
- **`courses`** — `id`, `title`, `credits`
- **`enrollments`** — `id`, `student_id`, `course_id`, `grade` (a *link table* tying one student to one course)
- **`alumni`** — `id`, `name`, `grad_year`, `major`

To see a student's name next to a course title, we have to **join** these tables
back together. Get your bearings first — every cell here is live:

```sql
SELECT * FROM students;
```

## INNER JOIN

An `INNER JOIN` stitches two tables together on a matching condition and keeps only
the rows that match on **both** sides. Here we connect each enrollment to its student:

```sql
SELECT students.name, enrollments.grade
FROM students
JOIN enrollments ON enrollments.student_id = students.id;
```

`JOIN` on its own means `INNER JOIN` — the words are interchangeable. Hit
**⚡ Visualize** to watch the matching rows light up and combine.

Typing full table names gets tedious. Give each table a short **alias** right after
its name and use it everywhere else:

```sql
SELECT s.name, e.grade
FROM students s
JOIN enrollments e ON e.student_id = s.id;
```

### Chaining joins

To pull a student's **name** and their **course title** together, hop across all
three tables — just add another `JOIN`:

```sql
SELECT s.name, c.title, e.grade
FROM students s
JOIN enrollments e ON s.id = e.student_id
JOIN courses c ON e.course_id = c.id;
```

A `WHERE` clause then filters the joined rows like any others:

```sql
SELECT s.name, c.title
FROM students s
JOIN enrollments e ON s.id = e.student_id
JOIN courses c ON e.course_id = c.id
WHERE e.grade = 'A';
```

## LEFT JOIN — keeping unmatched rows

`INNER JOIN` silently drops rows with no match. Notice **Ivy** never appears above —
she has no enrollments. A `LEFT JOIN` keeps **every row of the left table**, filling
the right side with NULLs when there's no match:

```sql
SELECT s.name, e.course_id, e.grade
FROM students s
LEFT JOIN enrollments e ON e.student_id = s.id;
```

That NULL pattern gives you a classic trick — *find the rows that have no match*
by keeping only the NULL ones:

```sql
SELECT s.name
FROM students s
LEFT JOIN enrollments e ON e.student_id = s.id
WHERE e.id IS NULL;
```

## RIGHT and FULL OUTER JOIN

- `RIGHT JOIN` is the mirror image: keep every row of the **right** table.
  Any right join can be rewritten as a left join with the tables swapped, so many
  developers just always write LEFT.
- `FULL OUTER JOIN` keeps unmatched rows from **both** sides.

```sql
SELECT s.name AS student_name, a.name AS alumni_name
FROM students s
FULL OUTER JOIN alumni a ON s.name = a.name;
```

Only Grace matches on both sides; everyone else appears with a NULL partner.

## CROSS JOIN

A `CROSS JOIN` (or a `JOIN` with no `ON`) pairs **every** left row with **every**
right row — sizes multiply fast, so it's rarely what you want, but it's the right
tool for "every combination" questions:

```sql
SELECT s.name, c.title
FROM students s
CROSS JOIN courses c
WHERE s.grade = 12;
```

## Self-joins

A table can join **to itself** — you just need two aliases so SQL can tell the
copies apart. Each student's `mentor_id` points at *another row of the same table*:

```sql
SELECT s.name AS student, m.name AS mentor
FROM students s
JOIN students m ON s.mentor_id = m.id;
```

Make it a `LEFT JOIN` to keep students who have no mentor:

```sql
SELECT s.name AS student, m.name AS mentor
FROM students s
LEFT JOIN students m ON s.mentor_id = m.id;
```

## UNION and set operators

Joins combine tables **side by side**. Set operators stack query results **on top
of each other**. The two queries must produce the same number of columns.

`UNION` removes duplicates; `UNION ALL` keeps them (and is faster):

```sql
SELECT name FROM students
UNION
SELECT name FROM alumni;
```

```sql
SELECT name FROM students
UNION ALL
SELECT name FROM alumni;
```

Compare the row counts — Grace is on both lists, so `UNION` drops one copy.

`INTERSECT` keeps rows that appear in **both** results; `EXCEPT` keeps rows in the
first result **but not** the second:

```sql
SELECT name FROM students
INTERSECT
SELECT name FROM alumni;
```

```sql
SELECT name FROM students
EXCEPT
SELECT name FROM alumni;
```

## Tips

- Always give a join an `ON` condition — a bare `JOIN` without one is a cross join.
- Qualify column names (`s.id`, `c.id`) whenever the same name exists in more than
  one table, and alias output columns (`AS student`) when two would share a name.
- Choosing a join: *"only matched pairs"* → INNER; *"all of table A, matched or
  not"* → LEFT; *"everything from both"* → FULL.
- Set operators compare **whole rows** — line the columns up in the same order in
  both queries.
