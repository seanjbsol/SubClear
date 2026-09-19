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
[Route("api/subcontractors/{subcontractorId:guid}/portal-invites")]
[RequireProFeature(ProFeature.Portal)]
public sealed class PortalInvitesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPortalInviteService _invites;

    public PortalInvitesController(AppDbContext db, ITenantContext tenant, IPortalInviteService invites)
    {
        _db = db;
        _tenant = tenant;
        _invites = invites;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PortalInviteDto>>> List(Guid subcontractorId, CancellationToken cancellationToken)
    {
        var sub = await _db.Subcontractors.AsNoTracking().FirstOrDefaultAsync(s => s.Id == subcontractorId, cancellationToken);
        if (sub is null)
        {
            return NotFound();
        }

        var rows = await _db.PortalInvites
            .AsNoTracking()
            .Where(i => i.SubcontractorId == subcontractorId)
            .ToListAsync(cancellationToken);

        return rows.OrderByDescending(i => i.CreatedAt).Select(ToDto).ToList();
    }

    [HttpPost]
    [Authorize(Roles = RoleSets.Write)]
    public async Task<ActionResult<PortalInviteDto>> Create(
        Guid subcontractorId,
        CreatePortalInviteRequest? request,
        CancellationToken cancellationToken)
    {
        var sub = await _db.Subcontractors.FirstOrDefaultAsync(s => s.Id == subcontractorId, cancellationToken);
        if (sub is null)
        {
            return NotFound();
        }

        TenantGuard.Ensure(sub.TenantId, _tenant.TenantId);
        var email = string.IsNullOrWhiteSpace(request?.Email) ? sub.Email : request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { title = "Email required", detail = "Add an email address on the subcontractor record, or pass one on the invite." });
        }

        var created = await _invites.CreateAsync(sub, email, _tenant.UserId, sendEmail: true, cancellationToken);
        return CreatedAtAction(nameof(List), new { subcontractorId }, new PortalInviteDto
        {
            Id = created.Invite.Id,
            SubcontractorId = created.Invite.SubcontractorId,
            Email = created.Invite.Email,
            ExpiresAt = created.Invite.ExpiresAt,
            CreatedAt = created.Invite.CreatedAt,
            PortalUrl = created.PortalUrl,
            Token = created.RawToken
        });
    }

    [HttpPost("{inviteId:guid}/revoke")]
    [Authorize(Roles = RoleSets.Write)]
    public async Task<ActionResult<PortalInviteDto>> Revoke(Guid subcontractorId, Guid inviteId, CancellationToken cancellationToken)
    {
        var invite = await _db.PortalInvites.FirstOrDefaultAsync(
            i => i.Id == inviteId && i.SubcontractorId == subcontractorId,
            cancellationToken);
        if (invite is null)
        {
            return NotFound();
        }

        TenantGuard.Ensure(invite.TenantId, _tenant.TenantId);
        invite.RevokedAt ??= DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(invite);
    }

    private static PortalInviteDto ToDto(PortalInvite invite) => new()
    {
        Id = invite.Id,
        SubcontractorId = invite.SubcontractorId,
        Email = invite.Email,
        ExpiresAt = invite.ExpiresAt,
        RevokedAt = invite.RevokedAt,
        LastUsedAt = invite.LastUsedAt,
        CreatedAt = invite.CreatedAt
    };
}
