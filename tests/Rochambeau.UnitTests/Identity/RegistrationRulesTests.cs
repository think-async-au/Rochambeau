using Rochambeau.Api.Common.Results;
using Rochambeau.Api.Modules.Identity.Contracts;
using Rochambeau.Api.Modules.Identity.Domain;

namespace Rochambeau.UnitTests.Identity;

public class RegistrationRulesTests
{
    private const string _validUsername = "Rocky";
    private const string _validEmail = "user-1@example.test";
    private const string _validPassword = "test-password-8";

    [Theory]
    [InlineData("abc")]
    [InlineData("abcdefghijklmnopqrst")] // 20
    [InlineData("Rocky_the-2nd")]
    [InlineData("A-1")]
    public void Username_within_the_rules_is_accepted(string raw)
    {
        Assert.True(Username.TryCreate(raw, out var username));
        Assert.Equal(raw, username!.Value);
    }

    [Theory]
    [InlineData("ab")]                    // 2
    [InlineData("abcdefghijklmnopqrstu")] // 21
    [InlineData("Rоcky")]            // Cyrillic "о"
    [InlineData("Rocky Balboa")]
    [InlineData("rocky!")]
    [InlineData("rocky.b")]
    [InlineData("Ünal")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Username_breaking_the_rules_is_rejected(string? raw)
    {
        Assert.False(Username.TryCreate(raw, out var username));
        Assert.Null(username);
    }

    [Fact]
    public void Username_is_trimmed_before_checking()
    {
        Assert.True(Username.TryCreate("  Rocky  ", out var username));
        Assert.Equal("Rocky", username!.Value);
    }

    [Fact]
    public void Username_trimmed_to_fewer_than_three_characters_is_rejected()
    {
        Assert.False(Username.TryCreate("  ab  ", out _));
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Password_length_is_the_only_rule(int length, bool accepted)
    {
        Assert.Equal(accepted, Password.TryCreate(new string('x', length), out _));
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("        ")]
    [InlineData("alllowercase")]
    public void Password_needs_no_character_classes(string raw)
    {
        Assert.True(Password.TryCreate(raw, out var password));
        Assert.Equal(raw, password!.Value);
    }

    [Fact]
    public void Password_is_not_trimmed()
    {
        Assert.True(Password.TryCreate(" pass word ", out var password));
        Assert.Equal(" pass word ", password!.Value);
        Assert.True(Password.TryCreate(" 123456 ", out _)); // 8 with the spaces; trimming would make it 6
    }

    [Theory]
    [InlineData("user@example.test")]
    [InlineData("first.last+tag@sub.example.test")]
    public void Well_formed_email_is_accepted(string raw)
    {
        Assert.True(EmailAddress.TryCreate(raw, out var email));
        Assert.Equal(raw, email!.Value);
    }

    [Theory]
    [InlineData("no-at-sign.example.test")]
    [InlineData("two@@example.test")]
    [InlineData("@example.test")]
    [InlineData("user@")]
    [InlineData("User Name <user@example.test>")]
    [InlineData("user name@example.test")]
    [InlineData("")]
    [InlineData(null)]
    public void Malformed_email_is_rejected(string? raw)
    {
        Assert.False(EmailAddress.TryCreate(raw, out _));
    }

    [Fact]
    public void Email_is_trimmed_and_keeps_its_letter_case()
    {
        Assert.True(EmailAddress.TryCreate(" User@Example.Test ", out var email));
        Assert.Equal("User@Example.Test", email!.Value);
    }

    [Theory]
    [InlineData(254, true)]
    [InlineData(255, false)]
    public void Email_is_at_most_254_characters(int length, bool accepted)
    {
        const string domain = "@example.test";
        var local = new string('a', 64);
        var labels = new string('b', length - local.Length - domain.Length - 1);
        var raw = $"{local}@{labels}.{domain[1..]}";
        Assert.Equal(length, raw.Length);

        Assert.Equal(accepted, EmailAddress.TryCreate(raw, out _));
    }

    [Fact]
    public void Valid_registration_returns_the_trimmed_values()
    {
        var result = RegistrationRules.Validate("  Rocky ", " User@Example.Test ", _validPassword);

        Assert.True(result.IsSuccess);
        Assert.Equal("Rocky", result.Value.Username.Value);
        Assert.Equal("User@Example.Test", result.Value.Email.Value);
        Assert.Equal(_validPassword, result.Value.Password.Value);
    }

    [Fact]
    public void Every_offending_field_is_reported_at_once()
    {
        var result = RegistrationRules.Validate("ab", "not-an-email", "short");

        Assert.True(result.IsFailure);
        var error = result.Error!;
        Assert.Equal("identity.validation_failed", error.Code);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(["email", "password", "username"], error.FieldErrors!.Keys.Order());
        Assert.Equal(
            ["The username must be 3 to 20 characters: Latin letters, digits, underscore or hyphen."],
            error.FieldErrors["username"]);
        Assert.Equal(["The password must be at least 8 characters."], error.FieldErrors["password"]);
        Assert.Single(error.FieldErrors["email"]);
    }

    [Fact]
    public void Only_offending_fields_are_reported()
    {
        var result = RegistrationRules.Validate(_validUsername, _validEmail, new string('x', 129));

        Assert.Equal(["password"], result.Error!.FieldErrors!.Keys);
        Assert.Equal(["The password must be at most 128 characters."], result.Error.FieldErrors["password"]);
    }

    [Fact]
    public void Policy_values_match_the_spec()
    {
        Assert.Equal(5, LockoutPolicy.MaxFailedAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), LockoutPolicy.Duration);
        Assert.Equal(5, RegistrationCapPolicy.MaxRegistrations);
        Assert.Equal(TimeSpan.FromMinutes(60), RegistrationCapPolicy.Window);
        Assert.Equal([AccountRole.Player, AccountRole.Administrator], Enum.GetValues<AccountRole>());
    }

    [Fact]
    public void Domain_has_no_framework_dependencies()
    {
        var forbidden = new[] { "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Microsoft.Extensions" };
        var domainTypes = typeof(Username).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Rochambeau.Api.Modules.Identity.Domain", StringComparison.Ordinal) == true);

        foreach (var type in domainTypes)
        {
            var referenced = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Select(f => f.FieldType)
                .Concat(type.GetProperties().Select(p => p.PropertyType))
                .Concat(type.GetMethods().SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)))
                .Append(type.BaseType)
                .OfType<Type>();

            Assert.DoesNotContain(referenced, r => forbidden.Any(f => r.Namespace?.StartsWith(f, StringComparison.Ordinal) == true));
        }
    }
}
