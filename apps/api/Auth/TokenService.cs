using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SubClear.Api.Domain;

namespace SubClear.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "SubClear";
    public string Audience { get; set; } = "SubClear";
    /// <summary>Signing key. Production must supply Jwt__Key via environment / secret store.</summary>
    public string Key { get; set; } = string.Empty;
    public int ExpiryHours { get; set; } = 12;
}

public interface ITokenService
{
    string CreateToken(UserAccount user, Membership membership, Tenant tenant);
}

public sealed class TokenService : ITokenService
{
    public const string TenantIdClaim = "tenant_id";
    public const string RoleClaim = "role";

    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string CreateToken(UserAccount user, Membership membership, Tenant tenant)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, membership.Role.ToString()),
            new(RoleClaim, membership.Role.ToString()),
            new(TenantIdClaim, membership.TenantId.ToString()),
            new("tenant_name", tenant.Name)
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_options.ExpiryHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public static class RoleSets
{
    public const string Write = "Owner,Admin,ContractsManager";
    public const string Admin = "Owner,Admin";
}
