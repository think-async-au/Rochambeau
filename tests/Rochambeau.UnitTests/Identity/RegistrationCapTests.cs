using Microsoft.Extensions.Time.Testing;
using Rochambeau.Api.Modules.Identity.Application;

namespace Rochambeau.UnitTests.Identity;

/// <summary>AC-15: at most 5 successful registrations per network address per rolling 60 minutes.</summary>
public class RegistrationCapTests
{
    private const string _address = "203.0.113.7";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Sixth_registration_within_the_window_is_capped()
    {
        var cap = new RegistrationCap(_time);
        ConfirmMany(cap, _address, 5);

        Assert.Null(cap.TryReserve(_address));
    }

    [Fact]
    public void Five_registrations_are_allowed()
    {
        var cap = new RegistrationCap(_time);
        ConfirmMany(cap, _address, 4);

        Assert.NotNull(cap.TryReserve(_address));
    }

    [Fact]
    public void A_slot_frees_up_when_the_oldest_confirmation_becomes_sixty_minutes_old()
    {
        var cap = new RegistrationCap(_time);
        ConfirmMany(cap, _address, 1);
        _time.Advance(TimeSpan.FromMinutes(10));
        ConfirmMany(cap, _address, 4);

        _time.Advance(TimeSpan.FromMinutes(50) - TimeSpan.FromSeconds(1));
        Assert.Null(cap.TryReserve(_address));

        _time.Advance(TimeSpan.FromSeconds(1));
        var reservation = cap.TryReserve(_address);
        Assert.NotNull(reservation);
        cap.Confirm(reservation);
        Assert.Null(cap.TryReserve(_address));
    }

    [Fact]
    public void Released_reservations_do_not_count()
    {
        var cap = new RegistrationCap(_time);
        for (var i = 0; i < 20; i++)
        {
            cap.Release(cap.TryReserve(_address)!);
        }

        ConfirmMany(cap, _address, 5);
        Assert.Null(cap.TryReserve(_address));
    }

    [Fact]
    public void Pending_reservations_count_against_the_cap()
    {
        var cap = new RegistrationCap(_time);
        ConfirmMany(cap, _address, 3);

        Assert.NotNull(cap.TryReserve(_address));
        Assert.NotNull(cap.TryReserve(_address));
        Assert.Null(cap.TryReserve(_address));
    }

    [Fact]
    public async Task Ten_parallel_reservations_with_four_confirmed_grant_exactly_one()
    {
        var cap = new RegistrationCap(_time);
        ConfirmMany(cap, _address, 4);
        using var start = new ManualResetEventSlim();

        var attempts = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                return cap.TryReserve(_address);
            }))
            .ToArray();
        start.Set();
        var reservations = await Task.WhenAll(attempts);

        Assert.Single(reservations, r => r is not null);
    }

    [Fact]
    public void Addresses_have_independent_windows()
    {
        var cap = new RegistrationCap(_time);
        ConfirmMany(cap, _address, 5);

        Assert.NotNull(cap.TryReserve("2001:db8::1"));
        Assert.Null(cap.TryReserve(_address));
    }

    [Fact]
    public void Confirming_or_releasing_twice_has_no_extra_effect()
    {
        var cap = new RegistrationCap(_time);
        var released = cap.TryReserve(_address)!;
        cap.Release(released);
        cap.Release(released);
        cap.Confirm(released);

        ConfirmMany(cap, _address, 5);
        Assert.Null(cap.TryReserve(_address));

        var other = new RegistrationCap(_time);
        var confirmed = other.TryReserve(_address)!;
        other.Confirm(confirmed);
        other.Confirm(confirmed);
        other.Release(confirmed);
        ConfirmMany(other, _address, 4);
        Assert.Null(other.TryReserve(_address));
    }

    [Fact]
    public void Expired_addresses_are_forgotten()
    {
        var cap = new RegistrationCap(_time);
        ConfirmMany(cap, _address, 2);
        cap.Release(cap.TryReserve("198.51.100.1")!);
        Assert.Equal(1, cap.TrackedAddressCount);

        _time.Advance(TimeSpan.FromMinutes(60));
        cap.Release(cap.TryReserve("198.51.100.2")!);

        Assert.Equal(0, cap.TrackedAddressCount);
    }

    private static void ConfirmMany(RegistrationCap cap, string address, int count)
    {
        for (var i = 0; i < count; i++)
        {
            cap.Confirm(cap.TryReserve(address) ?? throw new InvalidOperationException("unexpectedly capped"));
        }
    }
}
