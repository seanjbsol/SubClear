using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SubClear.Api.Auth;
using SubClear.Api.Domain;

namespace SubClear.Api.Data;

public sealed class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Used by global query filters. EF Core parameterises this property per query,
    /// so changing tenant on the scoped ITenantContext is honoured without leaking rows.
    /// </summary>
    public Guid CurrentTenantId => _tenantContext.TenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Subcontractor> Subcontractors => Set<Subcontractor>();
    public DbSet<ComplianceDocument> Documents => Set<ComplianceDocument>();
    public DbSet<ChaseLog> ChaseLogs => Set<ChaseLog>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectSubcontractor> ProjectSubcontractors => Set<ProjectSubcontractor>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<PortalInvite> PortalInvites => Set<PortalInvite>();
    public DbSet<EmailSendLog> EmailSendLogs => Set<EmailSendLog>();
    public DbSet<NetworkListing> NetworkListings => Set<NetworkListing>();
    public DbSet<LinkRequest> LinkRequests => Set<LinkRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CompanyNumber).HasMaxLength(20);
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(320).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
            entity.HasOne(e => e.Tenant).WithMany(t => t.Memberships).HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.User).WithMany(u => u.Memberships).HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<Subcontractor>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.TradingName).HasMaxLength(200);
            entity.Property(e => e.ContactName).HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(320);
            entity.Property(e => e.Phone).HasMaxLength(40);
            entity.Property(e => e.CompanyNumber).HasMaxLength(20);
            entity.Property(e => e.Notes).HasMaxLength(4000);
            entity.Property(e => e.Trade).HasMaxLength(120);
            entity.HasIndex(e => new { e.TenantId, e.Name });
            entity.HasIndex(e => new { e.TenantId, e.NetworkListingId });
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.NetworkListing).WithMany().HasForeignKey(e => e.NetworkListingId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ComplianceDocument>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(260);
            entity.Property(e => e.ContentType).HasMaxLength(200);
            entity.Property(e => e.StorageKey).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(4000);
            entity.Property(e => e.ReviewComment).HasMaxLength(4000);
            entity.HasIndex(e => new { e.TenantId, e.SubcontractorId, e.Type });
            entity.HasIndex(e => new { e.TenantId, e.ReviewStatus });
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
            entity.HasOne(e => e.Subcontractor)
                .WithMany(s => s.Documents)
                .HasForeignKey(e => e.SubcontractorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ReviewedBy)
                .WithMany()
                .HasForeignKey(e => e.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChaseLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Note).HasMaxLength(4000).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.SubcontractorId, e.ChaseDate });
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
            entity.HasOne(e => e.Subcontractor)
                .WithMany(s => s.ChaseLogs)
                .HasForeignKey(e => e.SubcontractorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Reference).HasMaxLength(80);
            entity.Property(e => e.SiteLocation).HasMaxLength(300);
            entity.HasIndex(e => new { e.TenantId, e.Name });
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<ProjectSubcontractor>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ProjectId, e.SubcontractorId }).IsUnique();
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Subcontractors)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Subcontractor)
                .WithMany(s => s.ProjectLinks)
                .HasForeignKey(e => e.SubcontractorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantSettings>(entity =>
        {
            entity.HasKey(e => e.TenantId);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
            entity.HasOne(e => e.Tenant).WithOne(t => t.Settings).HasForeignKey<TenantSettings>(e => e.TenantId);
        });

        modelBuilder.Entity<PortalInvite>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(320).IsRequired();
            entity.Property(e => e.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.SubcontractorId, e.ExpiresAt });
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.Subcontractor)
                .WithMany(s => s.PortalInvites)
                .HasForeignKey(e => e.SubcontractorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmailSendLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ToAddress).HasMaxLength(320).IsRequired();
            entity.Property(e => e.Subject).HasMaxLength(300).IsRequired();
            entity.Property(e => e.Body).HasMaxLength(8000).IsRequired();
            entity.Property(e => e.Provider).HasMaxLength(40).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.SentAt });
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.Subcontractor)
                .WithMany()
                .HasForeignKey(e => e.SubcontractorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<NetworkListing>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AnonymisedName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Trade).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Region).HasMaxLength(120).IsRequired();
            entity.Property(e => e.CompanyNumberHash).HasMaxLength(64);
            entity.HasIndex(e => new { e.Trade, e.Region });
            entity.HasIndex(e => e.SourceSubcontractorId).IsUnique();
        });

        modelBuilder.Entity<LinkRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(320);
            entity.Property(e => e.Message).HasMaxLength(2000);
            entity.HasIndex(e => new { e.TenantId, e.CreatedAt });
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.NetworkListing).WithMany().HasForeignKey(e => e.NetworkListingId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Subcontractor).WithMany().HasForeignKey(e => e.SubcontractorId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.CreatedBy).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceTenantOnEntries();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        EnforceTenantOnEntries();
        return base.SaveChanges();
    }

    /// <summary>
    /// Belt-and-braces: every tenant-owned row written in a request must match the JWT tenant.
    /// Combined with global query filters so a missed Where clause cannot leak or overwrite another org.
    /// </summary>
    private void EnforceTenantOnEntries()
    {
        if (_tenantContext.TenantId == Guid.Empty)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is EntityState.Added)
            {
                if (entry.Entity.TenantId == Guid.Empty)
                {
                    entry.Entity.TenantId = _tenantContext.TenantId;
                }
                else if (entry.Entity.TenantId != _tenantContext.TenantId)
                {
                    throw new InvalidOperationException("Refusing to persist a row for a different tenant.");
                }
            }
            else if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                if (entry.Entity.TenantId != _tenantContext.TenantId)
                {
                    throw new InvalidOperationException("Refusing to modify a row owned by a different tenant.");
                }
            }
        }
    }
}

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=subclear.dev.db")
            .Options;

        return new AppDbContext(options, new DesignTimeTenantContext());
    }
}
