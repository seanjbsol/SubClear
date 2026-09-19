namespace SubClear.Api.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Console, File, or Smtp. Development defaults to Console.</summary>
    public string Provider { get; set; } = "Console";

    public string FromName { get; set; } = "SubClear";

    public string FromAddress { get; set; } = "notices@subclear.uk";

    /// <summary>Directory for File provider (one .txt per send).</summary>
    public string FileDirectory { get; set; } = "emails";

    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;

    public string? SmtpUser { get; set; }

    public string? SmtpPassword { get; set; }

    public bool SmtpEnableSsl { get; set; } = true;
}

public sealed class PortalOptions
{
    public const string SectionName = "Portal";

    public int TokenLifetimeHours { get; set; } = 168;

    /// <summary>Public origin used in invite emails, e.g. https://app.subclear.uk (no trailing slash).</summary>
    public string? PublicBaseUrl { get; set; }
}

public sealed class ChaseOptions
{
    public const string SectionName = "Chase";

    public int DefaultCadenceDays { get; set; } = 7;

    public int MinCadenceDays { get; set; } = 3;

    public int MaxCadenceDays { get; set; } = 28;

    public bool BackgroundEnabled { get; set; }

    public int IntervalHours { get; set; } = 6;
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string RootPath { get; set; } = "App_Data/uploads";
}
