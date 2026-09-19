using SubClear.Api.Domain;

namespace SubClear.Api.Auth;

public interface ITenantContext
{
    Guid TenantId { get; }
    Guid UserId { get; }
    MembershipRole Role { get; }
    string Email { get; }
    string FullName { get; }
    bool IsAuthenticated { get; }

    void Set(Guid tenantId, Guid userId, MembershipRole role, string email, string fullName);

    /// <summary>Binds a tenant for anonymous portal or background jobs (no signed-in user).</summary>
    void SetTenant(Guid tenantId);
}

public sealed class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public MembershipRole Role { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public bool IsAuthenticated => TenantId != Guid.Empty && UserId != Guid.Empty;

    public void Set(Guid tenantId, Guid userId, MembershipRole role, string email, string fullName)
    {
        TenantId = tenantId;
        UserId = userId;
        Role = role;
        Email = email;
        FullName = fullName;
    }

    public void SetTenant(Guid tenantId)
    {
        TenantId = tenantId;
        UserId = Guid.Empty;
        Role = MembershipRole.Viewer;
        Email = string.Empty;
        FullName = string.Empty;
    }
}

public sealed class DesignTimeTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;
    public Guid UserId => Guid.Empty;
    public MembershipRole Role => MembershipRole.Viewer;
    public string Email => string.Empty;
    public string FullName => string.Empty;
    public bool IsAuthenticated => false;

    public void Set(Guid tenantId, Guid userId, MembershipRole role, string email, string fullName)
    {
        // Design-time / migration factory never authenticates a tenant.
    }

    public void SetTenant(Guid tenantId)
    {
        // Design-time / migration factory never authenticates a tenant.
    }
}
