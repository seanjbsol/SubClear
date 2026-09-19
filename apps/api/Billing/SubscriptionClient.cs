using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SubClear.Api.Billing;

/// <summary>
/// HttpClient for the central QckApp Subscription API. SubClear never talks to Stripe directly.
/// </summary>
public sealed class SubscriptionClient : ISubscriptionClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;
    private readonly SubscriptionApiOptions _options;
    private readonly ILogger<SubscriptionClient> _logger;

    public SubscriptionClient(HttpClient http, IOptions<SubscriptionApiOptions> options, ILogger<SubscriptionClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EntitlementsResult> GetEntitlementsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var product = Uri.EscapeDataString(_options.ProductCode);
        var path = $"api/v1/entitlements/{product}/{tenantId:D}";
        using var response = await SendAsync(HttpMethod.Get, path, null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new EntitlementsResult
            {
                ProductCode = _options.ProductCode,
                TenantId = tenantId,
                Status = "none"
            };
        }

        await EnsureSuccessAsync(response, "entitlements", cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseEntitlements(document.RootElement, tenantId);
    }

    public async Task UpsertTenantAsync(UpsertTenantRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            productCode = _options.ProductCode,
            name = request.Name,
            ownerEmail = request.OwnerEmail,
            externalTenantId = request.ExternalTenantId.ToString("D")
        };

        using var response = await SendAsync(HttpMethod.Put, "api/v1/tenants", payload, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response.Dispose();
            using var created = await SendAsync(HttpMethod.Post, "api/v1/tenants", payload, cancellationToken);
            await EnsureSuccessAsync(created, "tenant upsert", cancellationToken);
            return;
        }

        await EnsureSuccessAsync(response, "tenant upsert", cancellationToken);
    }

    public Task<BillingSessionResult> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSessionAsync(
            "api/v1/checkout/sessions",
            new
            {
                productCode = _options.ProductCode,
                externalTenantId = request.ExternalTenantId.ToString("D"),
                successUrl = request.SuccessUrl,
                cancelUrl = request.CancelUrl
            },
            "checkout session",
            cancellationToken);

    public Task<BillingSessionResult> CreatePortalSessionAsync(
        CreatePortalSessionRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSessionAsync(
            "api/v1/portal/sessions",
            new
            {
                productCode = _options.ProductCode,
                externalTenantId = request.ExternalTenantId.ToString("D"),
                returnUrl = request.ReturnUrl
            },
            "portal session",
            cancellationToken);

    private async Task<BillingSessionResult> CreateSessionAsync(
        string path,
        object payload,
        string operation,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, path, payload, cancellationToken);
        await EnsureSuccessAsync(response, operation, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var url = ReadString(document.RootElement, "url", "checkoutUrl", "portalUrl", "customerPortalUrl");
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new SubscriptionApiException($"Qck Subscription API {operation} did not return a URL.");
        }

        return new BillingSessionResult
        {
            Url = url,
            SessionId = ReadString(document.RootElement, "sessionId", "id")
        };
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        object? payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativePath);
        if (payload is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");
        }

        try
        {
            return await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Qck Subscription API {Method} {Path} failed.", method, relativePath);
            throw new SubscriptionApiException("The Qck Subscription API is unavailable.", statusCode: 503, inner: ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var status = (int)response.StatusCode;
        throw new SubscriptionApiException(
            $"Qck Subscription API {operation} failed ({status}): {TrimBody(body)}",
            statusCode: status >= 500 ? 503 : status);
    }

    private EntitlementsResult ParseEntitlements(JsonElement root, Guid tenantId)
    {
        var source = root;
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("entitlement", out var nested) && nested.ValueKind == JsonValueKind.Object)
            {
                source = nested;
            }
            else if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                source = data;
            }
        }

        var status = ReadString(source, "status", "subscriptionStatus", "state") ?? "none";
        var plan = ReadString(source, "plan", "planCode", "planId");
        var planName = ReadString(source, "planName", "planDisplayName", "name");
        DateTimeOffset? periodEnd = null;
        var periodRaw = ReadString(source, "currentPeriodEnd", "periodEnd", "renewsAt");
        if (DateTimeOffset.TryParse(periodRaw, out var parsed))
        {
            periodEnd = parsed;
        }

        return new EntitlementsResult
        {
            ProductCode = ReadString(source, "productCode") ?? _options.ProductCode,
            TenantId = tenantId,
            Status = status,
            Plan = plan,
            PlanName = planName,
            CurrentPeriodEnd = periodEnd
        };
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var property))
            {
                if (property.ValueKind == JsonValueKind.String)
                {
                    return property.GetString();
                }

                if (property.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                {
                    return property.ToString();
                }
            }
        }

        return null;
    }

    private static string TrimBody(string body) =>
        body.Length <= 400 ? body : body[..400];

    internal static void ConfigureHttpClient(HttpClient http, SubscriptionApiOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            var baseUrl = options.BaseUrl.Trim();
            if (!baseUrl.EndsWith('/'))
            {
                baseUrl += "/";
            }

            http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        }

        http.Timeout = TimeSpan.FromSeconds(15);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            http.DefaultRequestHeaders.Remove("X-Api-Key");
            http.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", options.ApiKey);
        }
    }
}
