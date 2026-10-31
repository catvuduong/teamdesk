using Api.Auth;

namespace Api.Tests.Unit;

public class AuthRulesTests
{
    [Fact]
    public void Signing_key_is_sha256_of_the_jwt_secret_when_set()
    {
        var key = SigningKey.Derive("my-secret", "postgres://db");

        Assert.Equal(32, key.Length);
        Assert.Equal(SigningKey.Derive("my-secret", null), key);
        Assert.NotEqual(SigningKey.Derive("other", null), key);
    }

    [Fact]
    public void Signing_key_falls_back_to_database_url_when_no_secret_is_set()
    {
        var fromUrl = SigningKey.Derive(null, "postgres://db");

        Assert.Equal(SigningKey.Derive("   ", "postgres://db"), fromUrl);
        Assert.NotEqual(SigningKey.Derive(null, "postgres://other"), fromUrl);
    }

    [Fact]
    public void Registration_rules_report_each_field()
    {
        var errors = AccountRules.ValidateRegistration("", "not-an-email", "short");

        Assert.Contains("name", errors.Keys);
        Assert.Contains("email", errors.Keys);
        Assert.Contains("password", errors.Keys);
    }

    [Fact]
    public void Valid_registration_has_no_errors()
    {
        Assert.Empty(AccountRules.ValidateRegistration("Ada", "  Ada@Example.com ", "long-enough-password"));
    }

    [Fact]
    public void Email_is_trimmed_and_lowercased()
    {
        Assert.Equal("ada@example.com", AccountRules.NormalizeEmail("  Ada@Example.COM "));
    }
}
