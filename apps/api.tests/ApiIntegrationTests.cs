using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SubClear.Api.Contracts;
using SubClear.Api.Data;
using SubClear.Api.Domain;

namespace SubClear.Api.Tests;

public class SubClearApiFactory : WebApplicationFactory<Program>
{
    protected string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"subclear-tests-{Guid.NewGuid():N}.db");

    protected virtual IReadOnlyDictionary<string, string?> ExtraSettings => new Dictionary<string, string?>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:ConnectionString"] = $"Data Source={DbPath}",
            ["Jwt:Key"] = "test-key-must-be-at-least-32-characters-long!!",
            ["Jwt:Issuer"] = "SubClear",
            ["Jwt:Audience"] = "SubClear",
            ["Seed:Enabled"] = "true",
            ["SubscriptionApi:UseStub"] = "true",
            ["SubscriptionApi:ProductCode"] = "SubClear",
            ["SubscriptionApi:StubStatus"] = "trialing",
            ["SubscriptionApi:StubPlan"] = "pro",
            ["Email:Provider"] = "File",
            ["Email:FileDirectory"] = Path.Combine(Path.GetTempPath(), $"subclear-emails-{Guid.NewGuid():N}"),
            ["Chase:BackgroundEnabled"] = "false",
            ["Portal:PublicBaseUrl"] = "https://portal.test.subclear.uk",
            ["Portal:TokenLifetimeHours"] = "168"
        };
        foreach (var pair in ExtraSettings)
        {
            settings[pair.Key] = pair.Value;
        }

        builder.UseEnvironment("Development");
        builder.UseSetting("Database:ConnectionString", $"Data Source={DbPath}");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(settings);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            File.Delete(DbPath);
            File.Delete(DbPath + "-wal");
            File.Delete(DbPath + "-shm");
        }
        catch
        {
            // Best-effort cleanup of the throwaway SQLite file.
        }
    }
}

public class ApiIntegrationTests : IClassFixture<SubClearApiFactory>
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly SubClearApiFactory _factory;

    public ApiIntegrationTests(SubClearApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ok");
        body.Should().Contain("SubClear API");
    }

    [Fact]
    public async Task Register_creates_tenant_and_owner_token()
    {
        var client = _factory.CreateClient();
        var email = $"owner.{Guid.NewGuid():N}@newco.test";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            organisationName = "Newco Civils Ltd",
            fullName = "Riley Owner",
            email,
            password = "DemoPassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await Read<AuthResponse>(response);
        auth.Token.Should().NotBeNullOrWhiteSpace();
        auth.User.Role.Should().Be(MembershipRole.Owner);
        auth.User.TenantId.Should().NotBe(Guid.Empty);
        auth.User.OrganisationName.Should().Be("Newco Civils Ltd");
    }

    [Fact]
    public async Task Demo_dashboard_has_mixed_traffic_lights()
    {
        var client = await AuthenticatedAsync(DemoSeeder.HumberOwnerEmail);

        var dashboard = await Get<DashboardDto>(client, "/api/dashboard");
        dashboard.TotalSubcontractors.Should().BeGreaterThanOrEqualTo(6);
        dashboard.NonCompliantCount.Should().BeGreaterThan(0);
        dashboard.ExpiringWithin30Days.Should().BeGreaterThan(0);
        dashboard.CompliantCount.Should().BeGreaterThan(0);

        var subs = await Get<List<SubcontractorSummaryDto>>(client, "/api/subcontractors");
        subs.Should().Contain(s => s.Name == "Riverside Scaffolding Ltd" && s.Compliance == ComplianceLight.Green);
        subs.Should().Contain(s => s.Name == "Grimsby Steel Erectors Ltd" && s.Compliance == ComplianceLight.Amber);
        subs.Should().Contain(s => s.Name == "North Sea Plant Hire Ltd" && s.Compliance == ComplianceLight.Red);
        subs.Should().Contain(s => s.Name == "Fenland Groundworks Ltd" && s.Compliance != ComplianceLight.Green);
        subs.Should().NotContain(s => s.Name.Contains("Teeside"));
    }

    [Fact]
    public async Task Second_tenant_cannot_see_or_mutate_first_tenant_rows()
    {
        var humber = await AuthenticatedAsync(DemoSeeder.HumberOwnerEmail);
        var humberSubs = await Get<List<SubcontractorSummaryDto>>(humber, "/api/subcontractors");
        var riverside = humberSubs.Single(s => s.Name == "Riverside Scaffolding Ltd");

        var northern = await AuthenticatedAsync(DemoSeeder.NorthernOwnerEmail);
        var northernSubs = await Get<List<SubcontractorSummaryDto>>(northern, "/api/subcontractors");
        northernSubs.Should().ContainSingle(s => s.Name == "Teeside Controls Ltd");
        northernSubs.Should().NotContain(s => s.Id == riverside.Id);

        var hidden = await northern.GetAsync($"/api/subcontractors/{riverside.Id}");
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var mutate = await northern.PostAsJsonAsync($"/api/subcontractors/{riverside.Id}/documents", new
        {
            type = "ssip",
            title = "Should not persist"
        });
        mutate.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var dashboard = await Get<DashboardDto>(northern, "/api/dashboard");
        dashboard.TotalSubcontractors.Should().BeGreaterThanOrEqualTo(1);
        northernSubs.Should().Contain(s => s.Name == "Teeside Controls Ltd");
    }

    [Fact]
    public async Task Subcontractor_and_document_crud_is_tenant_scoped()
    {
        var client = await AuthenticatedAsync(DemoSeeder.HumberOwnerEmail);

        var created = await client.PostAsJsonAsync("/api/subcontractors", new
        {
            name = "Barton Brickwork Ltd",
            contactName = "Lucy Barton",
            email = "lucy@bartonbrick.example",
            status = "active"
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var sub = await Read<SubcontractorSummaryDto>(created);
        sub.Compliance.Should().Be(ComplianceLight.Red);

        var docResponse = await client.PostAsJsonAsync($"/api/subcontractors/{sub.Id}/documents", new
        {
            type = "employersLiability",
            title = "EL certificate",
            expiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(8)).ToString("yyyy-MM-dd"),
            fileName = "el.pdf",
            contentType = "application/pdf",
            fileSizeBytes = 2048
        });
        docResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = await Read<DocumentDto>(docResponse);
        doc.Light.Should().Be(ComplianceLight.Green);
        doc.ReviewStatus.Should().Be(DocumentReviewStatus.Approved);

        var marked = await client.PostAsync($"/api/documents/{doc.Id}/mark-expired", null);
        marked.StatusCode.Should().Be(HttpStatusCode.OK);
        var expired = await Read<DocumentDto>(marked);
        expired.IsManuallyExpired.Should().BeTrue();
        expired.Light.Should().Be(ComplianceLight.Red);

        var pack = await Get<PackDto>(client, $"/api/subcontractors/{sub.Id}/pack");
        pack.Overall.Should().Be(ComplianceLight.Red);
        pack.Items.Should().Contain(i => i.Type == DocumentType.EmployersLiability && i.Expired);

        var chase = await client.PostAsJsonAsync($"/api/subcontractors/{sub.Id}/chases", new
        {
            note = "Asked for a replacement EL schedule from the broker.",
            outcome = "emailSent"
        });
        chase.StatusCode.Should().Be(HttpStatusCode.Created);

        var queue = await Get<List<ChaseQueueItemDto>>(client, "/api/chase-queue");
        queue.Should().Contain(q => q.SubcontractorId == sub.Id);
    }

    [Fact]
    public async Task Viewer_cannot_write()
    {
        var client = await AuthenticatedAsync(DemoSeeder.HumberViewerEmail);

        var response = await client.PostAsJsonAsync("/api/subcontractors", new { name = "Should Fail Ltd" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var list = await client.GetAsync("/api/subcontractors");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
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
        var auth = await Read<AuthResponse>(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private static async Task<T> Get<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await Read<T>(response);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(Json);
        payload.Should().NotBeNull();
        return payload!;
    }
}
