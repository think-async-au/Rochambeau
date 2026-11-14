using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Rochambeau.Api.Modules.Identity.Contracts;
using Rochambeau.Api.Modules.Identity.Domain;

namespace Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;

/// <summary>Maps <see cref="Account"/> to <c>identity.accounts</c> (data-model.md). Index names are matched by application code.</summary>
public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public const string Schema = "identity";
    public const string UsernameIndex = "ix_accounts_normalized_user_name";
    public const string EmailIndex = "ix_accounts_normalized_email";
    public const string RoleCheck = "ck_accounts_role";

    private static readonly ValueConverter<AccountRole, string> _roleConverter = new(
        role => RoleToDb(role),
        value => RoleFromDb(value));

    public void Configure(EntityTypeBuilder<Account> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("accounts", Schema, table =>
            table.HasCheckConstraint(RoleCheck, "role IN ('player', 'administrator')"));

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.UserName).HasMaxLength(Username.MaxLength).IsRequired();
        builder.Property(a => a.NormalizedUserName).HasMaxLength(Username.MaxLength).IsRequired();
        builder.Property(a => a.Email).HasMaxLength(256).IsRequired();
        builder.Property(a => a.NormalizedEmail).HasMaxLength(256).IsRequired();
        builder.Property(a => a.PasswordHash).IsRequired();
        builder.Property(a => a.SecurityStamp).IsRequired();
        builder.Property(a => a.ConcurrencyStamp).IsRequired().IsConcurrencyToken();
        builder.Property(a => a.Role).HasMaxLength(20).HasConversion(_roleConverter).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        builder.HasIndex(a => a.NormalizedUserName).HasDatabaseName(UsernameIndex).IsUnique();
        builder.HasIndex(a => a.NormalizedEmail).HasDatabaseName(EmailIndex).IsUnique();

        builder.Ignore(a => a.PhoneNumber);
        builder.Ignore(a => a.PhoneNumberConfirmed);
    }

    private static string RoleToDb(AccountRole role) => role switch
    {
        AccountRole.Player => "player",
        AccountRole.Administrator => "administrator",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role."),
    };

    private static AccountRole RoleFromDb(string value) => value switch
    {
        "player" => AccountRole.Player,
        "administrator" => AccountRole.Administrator,
        _ => throw new InvalidOperationException($"Unknown role '{value}' in identity.accounts."),
    };
}
