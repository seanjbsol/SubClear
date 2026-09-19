namespace SubClear.Api.Billing;

/// <summary>
/// Marks an action or controller that must stay reachable without an active subscription
/// (billing endpoints, /api/me).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipSubscriptionCheckAttribute : Attribute;
