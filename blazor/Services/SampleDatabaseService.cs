using Microsoft.Data.Sqlite;
using SqlVisualizer.Models;

namespace SqlVisualizer.Services;

/// <summary>
/// Seeds sample SQLite databases in-memory. Each Learn-section concept gets its
/// own database so lessons stay isolated; school/store also serve the playground.
/// </summary>
public class SampleDatabaseService
{
    private readonly SqliteConnectionService _conn;

    public SampleDatabaseService(SqliteConnectionService conn) => _conn = conn;

    public IReadOnlyList<(string Name, string Label)> Samples { get; } =
    [
        ("movies",  "movies.db (Film catalog)"),
        ("school",  "school.db (Students / Courses)"),
        ("store",   "store.db (Products / Orders)"),
        ("sales",   "sales.db (Customers / Orders / Items)"),
        ("company", "company.db (Employees / Departments)"),
        ("library", "library.db (Books / Members / Loans)"),
        ("sandbox", "sandbox.db (empty scratch database)")
    ];

    public void OpenSample(string name)
    {
        switch (name)
        {
            case "movies":  _conn.OpenInMemory("movies.db",  SeedMovies);  break;
            case "school":  _conn.OpenInMemory("school.db",  SeedSchool);  break;
            case "store":   _conn.OpenInMemory("store.db",   SeedStore);   break;
            case "sales":   _conn.OpenInMemory("sales.db",   SeedSales);   break;
            case "company": _conn.OpenInMemory("company.db", SeedCompany); break;
            case "library": _conn.OpenInMemory("library.db", SeedLibrary); break;
            case "sandbox": _conn.OpenInMemory("sandbox.db", _ => { });    break;
            default:
                throw new ArgumentException($"Unknown sample: {name}");
        }
    }

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    // ── movies.db — single rich table for the SELECT lesson ───────────────────

    private static void SeedMovies(SqliteConnection c) => Exec(c, """
        CREATE TABLE movies (
            id             INTEGER PRIMARY KEY,
            title          TEXT NOT NULL,
            genre          TEXT,
            year           INTEGER,
            rating         REAL,
            runtime_min    INTEGER,
            gross_millions REAL,
            director       TEXT
        );
        INSERT INTO movies VALUES
            (1,'The Shawshank Redemption','Drama',1994,9.3,142,28.3,'Frank Darabont'),
            (2,'The Godfather','Crime',1972,9.2,175,250.0,'Francis Ford Coppola'),
            (3,'The Dark Knight','Action',2008,9.0,152,1006.0,'Christopher Nolan'),
            (4,'Pulp Fiction','Crime',1994,8.9,154,213.9,'Quentin Tarantino'),
            (5,'Forrest Gump','Drama',1994,8.8,142,678.2,'Robert Zemeckis'),
            (6,'Inception','Sci-Fi',2010,8.8,148,836.8,'Christopher Nolan'),
            (7,'The Matrix','Sci-Fi',1999,8.7,136,467.2,'Lana Wachowski'),
            (8,'Goodfellas','Crime',1990,8.7,145,47.1,'Martin Scorsese'),
            (9,'Interstellar','Sci-Fi',2014,8.7,169,701.7,'Christopher Nolan'),
            (10,'Spirited Away','Animation',2001,8.6,125,395.6,'Hayao Miyazaki'),
            (11,'Parasite','Thriller',2019,8.5,132,258.8,'Bong Joon-ho'),
            (12,'The Lion King','Animation',1994,8.5,88,968.5,'Roger Allers'),
            (13,'Gladiator','Action',2000,8.5,155,460.6,'Ridley Scott'),
            (14,'Titanic','Drama',1997,7.9,194,2201.6,'James Cameron'),
            (15,'Jurassic Park','Adventure',1993,8.2,127,1046.0,'Steven Spielberg'),
            (16,'Avatar','Sci-Fi',2009,7.9,162,2923.7,'James Cameron'),
            (17,'Toy Story','Animation',1995,8.3,81,394.4,'John Lasseter'),
            (18,'Jaws','Thriller',1975,8.1,124,476.5,'Steven Spielberg'),
            (19,'Alien','Sci-Fi',1979,8.5,117,106.3,'Ridley Scott'),
            (20,'Casablanca','Drama',1942,8.5,102,NULL,'Michael Curtiz'),
            (21,'Psycho','Thriller',1960,8.5,109,50.0,'Alfred Hitchcock'),
            (22,'Rear Window','Thriller',1954,8.5,112,NULL,'Alfred Hitchcock'),
            (23,'Up','Animation',2009,8.3,96,735.1,'Pete Docter'),
            (24,'Whiplash','Drama',2014,8.5,106,49.0,'Damien Chazelle');
        """);

    // ── school.db — joins lesson (mentor_id → self-joins, alumni → unions) ────

    private static void SeedSchool(SqliteConnection c) => Exec(c, """
        CREATE TABLE students (
            id        INTEGER PRIMARY KEY,
            name      TEXT NOT NULL,
            grade     INTEGER,
            major     TEXT,
            mentor_id INTEGER REFERENCES students(id)
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
        CREATE TABLE alumni (
            id        INTEGER PRIMARY KEY,
            name      TEXT NOT NULL,
            grad_year INTEGER,
            major     TEXT
        );
        INSERT INTO students VALUES
            (1,'Alice',11,'Math',3),
            (2,'Bob',10,'Science',6),
            (3,'Carol',12,'Math',NULL),
            (4,'Dave',11,'History',3),
            (5,'Eve',10,'Science',6),
            (6,'Frank',12,'Math',NULL),
            (7,'Grace',10,'Art',NULL),
            (8,'Hank',11,'Science',6),
            (9,'Ivy',10,'Undeclared',3);
        INSERT INTO courses VALUES
            (1,'Algebra',3),
            (2,'Biology',4),
            (3,'World History',3),
            (4,'Chemistry',4),
            (5,'Art Studio',2),
            (6,'Music Theory',2);
        INSERT INTO enrollments VALUES
            (1,1,1,'A'),(2,1,2,'B'),(3,2,2,'A'),
            (4,2,4,'C'),(5,3,1,'A'),(6,3,3,'B'),
            (7,4,3,'A'),(8,5,2,'B'),(9,5,4,'A'),
            (10,6,1,'C'),(11,7,5,'A'),(12,8,2,'B'),
            (13,8,4,'A');
        INSERT INTO alumni VALUES
            (1,'Zoe',2021,'Math'),
            (2,'Yuri',2022,'Science'),
            (3,'Grace',2023,'Art'),
            (4,'Walt',2020,'History');
        """);

    // ── store.db — playground sample ──────────────────────────────────────────

    private static void SeedStore(SqliteConnection c) => Exec(c, """
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
        """);

    // ── sales.db — summary-queries lesson ─────────────────────────────────────

    private static void SeedSales(SqliteConnection c) => Exec(c, """
        CREATE TABLE customers (
            id   INTEGER PRIMARY KEY,
            name TEXT NOT NULL,
            city TEXT
        );
        CREATE TABLE products (
            id       INTEGER PRIMARY KEY,
            name     TEXT NOT NULL,
            price    REAL,
            category TEXT
        );
        CREATE TABLE orders (
            id          INTEGER PRIMARY KEY,
            customer_id INTEGER REFERENCES customers(id),
            order_date  TEXT,
            status      TEXT
        );
        CREATE TABLE order_items (
            id         INTEGER PRIMARY KEY,
            order_id   INTEGER REFERENCES orders(id),
            product_id INTEGER REFERENCES products(id),
            quantity   INTEGER,
            unit_price REAL
        );
        INSERT INTO customers VALUES
            (1,'Ana Flores','Austin'),
            (2,'Ben Wright','Dallas'),
            (3,'Cara Chen','Austin'),
            (4,'Dan Novak','Houston'),
            (5,'Elle Kim','Dallas'),
            (6,'Finn Ross','Austin');
        INSERT INTO products VALUES
            (1,'Espresso Machine',249.99,'Kitchen'),
            (2,'French Press',29.99,'Kitchen'),
            (3,'Yoga Mat',39.99,'Fitness'),
            (4,'Dumbbell Set',89.99,'Fitness'),
            (5,'Desk Lamp',34.99,'Office'),
            (6,'Notebook Pack',12.99,'Office'),
            (7,'Water Bottle',19.99,'Fitness'),
            (8,'Coffee Grinder',59.99,'Kitchen');
        INSERT INTO orders VALUES
            (1,1,'2024-03-02','shipped'),
            (2,2,'2024-03-05','shipped'),
            (3,1,'2024-03-11','cancelled'),
            (4,3,'2024-03-18','shipped'),
            (5,4,'2024-04-01','shipped'),
            (6,2,'2024-04-09','pending'),
            (7,5,'2024-04-14','shipped'),
            (8,1,'2024-04-22','shipped'),
            (9,3,'2024-05-03','pending'),
            (10,4,'2024-05-10','shipped'),
            (11,5,'2024-05-16','shipped'),
            (12,2,'2024-05-21','shipped');
        INSERT INTO order_items VALUES
            (1,1,1,1,249.99),(2,1,6,3,12.99),
            (3,2,3,1,39.99),(4,2,7,2,19.99),
            (5,3,2,1,29.99),
            (6,4,4,1,89.99),(7,4,7,1,19.99),(8,4,3,1,39.99),
            (9,5,5,2,34.99),(10,5,6,5,12.99),
            (11,6,8,1,59.99),
            (12,7,1,1,249.99),(13,7,2,1,29.99),(14,7,8,1,59.99),
            (15,8,6,10,11.99),
            (16,9,3,2,39.99),(17,9,7,3,17.99),
            (18,10,4,2,89.99),
            (19,11,5,1,34.99),(20,11,6,2,12.99),(21,11,7,1,19.99),
            (22,12,2,2,29.99),(23,12,8,1,59.99),(24,12,1,1,229.99);
        """);

    // ── company.db — subqueries lesson ────────────────────────────────────────

    private static void SeedCompany(SqliteConnection c) => Exec(c, """
        CREATE TABLE departments (
            id       INTEGER PRIMARY KEY,
            name     TEXT NOT NULL,
            location TEXT
        );
        CREATE TABLE employees (
            id         INTEGER PRIMARY KEY,
            name       TEXT NOT NULL,
            dept_id    INTEGER REFERENCES departments(id),
            title      TEXT,
            salary     INTEGER,
            manager_id INTEGER REFERENCES employees(id)
        );
        CREATE TABLE projects (
            id      INTEGER PRIMARY KEY,
            name    TEXT NOT NULL,
            dept_id INTEGER REFERENCES departments(id),
            budget  INTEGER
        );
        CREATE TABLE assignments (
            id          INTEGER PRIMARY KEY,
            employee_id INTEGER REFERENCES employees(id),
            project_id  INTEGER REFERENCES projects(id),
            hours       INTEGER
        );
        INSERT INTO departments VALUES
            (1,'Engineering','Building A'),
            (2,'Marketing','Building B'),
            (3,'Sales','Building B'),
            (4,'HR','Building A'),
            (5,'Research','Building C');
        INSERT INTO employees VALUES
            (1,'Maya',1,'Engineering Manager',105000,NULL),
            (2,'Liam',1,'Engineer',88000,1),
            (3,'Noah',1,'Engineer',92000,1),
            (4,'Ava',1,'Intern',45000,1),
            (5,'Emma',2,'Marketing Manager',95000,NULL),
            (6,'Oliver',2,'Analyst',67000,5),
            (7,'Sophia',3,'Sales Manager',98000,NULL),
            (8,'Jack',3,'Account Exec',72000,7),
            (9,'Mia',3,'Account Exec',69000,7),
            (10,'Lucas',4,'HR Manager',78000,NULL),
            (11,'Amelia',4,'Recruiter',58000,10),
            (12,'Ethan',1,'Engineer',99000,1);
        INSERT INTO projects VALUES
            (1,'Apollo',1,250000),
            (2,'Hermes',1,120000),
            (3,'Spring Campaign',2,80000),
            (4,'Fall Campaign',2,95000),
            (5,'CRM Rollout',3,60000),
            (6,'Onboarding Revamp',4,30000);
        INSERT INTO assignments VALUES
            (1,2,1,120),(2,3,1,90),(3,12,1,200),
            (4,2,2,60),(5,4,2,40),(6,6,3,150),
            (7,6,4,80),(8,8,5,100),(9,9,5,120),
            (10,11,6,90),(11,3,2,30);
        """);

    // ── library.db — INSERT/UPDATE/DELETE lesson ──────────────────────────────

    private static void SeedLibrary(SqliteConnection c) => Exec(c, """
        CREATE TABLE books (
            id        INTEGER PRIMARY KEY,
            title     TEXT NOT NULL,
            author    TEXT,
            year      INTEGER,
            available INTEGER NOT NULL DEFAULT 1
        );
        CREATE TABLE members (
            id     INTEGER PRIMARY KEY,
            name   TEXT NOT NULL,
            joined TEXT
        );
        CREATE TABLE loans (
            id          INTEGER PRIMARY KEY,
            book_id     INTEGER REFERENCES books(id),
            member_id   INTEGER REFERENCES members(id),
            loan_date   TEXT,
            return_date TEXT
        );
        INSERT INTO books VALUES
            (1,'Dune','Frank Herbert',1965,1),
            (2,'Neuromancer','William Gibson',1984,1),
            (3,'The Hobbit','J.R.R. Tolkien',1937,0),
            (4,'Emma','Jane Austen',1815,1),
            (5,'Dracula','Bram Stoker',1897,1),
            (6,'Frankenstein','Mary Shelley',1818,0),
            (7,'The Trial','Franz Kafka',1925,1),
            (8,'Beloved','Toni Morrison',1987,1);
        INSERT INTO members VALUES
            (1,'Rosa Marin','2023-01-15'),
            (2,'Theo Banks','2023-03-02'),
            (3,'June Park','2023-06-20'),
            (4,'Omar Reyes','2024-01-08'),
            (5,'Lena Wolf','2024-02-14');
        INSERT INTO loans VALUES
            (1,3,1,'2024-05-01',NULL),
            (2,6,2,'2024-05-03',NULL),
            (3,1,3,'2024-04-10','2024-04-24'),
            (4,4,1,'2024-03-12','2024-03-30'),
            (5,2,4,'2024-04-28','2024-05-15'),
            (6,5,5,'2024-05-10','2024-05-28');
        """);
}
