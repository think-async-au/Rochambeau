using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Rochambeau.Api.Common.Persistence;
using Rochambeau.Api.Modules.Identity.Domain;
using Rochambeau.Api.Modules.Identity.Infrastructure.Persistence;
using Rochambeau.IntegrationTests.Infrastructure;

namespace Rochambeau.IntegrationTests.Identity;

/// <summary>The identity.accounts schema: race guards (AC-03, AC-03b) and the role CHECK (accounts ADR-0003).</summary>
[Collection(PostgresTestGroup.Name)]
public sealed class AccountsSchemaTests(PostgresFixture postgres)
{
    // Booting the host in Development applies every migration (Program.cs).

    [Fact]
    public async Task Usernames_equal_ignoring_case_violate_the_named_unique_index()
    {
        await using var factory = new ApiFactory(postgres);
        _ = factory.Services;
        var name = Unique("Paper");
        await InsertRawAsync(name, $"{Unique("a")}@example.test");

        var ex = await Assert.ThrowsAsync<PostgresException>(() => InsertRawAsync(name.ToLowerInvariant(), $"{Unique("b")}@example.test"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
        Assert.Equal("ix_accounts_normalized_user_name", ex.ConstraintName);
    }

    [Fact]
    public async Task Emails_equal_ignoring_case_violate_the_named_unique_index()
    {
        await using var factory = new ApiFactory(postgres);
        _ = factory.Services;
        var email = $"{Unique("Same")}@Example.test";
        await InsertRawAsync(Unique("u"), email);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => InsertRawAsync(Unique("v"), email.ToUpperInvariant()));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
        Assert.Equal("ix_accounts_normalized_email", ex.ConstraintName);
    }

    [Fact]
    public async Task Role_outside_the_set_violates_the_check()
    {
        await using var factory = new ApiFactory(postgres);
        _ = factory.Services;
        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => InsertRawAsync(Unique("m"), $"{Unique("m")}@example.test", role: "moderator"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal("ck_accounts_role", ex.ConstraintName);
    }

    [Fact]
    public async Task Account_maps_its_role_in_lower_case_and_round_trips()
    {
        await using var factory = new ApiFactory(postgres);
        _ = factory.Services;
        var id = Guid.CreateVersion7();
        var createdAt = new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);
        var userName = Unique("Rocky");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<Account>().Add(new Account
            {
                Id = id,
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                Email = $"{userName}@example.test",
                NormalizedEmail = $"{userName}@EXAMPLE.TEST".ToUpperInvariant(),
                PasswordHash = "hash",
                SecurityStamp = "stamp",
                ConcurrencyStamp = "concurrency",
                Role = AccountRole.Administrator,
                CreatedAt = createdAt,
            });
            await db.SaveChangesAsync();
        }

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT role FROM identity.accounts WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        Assert.Equal("administrator", await command.ExecuteScalarAsync());

        await using var readScope = factory.Services.CreateAsyncScope();
        var stored = await readScope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Account>().SingleAsync(a => a.Id == id);
        Assert.Equal(AccountRole.Administrator, stored.Role);
        Assert.Equal(createdAt, stored.CreatedAt);
    }

    [Fact]
    public async Task Migrations_revert_to_zero_without_leaving_the_identity_schema_and_reapply()
    {
        await using var factory = new ApiFactory(postgres);
        _ = factory.Services;
        await using var scope = factory.Services.CreateAsyncScope();
        var migrator = scope.ServiceProvider.GetRequiredService<AppDbContext>().GetService<IMigrator>();

        await migrator.MigrateAsync("0");
        Assert.Equal(0L, await CountIdentitySchemasAsync());

        await migrator.MigrateAsync();
        Assert.Equal(1L, await CountIdentitySchemasAsync());
    }

    private async Task<object?> CountIdentitySchemasAsync()
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'identity'", connection);
        return await command.ExecuteScalarAsync();
    }

    private async Task InsertRawAsync(string userName, string email, string role = "player")
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO identity.accounts (id, user_name, normalized_user_name, email, normalized_email,
                email_confirmed, password_hash, security_stamp, concurrency_stamp, two_factor_enabled,
                lockout_enabled, lockout_end, access_failed_count, role, created_at)
            VALUES (@id, @u, upper(@u), @e, upper(@e), false, 'x', 's', 'c', false, true, NULL, 0, @role, now())
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("u", userName);
        command.Parameters.AddWithValue("e", email);
        command.Parameters.AddWithValue("role", role);
        await command.ExecuteNonQueryAsync();
    }

    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..Math.Min(20, prefix.Length + 12)];
}
