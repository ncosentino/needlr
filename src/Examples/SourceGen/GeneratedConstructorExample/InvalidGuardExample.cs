// This file intentionally contains no compiled code. It documents an invalid
// generated-constructor declaration and links it to the analyzer that catches
// it, so a reader browsing this example also sees a compile-time failure mode.
//
// [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)] only applies to
// string-compatible fields. Applying it to an "int" field is invalid and is
// reported at compile time by NDLRGEN048 -- see
// docs/analyzers/NDLRGEN048.md for the exact diagnostic and how to fix it:
//
// [GenerateConstructor]
// public partial class InvalidGuardExample
// {
//     [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)] // NDLRGEN048
//     private readonly int _retryCount;
// }
