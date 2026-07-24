using RecordConstructorOverloadExample;

Console.WriteLine("======================================================================");
Console.WriteLine(" Needlr generated positional-record constructor-overload example");
Console.WriteLine("======================================================================");
Console.WriteLine();
Console.WriteLine("The authored record keeps its seven-parameter primary constructor.");
Console.WriteLine("[RecordConstructorOverloadParameter] adds one guarded forwarding");
Console.WriteLine("constructor containing the marked PreparedScope property.");
Console.WriteLine();

var primaryRequest = new PreparedContinuationRequest(
    "evidence.bin",
    "inspect",
    1,
    ["SequentialRead"],
    ["SequentialRead"],
    [],
    0);
Console.WriteLine("-- PRIMARY CONSTRUCTOR --");
Console.WriteLine($"PreparedScope is null: {primaryRequest.PreparedScope is null}");
Console.WriteLine();

var preparedScope = new PreparedAccessScope(4096, 8192);
var preparedRequest = new PreparedContinuationRequest(
    "evidence.bin",
    "inspect",
    2,
    ["SequentialRead"],
    ["SequentialRead", "PositionedRead"],
    ["BoundedSpool"],
    preparedScope.Length,
    preparedScope);
Console.WriteLine("-- GENERATED FORWARDING OVERLOAD --");
Console.WriteLine(
    $"PreparedScope: offset={preparedRequest.PreparedScope?.Offset}, " +
    $"length={preparedRequest.PreparedScope?.Length}");
Console.WriteLine();

Console.WriteLine("-- BUILT-IN GUARD --");
try
{
    _ = new PreparedContinuationRequest(
        "evidence.bin",
        "inspect",
        3,
        ["SequentialRead"],
        ["SequentialRead", "PositionedRead"],
        ["BoundedSpool"],
        8192,
        null!);
    Console.WriteLine("UNEXPECTED: null PreparedScope was accepted");
}
catch (ArgumentNullException exception)
{
    Console.WriteLine(
        $"ArgumentNullException.ParamName = \"{exception.ParamName}\"");
}
Console.WriteLine();

Console.WriteLine("-- RECORD SEMANTICS REMAIN UNCHANGED --");
var clearedRequest = preparedRequest with { PreparedScope = null };
Console.WriteLine(
    $"with {{ PreparedScope = null }} remains legal: " +
    $"{clearedRequest.PreparedScope is null}");
Console.WriteLine("The guard belongs only to the generated constructor path.");
Console.WriteLine();

Console.WriteLine("Inspect the generated source under:");
Console.WriteLine(
    "obj\\Generated\\NexusLabs.Needlr.Generators\\" +
    "NexusLabs.Needlr.Generators.RecordConstructorOverloadGenerator\\");
