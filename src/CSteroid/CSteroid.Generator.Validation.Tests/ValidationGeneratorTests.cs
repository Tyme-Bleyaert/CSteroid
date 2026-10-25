using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using CSteroid.Generator.Testing;
using CSteroid.Generator.Generators;
using CSteroid.Generator.Mapping;
using CSteroid.Generator.Validation;
using Xunit;

namespace CSteroid.Tests
{
    public class ValidationGeneratorTests
    {
        private static readonly IIncrementalGenerator[] Generators = { new CSteroidGenerator(), new CSteroidMappingGenerator(), new CSteroidValidationGenerator() };

        private static Compilation RunGenerator(params string[] sources)
        {
            return GeneratorTestHarness.RunGenerator(Generators, sources);
        }

        [Fact]
        public void CustomValidation_GeneratesValidatorClass()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", ValidationType: ValidationType.Custom)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
}
");

            var files = GeneratorTestHarness.GetGeneratedFileNames(compilation);
            Assert.Contains("UserValidator.g.cs", files);

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "UserValidator.g.cs");
            Assert.Contains("internal partial class UserValidator : global::CSteroid.IValidator<User>", content);
            Assert.Contains("static partial void ConfigureValidation", content);
            Assert.Contains("public ValidationResult Validate(User model)", content);
            Assert.Contains("private static void BuildDefaultRules", content);
            Assert.Contains("public Task<ValidationResult> ValidateAsync(User model)", content);
        }

        [Fact]
        public void CustomValidation_SeedsDefaultRulesFromDataAnnotations()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using System.ComponentModel.DataAnnotations;

namespace System.ComponentModel.DataAnnotations
{
    public class RequiredAttribute : Attribute { }
    public class MaxLengthAttribute : Attribute
    {
        public MaxLengthAttribute(int length) { }
        public string ErrorMessage { get; set; }
    }
    public class RangeAttribute : Attribute
    {
        public RangeAttribute(int minimum, int maximum) { }
    }
}

[CanonicalModelAttribute(""User"", ValidationType: ValidationType.Custom)]
public interface IUserModelDefinition
{
    [Required]
    public string Name { get; set; }

    [MaxLength(50, ErrorMessage = ""Too long."")]
    public string Code { get; set; }

    [Range(0, 150)]
    public int Age { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "UserValidator.g.cs");
            Assert.Contains("rules.For(x => x.Name).NotNull().NotEmpty();", content);
            Assert.Contains("rules.For(x => x.Code).MaxLength(50).WithMessage(\"Too long.\");", content);
            Assert.Contains("rules.For(x => x.Age).InclusiveBetween(0, 150);", content);
        }

        [Fact]
        public void CustomValidation_ExcludedProperty_GeneratesNoRule()
        {
            // Excluded properties are dropped from info.Properties by ModelInfoFactory, so the
            // default-rule builder (which iterates info.Properties) never sees them.
            var compilation = RunGenerator(@"
using CSteroid;
using System.ComponentModel.DataAnnotations;

namespace System.ComponentModel.DataAnnotations
{
    public class RequiredAttribute : Attribute { }
}

[CanonicalModelAttribute(""User"", ValidationType: ValidationType.Custom, ExcludedProperties = new string[] { ""Secret"" })]
public interface IUserModelDefinition
{
    [Required]
    public string Name { get; set; }

    [Required]
    public string Secret { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "UserValidator.g.cs");
            Assert.Contains("rules.For(x => x.Name).NotNull().NotEmpty();", content);
            Assert.DoesNotContain("rules.For(x => x.Secret)", content);
        }

        [Fact]
        public void ValidationResult_WarningSeverityFailure_KeepsModelValid()
        {
            var compilation = GeneratorTestHarness.RunGenerator(new IIncrementalGenerator[] { new CSteroidGenerator(), new CSteroidValidationGenerator() }, @"
using CSteroid;
using System.ComponentModel.DataAnnotations;

namespace System.ComponentModel.DataAnnotations
{
    public class RequiredAttribute : Attribute { }
}

[CanonicalModelAttribute(""User"", ValidationType: ValidationType.Custom)]
public interface IUserModelDefinition
{
    public string Name { get; set; }
}

namespace CSteroid.Generated.Validation
{
    using CSteroid;
    using CSteroid.Generated.Models;

    internal partial class UserValidator
    {
        static partial void ConfigureValidation(ValidationConfiguration<User> config)
        {
            config.AddRules(rules =>
            {
                rules.For(x => x.Name).NotNull().WithSeverity(ValidationSeverity.Warning);
                rules.For(x => x.Name).Must((name) => !string.IsNullOrEmpty(name), ""Still invalid."").WithSeverity(ValidationSeverity.Error);
            });
        }
    }
}
");

            var assembly = GeneratorTestHarness.EmitAndLoad(compilation);
            var validatorType = assembly.GetType("CSteroid.Generated.Validation.UserValidator");
            var modelType = assembly.GetType("CSteroid.Generated.Models.User");
            Assert.NotNull(validatorType);
            Assert.NotNull(modelType);

            var user = System.Activator.CreateInstance(modelType!);
            modelType!.GetProperty("Name")!.SetValue(user, null);

            var validator = System.Activator.CreateInstance(validatorType!);
            var validateMethod = validatorType!.GetMethod("Validate");
            var result = validateMethod!.Invoke(validator, new[] { user });

            Assert.False(GeneratorTestHarness.IsValid(result!));

            // When only the Warning-severity rule fails, the model stays valid (FluentValidation semantics).
            modelType.GetProperty("Name")!.SetValue(user, "ok");
            var result2 = validateMethod!.Invoke(validator, new[] { user });
            Assert.True(GeneratorTestHarness.IsValid(result2!));
        }

        [Fact]
        public void CustomValidation_UserRules_AreApplied()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", ValidationType: ValidationType.Custom)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}

namespace CSteroid.Generated.Validation
{
    using CSteroid.Generated.Models;

    internal partial class UserValidator
    {
        static partial void ConfigureValidation(ValidationConfiguration<User> config)
        {
            config.AddRules(rules =>
            {
                rules.For(x => x.Name).NotEmpty();
            });
        }
    }
}
");

            var assembly = GeneratorTestHarness.EmitAndLoad(compilation);
            var validatorType = assembly.GetType("CSteroid.Generated.Validation.UserValidator");
            var userType = assembly.GetType("CSteroid.Generated.Models.User");
            Assert.NotNull(validatorType);
            Assert.NotNull(userType);

            var validator = Activator.CreateInstance(validatorType!);
            var validateMethod = validatorType!.GetMethod("Validate");
            Assert.NotNull(validateMethod);

            var user = Activator.CreateInstance(userType!);
            userType!.GetProperty("Name")!.SetValue(user, "");
            var result = validateMethod!.Invoke(validator, new[] { user });
            var isValid = (bool)result!.GetType().GetProperty("IsValid")!.GetValue(result)!;
            Assert.False(isValid);

            userType.GetProperty("Name")!.SetValue(user, "Alice");
            result = validateMethod.Invoke(validator, new[] { user });
            isValid = (bool)result!.GetType().GetProperty("IsValid")!.GetValue(result)!;
            Assert.True(isValid);
        }

        [Fact]
        public void CustomValidation_When_ConditionalRuleOnlyRunsWhenConditionIsTrue()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", ValidationType = ValidationType.Custom)]
public interface IUserModelDefinition
{
    public bool OptedInToEmails { get; set; }
    public string Email { get; set; }
}

namespace CSteroid.Generated.Validation
{
    using CSteroid.Generated.Models;

    internal partial class UserValidator
    {
        static partial void ConfigureValidation(ValidationConfiguration<User> config)
        {
            config.AddRules(rules =>
            {
                rules.For(x => x.Email)
                     .NotEmpty()
                     .When(u => u.OptedInToEmails);
            });
        }
    }
}
");

            var assembly = GeneratorTestHarness.EmitAndLoad(compilation);
            var validatorType = assembly.GetType("CSteroid.Generated.Validation.UserValidator");
            var userType = assembly.GetType("CSteroid.Generated.Models.User");
            Assert.NotNull(validatorType);
            Assert.NotNull(userType);

            var validator = Activator.CreateInstance(validatorType!);
            var validateMethod = validatorType!.GetMethod("Validate");
            Assert.NotNull(validateMethod);

            var user = Activator.CreateInstance(userType!);
            userType!.GetProperty("OptedInToEmails")!.SetValue(user, false);
            userType.GetProperty("Email")!.SetValue(user, "");
            var result = validateMethod!.Invoke(validator, new[] { user });
            var isValid = (bool)result!.GetType().GetProperty("IsValid")!.GetValue(result)!;
            Assert.True(isValid);

            userType.GetProperty("OptedInToEmails")!.SetValue(user, true);
            result = validateMethod.Invoke(validator, new[] { user });
            isValid = (bool)result!.GetType().GetProperty("IsValid")!.GetValue(result)!;
            Assert.False(isValid);

            userType.GetProperty("Email")!.SetValue(user, "alice@example.com");
            result = validateMethod.Invoke(validator, new[] { user });
            isValid = (bool)result!.GetType().GetProperty("IsValid")!.GetValue(result)!;
            Assert.True(isValid);
        }

        [Fact]
        public void CustomValidation_Unless_ConditionalRuleSkipsWhenConditionIsTrue()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", ValidationType = ValidationType.Custom)]
public interface IUserModelDefinition
{
    public bool IsAnonymous { get; set; }
    public string SocialSecurity { get; set; }
}

namespace CSteroid.Generated.Validation
{
    using CSteroid.Generated.Models;

    internal partial class UserValidator
    {
        static partial void ConfigureValidation(ValidationConfiguration<User> config)
        {
            config.AddRules(rules =>
            {
                rules.For(x => x.SocialSecurity)
                     .NotEmpty()
                     .Unless(u => u.IsAnonymous);
            });
        }
    }
}
");

            var assembly = GeneratorTestHarness.EmitAndLoad(compilation);
            var validatorType = assembly.GetType("CSteroid.Generated.Validation.UserValidator");
            var userType = assembly.GetType("CSteroid.Generated.Models.User");
            Assert.NotNull(validatorType);
            Assert.NotNull(userType);

            var validator = Activator.CreateInstance(validatorType!);
            var validateMethod = validatorType!.GetMethod("Validate");
            Assert.NotNull(validateMethod);

            var user = Activator.CreateInstance(userType!);
            userType!.GetProperty("IsAnonymous")!.SetValue(user, true);
            userType.GetProperty("SocialSecurity")!.SetValue(user, "");
            var result = validateMethod!.Invoke(validator, new[] { user });
            var isValid = (bool)result!.GetType().GetProperty("IsValid")!.GetValue(result)!;
            Assert.True(isValid);

            userType.GetProperty("IsAnonymous")!.SetValue(user, false);
            result = validateMethod.Invoke(validator, new[] { user });
            isValid = (bool)result!.GetType().GetProperty("IsValid")!.GetValue(result)!;
            Assert.False(isValid);

            userType.GetProperty("SocialSecurity")!.SetValue(user, "123-45-6789");
            result = validateMethod.Invoke(validator, new[] { user });
            isValid = (bool)result!.GetType().GetProperty("IsValid")!.GetValue(result)!;
            Assert.True(isValid);
        }

        [Fact]
        public void CustomValidation_ModelLevelMust_CrossPropertyValidation()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using System;

[CanonicalModelAttribute(""User"", ValidationType = ValidationType.Custom)]
public interface IUserModelDefinition
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

namespace CSteroid.Generated.Validation
{
    using CSteroid.Generated.Models;

    internal partial class UserValidator
    {
        static partial void ConfigureValidation(ValidationConfiguration<User> config)
        {
            config.AddRules(rules =>
            {
                rules.Must(
                    u => u.StartDate < u.EndDate,
                    ""Start date must be before end date."",
                    ""DATE_RANGE_INVALID"");
            });
        }
    }
}
");

            var assembly = GeneratorTestHarness.EmitAndLoad(compilation);
            var validatorType = assembly.GetType("CSteroid.Generated.Validation.UserValidator");
            var userType = assembly.GetType("CSteroid.Generated.Models.User");
            Assert.NotNull(validatorType);
            Assert.NotNull(userType);

            var validator = Activator.CreateInstance(validatorType!);
            var validateMethod = validatorType!.GetMethod("Validate");
            Assert.NotNull(validateMethod);

            var user = Activator.CreateInstance(userType!);
            userType!.GetProperty("StartDate")!.SetValue(user, new DateTime(2025, 1, 1));
            userType.GetProperty("EndDate")!.SetValue(user, new DateTime(2025, 1, 2));
            var result = validateMethod!.Invoke(validator, new[] { user });
            var isValid = (bool)result!.GetType().GetProperty("IsValid")!.GetValue(result)!;
            Assert.True(isValid);

            userType.GetProperty("EndDate")!.SetValue(user, new DateTime(2024, 1, 1));
            result = validateMethod.Invoke(validator, new[] { user });
            isValid = (bool)result!.GetType().GetProperty("IsValid")!.GetValue(result)!;
            Assert.False(isValid);

            var failures = result.GetType().GetProperty("Failures")!.GetValue(result) as System.Collections.IEnumerable;
            Assert.NotNull(failures);
            var failureList = new List<object>();
            foreach (var f in failures!) failureList.Add(f);
            Assert.Single(failureList);
            var failure = failureList[0];
            var errorCode = failure.GetType().GetProperty("ErrorCode")!.GetValue(failure) as string;
            Assert.Equal("DATE_RANGE_INVALID", errorCode);
        }

        [Fact]
        public void CustomValidation_CrossPropertyMust_OnPropertyRule()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", ValidationType = ValidationType.Custom)]
public interface IUserModelDefinition
{
    public string Password { get; set; }
    public string ConfirmPassword { get; set; }
}

namespace CSteroid.Generated.Validation
{
    using CSteroid.Generated.Models;

    internal partial class UserValidator
    {
        static partial void ConfigureValidation(ValidationConfiguration<User> config)
        {
            config.AddRules(rules =>
            {
                rules.For(x => x.ConfirmPassword)
                     .Must((model, value) => value == model.Password, ""Passwords must match."");
            });
        }
    }
}
");

            var assembly = GeneratorTestHarness.EmitAndLoad(compilation);
            var validatorType = assembly.GetType("CSteroid.Generated.Validation.UserValidator");
            var userType = assembly.GetType("CSteroid.Generated.Models.User");
            Assert.NotNull(validatorType);
            Assert.NotNull(userType);

            var validator = Activator.CreateInstance(validatorType!);
            var validateMethod = validatorType!.GetMethod("Validate");
            Assert.NotNull(validateMethod);

            var user = Activator.CreateInstance(userType!);
            userType!.GetProperty("Password")!.SetValue(user, "secret123");
            userType.GetProperty("ConfirmPassword")!.SetValue(user, "secret123");
            var result = validateMethod!.Invoke(validator, new[] { user });
            var isValid = (bool)result!.GetType().GetProperty("IsValid")!.GetValue(result)!;
            Assert.True(isValid);

            userType.GetProperty("ConfirmPassword")!.SetValue(user, "different");
            result = validateMethod.Invoke(validator, new[] { user });
            isValid = (bool)result!.GetType().GetProperty("IsValid")!.GetValue(result)!;
            Assert.False(isValid);
        }

        [Fact]
        public async Task AsyncValidation_GeneratesAsyncValidator()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using System.Threading;
using System.Threading.Tasks;

[CanonicalModelAttribute(""User"", ValidationType = ValidationType.Custom | ValidationType.Async)]
public interface IUserModelDefinition
{
    public string Email { get; set; }
}

namespace CSteroid.Generated.Validation
{
    using CSteroid.Generated.Models;

    internal partial class UserValidator
    {
        static partial void ConfigureValidation(ValidationConfiguration<User> config)
        {
            config.AddRules(rules =>
            {
                rules.For(x => x.Email)
                     .MustAsync(async (email, ct) =>
                     {
                         await Task.Delay(1, ct);
                         return !string.IsNullOrEmpty(email) && email.Contains(""@"");
                     }, ""Email is invalid."");
            });
        }
    }
}
");

            var files = GeneratorTestHarness.GetGeneratedFileNames(compilation);
            Assert.Contains("UserValidator.g.cs", files);

            var validator = GeneratorTestHarness.GetGeneratedSource(compilation, "UserValidator.g.cs");
            Assert.Contains("internal partial class UserValidator : global::CSteroid.IAsyncValidator<User>", validator);
            Assert.Contains("public async Task<ValidationResult> ValidateAsync(User model, CancellationToken cancellationToken = default)", validator);
            Assert.Contains("throw new InvalidOperationException(\"UserValidator contains async rules. Call ValidateAsync instead.\")", validator);

            var assembly = GeneratorTestHarness.EmitAndLoad(compilation);
            var validatorType = assembly.GetType("CSteroid.Generated.Validation.UserValidator");
            var userType = assembly.GetType("CSteroid.Generated.Models.User");
            Assert.NotNull(validatorType);
            Assert.NotNull(userType);

            var validatorInstance = Activator.CreateInstance(validatorType!);
            var validateMethod = validatorType!.GetMethod("Validate");
            Assert.NotNull(validateMethod);

            var user = Activator.CreateInstance(userType!);
            userType!.GetProperty("Email")!.SetValue(user, "");
            Assert.Throws<TargetInvocationException>(() => validateMethod!.Invoke(validatorInstance, new[] { user }));

            var validateAsyncMethod = validatorType.GetMethod("ValidateAsync");
            Assert.NotNull(validateAsyncMethod);

            var task = validateAsyncMethod!.Invoke(validatorInstance, new[] { user, CancellationToken.None }) as Task;
            Assert.NotNull(task);
            var result = await GeneratorTestHarness.GetTaskResultAsync(task!);
            Assert.False((bool)result.GetType().GetProperty("IsValid")!.GetValue(result)!);

            userType.GetProperty("Email")!.SetValue(user, "alice@example.com");
            task = validateAsyncMethod.Invoke(validatorInstance, new[] { user, CancellationToken.None }) as Task;
            Assert.NotNull(task);
            result = await GeneratorTestHarness.GetTaskResultAsync(task!);
            Assert.True((bool)result.GetType().GetProperty("IsValid")!.GetValue(result)!);
        }

        [Fact]
        public async Task AsyncValidation_RunsDefaultSyncRulesInAsyncValidator()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace System.ComponentModel.DataAnnotations
{
    public class RequiredAttribute : Attribute { }
    public class MaxLengthAttribute : Attribute
    {
        public MaxLengthAttribute(int length) { }
        public string ErrorMessage { get; set; }
    }
}

[CanonicalModelAttribute(""User"", ValidationType = ValidationType.Custom | ValidationType.Async | ValidationType.DataAnnotations)]
public interface IUserModelDefinition
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; }

    public string Code { get; set; }
}

namespace CSteroid.Generated.Validation
{
    using CSteroid.Generated.Models;

    internal partial class UserValidator
    {
        static partial void ConfigureValidation(ValidationConfiguration<User> config)
        {
            config.AddRules(rules =>
            {
                rules.For(x => x.Code)
                     .MustAsync(async (code, ct) =>
                     {
                         await Task.Delay(1, ct);
                         return code == null || code.StartsWith(""X-"");
                     }, ""Code must start with X-."");
            });
        }
    }
}
");

            var assembly = GeneratorTestHarness.EmitAndLoad(compilation);
            var validatorType = assembly.GetType("CSteroid.Generated.Validation.UserValidator");
            var userType = assembly.GetType("CSteroid.Generated.Models.User");
            Assert.NotNull(validatorType);
            Assert.NotNull(userType);

            var model = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("[Required]", model);
            Assert.Contains("[MaxLength(50)]", model);

            var validatorInstance = Activator.CreateInstance(validatorType!);
            var validateAsyncMethod = validatorType!.GetMethod("ValidateAsync");
            Assert.NotNull(validateAsyncMethod);

            var user = Activator.CreateInstance(userType!);
            userType!.GetProperty("Name")!.SetValue(user, "");
            userType.GetProperty("Code")!.SetValue(user, "bad");
            var task = validateAsyncMethod!.Invoke(validatorInstance, new[] { user, CancellationToken.None }) as Task;
            Assert.NotNull(task);
            var result = await GeneratorTestHarness.GetTaskResultAsync(task!);
            Assert.False((bool)result.GetType().GetProperty("IsValid")!.GetValue(result)!);

            var failures = result.GetType().GetProperty("Failures")!.GetValue(result) as System.Collections.IEnumerable;
            Assert.NotNull(failures);
            var failureList = new List<object>();
            foreach (var f in failures!) failureList.Add(f);
            Assert.Equal(2, failureList.Count);

            userType.GetProperty("Name")!.SetValue(user, "Alice");
            userType.GetProperty("Code")!.SetValue(user, "X-123");
            task = validateAsyncMethod.Invoke(validatorInstance, new[] { user, CancellationToken.None }) as Task;
            Assert.NotNull(task);
            result = await GeneratorTestHarness.GetTaskResultAsync(task!);
            Assert.True((bool)result.GetType().GetProperty("IsValid")!.GetValue(result)!);
        }

        [Fact]
        public void CustomNamespace_ValidatorGeneratedInCustomNamespace()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", CustomNamespace = ""MyApp.Domain.Models"", ValidationType = ValidationType.Custom)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
}
");

            var paths = compilation.SyntaxTrees
                .Select(t => t.FilePath)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            Assert.Contains(paths, p => p.Contains("MyApp/Domain/Models/UserValidator.g.cs") || p.Contains("MyApp\\Domain\\Models\\UserValidator.g.cs"));

            var validator = GeneratorTestHarness.GetGeneratedSource(compilation, "UserValidator.g.cs");
            Assert.Contains("namespace MyApp.Domain.Models", validator);
            Assert.Contains("internal partial class UserValidator : global::CSteroid.IValidator<User>", validator);
            Assert.DoesNotContain("using MyApp.Domain.Models", validator);
        }

        [Fact]
        public void ValidationProject_GeneratesValidatorInConfiguredNamespace()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[assembly: GeneratorConfig(ValidationProject = ""MyApp.Application.Validation"")]

[CanonicalModelAttribute(""User"", ValidationType = ValidationType.Custom)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
}
");

            var paths = compilation.SyntaxTrees
                .Select(t => t.FilePath)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            Assert.Contains(paths, p => p.Contains("MyApp/Application/Validation/UserValidator.g.cs") || p.Contains("MyApp\\Application\\Validation\\UserValidator.g.cs"));

            var validator = GeneratorTestHarness.GetGeneratedSource(compilation, "UserValidator.g.cs");
            Assert.Contains("namespace MyApp.Application.Validation", validator);
            Assert.Contains("using CSteroid.Generated.Models;", validator);
            Assert.Contains("internal partial class UserValidator : global::CSteroid.IValidator<User>", validator);
        }

        [Fact]
        public void ValidationProject_AndMappingProject_Combined()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[assembly: GeneratorConfig(ValidationProject = ""MyApp.Application.Validation"", MappingProject = ""MyApp.Infrastructure.Mappings"")]

[CanonicalModelAttribute(""User"", ValidationType = ValidationType.Custom, MappingTypes = MappingTypes.ExtensionMethod)]
[CanonicalModelAttribute(""UserDto"", MappingTypes = MappingTypes.ExtensionMethod)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            var paths = compilation.SyntaxTrees
                .Select(t => t.FilePath)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            Assert.Contains(paths, p => p.Contains("MyApp/Application/Validation/UserValidator.g.cs") || p.Contains("MyApp\\Application\\Validation\\UserValidator.g.cs"));
            Assert.Contains(paths, p => p.Contains("MyApp/Infrastructure/Mappings/CSteroidMappings.g.cs") || p.Contains("MyApp\\Infrastructure\\Mappings\\CSteroidMappings.g.cs"));

            var validator = GeneratorTestHarness.GetGeneratedSource(compilation, "UserValidator.g.cs");
            Assert.Contains("namespace MyApp.Application.Validation", validator);

            var mappings = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidMappings.g.cs");
            Assert.Contains("namespace MyApp.Infrastructure.Mappings", mappings);
            Assert.Contains("internal static class MappingExtensions", mappings);
        }

        [Fact]
        public void NestedValidation_GeneratedSource_WiresNestedValidators()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using System.Collections.Generic;
using CSteroid.Generated.Models;

[CanonicalModelAttribute(""Address"", ValidationType: ValidationType.Custom)]
public interface IAddressModelDefinition
{
    public string PostCode { get; set; }
}

[CanonicalModelAttribute(""User"", ValidationType: ValidationType.Custom)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public Address Address { get; set; }
    public List<Address> Addresses { get; set; }
}
");

            var validator = GeneratorTestHarness.GetGeneratedSource(compilation, "UserValidator.g.cs");
            Assert.Contains("rules.ForNested(x => x.Address);", validator);
            Assert.Contains("rules.ForEachNested(x => x.Addresses);", validator);
            Assert.Contains("ValidatorRegistry.Register<Address>(new AddressValidator());", validator);
        }

        [Fact]
        public void NestedValidation_NestedFailures_ArePrefixedWithPropertyName()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using CSteroid.Generated.Models;

namespace System.ComponentModel.DataAnnotations
{
    public class RequiredAttribute : Attribute { }
}

[CanonicalModelAttribute(""Address"", ValidationType: ValidationType.Custom)]
public interface IAddressModelDefinition
{
    [System.ComponentModel.DataAnnotations.Required]
    public string PostCode { get; set; }
}

[CanonicalModelAttribute(""User"", ValidationType: ValidationType.Custom)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public Address Address { get; set; }
}
");

            var assembly = GeneratorTestHarness.EmitAndLoad(compilation);
            var validatorType = assembly.GetType("CSteroid.Generated.Validation.UserValidator");
            var userType = assembly.GetType("CSteroid.Generated.Models.User");
            var addressType = assembly.GetType("CSteroid.Generated.Models.Address");
            Assert.NotNull(validatorType);
            Assert.NotNull(userType);
            Assert.NotNull(addressType);

            var validator = Activator.CreateInstance(validatorType!);
            var validateMethod = validatorType!.GetMethod("Validate");
            Assert.NotNull(validateMethod);

            var address = Activator.CreateInstance(addressType!);
            addressType!.GetProperty("PostCode")!.SetValue(address, "");
            var user = Activator.CreateInstance(userType!);
            userType!.GetProperty("Address")!.SetValue(user, address);

            var result = validateMethod!.Invoke(validator, new[] { user })!;
            Assert.False(GeneratorTestHarness.IsValid(result));
            Assert.Contains("Address.PostCode", GeneratorTestHarness.GetFailurePropertyNames(result));

            userType.GetProperty("Address")!.SetValue(user, null);
            result = validateMethod.Invoke(validator, new[] { user })!;
            Assert.True(GeneratorTestHarness.IsValid(result));

            addressType.GetProperty("PostCode")!.SetValue(address, "1234AB");
            userType.GetProperty("Address")!.SetValue(user, address);
            result = validateMethod.Invoke(validator, new[] { user })!;
            Assert.True(GeneratorTestHarness.IsValid(result));
        }

        [Fact]
        public void NestedValidation_CollectionFailures_IncludeIndex()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using System.Collections.Generic;
using CSteroid.Generated.Models;

namespace System.ComponentModel.DataAnnotations
{
    public class RequiredAttribute : Attribute { }
}

[CanonicalModelAttribute(""Address"", ValidationType: ValidationType.Custom)]
public interface IAddressModelDefinition
{
    [System.ComponentModel.DataAnnotations.Required]
    public string PostCode { get; set; }
}

[CanonicalModelAttribute(""User"", ValidationType: ValidationType.Custom)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public List<Address> Addresses { get; set; }
}
");

            var assembly = GeneratorTestHarness.EmitAndLoad(compilation);
            var validatorType = assembly.GetType("CSteroid.Generated.Validation.UserValidator");
            var userType = assembly.GetType("CSteroid.Generated.Models.User");
            var addressType = assembly.GetType("CSteroid.Generated.Models.Address");
            Assert.NotNull(validatorType);
            Assert.NotNull(userType);
            Assert.NotNull(addressType);

            var validator = Activator.CreateInstance(validatorType!);
            var validateMethod = validatorType!.GetMethod("Validate");
            Assert.NotNull(validateMethod);

            var validAddress = Activator.CreateInstance(addressType!);
            addressType!.GetProperty("PostCode")!.SetValue(validAddress, "1234AB");
            var invalidAddress = Activator.CreateInstance(addressType!);
            addressType.GetProperty("PostCode")!.SetValue(invalidAddress, "");

            var addresses = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(addressType))!;
            addresses.Add(validAddress);
            addresses.Add(invalidAddress);

            var user = Activator.CreateInstance(userType!);
            userType!.GetProperty("Addresses")!.SetValue(user, addresses);

            var result = validateMethod!.Invoke(validator, new[] { user })!;
            Assert.False(GeneratorTestHarness.IsValid(result));
            var names = GeneratorTestHarness.GetFailurePropertyNames(result);
            Assert.Contains("Addresses[1].PostCode", names);
            Assert.DoesNotContain("Addresses[0].PostCode", names);

            addressType.GetProperty("PostCode")!.SetValue(invalidAddress, "5678CD");
            result = validateMethod.Invoke(validator, new[] { user })!;
            Assert.True(GeneratorTestHarness.IsValid(result));
        }

        [Fact]
        public void NestedValidation_InlineOverride_UsesConfiguredRules()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using CSteroid.Generated.Models;

[CanonicalModelAttribute(""Address"", ValidationType: ValidationType.Custom)]
public interface IAddressModelDefinition
{
    public string PostCode { get; set; }
}

[CanonicalModelAttribute(""User"", ValidationType: ValidationType.Custom)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public Address Address { get; set; }
}

namespace CSteroid.Generated.Validation
{
    using CSteroid.Generated.Models;

    internal partial class UserValidator
    {
        static partial void ConfigureValidation(ValidationConfiguration<User> config)
        {
            config.AddRules(rules =>
            {
                rules.ForNested(x => x.Address, nested =>
                {
                    nested.For(a => a.PostCode).MinLength(5);
                });
            });
        }
    }
}
");

            var assembly = GeneratorTestHarness.EmitAndLoad(compilation);
            var validatorType = assembly.GetType("CSteroid.Generated.Validation.UserValidator");
            var userType = assembly.GetType("CSteroid.Generated.Models.User");
            var addressType = assembly.GetType("CSteroid.Generated.Models.Address");
            Assert.NotNull(validatorType);
            Assert.NotNull(userType);
            Assert.NotNull(addressType);

            var validator = Activator.CreateInstance(validatorType!);
            var validateMethod = validatorType!.GetMethod("Validate");
            Assert.NotNull(validateMethod);

            var address = Activator.CreateInstance(addressType!);
            addressType!.GetProperty("PostCode")!.SetValue(address, "123");
            var user = Activator.CreateInstance(userType!);
            userType!.GetProperty("Address")!.SetValue(user, address);

            var result = validateMethod!.Invoke(validator, new[] { user })!;
            Assert.False(GeneratorTestHarness.IsValid(result));
            Assert.Contains("Address.PostCode", GeneratorTestHarness.GetFailurePropertyNames(result));

            addressType.GetProperty("PostCode")!.SetValue(address, "12345");
            result = validateMethod.Invoke(validator, new[] { user })!;
            Assert.True(GeneratorTestHarness.IsValid(result));
        }

        [Fact]
        public void ForEach_AppliesElementRulesWithIndexPrefix()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using System.Collections.Generic;

[CanonicalModelAttribute(""User"", ValidationType: ValidationType.Custom)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public List<string> Tags { get; set; }
}

namespace CSteroid.Generated.Validation
{
    using CSteroid.Generated.Models;

    internal partial class UserValidator
    {
        static partial void ConfigureValidation(ValidationConfiguration<User> config)
        {
            config.AddRules(rules =>
            {
                rules.ForEach(x => x.Tags).NotEmpty();
            });
        }
    }
}
");

            var assembly = GeneratorTestHarness.EmitAndLoad(compilation);
            var validatorType = assembly.GetType("CSteroid.Generated.Validation.UserValidator");
            var userType = assembly.GetType("CSteroid.Generated.Models.User");
            Assert.NotNull(validatorType);
            Assert.NotNull(userType);

            var validator = Activator.CreateInstance(validatorType!);
            var validateMethod = validatorType!.GetMethod("Validate");
            Assert.NotNull(validateMethod);

            var user = Activator.CreateInstance(userType!);
            userType!.GetProperty("Tags")!.SetValue(user, new List<string> { "ok", "" });

            var result = validateMethod!.Invoke(validator, new[] { user })!;
            Assert.False(GeneratorTestHarness.IsValid(result));
            Assert.Equal(new[] { "Tags[1]" }, GeneratorTestHarness.GetFailurePropertyNames(result));

            userType.GetProperty("Tags")!.SetValue(user, new List<string> { "ok", "fine" });
            result = validateMethod.Invoke(validator, new[] { user })!;
            Assert.True(GeneratorTestHarness.IsValid(result));

            userType.GetProperty("Tags")!.SetValue(user, null);
            result = validateMethod.Invoke(validator, new[] { user })!;
            Assert.True(GeneratorTestHarness.IsValid(result));
        }

        [Fact]
        public async Task NestedValidation_AsyncValidator_ResolvesNestedAsyncValidator()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using CSteroid.Generated.Models;

namespace System.ComponentModel.DataAnnotations
{
    public class RequiredAttribute : Attribute { }
}

[CanonicalModelAttribute(""Address"", ValidationType: ValidationType.Custom | ValidationType.Async)]
public interface IAddressModelDefinition
{
    [System.ComponentModel.DataAnnotations.Required]
    public string PostCode { get; set; }
}

[CanonicalModelAttribute(""User"", ValidationType: ValidationType.Custom | ValidationType.Async)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public Address Address { get; set; }
}
");

            var assembly = GeneratorTestHarness.EmitAndLoad(compilation);
            var validatorType = assembly.GetType("CSteroid.Generated.Validation.UserValidator");
            var userType = assembly.GetType("CSteroid.Generated.Models.User");
            var addressType = assembly.GetType("CSteroid.Generated.Models.Address");
            Assert.NotNull(validatorType);
            Assert.NotNull(userType);
            Assert.NotNull(addressType);

            var validator = Activator.CreateInstance(validatorType!);
            var validateAsyncMethod = validatorType!.GetMethod("ValidateAsync");
            Assert.NotNull(validateAsyncMethod);

            var address = Activator.CreateInstance(addressType!);
            addressType!.GetProperty("PostCode")!.SetValue(address, "");
            var user = Activator.CreateInstance(userType!);
            userType!.GetProperty("Address")!.SetValue(user, address);

            var task = validateAsyncMethod!.Invoke(validator, new[] { user, CancellationToken.None }) as Task;
            Assert.NotNull(task);
            var result = await GeneratorTestHarness.GetTaskResultAsync(task!);
            Assert.False(GeneratorTestHarness.IsValid(result));
            Assert.Contains("Address.PostCode", GeneratorTestHarness.GetFailurePropertyNames(result));

            addressType.GetProperty("PostCode")!.SetValue(address, "1234AB");
            task = validateAsyncMethod.Invoke(validator, new[] { user, CancellationToken.None }) as Task;
            Assert.NotNull(task);
            result = await GeneratorTestHarness.GetTaskResultAsync(task!);
            Assert.True(GeneratorTestHarness.IsValid(result));
        }

        [Fact]
        public async Task InterfaceInheritance_ValidatorIncludesInheritedProperties()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using System.ComponentModel.DataAnnotations;

namespace System.ComponentModel.DataAnnotations
{
    public class RequiredAttribute : Attribute { }
}

public interface IEntityBase
{
    [Required]
    public string CreatedBy { get; set; }
}

[CanonicalModelAttribute(""Order"", ValidationType: ValidationType.Custom)]
public interface IOrderModelDefinition : IEntityBase
{
    [Required]
    public string Product { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "OrderValidator.g.cs");
            // Default rules are seeded for both the derived and the inherited property.
            Assert.Contains("rules.For(x => x.Product).NotNull().NotEmpty();", content);
            Assert.Contains("rules.For(x => x.CreatedBy).NotNull().NotEmpty();", content);

            // Runtime: an invalid inherited property fails validation.
            var assembly = GeneratorTestHarness.EmitAndLoad(compilation);
            var orderType = assembly.GetType("CSteroid.Generated.Models.Order");
            var validatorType = assembly.GetType("CSteroid.Generated.Validation.OrderValidator");
            Assert.NotNull(orderType);
            Assert.NotNull(validatorType);

            var validator = System.Activator.CreateInstance(validatorType!);
            var validateMethod = validatorType!.GetMethod("Validate");
            Assert.NotNull(validateMethod);

            var order = System.Activator.CreateInstance(orderType!);
            // Product set, but the inherited CreatedBy violates [Required] -> invalid.
            orderType!.GetProperty("Product")!.SetValue(order, "Widget");
            orderType.GetProperty("CreatedBy")!.SetValue(order, "");

            var result = validateMethod!.Invoke(validator, new[] { order });
            Assert.NotNull(result);
            Assert.False(GeneratorTestHarness.IsValid(result));
            Assert.Contains("CreatedBy", GeneratorTestHarness.GetFailurePropertyNames(result));
        }
    }
}
