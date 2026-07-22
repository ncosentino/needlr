using GeneratedConstructorExample;
using GeneratedConstructorExample.Generated;

using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

Console.WriteLine("===============================================================================");
Console.WriteLine("  Needlr [GenerateConstructor] / [ConstructorGuard] generated-constructor example  ");
Console.WriteLine("===============================================================================");
Console.WriteLine();
Console.WriteLine("Every service below is an ordinary partial class with private readonly fields");
Console.WriteLine("and no hand-written constructor. Needlr's source generator emits the");
Console.WriteLine("constructor, guard clauses, and assignments. Run with EmitCompilerGeneratedFiles");
Console.WriteLine("(already enabled) and inspect");
Console.WriteLine("obj\\Generated\\NexusLabs.Needlr.Generators\\NexusLabs.Needlr.Generators.GeneratedConstructorGenerator\\*.GeneratedConstructor.g.cs");
Console.WriteLine("to see the exact emitted source. Every guard call below is a direct, compile-time");
Console.WriteLine("resolved static method call. There is no reflection anywhere in this feature.");
Console.WriteLine();

var serviceProvider = new Syringe()
    .UsingSourceGen()
    .BuildServiceProvider();

Console.WriteLine("-- DEMO 1: bare [GenerateConstructor] - no guards --");
var plain = new PlainUserService(new UserRepository());
Console.WriteLine($"  PlainUserService(UserRepository) -> Repository = {plain.Repository.Describe()}");

var plainWithNull = new PlainUserService(null!);
Console.WriteLine($"  PlainUserService(null) -> allowed, Repository is null: {plainWithNull.Repository is null}");
Console.WriteLine();

Console.WriteLine("-- DEMO 2: class-level NonNullableReferences guard mode, resolved via Syringe --");
var guardedFromDi = serviceProvider.GetRequiredService<GuardedUserService>();
Console.WriteLine($"  serviceProvider.GetRequiredService<GuardedUserService>() -> Repository = {guardedFromDi.Repository.Describe()}");

try
{
    _ = new GuardedUserService(null!);
    Console.WriteLine("  UNEXPECTED: no exception thrown for null repository");
}
catch (ArgumentNullException ex)
{
    Console.WriteLine($"  new GuardedUserService(null) -> ArgumentNullException, ParamName = \"{ex.ParamName}\"");
}
Console.WriteLine();

Console.WriteLine("-- DEMO 3: field-level positive guard implicitly enables generation --");
var tenant = new TenantService(new UserRepository(), "acme");
Console.WriteLine($"  TenantService(repo, \"acme\") -> TenantName = \"{tenant.TenantName}\"");

var tenantWithNullRepo = new TenantService(null!, "acme");
Console.WriteLine($"  TenantService(null, \"acme\") -> allowed (repository has no guard), Repository is null: {tenantWithNullRepo.Repository is null}");

try
{
    _ = new TenantService(new UserRepository(), "   ");
    Console.WriteLine("  UNEXPECTED: no exception thrown for whitespace tenant name");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"  TenantService(repo, \"   \") -> ArgumentException, ParamName = \"{ex.ParamName}\"");
}
Console.WriteLine();

Console.WriteLine("-- DEMO 4: built-in NotNullOrEmpty guard (distinct from NotNullOrWhiteSpace above) --");
var entry = new ProductCatalogEntry("SKU-1");
Console.WriteLine($"  ProductCatalogEntry(\"SKU-1\") -> ProductCode = \"{entry.ProductCode}\"");

try
{
    _ = new ProductCatalogEntry(string.Empty);
    Console.WriteLine("  UNEXPECTED: no exception thrown for empty product code");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"  ProductCatalogEntry(\"\") -> ArgumentException, ParamName = \"{ex.ParamName}\"");
}

try
{
    _ = new ProductCatalogEntry(null!);
    Console.WriteLine("  UNEXPECTED: no exception thrown for null product code");
}
catch (ArgumentNullException ex)
{
    Console.WriteLine($"  ProductCatalogEntry(null) -> ArgumentNullException, ParamName = \"{ex.ParamName}\"");
}
Console.WriteLine();

Console.WriteLine("-- DEMO 5: nullable-reference and value-type fields are never auto-guarded --");
var nullableDemo = new NullableAndValueTypeService(new UserRepository(), null, 0, 30);
Console.WriteLine($"  NullableAndValueTypeService(repo, optionalRepo: null, retryCount: 0, timeout: 30)");
Console.WriteLine($"    OptionalRepository is null : {nullableDemo.OptionalRepository is null} (nullable reference, never auto-guarded)");
Console.WriteLine($"    RetryCount                 : {nullableDemo.RetryCount} (value type, never auto-guarded, 0 allowed)");
Console.WriteLine($"    OptionalTimeoutSeconds      : {nullableDemo.OptionalTimeoutSeconds} (Nullable<int>, guarded explicitly with NotNull)");

try
{
    _ = new NullableAndValueTypeService(new UserRepository(), null, 0, null);
    Console.WriteLine("  UNEXPECTED: no exception thrown for null optional timeout");
}
catch (ArgumentNullException ex)
{
    Console.WriteLine($"  NullableAndValueTypeService(..., optionalTimeoutSeconds: null) -> ArgumentNullException, ParamName = \"{ex.ParamName}\"");
    Console.WriteLine("    (NotNull applies to Nullable<T> value types too, not only reference types)");
}

try
{
    _ = new NullableAndValueTypeService(null!, null, 0, 30);
    Console.WriteLine("  UNEXPECTED: no exception thrown for null repository");
}
catch (ArgumentNullException ex)
{
    Console.WriteLine($"  NullableAndValueTypeService(null, ...) -> ArgumentNullException, ParamName = \"{ex.ParamName}\"");
    Console.WriteLine("    (the class-level NonNullableReferences default still guards the non-nullable repository field)");
}
Console.WriteLine();

Console.WriteLine("-- DEMO 6: a direct custom guard type, [ConstructorGuard(typeof(CollectionNotEmptyGuard))] --");
var order = new OrderService(new[] { "order-1", "order-2" });
Console.WriteLine($"  OrderService([\"order-1\", \"order-2\"]) -> Orders.Count = {order.Orders.Count}");

try
{
    _ = new OrderService(Array.Empty<string>());
    Console.WriteLine("  UNEXPECTED: no exception thrown for empty orders");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"  OrderService([]) -> ArgumentException from CollectionNotEmptyGuard.Validate, ParamName = \"{ex.ParamName}\"");
}
Console.WriteLine();

Console.WriteLine("-- DEMO 7: an explicit method selector, [ConstructorGuard(typeof(NumberGuards), nameof(NumberGuards.ValidatePositive))] --");
var retryPolicy = new RetryPolicy(3);
Console.WriteLine($"  RetryPolicy(3) -> RetryCount = {retryPolicy.RetryCount}");

try
{
    _ = new RetryPolicy(-1);
    Console.WriteLine("  UNEXPECTED: no exception thrown for a non-positive retry count");
}
catch (ArgumentOutOfRangeException ex)
{
    Console.WriteLine($"  RetryPolicy(-1) -> ArgumentOutOfRangeException from NumberGuards.ValidatePositive, ParamName = \"{ex.ParamName}\"");
}
Console.WriteLine();

Console.WriteLine("-- DEMO 8: an application-defined alias attribute, [CollectionNotEmpty] --");
var aliasOrder = new AliasOrderService(new[] { "order-9" });
Console.WriteLine($"  AliasOrderService([\"order-9\"]) -> Orders.Count = {aliasOrder.Orders.Count}");

try
{
    _ = new AliasOrderService(Array.Empty<string>());
    Console.WriteLine("  UNEXPECTED: no exception thrown for empty orders");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"  AliasOrderService([]) -> ArgumentException from the same CollectionNotEmptyGuard, ParamName = \"{ex.ParamName}\"");
    Console.WriteLine("    ([CollectionNotEmpty] and [ConstructorGuard(typeof(CollectionNotEmptyGuard))] normalize identically)");
}
Console.WriteLine();

Console.WriteLine("-- DEMO 9: [GenerateFactory] + [GenerateConstructor] interoperability --");
var reportFactory = serviceProvider.GetRequiredService<IReportBuilderFactory>();
var report = reportFactory.Create("acme-template");
Console.WriteLine($"  IReportBuilderFactory.Create(\"acme-template\") -> {report.Build()}");

var reportViaFunc = serviceProvider.GetRequiredService<Func<string, ReportBuilder>>();
var reportFromFunc = reportViaFunc("acme-func-template");
Console.WriteLine($"  Func<string, ReportBuilder>(\"acme-func-template\") -> {reportFromFunc.Build()}");

var concreteReportBuilder = serviceProvider.GetService<ReportBuilder>();
Console.WriteLine($"  serviceProvider.GetService<ReportBuilder>() is null: {concreteReportBuilder is null}");
Console.WriteLine("    (ReportBuilder has a non-injectable runtime parameter, so it is never auto-registered directly;");
Console.WriteLine("     only its generated factory and Func<> delegate are registered)");
Console.WriteLine();

Console.WriteLine("-- DEMO 10: a parameterized custom guard alias, [MinCount(3)] --");
var bulkOrder = new BulkOrderRequest(new[] { "sku-1", "sku-2", "sku-3" });
Console.WriteLine($"  BulkOrderRequest([\"sku-1\", \"sku-2\", \"sku-3\"]) -> LineItems.Count = {bulkOrder.LineItems.Count}");

try
{
    _ = new BulkOrderRequest(new[] { "sku-1", "sku-2" });
    Console.WriteLine("  UNEXPECTED: no exception thrown for only 2 line items");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"  BulkOrderRequest([\"sku-1\", \"sku-2\"]) -> ArgumentException from MinCountGuard.Validate, ParamName = \"{ex.ParamName}\"");
    Console.WriteLine("    (the alias usage's own \"3\" argument was forwarded onto the guard call: Validate(lineItems, 3, nameof(lineItems)))");
}
Console.WriteLine();

Console.WriteLine("===============================================================================");
Console.WriteLine("Done. See docs/generated-constructors.md for the full feature reference and");
Console.WriteLine("docs/analyzers/NDLRGEN039.md through NDLRGEN056.md and NDLRSIG003.md for every");
Console.WriteLine("diagnostic this feature can raise.");
Console.WriteLine("===============================================================================");
