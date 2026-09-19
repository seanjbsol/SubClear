using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using SubClear.Api.Auth;
using SubClear.Api.Billing;
using SubClear.Api.Data;
using SubClear.Api.Domain;
using SubClear.Api.Email;
using SubClear.Api.Options;
using SubClear.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .PostConfigure(options =>
    {
        if (string.IsNullOrWhiteSpace(options.Key) || options.Key.Length < 32)
        {
            if (builder.Environment.IsDevelopment())
            {
                options.Key = "dev-only-subclear-signing-key-change-in-production-32+";
            }
            else
            {
                throw new InvalidOperationException(
                    "Jwt:Key must be set to at least 32 characters in non-development environments (use Jwt__Key).");
            }
        }
    });

var dbProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var connectionString = builder.Configuration["Database:ConnectionString"]
                       ?? "Data Source=subclear.dev.db";

if (!dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    const string prefix = "Data Source=";
    if (connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
        var file = connectionString[prefix.Length..].Trim();
        if (!Path.IsPathRooted(file))
        {
            connectionString = prefix + Path.Combine(builder.Environment.ContentRootPath, file);
        }
    }
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddSingleton<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<RequireActiveSubscriptionFilter>();
builder.Services.AddScoped<RequireProFeatureFilter>();
builder.Services.AddScoped<IPortalInviteService, PortalInviteService>();
builder.Services.AddScoped<IDocumentChaseJob, DocumentChaseJob>();
builder.Services.AddSingleton<IDocumentStorage, LocalDocumentStorage>();
builder.Services.AddHostedService<DocumentChaseHostedService>();

builder.Services.AddOptions<SubscriptionApiOptions>()
    .Bind(builder.Configuration.GetSection(SubscriptionApiOptions.SectionName))
    .PostConfigure(options =>
    {
        if (string.IsNullOrWhiteSpace(options.ProductCode))
        {
            options.ProductCode = SubscriptionApiOptions.DefaultProductCode;
        }

        if (string.IsNullOrWhiteSpace(options.StubPlan))
        {
            options.StubPlan = PlanFeatures.Pro;
        }

        if (options.UseStub)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl) || string.IsNullOrWhiteSpace(options.ApiKey))
        {
            if (builder.Environment.IsDevelopment())
            {
                options.UseStub = true;
                return;
            }

            throw new InvalidOperationException(
                "SubscriptionApi:BaseUrl and SubscriptionApi:ApiKey must be set when SubscriptionApi:UseStub is false (use SubscriptionApi__BaseUrl and SubscriptionApi__ApiKey).");
        }
    });

var useStub = builder.Configuration.GetValue("SubscriptionApi:UseStub", builder.Environment.IsDevelopment());
if (!useStub
    && string.IsNullOrWhiteSpace(builder.Configuration["SubscriptionApi:BaseUrl"])
    && builder.Environment.IsDevelopment())
{
    useStub = true;
}

if (useStub)
{
    builder.Services.PostConfigure<SubscriptionApiOptions>(options =>
    {
        options.UseStub = true;
        if (string.IsNullOrWhiteSpace(options.StubPlan))
        {
            options.StubPlan = PlanFeatures.Pro;
        }
    });
    builder.Services.AddSingleton<ISubscriptionClient, StubSubscriptionClient>();
}
else
{
    builder.Services.AddHttpClient<ISubscriptionClient, SubscriptionClient>((sp, http) =>
    {
        var options = sp.GetRequiredService<IOptions<SubscriptionApiOptions>>().Value;
        SubscriptionClient.ConfigureHttpClient(http, options);
    });
}

builder.Services.AddOptions<EmailOptions>().Bind(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddOptions<PortalOptions>().Bind(builder.Configuration.GetSection(PortalOptions.SectionName));
builder.Services.AddOptions<ChaseOptions>().Bind(builder.Configuration.GetSection(ChaseOptions.SectionName));
builder.Services.AddOptions<StorageOptions>().Bind(builder.Configuration.GetSection(StorageOptions.SectionName));

var emailProvider = builder.Configuration["Email:Provider"] ?? (builder.Environment.IsDevelopment() ? "Console" : "Smtp");
var smtpHost = builder.Configuration["Email:SmtpHost"];
if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(smtpHost))
{
    if (builder.Environment.IsDevelopment())
    {
        emailProvider = "Console";
    }
    else
    {
        throw new InvalidOperationException(
            "Email:SmtpHost must be set when Email:Provider is Smtp (or use Console/File in development).");
    }
}

if (emailProvider.Equals("File", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IEmailSender, FileEmailSender>();
}
else if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.ConfigureOptions<ConfigureJwtBearerOptions>();

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<RequireActiveSubscriptionFilter>();
        options.Filters.Add<RequireProFeatureFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SubClear API",
        Version = "v1",
        Description = "UK SaaS subcontractor compliance register for mid-tier main contractors. Tenant isolation is enforced with JWT tenant_id claims, EF Core global query filters, and explicit TenantId checks on writes."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT bearer token from POST /api/auth/login or /api/auth/register.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();
_ = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
_ = app.Services.GetRequiredService<IOptions<SubscriptionApiOptions>>().Value;

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    var seedEnabled = app.Configuration.GetValue("Seed:Enabled", true);
    if (seedEnabled)
    {
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<UserAccount>>();
        await DemoSeeder.SeedAsync(db, hasher);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
