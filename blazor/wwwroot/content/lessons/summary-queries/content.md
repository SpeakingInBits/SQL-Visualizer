# Summary Queries

So far every query returned individual rows. **Summary queries** collapse many rows
into a few numbers — counts, totals, averages. This lesson uses the **`sales`**
database:

- **`customers`** — `id`, `name`, `city`
- **`products`** — `id`, `name`, `price`, `category`
- **`orders`** — `id`, `customer_id`, `order_date`, `status`
- **`order_items`** — `id`, `order_id`, `product_id`, `quantity`, `unit_price`

## Aggregate functions

An **aggregate function** eats a whole column of values and spits out one value.
The big five:

```sql
SELECT COUNT(*)      AS orders,
       MIN(order_date) AS first_order,
       MAX(order_date) AS last_order
FROM orders;
```

```sql
SELECT AVG(price) AS avg_price,
       SUM(price) AS total_price
FROM products;
```

Aggregates ignore NULLs — and that makes `COUNT` come in three flavors:

- `COUNT(*)` — number of **rows**
- `COUNT(col)` — number of rows where `col` is **not NULL**
- `COUNT(DISTINCT col)` — number of **different** non-NULL values

```sql
SELECT COUNT(*) AS order_count,
       COUNT(DISTINCT customer_id) AS customers_who_ordered
FROM orders;
```

Expressions work inside aggregates. Revenue is quantity × price, summed:

```sql
SELECT SUM(quantity * unit_price) AS total_revenue
FROM order_items;
```

`ROUND(x, 2)` tidies up decimal noise: `ROUND(AVG(price), 2)`.

## GROUP BY — one summary row per group

The real power move: `GROUP BY` splits the rows into buckets and runs the
aggregates **once per bucket**:

```sql
SELECT status, COUNT(*) AS order_count
FROM orders
GROUP BY status;
```

Group by any column — or an expression. `substr(order_date, 1, 7)` chops a date
like `2024-03-02` down to its year-month:

```sql
SELECT substr(order_date, 1, 7) AS month, COUNT(*) AS orders
FROM orders
GROUP BY substr(order_date, 1, 7);
```

**The golden rule:** every column in your `SELECT` must be either inside an
aggregate or listed in `GROUP BY`. Anything else is asking "which single value
should represent the whole bucket?" — an unanswerable question.

### Grouping joined rows

Join first, group after. Orders per customer, by name:

```sql
SELECT c.name, COUNT(o.id) AS order_count
FROM customers c
JOIN orders o ON o.customer_id = c.id
GROUP BY c.name;
```

Note `COUNT(o.id)` and a LEFT JOIN can include customers with **zero** orders —
`COUNT(*)` would count the customer's own row and report 1:

```sql
SELECT c.name, COUNT(o.id) AS order_count
FROM customers c
LEFT JOIN orders o ON o.customer_id = c.id
GROUP BY c.name;
```

## HAVING — filtering groups

`WHERE` filters **rows before grouping**. To filter the **groups themselves**, use
`HAVING` — it runs after the aggregates exist:

```sql
SELECT c.name, COUNT(o.id) AS order_count
FROM customers c
JOIN orders o ON o.customer_id = c.id
GROUP BY c.name
HAVING COUNT(o.id) >= 3;
```

Both can appear in one query — each doing its own job:

```sql
SELECT c.name, COUNT(o.id) AS shipped_orders
FROM customers c
JOIN orders o ON o.customer_id = c.id
WHERE o.status = 'shipped'
GROUP BY c.name
HAVING COUNT(o.id) >= 2;
```

## The full clause order

```text
SELECT columns / aggregates
FROM table
JOIN … ON …
WHERE row filter        (before grouping)
GROUP BY bucket columns
HAVING group filter     (after aggregating)
ORDER BY sort keys
LIMIT row count
```

## Tips

- `WHERE` can't see aggregates (`WHERE COUNT(*) > 2` is an error) — that's what
  `HAVING` is for.
- Grouping by `id` and grouping by `name` differ if two rows share a name — when in
  doubt, group by the id and *also* select the name.
- An aggregate query with **no** GROUP BY treats the whole table as one big group
  and returns exactly one row.
