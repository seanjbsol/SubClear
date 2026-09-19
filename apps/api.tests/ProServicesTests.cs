using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SubClear.Api.Auth;
using SubClear.Api.Contracts;
using SubClear.Api.Data;
using SubClear.Api.Domain;

namespace SubClear.Api.Tests;

public class ProServicesTests : IClassFixture<SubClearApiFactory>
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly SubClearApiFactory _factory;

    public ProServicesTests(SubClearApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Portal_invite_token_lets_sub_upload_onto_tenant_record()
    {
        var owner = await AuthenticatedAsync(DemoSeeder.HumberOwnerEmail);
        var createdSub = await owner.PostAsJsonAsync("/api/subcontractors", new
        {
            name = "Portal Upload Ltd",
            email = "ops@portalupload.example",
            status = "active"
        });
        createdSub.EnsureSuccessStatusCode();
        var ground = await Read<SubcontractorSummaryDto>(createdSub);

        var inviteResponse = await owner.PostAsJsonAsync($"/api/subcontractors/{ground.Id}/portal-invites", new { email = "ops@portalupload.example" });
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var invite = await Read<PortalInviteDto>(inviteResponse);
        invite.Token.Should().NotBeNullOrWhiteSpace();
        invite.PortalUrl.Should().StartWith("https://portal.test.subclear.uk/portal/");

        var anonymous = _factory.CreateClient();
        var session = await Get<PortalSessionDto>(anonymous, $"/api/portal/{invite.Token}");
        session.OrganisationName.Should().Be("Humber Civils Ltd");
        session.SubcontractorName.Should().Be("Portal Upload Ltd");
        session.Items.Should().Contain(i => i.Type == DocumentType.Rams && i.Missing);

        var page = await anonymous.GetAsync($"/portal/{invite.Token}");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await page.Content.ReadAsStringAsync();
        html.Should().Contain("Humber Civils Ltd");
        html.Should().Contain("RAMS");

        var upload = await anonymous.PostAsJsonAsync($"/api/portal/{invite.Token}/documents", new
        {
            type = "rams",
            title = "RAMS pack for A180",
            expiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)).ToString("yyyy-MM-dd"),
            fileName = "rams.pdf",
            contentType = "application/pdf",
            fileSizeBytes = 4096
        });
        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var uploaded = await Read<DocumentDto>(upload);
        uploaded.SubcontractorId.Should().Be(ground.Id);
        uploaded.Type.Should().Be(DocumentType.Rams);
        uploaded.ReviewStatus.Should().Be(DocumentReviewStatus.Pending);

        var pack = await Get<PackDto>(owner, $"/api/subcontractors/{ground.Id}/pack");
        pack.Items.Should().Contain(i => i.Type == DocumentType.Rams && i.DocumentId == uploaded.Id && i.ReviewStatus == DocumentReviewStatus.Pending);

        var northern = await AuthenticatedAsync(DemoSeeder.NorthernOwnerEmail);
        var hidden = await northern.GetAsync($"/api/documents/{uploaded.Id}/review");
        hidden.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        var northernDocs = await northern.GetAsync($"/api/subcontractors/{ground.Id}");
        northernDocs.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Expired_portal_token_returns_gone()
    {
        var owner = await AuthenticatedAsync(DemoSeeder.HumberOwnerEmail);
        var createdSub = await owner.PostAsJsonAsync("/api/subcontractors", new
        {
            name = "Portal Expiry Ltd",
            email = "ops@portalexpiry.example",
            status = "active"
        });
        var sub = await Read<SubcontractorSummaryDto>(createdSub);
        var inviteResponse = await owner.PostAsJsonAsync($"/api/subcontractors/{sub.Id}/portal-invites", new { });
        var invite = await Read<PortalInviteDto>(inviteResponse);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            var login = await owner.GetAsync("/api/me");
            var me = await login.Content.ReadFromJsonAsync<MeResponse>(Json);
            tenant.SetTenant(me!.TenantId);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.PortalInvites.FindAsync(invite.Id);
            row.Should().NotBeNull();
            row!.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        var anonymous = _factory.CreateClient();
        var expired = await anonymous.GetAsync($"/api/portal/{invite.Token}");
        expired.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task Chase_job_sends_and_logs_then_respects_cadence()
    {
        var owner = await AuthenticatedAsync(DemoSeeder.HumberOwnerEmail);
        var first = await owner.PostAsync("/api/chase-automation/run", null);
        if (first.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"Chase job failed ({(int)first.StatusCode}): {await first.Content.ReadAsStringAsync()}");
        }
        var result = await Read<ChaseJobResultDto>(first);
        result.EmailsSent.Should().BeGreaterThan(0);
        result.SkippedNotPro.Should().Be(0);

        var log = await Get<List<EmailSendLogDto>>(owner, "/api/email-log");
        log.Should().Contain(e => e.Kind == EmailKind.DocumentChase);
        log.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.Subject));

        var second = await owner.PostAsync("/api/chase-automation/run", null);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var again = await Read<ChaseJobResultDto>(second);
        again.EmailsSent.Should().Be(0);
        again.SkippedNotDue.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Reviewer_and_owner_can_approve_and_reject_with_comments()
    {
        var owner = await AuthenticatedAsync(DemoSeeder.HumberOwnerEmail);
        var created = await owner.PostAsJsonAsync("/api/subcontractors", new
        {
            name = "Review Queue Ltd",
            email = "docs@reviewqueue.example",
            status = "active"
        });
        var sub = await Read<SubcontractorSummaryDto>(created);
        var contracts = await AuthenticatedAsync(DemoSeeder.HumberContractsEmail);
        var uploaded = await contracts.PostAsJsonAsync($"/api/subcontractors/{sub.Id}/documents", new
        {
            type = "ssip",
            title = "SSIP certificate",
            expiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(10)).ToString("yyyy-MM-dd")
        });
        uploaded.StatusCode.Should().Be(HttpStatusCode.Created);
        var pendingDoc = await Read<DocumentDto>(uploaded);
        pendingDoc.ReviewStatus.Should().Be(DocumentReviewStatus.Pending);

        var queue = await Get<List<ReviewQueueItemDto>>(owner, "/api/review-queue");
        queue.Should().Contain(q => q.DocumentId == pendingDoc.Id);

        var reject = await owner.PostAsJsonAsync($"/api/documents/{pendingDoc.Id}/review", new
        {
            decision = "rejected",
            comment = "Certificate is cropped — please send the full schedule."
        });
        reject.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejected = await Read<DocumentDto>(reject);
        rejected.ReviewStatus.Should().Be(DocumentReviewStatus.Rejected);
        rejected.ReviewComment.Should().Contain("cropped");

        var forbidden = await contracts.PostAsJsonAsync($"/api/documents/{pendingDoc.Id}/review", new
        {
            decision = "approved",
            comment = "Looks fine"
        });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var reviewer = await AuthenticatedAsync(DemoSeeder.HumberReviewerEmail);
        var createdRams = await contracts.PostAsJsonAsync($"/api/subcontractors/{sub.Id}/documents", new
        {
            type = "rams",
            title = "RAMS received by email",
            expiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(4)).ToString("yyyy-MM-dd")
        });
        createdRams.StatusCode.Should().Be(HttpStatusCode.Created);
        var awaiting = await Read<DocumentDto>(createdRams);
        awaiting.ReviewStatus.Should().Be(DocumentReviewStatus.Pending);

        var approve = await reviewer.PostAsJsonAsync($"/api/documents/{awaiting.Id}/review", new
        {
            decision = "approved",
            comment = "RAMS cover the A180 junction works."
        });
        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await Read<DocumentDto>(approve);
        approved.ReviewStatus.Should().Be(DocumentReviewStatus.Approved);
        approved.ReviewedByName.Should().Be("Maya Chen");

        var pack = await Get<PackDto>(owner, $"/api/subcontractors/{sub.Id}/pack");
        pack.Items.Should().Contain(i => i.Type == DocumentType.Ssip && i.ReviewStatus == DocumentReviewStatus.Rejected);
        pack.Items.Should().Contain(i => i.Type == DocumentType.Rams && i.ReviewStatus == DocumentReviewStatus.Approved);
    }

    [Fact]
    public async Task Directory_link_creates_tenant_scoped_sub_without_exposing_source()
    {
        var owner = await AuthenticatedAsync(DemoSeeder.HumberOwnerEmail);
        var subs = await Get<List<SubcontractorSummaryDto>>(owner, "/api/subcontractors");
        var green = subs.Single(s => s.Name == "Riverside Scaffolding Ltd");
        var published = await owner.PostAsJsonAsync($"/api/subcontractors/{green.Id}/publish-to-network", new
        {
            trade = "Precision millwork",
            region = "North East Lincolnshire"
        });
        published.StatusCode.Should().Be(HttpStatusCode.OK);
        var listing = await Read<DirectoryListingDto>(published);
        listing.AnonymisedName.Should().Be("Precision millwork contractor");
        listing.AnonymisedName.Should().NotContain("Riverside");
        listing.AnonymisedName.Should().NotContain("Teeside");

        var directory = await Get<List<DirectoryListingDto>>(owner, "/api/directory");
        directory.Should().Contain(d => d.Id == listing.Id);
        directory.Should().OnlyContain(d => !d.AnonymisedName.Contains("Teeside", StringComparison.OrdinalIgnoreCase));

        var seedListing = directory.First(d => d.AnonymisedName == "Groundworks contractor");
        var humberSubs = await Get<List<SubcontractorSummaryDto>>(owner, "/api/subcontractors");
        Guid linkedId;
        var already = humberSubs.FirstOrDefault(s => s.Name == seedListing.AnonymisedName);
        if (already is not null)
        {
            linkedId = already.Id;
        }
        else
        {
            var linked = await owner.PostAsJsonAsync("/api/directory/link-requests", new { networkListingId = seedListing.Id });
            linked.StatusCode.Should().Be(HttpStatusCode.Created);
            var request = await Read<LinkRequestDto>(linked);
            request.SubcontractorId.Should().NotBeNull();
            linkedId = request.SubcontractorId!.Value;
        }

        var detail = await Get<SubcontractorDetailDto>(owner, $"/api/subcontractors/{linkedId}");
        detail.Name.Should().Be(seedListing.AnonymisedName);

        var northern = await AuthenticatedAsync(DemoSeeder.NorthernOwnerEmail);
        var hidden = await northern.GetAsync($"/api/subcontractors/{linkedId}");
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var northernList = await Get<List<SubcontractorSummaryDto>>(northern, "/api/subcontractors");
        northernList.Should().NotContain(s => s.Id == linkedId);
        northernList.Should().NotContain(s => s.Name.Contains("Riverside", StringComparison.OrdinalIgnoreCase));
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

public class StarterEntitlementTests : IClassFixture<StarterSubscriptionApiFactory>
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly StarterSubscriptionApiFactory _factory;

    public StarterEntitlementTests(StarterSubscriptionApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Starter_is_blocked_from_portal_chase_and_review_with_upgrade_path()
    {
        var owner = await AuthenticatedAsync(DemoSeeder.HumberOwnerEmail);
        var entitlements = await Get<EntitlementsDto>(owner, "/api/billing/entitlements");
        entitlements.IsPro.Should().BeFalse();
        entitlements.HasPortal.Should().BeFalse();
        entitlements.Plan.Should().Be("starter");
        entitlements.SubcontractorLimit.Should().Be(6);

        var subs = await Get<List<SubcontractorSummaryDto>>(owner, "/api/subcontractors");
        var ground = subs.Single(s => s.Name == "Fenland Groundworks Ltd");

        var invite = await owner.PostAsJsonAsync($"/api/subcontractors/{ground.Id}/portal-invites", new { });
        invite.StatusCode.Should().Be((HttpStatusCode)402);
        var inviteBody = await invite.Content.ReadAsStringAsync();
        inviteBody.Should().Contain("checkoutHint");
        inviteBody.Should().Contain("Pro");

        var chase = await owner.PostAsync("/api/chase-automation/run", null);
        chase.StatusCode.Should().Be((HttpStatusCode)402);

        var reviewQueue = await owner.GetAsync("/api/review-queue");
        reviewQueue.StatusCode.Should().Be((HttpStatusCode)402);

        var pending = await Get<List<SubcontractorSummaryDto>>(owner, "/api/subcontractors");
        var docs = await owner.GetAsync($"/api/subcontractors/{ground.Id}");
        docs.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await Read<SubcontractorDetailDto>(docs);
        var ssip = detail.Documents.First(d => d.Type == DocumentType.Ssip);
        var review = await owner.PostAsJsonAsync($"/api/documents/{ssip.Id}/review", new { decision = "approved" });
        review.StatusCode.Should().Be((HttpStatusCode)402);

        var manual = await owner.PostAsJsonAsync($"/api/subcontractors/{ground.Id}/chases", new
        {
            note = "Phoned Ben and asked for RAMS. Left a message with the office.",
            outcome = "leftVoicemail"
        });
        manual.StatusCode.Should().Be(HttpStatusCode.Created);

        var extra = await owner.PostAsJsonAsync("/api/subcontractors", new { name = "Over Limit Ltd", status = "active" });
        extra.StatusCode.Should().Be((HttpStatusCode)402);
        var extraBody = await extra.Content.ReadAsStringAsync();
        extraBody.Should().Contain("subcontractorLimit");
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

public sealed class StarterSubscriptionApiFactory : SubClearApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SubscriptionApi:UseStub"] = "true",
                ["SubscriptionApi:StubStatus"] = "active",
                ["SubscriptionApi:StubPlan"] = "starter",
                ["SubscriptionApi:StarterSubcontractorLimit"] = "6"
            });
        });
    }
}
