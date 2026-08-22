namespace PixelVoxel.Core;

/// <summary>Describes one coordinate's value before and after an atomic edit.</summary>
public sealed record VoxelChange(
    VoxelCoordinate Coordinate,
    VoxelCell? Before,
    VoxelCell? After);

/// <summary>Contains an atomic, reversible change to voxel values and document dimensions.</summary>
public sealed class VoxelChangeSet
{
    /// <summary>Initializes one reversible document change.</summary>
    public VoxelChangeSet(
        string description,
        VoxelDimensions beforeDimensions,
        VoxelDimensions afterDimensions,
        IEnumerable<VoxelChange> changes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Description = description;
        BeforeDimensions = beforeDimensions;
        AfterDimensions = afterDimensions;
        Changes = changes?.ToArray() ?? throw new ArgumentNullException(nameof(changes));
    }

    /// <summary>Gets the user-facing edit description.</summary>
    public string Description { get; }

    /// <summary>Gets dimensions expected before applying this change.</summary>
    public VoxelDimensions BeforeDimensions { get; }

    /// <summary>Gets dimensions after applying this change.</summary>
    public VoxelDimensions AfterDimensions { get; }

    /// <summary>Gets changed coordinates.</summary>
    public IReadOnlyList<VoxelChange> Changes { get; }

    /// <summary>Gets whether this change has no observable effect.</summary>
    public bool IsEmpty => BeforeDimensions == AfterDimensions && Changes.Count == 0;

    /// <summary>Creates the change that restores the prior state.</summary>
    public VoxelChangeSet Inverse() =>
        new(
            $"Undo {Description}",
            AfterDimensions,
            BeforeDimensions,
            Changes.Select(change => new VoxelChange(change.Coordinate, change.After, change.Before)));
}

/// <summary>Builds one atomic change from the current document state.</summary>
public interface IVoxelEditCommand
{
    /// <summary>Gets the user-facing edit description.</summary>
    string Description { get; }

    /// <summary>Creates a reversible change without mutating the document.</summary>
    VoxelChangeSet CreateChangeSet(VoxelDocument document);
}

/// <summary>Tracks reversible document edits without exposing storage representation.</summary>
public sealed class VoxelEditHistory
{
    private readonly int _capacity;
    private readonly List<HistoryEntry> _entries = [];
    private int _cursor;
    private long _nextStateId = 1;
    private long _currentStateId;
    private long _cleanStateId;

    /// <summary>Initializes an edit history with a bounded entry count.</summary>
    public VoxelEditHistory(int capacity = 256)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    /// <summary>Occurs when history position or dirty state changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets whether an edit can be undone.</summary>
    public bool CanUndo => _cursor > 0;

    /// <summary>Gets whether an edit can be redone.</summary>
    public bool CanRedo => _cursor < _entries.Count;

    /// <summary>Gets whether the current state differs from the last clean checkpoint.</summary>
    public bool IsDirty => _currentStateId != _cleanStateId;

    /// <summary>Executes and records one edit command.</summary>
    public bool Execute(VoxelDocument document, IVoxelEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Execute(document, command.CreateChangeSet(document), 0);
    }

    /// <summary>Executes a command associated with one project frame context.</summary>
    public bool Execute(VoxelDocument document, IVoxelEditCommand command, int contextId)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Execute(document, command.CreateChangeSet(document), contextId);
    }

    /// <summary>Executes and records one prebuilt atomic change.</summary>
    public bool Execute(VoxelDocument document, VoxelChangeSet changeSet)
        => Execute(document, changeSet, 0);

    /// <summary>Executes a prebuilt change associated with one project frame context.</summary>
    public bool Execute(VoxelDocument document, VoxelChangeSet changeSet, int contextId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(changeSet);
        if (changeSet.IsEmpty) return false;

        if (_cursor < _entries.Count)
        {
            _entries.RemoveRange(_cursor, _entries.Count - _cursor);
        }

        long beforeStateId = _currentStateId;
        long afterStateId = _nextStateId++;
        document.Apply(changeSet);
        _entries.Add(new HistoryEntry(changeSet, contextId, beforeStateId, afterStateId));
        _cursor++;
        _currentStateId = afterStateId;
        if (_entries.Count > _capacity)
        {
            _entries.RemoveAt(0);
            _cursor--;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Restores the document state before the most recent edit.</summary>
    public bool Undo(VoxelDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!CanUndo) return false;
        HistoryEntry entry = _entries[_cursor - 1];
        document.Apply(entry.ChangeSet.Inverse());
        _cursor--;
        _currentStateId = entry.BeforeStateId;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Undoes the latest project-wide edit and resolves its owning frame document.</summary>
    public bool Undo(Func<int, VoxelDocument> resolveDocument, out int contextId)
    {
        ArgumentNullException.ThrowIfNull(resolveDocument);
        contextId = 0;
        if (!CanUndo) return false;
        HistoryEntry entry = _entries[_cursor - 1];
        VoxelDocument document = resolveDocument(entry.ContextId) ??
            throw new InvalidOperationException($"Edit context {entry.ContextId} has no document.");
        document.Apply(entry.ChangeSet.Inverse());
        contextId = entry.ContextId;
        _cursor--;
        _currentStateId = entry.BeforeStateId;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Reapplies the next previously undone edit.</summary>
    public bool Redo(VoxelDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!CanRedo) return false;
        HistoryEntry entry = _entries[_cursor];
        document.Apply(entry.ChangeSet);
        _cursor++;
        _currentStateId = entry.AfterStateId;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Redoes the next project-wide edit and resolves its owning frame document.</summary>
    public bool Redo(Func<int, VoxelDocument> resolveDocument, out int contextId)
    {
        ArgumentNullException.ThrowIfNull(resolveDocument);
        contextId = 0;
        if (!CanRedo) return false;
        HistoryEntry entry = _entries[_cursor];
        VoxelDocument document = resolveDocument(entry.ContextId) ??
            throw new InvalidOperationException($"Edit context {entry.ContextId} has no document.");
        document.Apply(entry.ChangeSet);
        contextId = entry.ContextId;
        _cursor++;
        _currentStateId = entry.AfterStateId;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Marks the current state as saved.</summary>
    public void MarkClean()
    {
        _cleanStateId = _currentStateId;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Clears session history and treats the document as saved.</summary>
    public void Clear()
    {
        _entries.Clear();
        _cursor = 0;
        _currentStateId = _nextStateId++;
        _cleanStateId = _currentStateId;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed record HistoryEntry(
        VoxelChangeSet ChangeSet,
        int ContextId,
        long BeforeStateId,
        long AfterStateId);
}

/// <summary>Defines an inclusive, axis-aligned selection in voxel coordinates.</summary>
public readonly record struct VoxelSelectionBox
{
    /// <summary>Initializes a normalized selection from two corner coordinates.</summary>
    public VoxelSelectionBox(VoxelCoordinate first, VoxelCoordinate second)
    {
        Minimum = new VoxelCoordinate(
            Math.Min(first.X, second.X),
            Math.Min(first.Y, second.Y),
            Math.Min(first.Z, second.Z));
        Maximum = new VoxelCoordinate(
            Math.Max(first.X, second.X),
            Math.Max(first.Y, second.Y),
            Math.Max(first.Z, second.Z));
    }

    /// <summary>Gets the inclusive minimum corner.</summary>
    public VoxelCoordinate Minimum { get; }

    /// <summary>Gets the inclusive maximum corner.</summary>
    public VoxelCoordinate Maximum { get; }

    /// <summary>Gets whether a coordinate is inside this selection.</summary>
    public bool Contains(VoxelCoordinate coordinate) =>
        coordinate.X >= Minimum.X && coordinate.X <= Maximum.X &&
        coordinate.Y >= Minimum.Y && coordinate.Y <= Maximum.Y &&
        coordinate.Z >= Minimum.Z && coordinate.Z <= Maximum.Z;
}

/// <summary>Adds uniformly colored voxels to empty coordinates.</summary>
public sealed class AddVoxelsCommand : IVoxelEditCommand
{
    private readonly VoxelCoordinate[] _coordinates;
    private readonly Rgba32Color _color;

    /// <summary>Initializes a batched add command.</summary>
    public AddVoxelsCommand(IEnumerable<VoxelCoordinate> coordinates, Rgba32Color color)
    {
        _coordinates = DistinctCoordinates(coordinates);
        _color = color;
    }

    /// <inheritdoc />
    public string Description => "Add voxels";

    /// <inheritdoc />
    public VoxelChangeSet CreateChangeSet(VoxelDocument document)
    {
        VoxelDimensions dimensions = document.Storage.Dimensions;
        foreach (VoxelCoordinate coordinate in _coordinates)
        {
            EnsureInBounds(dimensions, coordinate);
            if (document.Storage.TryGetCell(coordinate, out _))
            {
                throw new InvalidOperationException($"Voxel {coordinate} is already occupied.");
            }
        }

        return new VoxelChangeSet(
            Description,
            dimensions,
            dimensions,
            _coordinates.Select(coordinate =>
                new VoxelChange(coordinate, null, VoxelCell.CreateUniform(_color))));
    }

    internal static VoxelCoordinate[] DistinctCoordinates(IEnumerable<VoxelCoordinate> coordinates) =>
        coordinates?.Distinct().ToArray() ?? throw new ArgumentNullException(nameof(coordinates));

    internal static void EnsureInBounds(VoxelDimensions dimensions, VoxelCoordinate coordinate)
    {
        if (!EditableVoxelStorage.Contains(dimensions, coordinate))
        {
            throw new InvalidOperationException($"Voxel {coordinate} is outside the document bounds.");
        }
    }
}

/// <summary>Removes occupied voxels as one edit.</summary>
public sealed class EraseVoxelsCommand : IVoxelEditCommand
{
    private readonly VoxelCoordinate[] _coordinates;

    /// <summary>Initializes a batched erase command.</summary>
    public EraseVoxelsCommand(IEnumerable<VoxelCoordinate> coordinates) =>
        _coordinates = AddVoxelsCommand.DistinctCoordinates(coordinates);

    /// <inheritdoc />
    public string Description => "Erase voxels";

    /// <inheritdoc />
    public VoxelChangeSet CreateChangeSet(VoxelDocument document)
    {
        VoxelDimensions dimensions = document.Storage.Dimensions;
        List<VoxelChange> changes = [];
        foreach (VoxelCoordinate coordinate in _coordinates)
        {
            AddVoxelsCommand.EnsureInBounds(dimensions, coordinate);
            if (document.Storage.TryGetCell(coordinate, out VoxelCell? cell))
            {
                changes.Add(new VoxelChange(coordinate, cell, null));
            }
        }

        return new VoxelChangeSet(Description, dimensions, dimensions, changes);
    }
}

/// <summary>Changes one face or all faces of occupied voxels.</summary>
public sealed class PaintVoxelsCommand : IVoxelEditCommand
{
    private readonly VoxelCoordinate[] _coordinates;
    private readonly VoxelFace _face;
    private readonly Rgba32Color _color;
    private readonly bool _paintAllFaces;

    /// <summary>Initializes a batched face-paint command.</summary>
    public PaintVoxelsCommand(
        IEnumerable<VoxelCoordinate> coordinates,
        VoxelFace face,
        Rgba32Color color,
        bool paintAllFaces = false)
    {
        _coordinates = AddVoxelsCommand.DistinctCoordinates(coordinates);
        _face = face;
        _color = color;
        _paintAllFaces = paintAllFaces;
    }

    /// <inheritdoc />
    public string Description => _paintAllFaces ? "Paint voxel" : $"Paint {_face} face";

    /// <inheritdoc />
    public VoxelChangeSet CreateChangeSet(VoxelDocument document)
    {
        VoxelDimensions dimensions = document.Storage.Dimensions;
        List<VoxelChange> changes = [];
        foreach (VoxelCoordinate coordinate in _coordinates)
        {
            AddVoxelsCommand.EnsureInBounds(dimensions, coordinate);
            if (!document.Storage.TryGetCell(coordinate, out VoxelCell? cell) || cell is null)
            {
                continue;
            }

            VoxelCell painted = _paintAllFaces
                ? cell.WithAllFaceColors(_color)
                : cell.WithFaceColor(_face, _color);
            if (!painted.Equals(cell))
            {
                changes.Add(new VoxelChange(coordinate, cell, painted));
            }
        }

        return new VoxelChangeSet(Description, dimensions, dimensions, changes);
    }
}

/// <summary>Moves all occupied voxels inside an axis-aligned selection.</summary>
public sealed class MoveVoxelSelectionCommand : IVoxelEditCommand
{
    private readonly VoxelSelectionBox _selection;
    private readonly VoxelCoordinate _delta;

    /// <summary>Initializes a selection move command.</summary>
    public MoveVoxelSelectionCommand(VoxelSelectionBox selection, VoxelCoordinate delta)
    {
        _selection = selection;
        _delta = delta;
    }

    /// <inheritdoc />
    public string Description => "Move voxel selection";

    /// <inheritdoc />
    public VoxelChangeSet CreateChangeSet(VoxelDocument document)
    {
        VoxelDimensions dimensions = document.Storage.Dimensions;
        Dictionary<VoxelCoordinate, VoxelCell> sources = document.Storage.GetOccupiedCells()
            .Where(entry => _selection.Contains(entry.Coordinate))
            .ToDictionary(entry => entry.Coordinate, entry => entry.Cell);
        if (sources.Count == 0 || _delta == default)
        {
            return new VoxelChangeSet(Description, dimensions, dimensions, []);
        }

        Dictionary<VoxelCoordinate, VoxelCell> destinations = [];
        foreach ((VoxelCoordinate source, VoxelCell cell) in sources)
        {
            VoxelCoordinate destination = new(
                checked(source.X + _delta.X),
                checked(source.Y + _delta.Y),
                checked(source.Z + _delta.Z));
            AddVoxelsCommand.EnsureInBounds(dimensions, destination);
            if (document.Storage.TryGetCell(destination, out _) && !sources.ContainsKey(destination))
            {
                throw new InvalidOperationException($"Voxel {destination} blocks the selection move.");
            }

            destinations.Add(destination, cell);
        }

        HashSet<VoxelCoordinate> affected = [.. sources.Keys, .. destinations.Keys];
        List<VoxelChange> changes = [];
        foreach (VoxelCoordinate coordinate in affected.OrderBy(item => item.X).ThenBy(item => item.Y).ThenBy(item => item.Z))
        {
            sources.TryGetValue(coordinate, out VoxelCell? sourceCell);
            destinations.TryGetValue(coordinate, out VoxelCell? destinationCell);
            VoxelCell? before = document.Storage.TryGetCell(coordinate, out VoxelCell? existing) ? existing : null;
            VoxelCell? after = destinationCell ?? (sourceCell is not null ? null : before);
            if (!Equals(before, after))
            {
                changes.Add(new VoxelChange(coordinate, before, after));
            }
        }

        return new VoxelChangeSet(Description, dimensions, dimensions, changes);
    }
}

/// <summary>Changes document dimensions while optionally recording clipped voxels.</summary>
public sealed class ResizeVoxelVolumeCommand : IVoxelEditCommand
{
    private readonly VoxelDimensions _dimensions;
    private readonly bool _allowClipping;

    /// <summary>Initializes an origin-anchored volume resize.</summary>
    public ResizeVoxelVolumeCommand(VoxelDimensions dimensions, bool allowClipping)
    {
        EditableVoxelStorage.ValidateDimensions(dimensions);
        long candidateCount = checked((long)dimensions.Width * dimensions.Height * dimensions.Depth);
        if (candidateCount > 1_048_576)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dimensions),
                $"Voxel volume {candidateCount:N0} exceeds the 1,048,576-cell limit.");
        }

        _dimensions = dimensions;
        _allowClipping = allowClipping;
    }

    /// <inheritdoc />
    public string Description => "Resize voxel volume";

    /// <inheritdoc />
    public VoxelChangeSet CreateChangeSet(VoxelDocument document)
    {
        VoxelDimensions before = document.Storage.Dimensions;
        VoxelEntry[] clipped = document.Storage.GetOccupiedCells()
            .Where(entry => !EditableVoxelStorage.Contains(_dimensions, entry.Coordinate))
            .ToArray();
        if (clipped.Length > 0 && !_allowClipping)
        {
            throw new InvalidOperationException($"Resizing would remove {clipped.Length} occupied voxels.");
        }

        return new VoxelChangeSet(
            Description,
            before,
            _dimensions,
            clipped.Select(entry => new VoxelChange(entry.Coordinate, entry.Cell, null)));
    }
}
