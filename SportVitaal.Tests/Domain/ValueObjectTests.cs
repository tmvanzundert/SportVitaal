using SportVitaal.Domain.ValueObjects;

namespace SportVitaal.Tests.Domain;

/// <summary>
/// Unit tests for the value objects: Money and Email.
/// </summary>
[TestFixture]
public class ValueObjectTests
{
    // ---- Money ----

    [Test]
    public void Money_RoundsToTwoDecimalsAndUppercasesCurrency()
    {
        var m = new Money(29.005m, "eur");
        Assert.Multiple(() =>
        {
            Assert.That(m.Amount, Is.EqualTo(29.00m).Within(0.0001m));
            Assert.That(m.Currency, Is.EqualTo("EUR"));
        });
    }

    [Test]
    public void Money_NegativeAmount_Throws()
        => Assert.Throws<ArgumentException>(() => new Money(-1m));

    [Test]
    public void Money_Add_SameCurrency_Sums()
    {
        var total = new Money(10m).Add(new Money(5.5m));
        Assert.That(total.Amount, Is.EqualTo(15.5m));
    }

    [Test]
    public void Money_Add_DifferentCurrency_Throws()
        => Assert.Throws<InvalidOperationException>(() => new Money(10m, "EUR").Add(new Money(5m, "USD")));

    [Test]
    public void Money_Equality_IsByValue()
    {
        // Two distinct instances with the same value should compare equal.
        var a = new Money(29m, "EUR");
        var b = new Money(29m, "eur"); // currency normalised to upper-case
        Assert.That(a, Is.EqualTo(b));
    }

    // ---- Email ----

    [TestCase("user@example.com")]
    [TestCase("a.b-c@sub.domain.nl")]
    public void Email_Valid_IsAccepted(string address)
        => Assert.That(new Email(address).Address, Is.EqualTo(address));

    [TestCase("")]
    [TestCase("not-an-email")]
    [TestCase("missing@domain")]
    [TestCase("two@@at.com")]
    public void Email_Invalid_Throws(string address)
        => Assert.Throws<ArgumentException>(() => new Email(address));
}
