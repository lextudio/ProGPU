namespace ProGPU.GameEngine.Simulation;

public readonly record struct Bounds2D(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool IsValid => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Width) && double.IsFinite(Height) &&
        double.IsFinite(Right) && double.IsFinite(Bottom) && Width > 0 && Height > 0 && Right > X && Bottom > Y;
    public bool Intersects(Bounds2D other) => X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
}

public enum StaticContactKind { Solid, OneWayTop }
public readonly record struct StaticCollider2D(int Id, Bounds2D Bounds, StaticContactKind Kind = StaticContactKind.Solid);
public readonly record struct KinematicMove2D(Bounds2D Bounds, double MovedX, double MovedY, int HorizontalContact, int VerticalContact,
    bool Grounded, bool OverlappingStart);

/// <summary>
/// Immutable static collision generation. Original axis-separated swept character
/// motion, with nearest-boundary selection independent of input ordering. Building
/// sorts O(N log N) time/O(N) storage. Queries use two binary bounds and scan candidate
/// intervals, O(log N + K), worst O(N) for overlapping long intervals. Move queries
/// both swept axes without allocating, copying colliders or invoking callbacks.
/// This is a kinematic rectangle solver, not a general rigid-body engine.
/// </summary>
public sealed class StaticCollisionWorld2D
{
    public const int MaximumColliders = 65_536;
    private readonly StaticCollider2D[] _colliders;
    private readonly double[] _prefixRight;
    public int Count => _colliders.Length;

    public StaticCollisionWorld2D(ReadOnlySpan<StaticCollider2D> colliders)
    {
        if (colliders.Length > MaximumColliders) throw new ArgumentOutOfRangeException(nameof(colliders));
        var identities = new HashSet<int>();
        foreach (var collider in colliders)
            if (collider.Id < 0 || !identities.Add(collider.Id) || !collider.Bounds.IsValid ||
                collider.Kind is not (StaticContactKind.Solid or StaticContactKind.OneWayTop))
                throw new ArgumentException("Static colliders require distinct nonnegative IDs, valid rectangles and supported contact kinds.", nameof(colliders));
        _colliders = colliders.ToArray();
        Array.Sort(_colliders, static (a, b) => { int order = a.Bounds.X.CompareTo(b.Bounds.X); return order != 0 ? order : a.Id.CompareTo(b.Id); });
        _prefixRight = new double[_colliders.Length]; double right = double.NegativeInfinity;
        for (int i = 0; i < _colliders.Length; i++) _prefixRight[i] = right = Math.Max(right, _colliders[i].Bounds.Right);
    }

    public Query QueryBounds(Bounds2D bounds)
    {
        if (!bounds.IsValid) throw new ArgumentException("Query bounds must be a finite positive rectangle.", nameof(bounds));
        int lo = 0, hi = _colliders.Length;
        while (lo < hi) { int mid = lo + (hi - lo) / 2; if (_colliders[mid].Bounds.X <= bounds.Right) lo = mid + 1; else hi = mid; }
        int end = lo; lo = 0; hi = end;
        while (lo < hi) { int mid = lo + (hi - lo) / 2; if (_prefixRight[mid] < bounds.X) lo = mid + 1; else hi = mid; }
        return new(_colliders, lo, end, bounds);
    }

    public ref struct Query
    {
        private readonly ReadOnlySpan<StaticCollider2D> _items;
        private readonly int _end;
        private readonly Bounds2D _bounds;
        private int _next;
        public StaticCollider2D Current { get; private set; }
        internal Query(ReadOnlySpan<StaticCollider2D> items, int start, int end, Bounds2D bounds)
        { _items = items; _next = start; _end = end; _bounds = bounds; Current = default; }
        public bool MoveNext()
        {
            while (_next < _end)
            {
                var item = _items[_next++]; var b = item.Bounds;
                // Closed bounds keep exact edge contacts available to the solver.
                if (b.Right < _bounds.X || b.Bottom < _bounds.Y || b.Y > _bounds.Bottom) continue;
                Current = item; return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Sweep X then Y against static solids. One-way tops collide only while moving
    /// down from at/above their top; ignoreOneWay enables explicit drop-through.
    /// Initial penetration is reported, never silently resolved in an arbitrary
    /// direction. Contacts choose the lowest stable ID when boundaries coincide.
    /// No gravity, jump strength, floor clamp, section clamp or timestep is imposed.
    /// </summary>
    public KinematicMove2D Move(Bounds2D body, double deltaX, double deltaY, bool ignoreOneWay = false)
    {
        if (!body.IsValid || !double.IsFinite(deltaX) || !double.IsFinite(deltaY) ||
            !double.IsFinite(body.X + deltaX + body.Width) || !double.IsFinite(body.Y + deltaY + body.Height))
            throw new ArgumentException("Kinematic movement needs finite geometry and displacement.");
        bool overlapping = false; var initial = QueryBounds(body);
        while (initial.MoveNext())
            if (initial.Current.Kind == StaticContactKind.Solid && body.Intersects(initial.Current.Bounds)) { overlapping = true; break; }
        double moveX = deltaX, moveY = deltaY; int horizontal = -1, vertical = -1; bool grounded = false;
        var query = QueryBounds(new(Math.Min(body.X, body.X + deltaX), body.Y, body.Width + Math.Abs(deltaX), body.Height));
        while (query.MoveNext())
        {
            var item = query.Current; var b = item.Bounds;
            if (item.Kind != StaticContactKind.Solid || body.Y >= b.Bottom || body.Bottom <= b.Y) continue;
            if (deltaX > 0 && body.Right <= b.X)
            {
                double distance = b.X - body.Right;
                if (distance < moveX || distance == moveX && Earlier(item.Id, horizontal)) { moveX = distance; horizontal = item.Id; }
            }
            else if (deltaX < 0 && body.X >= b.Right)
            {
                double distance = b.Right - body.X;
                if (distance > moveX || distance == moveX && Earlier(item.Id, horizontal)) { moveX = distance; horizontal = item.Id; }
            }
        }
        var moved = body with { X = body.X + moveX };
        query = QueryBounds(new(moved.X, Math.Min(moved.Y, moved.Y + deltaY), moved.Width, moved.Height + Math.Abs(deltaY)));
        while (query.MoveNext())
        {
            var item = query.Current; var b = item.Bounds;
            if (moved.X >= b.Right || moved.Right <= b.X || ignoreOneWay && item.Kind == StaticContactKind.OneWayTop) continue;
            if (deltaY >= 0 && moved.Bottom <= b.Y)
            {
                double distance = b.Y - moved.Bottom;
                if (distance < moveY || distance == moveY && Earlier(item.Id, vertical))
                { moveY = distance; vertical = item.Id; grounded = true; }
            }
            else if (deltaY < 0 && item.Kind == StaticContactKind.Solid && moved.Y >= b.Bottom)
            {
                double distance = b.Bottom - moved.Y;
                if (distance > moveY || distance == moveY && Earlier(item.Id, vertical)) { moveY = distance; vertical = item.Id; }
            }
        }
        moved = moved with { Y = body.Y + moveY };
        return new(moved, moveX, moveY, horizontal, vertical, grounded, overlapping);
    }
    private static bool Earlier(int candidate, int current) => current < 0 || candidate < current;
}
