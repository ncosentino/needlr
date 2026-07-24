namespace RecordConstructorOverloadExample;

/// <summary>
/// Identifies the exact prepared byte range available to a continuation request.
/// </summary>
/// <param name="Offset">The starting byte offset.</param>
/// <param name="Length">The prepared byte count.</param>
public sealed record PreparedAccessScope(
    long Offset,
    long Length);
