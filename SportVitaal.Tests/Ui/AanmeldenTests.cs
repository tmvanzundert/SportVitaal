using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SportVitaal.BlazerWasm.Pages;
using SportVitaal.BlazerWasm.Services;

namespace SportVitaal.Tests.Ui;

/// <summary>UI tests for the public Aanmelden (signup) page, covering rendering, validation and the full purchase flow.</summary>
[TestFixture]
public class AanmeldenTests
{
    private static Bunit.TestContext CreateContext(StubHttpMessageHandler handler)
    {
        var ctx = new Bunit.TestContext();
        var tokens = new TokenProvider();
        ctx.Services.AddSingleton(tokens);
        ctx.Services.AddSingleton(new SportVitaalApiClient(handler.ToHttpClient(), tokens));
        return ctx;
    }

    /// <summary>A handler wired so the whole signup flow (register → login → purchase → payment) succeeds.</summary>
    private static StubHttpMessageHandler HappyPathHandler()
    {
        // SportVitaalApiClient decodes the client secret as base64("{paymentId}:simulated_secret").
        var clientSecret = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("pay_123:simulated_secret"));

        return new StubHttpMessageHandler()
            .When("api/auth/register", "", HttpStatusCode.OK)
            .When("api/auth/login", """{ "token": "jwt-token" }""")
            .When("api/memberships/purchase",
                $$"""{ "clientSecret": "{{clientSecret}}", "amount": 55, "currency": "EUR", "startDate": "2026-07-01T00:00:00" }""")
            .When("api/payments/webhook", "", HttpStatusCode.OK);
    }

    private static void FillCredentials(IRenderedFragment cut)
    {
        cut.Find("#firstname").Change("Nieuw");
        cut.Find("#lastname").Change("Lid");
        cut.Find("#email").Change("nieuw@lid.nl");
        cut.Find("#password").Change("Wachtwoord1!");
    }

    [Test]
    public void Renders_the_signup_form_with_all_plan_options()
    {
        using var ctx = CreateContext(new StubHttpMessageHandler());

        var cut = ctx.RenderComponent<Aanmelden>();

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("form"), Is.Not.Null);
            Assert.That(cut.FindAll("select option"), Has.Count.EqualTo(4));
            Assert.That(cut.Find("button[type=submit]").TextContent, Does.Contain("Aanmelden"));
        });
    }

    [Test]
    public void Submitting_without_credentials_shows_a_validation_error()
    {
        using var ctx = CreateContext(new StubHttpMessageHandler());

        var cut = ctx.RenderComponent<Aanmelden>();
        cut.Find("#firstname").Change("Nieuw");
        cut.Find("#lastname").Change("Lid");
        cut.Find("form").Submit();

        var alert = cut.Find(".alert-danger");
        Assert.That(alert.TextContent, Does.Contain("Vul een e-mailadres en wachtwoord in"));
    }

    [Test]
    public void Submitting_without_a_name_shows_a_validation_error()
    {
        using var ctx = CreateContext(new StubHttpMessageHandler());

        var cut = ctx.RenderComponent<Aanmelden>();
        cut.Find("#email").Change("nieuw@lid.nl");
        cut.Find("#password").Change("Wachtwoord1!");
        cut.Find("form").Submit();

        var alert = cut.Find(".alert-danger");
        Assert.That(alert.TextContent, Does.Contain("Vul je voor- en achternaam in"));
    }

    [Test]
    public void Successful_signup_shows_the_confirmation_panel()
    {
        using var ctx = CreateContext(HappyPathHandler());

        var cut = ctx.RenderComponent<Aanmelden>();
        FillCredentials(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            var success = cut.Find(".alert-success");
            Assert.Multiple(() =>
            {
                Assert.That(success.TextContent, Does.Contain("Welkom bij SportVitaal!"));
                Assert.That(success.TextContent, Does.Contain("55"));
            });
            Assert.That(cut.FindAll("form"), Is.Empty, "the form should be replaced by the confirmation panel");
        });
    }

    [Test]
    public void Existing_email_shows_a_friendly_error()
    {
        var handler = new StubHttpMessageHandler()
            .When("api/auth/register", "User already exists", HttpStatusCode.BadRequest);
        using var ctx = CreateContext(handler);

        var cut = ctx.RenderComponent<Aanmelden>();
        FillCredentials(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find(".alert-danger");
            Assert.That(alert.TextContent, Does.Contain("Er bestaat al een account met dit e-mailadres"));
        });
    }

    [Test]
    public void Failed_payment_shows_an_error_and_keeps_the_form()
    {
        var clientSecret = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("pay_123:simulated_secret"));
        var handler = new StubHttpMessageHandler()
            .When("api/auth/register", "", HttpStatusCode.OK)
            .When("api/auth/login", """{ "token": "jwt-token" }""")
            .When("api/memberships/purchase",
                $$"""{ "clientSecret": "{{clientSecret}}", "amount": 55, "currency": "EUR", "startDate": "2026-07-01T00:00:00" }""")
            .When("api/payments/webhook", "", HttpStatusCode.InternalServerError);
        using var ctx = CreateContext(handler);

        var cut = ctx.RenderComponent<Aanmelden>();
        FillCredentials(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            Assert.That(cut.Find(".alert-danger").TextContent, Does.Contain("betaling is niet verwerkt"));
            Assert.That(cut.FindAll("form"), Is.Not.Empty, "the form should remain so the user can retry");
        });
    }
}
