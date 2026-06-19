using SportVitaal.Application.Services;

namespace SportVitaal.Tests.Application;

/// <summary>
/// Unit tests for <see cref="PasswordHasher"/> (PBKDF2 hash/verify) and <see cref="PasswordPolicy"/>
/// (strength rules).
/// </summary>
[TestFixture]
public class PasswordTests
{
    // ---- PasswordHasher ----

    [Test]
    public void HashPassword_ThenVerify_WithCorrectPassword_ReturnsTrue()
    {
        var hash = PasswordHasher.HashPassword("Sterk!Wachtwoord1");
        Assert.That(PasswordHasher.Verify(hash, "Sterk!Wachtwoord1"), Is.True);
    }

    [Test]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        var hash = PasswordHasher.HashPassword("Sterk!Wachtwoord1");
        Assert.That(PasswordHasher.Verify(hash, "verkeerd"), Is.False);
    }

    [Test]
    public void HashPassword_ProducesDifferentHashesPerCall()
    {
        // Random salt means two hashes of the same password should differ.
        var first = PasswordHasher.HashPassword("zelfde");
        var second = PasswordHasher.HashPassword("zelfde");
        Assert.That(first, Is.Not.EqualTo(second));
    }

    [Test]
    public void Verify_WithMalformedHash_ReturnsFalse()
        => Assert.That(PasswordHasher.Verify("not-base64!!", "whatever"), Is.False);

    // ---- PasswordPolicy ----

    [Test]
    public void Validate_StrongPassword_IsValid()
    {
        var (isValid, reasons) = PasswordPolicy.Validate("Sterk!1abc");
        Assert.That(isValid, Is.True);
        Assert.That(reasons, Is.Empty);
    }

    [TestCase("short1!", Description = "too short")]
    [TestCase("lowercase1!", Description = "no uppercase")]
    [TestCase("UPPERCASE1!", Description = "no lowercase")]
    [TestCase("NoDigits!", Description = "no digit")]
    [TestCase("NoSpecial1", Description = "no special character")]
    public void Validate_WeakPassword_IsInvalid(string password)
    {
        var (isValid, reasons) = PasswordPolicy.Validate(password);
        Assert.That(isValid, Is.False);
        Assert.That(reasons, Is.Not.Empty);
    }
}
