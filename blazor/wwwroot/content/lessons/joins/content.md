# Joining Tables

Real databases spread information across several tables so nothing is repeated.
The **`school`** database (loaded for this lesson) has three:

- **`students`** — `id`, `name`, `grade`, `major`
- **`courses`** — `id`, `title`, `credits`
- **`enrollments`** — `id`, `student_id`, `course_id`, `grade`

`enrollments` is a *link table*: each row ties one student to one course. To see a
student's name next to a course title, we have to **join** these tables back together.

First, get your bearings — run this to see the raw students. Every cell below is live:
edit it and hit **▶ Run**, or press **⚡ Visualize** to animate what the query does.

```sql
SELECT * FROM students;
```

## INNER JOIN

An `INNER JOIN` stitches two tables together on a matching condition and keeps only the
rows that match on **both** sides. Here we connect each enrollment to its student:

```sql
SELECT students.name, enrollments.grade
FROM students
JOIN enrollments ON enrollments.student_id = students.id;
```

`JOIN` on its own means `INNER JOIN` — the words are interchangeable.

Hit **⚡ Visualize** on that cell to watch the matching rows light up and combine into
the joined result, one row at a time.

## Table aliases

Typing full table names gets tedious. Give each table a short **alias** right after its
name and use it everywhere else:

```sql
SELECT s.name, e.grade
FROM students s
JOIN enrollments e ON e.student_id = s.id;
```

`students s` means "call this table `s` for the rest of the query."

## Chaining multiple joins

To pull a student's **name** and their **course title** together, we hop across all three
tables: `students → enrollments → courses`. Just add another `JOIN`:

```sql
SELECT s.name, c.title, e.grade
FROM students s
JOIN enrollments e ON s.id = e.student_id
JOIN courses c ON e.course_id = c.id;
```

**Visualize** this one — you'll see three tables on the left feeding connectors into the
joined result table on the right as each combined row forms.

The join order in your `FROM` clause doesn't have to match how the tables relate. Here we
start from `enrollments` (the link table in the middle) and branch out to both sides — the
result is the same:

```sql
SELECT s.name, c.title
FROM enrollments e
JOIN students s ON e.student_id = s.id
JOIN courses c ON e.course_id = c.id;
```

## Filtering a join

A `WHERE` clause works on the joined rows just like on a single table. Show only the
A-grade enrollments:

```sql
SELECT s.name, c.title, e.grade
FROM students s
JOIN enrollments e ON s.id = e.student_id
JOIN courses c ON e.course_id = c.id
WHERE e.grade = 'A';
```

## Tips

- Always give a join an `ON` condition. A `JOIN` with no `ON` is a **cross join** —
  every row paired with every other row.
- Qualify column names (`s.id`, `c.id`) when the same name exists in more than one table.
- `INNER JOIN` keeps only matches. To keep *all* rows from one side even when there's no
  match, you'd reach for a `LEFT JOIN` — a topic for the next lesson.
