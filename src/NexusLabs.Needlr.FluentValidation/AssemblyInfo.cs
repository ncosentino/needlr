using NexusLabs.Needlr.Generators;

// Register FluentValidation's AbstractValidator<T> as a recognized validator base type.
// This allows the Needlr analyzer to skip NDLRGEN014 for FluentValidation validators.
[assembly: ValidatorProvider("FluentValidation.AbstractValidator`1")]
