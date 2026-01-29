using System.Collections.Generic;

using FluentValidation;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.FluentValidation.IntegrationTests;

public sealed class FluentValidationIntegrationTests
{
    [Fact]
    public void FluentValidation_Validator_IsRegistered_Automatically()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payment:MerchantId"] = "merch_123",
                ["Payment:ApiKey"] = "sk_test_abc123",
                ["Payment:MaxRetries"] = "5"
            })
            .Build();

        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider(configuration);

        var validator = provider.GetService<IValidator<PaymentOptions>>();
        Assert.NotNull(validator);
        Assert.IsType<PaymentOptionsValidator>(validator);
    }

    [Fact]
    public void FluentValidation_ValidOptions_PassesValidation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payment:MerchantId"] = "merch_123",
                ["Payment:ApiKey"] = "sk_test_abc123",
                ["Payment:MaxRetries"] = "5"
            })
            .Build();

        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider(configuration);

        var options = provider.GetRequiredService<IOptions<PaymentOptions>>();
        Assert.NotNull(options.Value);
        Assert.Equal("merch_123", options.Value.MerchantId);
        Assert.Equal("sk_test_abc123", options.Value.ApiKey);
        Assert.Equal(5, options.Value.MaxRetries);
    }

    [Fact]
    public void FluentValidation_InvalidOptions_ThrowsOnAccess()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payment:MerchantId"] = "",
                ["Payment:ApiKey"] = "invalid_key",
                ["Payment:MaxRetries"] = "50"
            })
            .Build();

        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider(configuration);

        var options = provider.GetRequiredService<IOptions<PaymentOptions>>();

        var ex = Assert.Throws<OptionsValidationException>(() => options.Value);
        Assert.Contains("Merchant ID is required", ex.Message);
    }

    [Fact]
    public void FluentValidation_IValidateOptions_IsRegistered()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payment:MerchantId"] = "merch_123",
                ["Payment:ApiKey"] = "sk_test_abc123"
            })
            .Build();

        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider(configuration);

        var validateOptions = provider.GetService<IValidateOptions<PaymentOptions>>();
        Assert.NotNull(validateOptions);
    }

    [Fact]
    public void FluentValidation_NoManualWiring_Required()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payment:MerchantId"] = "test_merchant",
                ["Payment:ApiKey"] = "pk_live_xyz789",
                ["Payment:MaxRetries"] = "3"
            })
            .Build();

        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider(configuration);

        var options = provider.GetRequiredService<IOptions<PaymentOptions>>();

        Assert.Equal("test_merchant", options.Value.MerchantId);
        Assert.Equal("pk_live_xyz789", options.Value.ApiKey);
        Assert.Equal(3, options.Value.MaxRetries);
    }
}
