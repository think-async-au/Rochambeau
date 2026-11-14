namespace Rochambeau.Api.Modules.Identity.Domain;

/// <summary>Temporary block after repeated failed logins (AC-06).</summary>
public static class LockoutPolicy
{
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(15);
}

/// <summary>At most this many successful registrations per network address per rolling window (AC-15).</summary>
public static class RegistrationCapPolicy
{
    public const int MaxRegistrations = 5;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(60);
}
