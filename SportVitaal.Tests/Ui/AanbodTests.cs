using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SportVitaal.BlazerWasm.Pages;
using SportVitaal.BlazerWasm.Services;

namespace SportVitaal.Tests.Ui;

/// <summary>UI tests for the public Aanbod (workout offering) page, which loads workouts from the API on init.</summary>
[TestFixture]
public class AanbodTests
{
    private static Bunit.TestContext CreateContext(StubHttpMessageHandler handler)
    {
        var ctx = new Bunit.TestContext();
        var tokens = new TokenProvider();
        ctx.Services.AddSingleton(tokens);
        ctx.Services.AddSingleton(new SportVitaalApiClient(handler.ToHttpClient(), tokens));
        return ctx;
    }

    [Test]
    public void Renders_a_card_for_each_returned_workout()
    {
        const string json = """
            [
              { "id": "11111111-1111-1111-1111-111111111111", "name": "Spinning", "description": "Pittige rit", "defaultDurationMinutes": 45 },
              { "id": "22222222-2222-2222-2222-222222222222", "name": "Yoga", "description": null, "defaultDurationMinutes": 60 }
            ]
            """;
        var handler = new StubHttpMessageHandler().WhenGet("api/workouts", json);
        using var ctx = CreateContext(handler);

        var cut = ctx.RenderComponent<Aanbod>();

        Assert.That(cut.FindAll(".card"), Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("Spinning"));
            Assert.That(cut.Markup, Does.Contain("Yoga"));
            Assert.That(cut.Markup, Does.Contain("45 min"));
        });
    }

    [Test]
    public void Shows_an_empty_message_when_there_are_no_workouts()
    {
        var handler = new StubHttpMessageHandler().WhenGet("api/workouts", "[]");
        using var ctx = CreateContext(handler);

        var cut = ctx.RenderComponent<Aanbod>();

        Assert.That(cut.FindAll(".card"), Is.Empty);
        Assert.That(cut.Markup, Does.Contain("geen workouts"));
    }

    [Test]
    public void Shows_an_error_alert_when_the_api_fails()
    {
        var handler = new StubHttpMessageHandler().WhenStatus("api/workouts", HttpStatusCode.InternalServerError);
        using var ctx = CreateContext(handler);

        var cut = ctx.RenderComponent<Aanbod>();

        var alert = cut.Find(".alert-danger");
        Assert.That(alert.TextContent, Does.Contain("niet worden geladen"));
    }
}
