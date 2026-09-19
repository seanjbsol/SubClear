using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using SubClear.Api.Contracts;
using SubClear.Api.Data;
using SubClear.Api.Domain;

namespace SubClear.Api.Tests;

public class BillingIntegrationTests : IClassFixture<SubClearApiFactory>
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly SubClearApiFactory _factory;

    public BillingIntegrationTests(SubClearApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Entitlements_returns_stub_plan_for_owner()
    {
        var client = await AuthenticatedAsync(DemoSeeder.HumberOwnerEmail);
        var response = await client.GetAsync("/api/billing/entitlements");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EntitlementsDto>(Json);
        body.Should().NotBeNull();
        body!.ProductCode.Should().Be("SubClear");
        body.Status.Should().Be("trialing");
        body.Plan.Should().Be("stub");
        body.IsEntitled.Should().BeTrue();
        body.TenantId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Owner_can_open_stub_checkout_and_portal()
    {
        var client = await AuthenticatedAsync(DemoSeeder.HumberOwnerEmail);

        var checkout = await client.PostAsJsonAsync("/api/billing/checkout", new { });
        checkout.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkoutBody = await checkout.Content.ReadFromJsonAsync<BillingSessionDto>(Json);
        checkoutBody!.Url.Should().StartWith("https://billing.stub.qckapp.local/checkout/");

        var portal = await client.PostAsJsonAsync("/api/billing/portal", new { });
        portal.StatusCode.Should().Be(HttpStatusCode.OK);
        var portalBody = await portal.Content.ReadFromJsonAsync<BillingSessionDto>(Json);
        portalBody!.Url.Should().StartWith("https://billing.stub.qckapp.local/portal/");
    }

    [Fact]
    public async Task Viewer_cannot_create_checkout_or_portal()
    {
        var client = await AuthenticatedAsync(DemoSeeder.HumberViewerEmail);

        var checkout = await client.PostAsJsonAsync("/api/billing/checkout", new { });
        checkout.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var portal = await client.PostAsJsonAsync("/api/billing/portal", new { });
        portal.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var entitlements = await client.GetAsync("/api/billing/entitlements");
        entitlements.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Inactive_stub_subscription_returns_402_on_dashboard()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SubscriptionApi:UseStub"] = "true",
                    ["SubscriptionApi:StubStatus"] = "canceled"
                });
            });
        });

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = DemoSeeder.HumberOwnerEmail,
            password = DemoSeeder.DemoPassword
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var dashboard = await client.GetAsync("/api/dashboard");
        dashboard.StatusCode.Should().Be((HttpStatusCode)402);
        var payload = await dashboard.Content.ReadAsStringAsync();
        payload.Should().Contain("checkoutHint");
        payload.Should().Contain("/api/billing/checkout");

        var entitlements = await client.GetAsync("/api/billing/entitlements");
        entitlements.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await entitlements.Content.ReadFromJsonAsync<EntitlementsDto>(Json);
        body!.IsEntitled.Should().BeFalse();
        body.Status.Should().Be("canceled");
    }

    [Fact]
    public async Task Register_still_creates_owner_when_stub_billing_is_enabled()
    {
        var client = _factory.CreateClient();
        var email = $"billing.{Guid.NewGuid():N}@newco.test";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            organisationName = "Billing Newco Ltd",
            fullName = "Blair Owner",
            email,
            password = "DemoPassword123!"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpClient> AuthenticatedAsync(string email)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = DemoSeeder.DemoPassword
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        auth.Should().NotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }
}
