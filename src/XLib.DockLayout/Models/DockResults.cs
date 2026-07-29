namespace XLib.DockLayout.Models;

/// <summary>Describes a predictable DockLayout operation error.</summary>
public sealed record DockLayoutError
{
    /// <summary>Initializes a DockLayout operation error.</summary>
    public DockLayoutError(string code, string message)
    {
        Code = code;
        Message = message;
    }

    /// <summary>Gets the stable machine-readable error code.</summary>
    public string Code { get; }

    /// <summary>Gets the human-readable error message.</summary>
    public string Message { get; }
}

/// <summary>Represents a DockLayout operation result without a value.</summary>
public readonly record struct DockLayoutResult
{
    private DockLayoutResult(bool success, DockLayoutError? error)
    {
        Success = success;
        Error = error;
    }

    /// <summary>Gets whether the operation succeeded.</summary>
    public bool Success { get; }

    /// <summary>Gets the predictable operation error when unsuccessful.</summary>
    public DockLayoutError? Error { get; }

    /// <summary>Creates a successful result.</summary>
    public static DockLayoutResult Ok() => new(true, null);

    /// <summary>Creates a failed result.</summary>
    public static DockLayoutResult Fail(string code, string message) => new(false, new DockLayoutError(code, message));
}

/// <summary>Represents a DockLayout operation result with a value.</summary>
public readonly record struct DockLayoutResult<T>
{
    private DockLayoutResult(bool success, T? value, DockLayoutError? error)
    {
        Success = success;
        Value = value;
        Error = error;
    }

    /// <summary>Gets whether the operation succeeded.</summary>
    public bool Success { get; }

    /// <summary>Gets the operation value when successful.</summary>
    public T? Value { get; }

    /// <summary>Gets the predictable operation error when unsuccessful.</summary>
    public DockLayoutError? Error { get; }

    /// <summary>Creates a successful result.</summary>
    public static DockLayoutResult<T> Ok(T value) => new(true, value, null);

    /// <summary>Creates a failed result.</summary>
    public static DockLayoutResult<T> Fail(string code, string message) => new(false, default, new DockLayoutError(code, message));
}

/// <summary>Reports the result of an XML layout restore.</summary>
public sealed record LayoutRestoreReport
{
    /// <summary>Gets the imported layout version.</summary>
    public required string Version { get; init; }
    /// <summary>Gets the number of restored panels.</summary>
    public int PanelCount { get; init; }
    /// <summary>Gets the number of restored views.</summary>
    public int ViewCount { get; init; }
    /// <summary>Gets the number of restored tabs.</summary>
    public int TabCount { get; init; }
    /// <summary>Gets ID remapping performed during restore.</summary>
    public IReadOnlyDictionary<string, string> IdMappings { get; init; } = new Dictionary<string, string>();
}
