using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SubClear.Api.Domain;

namespace SubClear.Api.Data;

public static class DemoIds
{
    public static readonly Guid HumberTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    public static readonly Guid NorthernTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1");

    public static readonly Guid HumberOwnerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
    public static readonly Guid HumberContractsId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3");
    public static readonly Guid HumberViewerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4");
    public static readonly Guid NorthernOwnerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
}

public static class DemoSeeder
{
    public const string DemoPassword = "DemoPassword123!";
    public const string HumberOwnerEmail = "owner@demo.subclear.uk";
    public const string HumberContractsEmail = "contracts@demo.subclear.uk";
    public const string HumberViewerEmail = "viewer@demo.subclear.uk";
    public const string NorthernOwnerEmail = "owner@northern.demo.subclear.uk";

    public static async Task SeedAsync(AppDbContext db, IPasswordHasher<UserAccount> hasher)
    {
        if (await db.Tenants.AnyAsync(t => t.Id == DemoIds.HumberTenantId))
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;

        var humber = new Tenant
        {
            Id = DemoIds.HumberTenantId,
            Name = "Humber Civils Ltd",
            CompanyNumber = "09876543",
            CreatedAt = now
        };
        var northern = new Tenant
        {
            Id = DemoIds.NorthernTenantId,
            Name = "Northern M&E Ltd",
            CompanyNumber = "01234567",
            CreatedAt = now
        };

        var owner = User(DemoIds.HumberOwnerId, HumberOwnerEmail, "Sarah Keane", hasher, now);
        var contracts = User(DemoIds.HumberContractsId, HumberContractsEmail, "Tom Ellis", hasher, now);
        var viewer = User(DemoIds.HumberViewerId, HumberViewerEmail, "Priya Shah", hasher, now);
        var northernOwner = User(DemoIds.NorthernOwnerId, NorthernOwnerEmail, "James Okafor", hasher, now);

        db.Tenants.AddRange(humber, northern);
        db.Users.AddRange(owner, contracts, viewer, northernOwner);
        db.Memberships.AddRange(
            Member(humber.Id, owner.Id, MembershipRole.Owner, now),
            Member(humber.Id, contracts.Id, MembershipRole.ContractsManager, now),
            Member(humber.Id, viewer.Id, MembershipRole.Viewer, now),
            Member(northern.Id, northernOwner.Id, MembershipRole.Owner, now));

        var riverside = Sub("Riverside Scaffolding Ltd", "Claire Dunn", "claire@riversidescaffolding.example", "01472 500100", SubcontractorStatus.Active, now, humber.Id);
        var steel = Sub("Grimsby Steel Erectors Ltd", "Mark Hewitt", "mark@grimbysteel.example", "01472 500200", SubcontractorStatus.Active, now, humber.Id);
        var plant = Sub("North Sea Plant Hire Ltd", "Aisha Khan", "aisha@northseaplant.example", "01472 500300", SubcontractorStatus.Active, now, humber.Id);
        var ground = Sub("Fenland Groundworks Ltd", "Ben Cooper", "ben@fenlandgroundworks.example", "01472 500400", SubcontractorStatus.Active, now, humber.Id);
        var electrical = Sub("Humber Electrical Contracts Ltd", "Nina Patel", "nina@humberelectrical.example", "01472 500500", SubcontractorStatus.Active, now, humber.Id);
        var roofing = Sub("Holderness Roofing Ltd", "Owen Briggs", "owen@holdernessroofing.example", "01472 500600", SubcontractorStatus.OnHold, now, humber.Id);
        var isolated = Sub("Teeside Controls Ltd", "Hidden Contact", "hidden@northern.example", "01642 100100", SubcontractorStatus.Active, now, northern.Id);

        db.Subcontractors.AddRange(riverside, steel, plant, ground, electrical, roofing, isolated);

        // Green — all required docs well in date.
        AddPack(db, riverside, today.AddMonths(10), today.AddMonths(11), today.AddMonths(9), today.AddMonths(8), today.AddMonths(6), now, humber.Id);

        // Amber — public liability expires inside 30 days.
        AddPack(db, steel, today.AddMonths(7), today.AddDays(18), today.AddMonths(5), today.AddMonths(4), today.AddMonths(3), now, humber.Id);

        // Red — employers' liability expired last month.
        AddPack(db, plant, today.AddMonths(-1), today.AddMonths(6), today.AddMonths(6), today.AddMonths(4), today.AddMonths(2), now, humber.Id);

        // Red — missing SSIP and RAMS.
        db.Documents.AddRange(
            Doc(ground, DocumentType.EmployersLiability, "EL certificate", today.AddMonths(8), now, humber.Id),
            Doc(ground, DocumentType.PublicLiability, "PL £10m", today.AddMonths(8), now, humber.Id),
            Doc(ground, DocumentType.ProfessionalIndemnity, "PI £2m", today.AddMonths(8), now, humber.Id));

        // Green.
        AddPack(db, electrical, today.AddMonths(12), today.AddMonths(12), today.AddMonths(12), today.AddMonths(10), today.AddMonths(9), now, humber.Id);

        // Amber — RAMS due in 12 days.
        AddPack(db, roofing, today.AddMonths(6), today.AddMonths(6), today.AddMonths(6), today.AddMonths(6), today.AddDays(12), now, humber.Id);

        // Isolated tenant row — must never appear for Humber users.
        AddPack(db, isolated, today.AddMonths(6), today.AddMonths(6), today.AddMonths(6), today.AddMonths(6), today.AddMonths(6), now, northern.Id);

        db.ChaseLogs.AddRange(
            new ChaseLog
            {
                Id = Guid.NewGuid(),
                TenantId = humber.Id,
                SubcontractorId = plant.Id,
                ChaseDate = today.AddDays(-4),
                Note = "Left voicemail asking for the renewed EL schedule. They said the broker is issuing this week.",
                Outcome = ChaseOutcome.LeftVoicemail,
                CreatedByUserId = contracts.Id,
                CreatedAt = now.AddDays(-4)
            },
            new ChaseLog
            {
                Id = Guid.NewGuid(),
                TenantId = humber.Id,
                SubcontractorId = ground.Id,
                ChaseDate = today.AddDays(-2),
                Note = "Emailed for SSIP certificate and current RAMS for the A180 job. No pack received yet.",
                Outcome = ChaseOutcome.EmailSent,
                CreatedByUserId = contracts.Id,
                CreatedAt = now.AddDays(-2)
            },
            new ChaseLog
            {
                Id = Guid.NewGuid(),
                TenantId = humber.Id,
                SubcontractorId = steel.Id,
                ChaseDate = today.AddDays(-1),
                Note = "Reminded Mark that PL expires in under three weeks — need the renewal before site induction.",
                Outcome = ChaseOutcome.EmailSent,
                CreatedByUserId = owner.Id,
                CreatedAt = now.AddDays(-1)
            });

        var a180 = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = humber.Id,
            Name = "A180 Junction Improvements",
            Reference = "HC-A180-26",
            SiteLocation = "Stallingborough, North East Lincolnshire",
            Status = ProjectStatus.Live,
            CreatedAt = now,
            UpdatedAt = now
        };
        var tankFarm = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = humber.Id,
            Name = "Immingham Tank Farm Civils",
            Reference = "HC-ITF-26",
            SiteLocation = "Immingham Dock",
            Status = ProjectStatus.Mobilising,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Projects.AddRange(a180, tankFarm);
        db.ProjectSubcontractors.AddRange(
            Link(a180, riverside, humber.Id, now),
            Link(a180, steel, humber.Id, now),
            Link(a180, ground, humber.Id, now),
            Link(tankFarm, plant, humber.Id, now),
            Link(tankFarm, electrical, humber.Id, now));

        await db.SaveChangesAsync();
    }

    private static UserAccount User(Guid id, string email, string name, IPasswordHasher<UserAccount> hasher, DateTimeOffset now)
    {
        var user = new UserAccount
        {
            Id = id,
            Email = email,
            FullName = name,
            CreatedAt = now
        };
        user.PasswordHash = hasher.HashPassword(user, DemoPassword);
        return user;
    }

    private static Membership Member(Guid tenantId, Guid userId, MembershipRole role, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        UserId = userId,
        Role = role,
        CreatedAt = now
    };

    private static Subcontractor Sub(
        string name,
        string contact,
        string email,
        string phone,
        SubcontractorStatus status,
        DateTimeOffset now,
        Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = name,
        ContactName = contact,
        Email = email,
        Phone = phone,
        Status = status,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static void AddPack(
        AppDbContext db,
        Subcontractor sub,
        DateOnly el,
        DateOnly pl,
        DateOnly pi,
        DateOnly ssip,
        DateOnly rams,
        DateTimeOffset now,
        Guid tenantId)
    {
        db.Documents.AddRange(
            Doc(sub, DocumentType.EmployersLiability, "Employers' liability certificate", el, now, tenantId),
            Doc(sub, DocumentType.PublicLiability, "Public liability £10m", pl, now, tenantId),
            Doc(sub, DocumentType.ProfessionalIndemnity, "Professional indemnity £2m", pi, now, tenantId),
            Doc(sub, DocumentType.Ssip, "SSIP / SafeContractor", ssip, now, tenantId),
            Doc(sub, DocumentType.Rams, "RAMS pack", rams, now, tenantId));
    }

    private static ComplianceDocument Doc(
        Subcontractor sub,
        DocumentType type,
        string title,
        DateOnly expiry,
        DateTimeOffset now,
        Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        SubcontractorId = sub.Id,
        Type = type,
        Title = title,
        ExpiryDate = expiry,
        FileName = $"{type}-{sub.Name.Replace(' ', '-')}.pdf",
        ContentType = "application/pdf",
        FileSizeBytes = 128_000,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static ProjectSubcontractor Link(Project project, Subcontractor sub, Guid tenantId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        ProjectId = project.Id,
        SubcontractorId = sub.Id,
        CreatedAt = now
    };
}
