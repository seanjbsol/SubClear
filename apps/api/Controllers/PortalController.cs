using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SubClear.Api.Auth;
using SubClear.Api.Billing;
using SubClear.Api.Contracts;
using SubClear.Api.Data;
using SubClear.Api.Domain;
using SubClear.Api.Mapping;
using SubClear.Api.Services;

namespace SubClear.Api.Controllers;

[ApiController]
[AllowAnonymous]
[SkipSubscriptionCheck]
[Route("api/portal/{token}")]
public sealed class PortalApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPortalInviteService _invites;
    private readonly IDocumentStorage _storage;

    public PortalApiController(
        AppDbContext db,
        ITenantContext tenant,
        IPortalInviteService invites,
        IDocumentStorage storage)
    {
        _db = db;
        _tenant = tenant;
        _invites = invites;
        _storage = storage;
    }

    [HttpGet]
    public async Task<ActionResult<PortalSessionDto>> Get(string token, CancellationToken cancellationToken)
    {
        var loaded = await BindAsync(token, cancellationToken);
        if (loaded.Result is not null)
        {
            return loaded.Result;
        }

        var session = loaded.Value!.Value;
        return Ok(ToSession(session.Invite, session.Sub, session.Organisation));
    }

    [HttpPost("documents")]
    [Consumes("application/json")]
    public async Task<ActionResult<DocumentDto>> UploadJson(
        string token,
        DocumentWriteRequest request,
        CancellationToken cancellationToken) =>
        await SaveAsync(token, request, file: null, cancellationToken);

    [HttpPost("documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<DocumentDto>> UploadForm(
        string token,
        [FromForm] DocumentType type,
        [FromForm] string title,
        [FromForm] DateOnly? expiryDate,
        [FromForm] string? notes,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        var request = new DocumentWriteRequest
        {
            Type = type,
            Title = title ?? file?.FileName ?? "Uploaded document",
            ExpiryDate = expiryDate,
            FileName = file?.FileName,
            ContentType = file?.ContentType,
            FileSizeBytes = file?.Length,
            Notes = notes
        };
        return await SaveAsync(token, request, file, cancellationToken);
    }

    private async Task<ActionResult<DocumentDto>> SaveAsync(
        string token,
        DocumentWriteRequest request,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var loaded = await BindAsync(token, cancellationToken);
        if (loaded.Result is not null)
        {
            return loaded.Result;
        }

        var bound = loaded.Value!.Value;
        var invite = bound.Invite;
        var sub = bound.Sub;
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { title = "Title required", detail = "Please give the document a title." });
        }

        var now = DateTimeOffset.UtcNow;
        var doc = new ComplianceDocument
        {
            Id = Guid.NewGuid(),
            TenantId = invite.TenantId,
            SubcontractorId = sub.Id,
            Type = request.Type,
            Title = request.Title.Trim(),
            ExpiryDate = request.ExpiryDate,
            FileName = string.IsNullOrWhiteSpace(request.FileName) ? file?.FileName : request.FileName.Trim(),
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? file?.ContentType : request.ContentType.Trim(),
            FileSizeBytes = request.FileSizeBytes ?? file?.Length,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            ReviewStatus = DocumentReviewStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (file is { Length: > 0 })
        {
            await using var stream = file.OpenReadStream();
            doc.StorageKey = await _storage.SaveAsync(invite.TenantId, doc.Id, file.FileName, stream, cancellationToken);
            doc.FileName ??= file.FileName;
            doc.ContentType ??= file.ContentType;
            doc.FileSizeBytes ??= file.Length;
        }

        _db.Documents.Add(doc);
        invite.LastUsedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        return Created($"/api/portal/{token}/documents", DtoMapper.ToDocument(doc, DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    private async Task<(ActionResult? Result, (PortalInvite Invite, Subcontractor Sub, string Organisation)? Value)> BindAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var invite = await _invites.FindActiveByRawTokenAsync(token, cancellationToken);
        if (invite is null)
        {
            return (NotFound(new { title = "Link not found", detail = "That upload link is invalid or has been revoked." }), null);
        }

        if (invite.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return (StatusCode(StatusCodes.Status410Gone, new
            {
                title = "Link expired",
                detail = "This upload link has expired. Ask your contracts manager to send a new one."
            }), null);
        }

        _tenant.SetTenant(invite.TenantId);
        var sub = await _db.Subcontractors.Include(s => s.Documents).FirstOrDefaultAsync(s => s.Id == invite.SubcontractorId, cancellationToken);
        if (sub is null)
        {
            return (NotFound(new { title = "Link not found", detail = "That upload link is no longer valid." }), null);
        }

        var organisation = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => t.Id == invite.TenantId)
            .Select(t => t.Name)
            .FirstAsync(cancellationToken);

        return (null, (invite, sub, organisation));
    }

    internal static PortalSessionDto ToSession(PortalInvite invite, Subcontractor sub, string organisation)
    {
        var pack = DtoMapper.ToPack(sub, DateOnly.FromDateTime(DateTime.UtcNow));
        return new PortalSessionDto
        {
            OrganisationName = organisation,
            SubcontractorName = sub.Name,
            ContactName = sub.ContactName,
            ExpiresAt = invite.ExpiresAt,
            Items = pack.Items.Where(i => i.Required).ToList()
        };
    }
}

[AllowAnonymous]
[SkipSubscriptionCheck]
[Route("portal/{token}")]
public sealed class PortalPageController : Controller
{
    private readonly IPortalInviteService _invites;
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public PortalPageController(IPortalInviteService invites, AppDbContext db, ITenantContext tenant)
    {
        _invites = invites;
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> Get(string token, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(token, cancellationToken);
        if (loaded.Error is not null)
        {
            return Content(PortalHtml.Error(loaded.Error), "text/html; charset=utf-8");
        }

        return Content(PortalHtml.Form(token, loaded.Session!), "text/html; charset=utf-8");
    }

    [HttpPost]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Post(
        string token,
        [FromForm] DocumentType type,
        [FromForm] string title,
        [FromForm] DateOnly? expiryDate,
        [FromForm] string? notes,
        [FromForm] IFormFile? file,
        [FromServices] IDocumentStorage storage,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(token, cancellationToken);
        if (loaded.Error is not null || loaded.Invite is null || loaded.Sub is null)
        {
            return Content(PortalHtml.Error(loaded.Error ?? "This upload link is no longer valid."), "text/html; charset=utf-8");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return Content(PortalHtml.Error("Please give the document a title."), "text/html; charset=utf-8");
        }

        var now = DateTimeOffset.UtcNow;
        var doc = new ComplianceDocument
        {
            Id = Guid.NewGuid(),
            TenantId = loaded.Invite.TenantId,
            SubcontractorId = loaded.Sub.Id,
            Type = type,
            Title = title.Trim(),
            ExpiryDate = expiryDate,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            ReviewStatus = DocumentReviewStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        if (file is { Length: > 0 })
        {
            await using var stream = file.OpenReadStream();
            doc.StorageKey = await storage.SaveAsync(loaded.Invite.TenantId, doc.Id, file.FileName, stream, cancellationToken);
            doc.FileName = file.FileName;
            doc.ContentType = file.ContentType;
            doc.FileSizeBytes = file.Length;
        }

        _db.Documents.Add(doc);
        loaded.Invite.LastUsedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        var session = PortalApiController.ToSession(loaded.Invite, loaded.Sub, loaded.Organisation ?? "Your main contractor");
        return Content(PortalHtml.Success(token, session, title.Trim()), "text/html; charset=utf-8");
    }

    private async Task<(string? Error, PortalInvite? Invite, Subcontractor? Sub, string? Organisation, PortalSessionDto? Session)> LoadAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var invite = await _invites.FindActiveByRawTokenAsync(token, cancellationToken);
        if (invite is null)
        {
            return ("This upload link is invalid or has been revoked.", null, null, null, null);
        }

        if (invite.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return ("This upload link has expired. Please ask your contracts manager for a new one.", null, null, null, null);
        }

        _tenant.SetTenant(invite.TenantId);
        var sub = await _db.Subcontractors.Include(s => s.Documents).FirstOrDefaultAsync(s => s.Id == invite.SubcontractorId, cancellationToken);
        if (sub is null)
        {
            return ("This upload link is no longer valid.", null, null, null, null);
        }

        var organisation = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => t.Id == invite.TenantId)
            .Select(t => t.Name)
            .FirstAsync(cancellationToken);
        return (null, invite, sub, organisation, PortalApiController.ToSession(invite, sub, organisation));
    }
}

internal static class PortalHtml
{
    public static string Error(string message) => Page("Upload link", $"<p>{WebUtility.HtmlEncode(message)}</p>");

    public static string Success(string token, PortalSessionDto session, string uploadedTitle)
    {
        var body = $"""
            <p>Thank you. <strong>{WebUtility.HtmlEncode(uploadedTitle)}</strong> has been sent to {WebUtility.HtmlEncode(session.OrganisationName)} for review.</p>
            <p><a href="/portal/{WebUtility.UrlEncode(token)}">Upload another document</a></p>
            """;
        return Page("Document received", body);
    }

    public static string Form(string token, PortalSessionDto session)
    {
        var items = string.Join("", session.Items.Select(item =>
        {
            var status = item.Missing ? "Not on file" : item.Expired ? "Expired" : item.ReviewStatus == DocumentReviewStatus.Pending ? "Awaiting review" : $"Expires {item.ExpiryDate:dd/MM/yyyy}";
            return $"<li><strong>{WebUtility.HtmlEncode(item.Label)}</strong> — {WebUtility.HtmlEncode(status)}</li>";
        }));

        var action = $"/portal/{WebUtility.UrlEncode(token)}";
        var body = $"""
            <p>{WebUtility.HtmlEncode(session.OrganisationName)} has asked {WebUtility.HtmlEncode(session.SubcontractorName)} to upload current compliance documents. You do not need an account or the SubClear app.</p>
            <p class="meta">This private link expires on {session.ExpiresAt:dd MMMM yyyy} at {session.ExpiresAt:HH:mm} UTC.</p>
            <h2>Required pack</h2>
            <ul>{items}</ul>
            <form method="post" action="{action}" enctype="multipart/form-data">
              <label>Document type
                <select name="type" required>
                  <option value="1">Employers' liability (EL)</option>
                  <option value="2">Public liability (PL)</option>
                  <option value="3">Professional indemnity (PI)</option>
                  <option value="4">SSIP</option>
                  <option value="5">RAMS</option>
                  <option value="6">CSCS</option>
                  <option value="7">Other</option>
                </select>
              </label>
              <label>Title
                <input name="title" required maxlength="200" placeholder="EL certificate 2026/27" />
              </label>
              <label>Expiry date
                <input name="expiryDate" type="date" />
              </label>
              <label>File (PDF or image, optional)
                <input name="file" type="file" accept=".pdf,.png,.jpg,.jpeg,.webp,application/pdf,image/*" />
              </label>
              <label>Notes
                <textarea name="notes" maxlength="4000" rows="3"></textarea>
              </label>
              <button type="submit">Upload document</button>
            </form>
            <p class="meta">Uploads land on the main contractor's register and go to their review queue. Do not share this link.</p>
            """;
        return Page("Upload documents", body);
    }

    private static string Page(string title, string body) =>
        $$"""
        <!DOCTYPE html>
        <html lang="en-GB">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>{{WebUtility.HtmlEncode(title)}} · SubClear</title>
          <style>
            body { font-family: Georgia, "Times New Roman", serif; background: #F4F6F8; color: #122033; margin: 0; }
            main { max-width: 40rem; margin: 2rem auto; background: #fff; padding: 2rem; border: 1px solid #D9E1EA; border-radius: 12px; }
            h1 { font-family: system-ui, sans-serif; color: #0B1F3A; font-size: 1.6rem; }
            h2 { font-family: system-ui, sans-serif; color: #0B1F3A; font-size: 1.1rem; }
            .meta { color: #5C6B7A; }
            label { display: block; margin: 1rem 0; font-family: system-ui, sans-serif; font-weight: 600; color: #5C6B7A; }
            input, select, textarea { display: block; width: 100%; margin-top: 0.35rem; padding: 0.6rem; box-sizing: border-box; border: 1px solid #D9E1EA; border-radius: 8px; font: inherit; color: #122033; }
            button { background: #0B1F3A; color: #fff; border: 0; border-radius: 8px; padding: 0.75rem 1.2rem; font-weight: 700; cursor: pointer; }
            ul { padding-left: 1.2rem; }
          </style>
        </head>
        <body>
          <main>
            <p class="meta">SubClear</p>
            <h1>{{WebUtility.HtmlEncode(title)}}</h1>
            {{body}}
          </main>
        </body>
        </html>
        """;
}
