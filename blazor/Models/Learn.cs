namespace SqlVisualizer.Models;

// ── Learn section (notebook lessons) ──────────────────────────────────────────

/// Metadata for a single lesson (from meta.json / manifest.json).
public record LessonMeta(
    string Id,
    string Title,
    string Description,
    string Database,   // sample DB id to auto-load, e.g. "school" | "store" ("" = none)
    int Order)
{
    /// "lesson" (markdown + live cells + practice) or "quiz" (markdown + quiz engine).
    public string Kind { get; init; } = "lesson";

    /// Number of practice problems (or quiz questions), for index-page progress.
    public int ExerciseCount { get; init; }
}

/// A loaded lesson: its metadata plus the raw markdown body (content.md).
public record Lesson(LessonMeta Meta, string Content);

/// The content manifest listing every available lesson.
public record LearnManifest(List<LessonMeta> Lessons);

// ── Practice problems (practice.json) ─────────────────────────────────────────

/// One practice problem. Two checking modes:
///  - "query" (default): the learner writes a SELECT; their result set is
///    compared against the result of <see cref="Solution"/>.
///  - "state": the learner mutates the database (DML/DDL). The checker re-seeds
///    the DB, applies <see cref="Solution"/>, runs <see cref="Verify"/> to get
///    the expected state, then re-seeds again, applies the learner's SQL, and
///    compares the same verify query.
public record PracticeProblem(
    string Id,
    string Prompt,
    string Solution)
{
    public string Type { get; init; } = "query";
    public string? Hint { get; init; }
    /// SELECT used to inspect DB state for "state" problems.
    public string? Verify { get; init; }
    /// Optional SQL pre-filled in the editor as a starting point.
    public string? Starter { get; init; }
}

public record PracticeSet(List<PracticeProblem> Problems);

// ── Quiz (quiz.json) ──────────────────────────────────────────────────────────

public record QuizQuestion(
    string Id,
    string Question,
    List<string> Choices,
    int Answer,               // index into Choices
    string? Explanation);

public record Quiz(List<QuizQuestion> Questions)
{
    /// Fraction of correct answers needed to pass (lesson counts as complete).
    public double PassThreshold { get; init; } = 0.7;
}
