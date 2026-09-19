namespace SubClear.Api.Billing;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireProFeatureAttribute : Attribute
{
    public RequireProFeatureAttribute(ProFeature feature)
    {
        Feature = feature;
    }

    public ProFeature Feature { get; }
}
