# Subqueries

A **subquery** is a query nested inside another query, in parentheses. They let you
answer questions in two steps — "first find X, then use X to find Y" — without
leaving SQL. This lesson uses the **`company`** database:

- **`departments`** — `id`, `name`, `location`
- **`employees`** — `id`, `name`, `dept_id`, `title`, `salary`, `manager_id`
- **`projects`** — `id`, `name`, `dept_id`, `budget`
- **`assignments`** — `id`, `employee_id`, `project_id`, `hours`

## Scalar subqueries — one value

A subquery that returns exactly **one value** can stand anywhere a value could.
The classic: compare each row against an aggregate of the whole table.

```sql
SELECT name, salary
FROM employees
WHERE salary > (SELECT AVG(salary) FROM employees);
```

The inner query runs first (average salary ≈ 80k), then the outer query filters
against that number. You can't do this with `WHERE salary > AVG(salary)` — this
two-step is exactly what subqueries are for.

They work in the `SELECT` list too:

```sql
SELECT name,
       salary,
       salary - (SELECT AVG(salary) FROM employees) AS diff_from_avg
FROM employees;
```

## IN — subqueries returning a list

When the subquery returns a **column of values**, test membership with `IN`:

```sql
SELECT name
FROM employees
WHERE dept_id IN (SELECT id FROM departments WHERE location = 'Building B');
```

`NOT IN` finds the opposite — but beware of one famous trap: if the subquery's
result **contains a NULL**, `NOT IN` matches *nothing at all*. Filter the NULLs out:

```sql
SELECT name
FROM employees
WHERE id NOT IN (SELECT manager_id FROM employees WHERE manager_id IS NOT NULL);
```

That's everyone who isn't anyone's manager. Remove the `IS NOT NULL` filter and
watch the result vanish.

## Correlated subqueries

The subqueries above ran once. A **correlated** subquery mentions the outer row, so
it re-runs **for every row**. Read `e.dept_id` here — it reaches out to the outer
query:

```sql
SELECT name, salary
FROM employees e
WHERE salary > (SELECT AVG(salary)
                FROM employees
                WHERE dept_id = e.dept_id);
```

"Employees above the average *of their own department*" — each department gets its
own bar to clear.

In the SELECT list, a correlated subquery computes a per-row lookup:

```sql
SELECT d.name,
       (SELECT COUNT(*) FROM employees e WHERE e.dept_id = d.id) AS headcount
FROM departments d;
```

Note Research shows a headcount of **0** — an INNER JOIN version would have dropped
it entirely.

## EXISTS — "is there at least one?"

`EXISTS (subquery)` is true when the subquery returns **any row at all**. It's the
natural way to ask has-a / lacks-a questions:

```sql
SELECT d.name
FROM departments d
WHERE EXISTS (SELECT 1 FROM projects p WHERE p.dept_id = d.id);
```

`NOT EXISTS` finds the ones with none — and unlike `NOT IN`, it is completely
immune to the NULL trap:

```sql
SELECT d.name
FROM departments d
WHERE NOT EXISTS (SELECT 1 FROM employees e WHERE e.dept_id = d.id);
```

## Subqueries in FROM — derived tables

A subquery in the `FROM` clause acts as a temporary table (give it an alias!).
Use it to aggregate first, then query the aggregates:

```sql
SELECT d.name, ROUND(t.avg_sal, 2) AS avg_salary
FROM (SELECT dept_id, AVG(salary) AS avg_sal
      FROM employees
      GROUP BY dept_id) t
JOIN departments d ON d.id = t.dept_id
WHERE t.avg_sal > 80000;
```

The inner query builds a per-department salary table; the outer query filters and
joins it — something `HAVING` alone could also do here, but derived tables scale to
questions HAVING can't touch (like *averaging the averages*).

## Tips

- A scalar subquery that returns more than one row is a runtime error — use `IN`
  when multiple values are possible.
- Prefer `EXISTS` / `NOT EXISTS` over `IN` / `NOT IN` when the subquery might
  produce NULLs, or when you only care *whether* a match exists.
- Correlated subqueries are powerful but run per-row — when performance matters, a
  JOIN or derived table often does the same job in one pass.
- Subqueries nest arbitrarily deep. If you nest three levels, consider whether a
  join reads better.
