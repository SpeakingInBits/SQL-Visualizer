"""
Seed sample SQLite databases into app_data/samples/ on first run.

Two databases are created:
  school.db  — students / courses / enrollments  (ORDER BY, WHERE, JOIN demos)
  store.db   — categories / products / orders    (aggregate + JOIN demos)
"""
from __future__ import annotations

import sqlite3
from pathlib import Path

APP_DATA_DIR = Path(__file__).parent / "app_data"
SAMPLES_DIR = APP_DATA_DIR / "samples"


# ── school.db ─────────────────────────────────────────────────────────────────

def _create_school(conn: sqlite3.Connection) -> None:
    conn.executescript("""
        CREATE TABLE students (
            id    INTEGER PRIMARY KEY,
            name  TEXT    NOT NULL,
            age   INTEGER,
            grade TEXT,
            gpa   REAL
        );

        CREATE TABLE courses (
            id      INTEGER PRIMARY KEY,
            title   TEXT    NOT NULL,
            teacher TEXT,
            credits INTEGER
        );

        CREATE TABLE enrollments (
            id         INTEGER PRIMARY KEY,
            student_id INTEGER REFERENCES students(id),
            course_id  INTEGER REFERENCES courses(id),
            score      INTEGER
        );

        INSERT INTO students VALUES
            (1,  'Alice Johnson',   19, 'Sophomore', 3.8),
            (2,  'Bob Martinez',    20, 'Junior',    3.1),
            (3,  'Carol Lee',       18, 'Freshman',  3.9),
            (4,  'David Kim',       21, 'Senior',    2.7),
            (5,  'Emma Wilson',     19, 'Sophomore', 3.5),
            (6,  'Frank Chen',      22, 'Senior',    3.2),
            (7,  'Grace Patel',     18, 'Freshman',  3.7),
            (8,  'Henry Brown',     20, 'Junior',    2.9),
            (9,  'Iris Nakamura',   19, 'Sophomore', 3.6),
            (10, 'James O''Brien',  21, 'Senior',    3.0),
            (11, 'Karen Smith',     18, 'Freshman',  4.0),
            (12, 'Luis Ramirez',    20, 'Junior',    3.3),
            (13, 'Mia Thompson',    22, 'Senior',    2.8),
            (14, 'Noah Davis',      19, 'Sophomore', 3.4),
            (15, 'Olivia Garcia',   20, 'Junior',    3.6);

        INSERT INTO courses VALUES
            (1, 'Introduction to Databases', 'Dr. Foster',  3),
            (2, 'Data Structures',           'Dr. Yuen',    4),
            (3, 'Web Development',           'Prof. Russo', 3),
            (4, 'Statistics',                'Dr. Okafor',  3),
            (5, 'Machine Learning',          'Dr. Yuen',    4),
            (6, 'Operating Systems',         'Prof. Russo', 4);

        INSERT INTO enrollments VALUES
            (1,  1, 1, 92), (2,  1, 2, 85), (3,  1, 4, 90),
            (4,  2, 1, 74), (5,  2, 3, 80), (6,  2, 6, 68),
            (7,  3, 1, 98), (8,  3, 2, 95), (9,  3, 5, 97),
            (10, 4, 3, 61), (11, 4, 4, 70),
            (12, 5, 1, 88), (13, 5, 3, 84), (14, 5, 4, 79),
            (15, 6, 2, 91), (16, 6, 5, 87), (17, 6, 6, 83),
            (18, 7, 1, 96), (19, 7, 4, 93),
            (20, 8, 1, 72), (21, 8, 3, 65),
            (22, 9, 2, 89), (23, 9, 4, 82),
            (24,10, 1, 75), (25,10, 3, 71), (26,10, 6, 69),
            (27,11, 1, 99), (28,11, 2, 97), (29,11, 4, 100),
            (30,12, 3, 77), (31,12, 5, 81),
            (32,13, 1, 63), (33,13, 6, 58),
            (34,14, 2, 86), (35,14, 4, 80),
            (36,15, 1, 90), (37,15, 3, 88), (38,15, 5, 92);
    """)
    conn.commit()


# ── store.db ──────────────────────────────────────────────────────────────────

def _create_store(conn: sqlite3.Connection) -> None:
    conn.executescript("""
        CREATE TABLE categories (
            id   INTEGER PRIMARY KEY,
            name TEXT NOT NULL
        );

        CREATE TABLE products (
            id          INTEGER PRIMARY KEY,
            name        TEXT    NOT NULL,
            category_id INTEGER REFERENCES categories(id),
            price       REAL,
            stock       INTEGER
        );

        CREATE TABLE orders (
            id            INTEGER PRIMARY KEY,
            product_id    INTEGER REFERENCES products(id),
            quantity      INTEGER,
            order_date    TEXT,
            customer_name TEXT
        );

        INSERT INTO categories VALUES
            (1, 'Electronics'),
            (2, 'Books'),
            (3, 'Clothing'),
            (4, 'Food & Drink');

        INSERT INTO products VALUES
            (1,  'Laptop Pro 15',      1, 1299.99, 12),
            (2,  'Wireless Mouse',     1,   29.99, 85),
            (3,  'USB-C Hub',          1,   49.99, 40),
            (4,  'SQL for Beginners',  2,   34.99, 60),
            (5,  'Python Crash Course',2,   29.99, 75),
            (6,  'Clean Code',         2,   39.99, 50),
            (7,  'Winter Jacket',      3,   89.99,  8),
            (8,  'Denim Jeans',        3,   59.99, 20),
            (9,  'Running Shoes',      3,  119.99, 15),
            (10, 'Coffee Beans 1kg',   4,   18.99, 200),
            (11, 'Green Tea 50 bags',  4,    9.99, 150),
            (12, 'Protein Bars x12',   4,   24.99, 90);

        INSERT INTO orders VALUES
            (1,  4,  2, '2025-01-05', 'Alice Johnson'),
            (2,  2,  1, '2025-01-07', 'Bob Martinez'),
            (3,  10, 3, '2025-01-10', 'Carol Lee'),
            (4,  1,  1, '2025-01-12', 'David Kim'),
            (5,  5,  1, '2025-01-15', 'Emma Wilson'),
            (6,  7,  1, '2025-01-18', 'Frank Chen'),
            (7,  3,  2, '2025-01-20', 'Grace Patel'),
            (8,  11, 4, '2025-01-22', 'Henry Brown'),
            (9,  6,  1, '2025-01-25', 'Iris Nakamura'),
            (10, 4,  3, '2025-01-28', 'James O''Brien'),
            (11, 9,  1, '2025-02-01', 'Karen Smith'),
            (12, 2,  2, '2025-02-03', 'Luis Ramirez'),
            (13, 12, 5, '2025-02-05', 'Mia Thompson'),
            (14, 5,  2, '2025-02-08', 'Noah Davis'),
            (15, 10, 2, '2025-02-10', 'Olivia Garcia'),
            (16, 8,  1, '2025-02-12', 'Alice Johnson'),
            (17, 3,  1, '2025-02-14', 'Bob Martinez'),
            (18, 1,  1, '2025-02-18', 'Carol Lee'),
            (19, 6,  2, '2025-02-20', 'David Kim'),
            (20, 11, 6, '2025-02-22', 'Emma Wilson');
    """)
    conn.commit()


# ── Public API ────────────────────────────────────────────────────────────────

_CREATORS = {
    "school.db": _create_school,
    "store.db":  _create_store,
}


def ensure_samples() -> None:
    """Create sample databases if they do not already exist."""
    SAMPLES_DIR.mkdir(parents=True, exist_ok=True)
    for filename, creator in _CREATORS.items():
        path = SAMPLES_DIR / filename
        if not path.exists():
            conn = sqlite3.connect(path)
            try:
                creator(conn)
            finally:
                conn.close()


def list_samples() -> list[dict]:
    """Return [{name, path}] for every .db file in the samples directory."""
    SAMPLES_DIR.mkdir(parents=True, exist_ok=True)
    return [
        {"name": p.stem, "path": str(p)}
        for p in sorted(SAMPLES_DIR.glob("*.db"))
    ]
