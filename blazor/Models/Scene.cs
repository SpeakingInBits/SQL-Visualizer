namespace SqlVisualizer.Models;

// ── Node-graph visualization scene ───────────────────────────────────────────
//
// A scene is a static graph of positioned nodes + edges (world coordinates in
// pixels) plus a list of animation frames. Each frame is a *diff* over the base
// scene: which nodes are hidden, what state class each node carries, and which
// edges are active. The NodeCanvas renders the scene at a given frame index and
// handles pan/zoom; it knows nothing about SQL.

/// A positioned element on the canvas.
public record VizNode(
    string Id,
    string Kind,               // "row" | "header" | "title" | "card" | "label"
    double X, double Y,        // top-left in world coordinates
    double W, double H,
    List<string> Cells,        // row/header: one string per column cell
    string? Title = null,      // title/label/card text
    string? Badge = null);     // card: the condition expression

/// A connector between two nodes.
public record VizEdge(
    string FromId,
    string ToId,
    string Kind = "");         // "static" edges always draw; others draw only when active

/// One animation step — a diff over the base scene.
public record VizFrame(
    Dictionary<string, string> States,   // nodeId -> state class ("" = default)
    HashSet<string> Hidden,              // nodes not drawn this frame
    List<int> ActiveEdges,               // indices into VizScene.Edges to highlight
    string? Phase = null);               // human-readable phase label

public record VizScene(
    string VizType,
    string Title,
    string Subtitle,
    List<VizNode> Nodes,
    List<VizEdge> Edges,
    List<VizFrame> Frames,
    double Width,
    double Height);
