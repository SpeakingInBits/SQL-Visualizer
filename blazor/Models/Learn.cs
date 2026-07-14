namespace SqlVisualizer.Models;

// ── Learn section (notebook lessons) ──────────────────────────────────────────

/// Metadata for a single lesson (from meta.json / manifest.json).
public record LessonMeta(
    string Id,
    string Title,
    string Description,
    string Database,   // sample DB id to auto-load, e.g. "school" | "store"
    int Order);

/// A loaded lesson: its metadata plus the raw markdown body (content.md).
public record Lesson(LessonMeta Meta, string Content);

/// The content manifest listing every available lesson.
public record LearnManifest(List<LessonMeta> Lessons);
