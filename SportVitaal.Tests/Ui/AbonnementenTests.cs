using Bunit;
using SportVitaal.BlazerWasm.Pages;

namespace SportVitaal.Tests.Ui;

/// <summary>UI tests for the public Abonnementen (memberships) page, which renders purely from static data.</summary>
[TestFixture]
public class AbonnementenTests
{
    [Test]
    public void Renders_a_card_for_every_plan()
    {
        using var ctx = new Bunit.TestContext();

        var cut = ctx.RenderComponent<Abonnementen>();

        Assert.That(cut.FindAll(".card"), Has.Count.EqualTo(4));
    }

    [Test]
    public void Each_card_pairs_its_title_period_and_price()
    {
        using var ctx = new Bunit.TestContext();

        var cut = ctx.RenderComponent<Abonnementen>();

        // (title, period, price) as rendered, scoped per card so a misplaced price fails the test.
        var cards = cut.FindAll(".card").Select(c =>
        {
            var title = c.QuerySelector(".card-title")!.TextContent.Trim();
            var period = c.QuerySelector(".text-muted")!.TextContent.Trim();
            var price = c.QuerySelector(".display-6")!.TextContent.Replace("€", "").Trim();
            return (title, period, price);
        }).ToList();

        Assert.That(cards, Is.EquivalentTo(new[]
        {
            ("Max 2x per week", "per maand", "29"),
            ("Max 2x per week", "per jaar", "299"),
            ("Onbeperkt sporten", "per maand", "55"),
            ("Onbeperkt sporten", "per jaar", "549"),
        }));
    }

    [Test]
    public void Each_plan_links_to_aanmelden_with_its_type()
    {
        using var ctx = new Bunit.TestContext();

        var cut = ctx.RenderComponent<Abonnementen>();

        var links = cut.FindAll("a.btn");
        Assert.That(links, Has.Count.EqualTo(4));
        Assert.That(links.Select(l => l.GetAttribute("href")), Has.All.Contains("aanmelden?plan="));
    }
}
