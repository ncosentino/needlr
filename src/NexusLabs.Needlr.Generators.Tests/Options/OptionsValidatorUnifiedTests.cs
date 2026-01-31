// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests.Options;

/// <summary>
/// Tests for the unified validator design.
/// Convention-based discovery, ValidateMethod, Validator property, ValidationError.
/// </summary>
public sealed class OptionsValidatorUnifiedTests
{
    [Fact]
    public void Generator_ConventionValidateMethod_DiscoveredAutomatically()
    {
        // Convention: method named "Validate" returning IEnumerable<ValidationError>
        // No [OptionsValidator] attribute needed!
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true)]
                public class ApiOptions
                {
                    public string Key { get; set; } = "";
                    
                    // Convention: named "Validate", no attribute needed
                    public IEnumerable<ValidationError> Validate()
                    {
                        if (string.IsNullOrEmpty(Key))
                            yield return "Key is required";
                    }
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should generate validator that calls the Validate method
        Assert.Contains("IValidateOptions<global::TestApp.ApiOptions>", generated);
        Assert.Contains("ApiOptionsValidator", generated);
        Assert.Contains("options.Validate()", generated);
    }

    [Fact]
    public void Generator_NoValidateMethod_NoValidatorGenerated()
    {
        // If no Validate() method exists, don't generate a custom validator
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true)]
                public class SimpleOptions
                {
                    public string Value { get; set; } = "";
                    // No Validate() method
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should still have ValidateOnStart chain but no custom validator class
        Assert.Contains("ValidateOnStart()", generated);
        Assert.DoesNotContain("SimpleOptionsValidator", generated);
    }    [Fact]
    public void Generator_ValidateMethodProperty_UsesCustomMethodName()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true, ValidateMethod = "CheckConfiguration")]
                public class ConfigOptions
                {
                    public string Setting { get; set; } = "";
                    
                    public IEnumerable<ValidationError> CheckConfiguration()
                    {
                        if (string.IsNullOrEmpty(Setting))
                            yield return "Setting is required";
                    }
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should call the custom method name
        Assert.Contains("options.CheckConfiguration()", generated);
    }

    [Fact]
    public void Generator_ValidateMethodWithNameof_Works()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true, ValidateMethod = nameof(DbOptions.ValidateConnection))]
                public class DbOptions
                {
                    public string ConnectionString { get; set; } = "";
                    
                    public IEnumerable<ValidationError> ValidateConnection()
                    {
                        if (string.IsNullOrEmpty(ConnectionString))
                            yield return "ConnectionString is required";
                    }
                }
            }
            """;

        var generated = RunGenerator(source);

        Assert.Contains("options.ValidateConnection()", generated);
    }    [Fact]
    public void Generator_ValidationErrorImplicitString_Works()
    {
        // The yield return "string" should work via implicit conversion
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true)]
                public class TestOptions
                {
                    public string Name { get; set; } = "";
                    
                    public IEnumerable<ValidationError> Validate()
                    {
                        if (string.IsNullOrEmpty(Name))
                            yield return "Name is required";  // Implicit conversion
                    }
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should compile and generate validator
        Assert.Contains("TestOptionsValidator", generated);
    }    [Fact]
    public void Generator_ExternalValidator_WithIOptionsValidator_GeneratesAdapter()
    {
        // External validator using IOptionsValidator<T>
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true, Validator = typeof(PaymentOptionsValidator))]
                public class PaymentOptions
                {
                    public string MerchantId { get; set; } = "";
                }
                
                public class PaymentOptionsValidator : IOptionsValidator<PaymentOptions>
                {
                    public IEnumerable<ValidationError> Validate(PaymentOptions options)
                    {
                        if (string.IsNullOrEmpty(options.MerchantId))
                            yield return "MerchantId is required";
                    }
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should generate adapter that uses the external validator
        Assert.Contains("PaymentOptionsValidator", generated);
        Assert.Contains("IValidateOptions<global::TestApp.PaymentOptions>", generated);
        // External validator needs DI injection (not static)
        Assert.Contains("_validator.Validate(options)", generated);
    }

    [Fact]
    public void Generator_ExternalValidator_WithCustomMethodName_UsesSpecifiedMethod()
    {
        // External validator with custom method name
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true, Validator = typeof(OrderValidator), ValidateMethod = "CheckOrder")]
                public class OrderOptions
                {
                    public decimal Amount { get; set; }
                }
                
                public class OrderValidator
                {
                    public IEnumerable<ValidationError> CheckOrder(OrderOptions options)
                    {
                        if (options.Amount <= 0)
                            yield return "Amount must be positive";
                    }
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should call the custom method name on the external validator
        Assert.Contains("_validator.CheckOrder(options)", generated);
    }

    [Fact]
    public void Generator_ExternalValidator_StaticMethod_NoInjection()
    {
        // External validator with static method - no DI needed
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true, Validator = typeof(InventoryValidator))]
                public class InventoryOptions
                {
                    public int StockLevel { get; set; }
                }
                
                public static class InventoryValidator
                {
                    public static IEnumerable<ValidationError> Validate(InventoryOptions options)
                    {
                        if (options.StockLevel < 0)
                            yield return "Stock level cannot be negative";
                    }
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should call static method directly without injection
        Assert.Contains("InventoryValidator.Validate(options)", generated);
        // Should NOT have constructor with validator parameter
        Assert.DoesNotContain("_validator", generated);
    }

    [Fact]
    public void Generator_ExternalValidator_Registered_ForInstanceMethods()
    {
        // When external validator has instance methods, it must be registered in DI
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true, Validator = typeof(ShippingValidator))]
                public class ShippingOptions
                {
                    public string Carrier { get; set; } = "";
                }
                
                public class ShippingValidator
                {
                    public IEnumerable<ValidationError> Validate(ShippingOptions options)
                    {
                        if (string.IsNullOrEmpty(options.Carrier))
                            yield return "Carrier is required";
                    }
                }
            }
            """;

        var generated = RunGenerator(source);

        // External validator should be registered in DI
        Assert.Contains("AddSingleton<global::TestApp.ShippingValidator>()", generated);
    }

    private static string RunGenerator(string source)
    {
        return GeneratorTestRunner.ForOptions()
            .WithSource(source)
            .RunTypeRegistryGenerator();
    }
}
