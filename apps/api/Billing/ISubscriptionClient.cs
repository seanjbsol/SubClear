namespace SubClear.Api.Billing;

public interface ISubscriptionClient
{
    Task<EntitlementsResult> GetEntitlementsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task UpsertTenantAsync(UpsertTenantRequest request, CancellationToken cancellationToken = default);

    Task<BillingSessionResult> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<BillingSessionResult> CreatePortalSessionAsync(
        CreatePortalSessionRequest request,
        CancellationToken cancellationToken = default);
}
