using Microsoft.Data.Sqlite;
using SqlVisualizer.Models;

namespace SqlVisualizer.Services;

/// <summary>
/// Seeds sample SQLite databases in-memory (mirrors Python seed.py).
/// </summary>
public class SampleDatabaseService
{
    private readonly SqliteConnectionService _conn;

    public SampleDatabaseService(SqliteConnectionService conn) => _conn = conn;

    public IReadOnlyList<(string Name, string Label)> Samples { get; } =
    [
        ("school", "school.db (Students / Courses)"),
        ("store",  "store.db (Products / Orders)")
    ];

    public void OpenSample(string name)
    {
        switch (name)
        {
            case "school":
                _conn.OpenInMemory("school.db", SeedSchool);
                break;
            case "store":
                _conn.OpenInMemory("store.db", SeedStore);
                break;
            default:
                throw new ArgumentException($"Unknown sample: {name}");
        }
    }

    // ── school.db ─────────────────────────────────────────────────────────────

    private static void SeedSchool(SqliteConnection c)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE students (
                id       INTEGER PRIMARY KEY,
                name     TEXT NOT NULL,
                grade    INTEGER,
                major    TEXT
            );
            CREATE TABLE courses (
                id       INTEGER PRIMARY KEY,
                title    TEXT NOT NULL,
                credits  INTEGER
            );
            CREATE TABLE enrollments (
                id         INTEGER PRIMARY KEY,
                student_id INTEGER REFERENCES students(id),
                course_id  INTEGER REFERENCES courses(id),
                grade      TEXT
            );
            INSERT INTO students VALUES
                (1,'Alice',11,'Math'),
                (2,'Bob',10,'Science'),
                (3,'Carol',12,'Math'),
                (4,'Dave',11,'History'),
                (5,'Eve',10,'Science'),
                (6,'Frank',12,'Math'),
                (7,'Grace',10,'Art'),
                (8,'Hank',11,'Science');
            INSERT INTO courses VALUES
                (1,'Algebra',3),
                (2,'Biology',4),
                (3,'World History',3),
                (4,'Chemistry',4),
                (5,'Art Studio',2);
            INSERT INTO enrollments VALUES
                (1,1,1,'A'),(2,1,2,'B'),(3,2,2,'A'),
                (4,2,4,'C'),(5,3,1,'A'),(6,3,3,'B'),
                (7,4,3,'A'),(8,5,2,'B'),(9,5,4,'A'),
                (10,6,1,'C'),(11,7,5,'A'),(12,8,2,'B'),
                (13,8,4,'A');
            """;
        cmd.ExecuteNonQuery();
    }

    // ── store.db ──────────────────────────────────────────────────────────────

    private static void SeedStore(SqliteConnection c)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE categories (
                id   INTEGER PRIMARY KEY,
                name TEXT NOT NULL
            );
            CREATE TABLE products (
                id          INTEGER PRIMARY KEY,
                name        TEXT NOT NULL,
                category_id INTEGER REFERENCES categories(id),
                price       REAL,
                stock       INTEGER
            );
            CREATE TABLE orders (
                id         INTEGER PRIMARY KEY,
                product_id INTEGER REFERENCES products(id),
                quantity   INTEGER,
                order_date TEXT
            );
            INSERT INTO categories VALUES
                (1,'Electronics'),(2,'Clothing'),(3,'Food');
            INSERT INTO products VALUES
                (1,'Laptop',1,999.99,15),
                (2,'Phone',1,699.99,30),
                (3,'T-Shirt',2,19.99,100),
                (4,'Jeans',2,49.99,60),
                (5,'Coffee',3,12.99,200),
                (6,'Tea',3,8.99,150),
                (7,'Headphones',1,149.99,25);
            INSERT INTO orders VALUES
                (1,1,2,'2024-01-05'),
                (2,2,5,'2024-01-07'),
                (3,3,10,'2024-01-08'),
                (4,5,20,'2024-01-10'),
                (5,7,3,'2024-01-12'),
                (6,4,7,'2024-01-15'),
                (7,6,15,'2024-01-20'),
                (8,1,1,'2024-01-22'),
                (9,2,4,'2024-01-25');
            """;
        cmd.ExecuteNonQuery();
    }
}
