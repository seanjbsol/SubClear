using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SubClear.Api.Auth;
using SubClear.Api.Billing;
using SubClear.Api.Contracts;
using SubClear.Api.Data;
using SubClear.Api.Domain;
using SubClear.Api.Services;

namespace SubClear.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/directory")]
public sealed class DirectoryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISubscriptionClient _subscriptions;
    private readonly IPortalInviteService _invites;

    public DirectoryController(
        AppDbContext db,
        ITenantContext tenant,
        ISubscriptionClient subscriptions,
        IPortalInviteService invites)
    {
        _db = db;
        _tenant = tenant;
        _subscriptions = subscriptions;
        _invites = invites;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DirectoryListingDto>>> List(CancellationToken cancellationToken)
    {
        var rows = await _db.NetworkListings
            .AsNoTracking()
            .OrderBy(l => l.Trade)
            .ThenBy(l => l.Region)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    [HttpGet("link-requests")]
    public async Task<ActionResult<IReadOnlyList<LinkRequestDto>>> LinkRequests(CancellationToken cancellationToken)
    {
        var rows = await _db.LinkRequests.AsNoTracking().ToListAsync(cancellationToken);
        return rows.OrderByDescending(r => r.CreatedAt).Select(r => ToDto(r, portalUrl: null)).ToList();
    }

    [HttpPost("link-requests")]
    [Authorize(Roles = RoleSets.Write)]
    public async Task<ActionResult<LinkRequestDto>> CreateLink(LinkRequestWriteRequest request, CancellationToken cancellationToken)
    {
        if (request.NetworkListingId is null && string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                title = "Invite or directory link required",
                detail = "Choose a directory listing to link, or invite a subcontractor by email."
            });
        }

        var entitlements = await _subscriptions.GetEntitlementsAsync(_tenant.TenantId, cancellationToken);
        if (entitlements.SubcontractorLimit is { } limit)
        {
            var count = await _db.Subcontractors.CountAsync(cancellationToken);
            if (count >= limit)
            {
                return BillingProblems.UpgradeRequired(
                    $"Starter includes up to {limit} subcontractors. Upgrade to Pro for an unlimited register.",
                    "subcontractorLimit");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && !entitlements.HasPortal)
        {
            return BillingProblems.UpgradeRequired(
                "Email invites use the SubClear Pro upload portal. Upgrade to send a magic link, or add the subcontractor by hand.",
                nameof(ProFeature.Portal));
        }

        NetworkListing? listing = null;
        if (request.NetworkListingId is { } listingId)
        {
            listing = await _db.NetworkListings.FirstOrDefaultAsync(l => l.Id == listingId, cancellationToken);
            if (listing is null)
            {
                return NotFound(new { title = "Listing not found", detail = "That directory company is no longer listed." });
            }

            var already = await _db.Subcontractors
                .IgnoreQueryFilters()
                .AnyAsync(
                    s => s.TenantId == _tenant.TenantId && s.NetworkListingId == listing.Id,
                    cancellationToken);
            if (already)
            {
                return Conflict(new { title = "Already linked", detail = "That directory company is already on this register." });
            }
        }

        var now = DateTimeOffset.UtcNow;
        var name = string.IsNullOrWhiteSpace(request.Name)
            ? listing?.AnonymisedName ?? "Invited subcontractor"
            : request.Name.Trim();

        var sub = new Subcontractor
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            Name = name,
            ContactName = TrimToNull(request.ContactName),
            Email = TrimToNull(request.Email)?.ToLowerInvariant(),
            Trade = listing?.Trade,
            Notes = listing is null
                ? TrimToNull(request.Message)
                : $"Linked from the SubClear network ({listing.Trade}, {listing.Region}).",
            NetworkListingId = listing?.Id,
            Status = SubcontractorStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Subcontractors.Add(sub);

        string? portalUrl = null;
        if (!string.IsNullOrWhiteSpace(sub.Email))
        {
            var invite = await _invites.CreateAsync(sub, sub.Email, _tenant.UserId, sendEmail: true, cancellationToken);
            portalUrl = invite.PortalUrl;
        }

        var link = new LinkRequest
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            NetworkListingId = listing?.Id,
            SubcontractorId = sub.Id,
            Email = sub.Email,
            Message = TrimToNull(request.Message),
            Status = LinkRequestStatus.Accepted,
            CreatedByUserId = _tenant.UserId,
            CreatedAt = now
        };
        _db.LinkRequests.Add(link);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(LinkRequests), ToDto(link, portalUrl));
    }

    private static DirectoryListingDto ToDto(NetworkListing listing) => new()
    {
        Id = listing.Id,
        AnonymisedName = listing.AnonymisedName,
        Trade = listing.Trade,
        Region = listing.Region,
        VerifiedAt = listing.VerifiedAt
    };

    private static LinkRequestDto ToDto(LinkRequest request, string? portalUrl) => new()
    {
        Id = request.Id,
        NetworkListingId = request.NetworkListingId,
        SubcontractorId = request.SubcontractorId,
        Email = request.Email,
        Message = request.Message,
        Status = request.Status,
        CreatedAt = request.CreatedAt,
        PortalUrl = portalUrl
    };

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

[ApiController]
[Authorize]
[Route("api/subcontractors/{subcontractorId:guid}/publish-to-network")]
public sealed class PublishToNetworkController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public PublishToNetworkController(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpPost]
    [Authorize(Roles = RoleSets.Admin)]
    public async Task<ActionResult<DirectoryListingDto>> Publish(
        Guid subcontractorId,
        PublishToNetworkRequest? request,
        CancellationToken cancellationToken)
    {
        var sub = await _db.Subcontractors.Include(s => s.Documents).FirstOrDefaultAsync(s => s.Id == subcontractorId, cancellationToken);
        if (sub is null)
        {
            return NotFound();
        }

        TenantGuard.Ensure(sub.TenantId, _tenant.TenantId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var pack = ComplianceCalculator.PackStatuses(sub.Documents, today)
            .Where(p => p.Required)
            .ToList();
        if (pack.Any(p => p.Light == ComplianceLight.Red || p.ReviewStatus != DocumentReviewStatus.Approved))
        {
            return BadRequest(new
            {
                title = "Pack not ready",
                detail = "Publish to the network only when every required document is in date and approved."
            });
        }

        var existing = await _db.NetworkListings.FirstOrDefaultAsync(l => l.SourceSubcontractorId == sub.Id, cancellationToken);
        var trade = string.IsNullOrWhiteSpace(request?.Trade) ? sub.Trade ?? "General contracting" : request.Trade.Trim();
        var region = string.IsNullOrWhiteSpace(request?.Region) ? "United Kingdom" : request.Region.Trim();
        var listing = existing ?? new NetworkListing
        {
            Id = Guid.NewGuid(),
            SourceTenantId = sub.TenantId,
            SourceSubcontractorId = sub.Id
        };
        listing.AnonymisedName = $"{trade} contractor";
        listing.Trade = trade;
        listing.Region = region;
        listing.CompanyNumberHash = HashCompanyNumber(sub.CompanyNumber);
        listing.VerifiedAt = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            _db.NetworkListings.Add(listing);
        }

        sub.NetworkListingId = listing.Id;
        sub.Trade = trade;
        await _db.SaveChangesAsync(cancellationToken);

        return new DirectoryListingDto
        {
            Id = listing.Id,
            AnonymisedName = listing.AnonymisedName,
            Trade = listing.Trade,
            Region = listing.Region,
            VerifiedAt = listing.VerifiedAt
        };
    }

    private static string? HashCompanyNumber(string? companyNumber)
    {
        if (string.IsNullOrWhiteSpace(companyNumber))
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(companyNumber.Trim().ToUpperInvariant()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
