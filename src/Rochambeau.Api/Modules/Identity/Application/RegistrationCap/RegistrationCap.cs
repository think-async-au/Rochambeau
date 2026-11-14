using Rochambeau.Api.Modules.Identity.Domain;

namespace Rochambeau.Api.Modules.Identity.Application;

/// <summary>
/// In-process sliding window of <b>successful</b> registrations per network address (AC-15, SAD §8).
/// Reserving a slot is atomic and happens before the insert; pending reservations count, so parallel
/// requests cannot exceed the cap. A failed registration releases its slot, so failures never count.
/// Addresses live only in memory and only while they have activity inside the window (spec §6.1).
/// One instance per process: move to a shared store under an ADR before a second instance (SAD §11).
/// </summary>
public sealed class RegistrationCap(TimeProvider time)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, AddressWindow> _windows = new(StringComparer.Ordinal);

    /// <summary>Number of addresses currently held in memory.</summary>
    public int TrackedAddressCount
    {
        get
        {
            lock (_lock)
            {
                return _windows.Count;
            }
        }
    }

    /// <summary>Reserves a slot for <paramref name="addressKey"/>, or returns <see langword="null"/> when the cap is reached.</summary>
    public RegistrationReservation? TryReserve(string addressKey)
    {
        ArgumentNullException.ThrowIfNull(addressKey);

        lock (_lock)
        {
            var now = time.GetUtcNow();
            PruneAll(now);

            if (!_windows.TryGetValue(addressKey, out var window))
            {
                window = new AddressWindow();
                _windows[addressKey] = window;
            }

            if (window.Confirmed.Count + window.Pending >= RegistrationCapPolicy.MaxRegistrations)
            {
                return null;
            }

            window.Pending++;
            return new RegistrationReservation(addressKey);
        }
    }

    /// <summary>The registration succeeded: the slot counts for the next 60 minutes.</summary>
    public void Confirm(RegistrationReservation reservation) => Settle(reservation, confirmed: true);

    /// <summary>The registration failed: the slot is given back.</summary>
    public void Release(RegistrationReservation reservation) => Settle(reservation, confirmed: false);

    private void Settle(RegistrationReservation reservation, bool confirmed)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        lock (_lock)
        {
            if (reservation.Settled || !_windows.TryGetValue(reservation.AddressKey, out var window))
            {
                return;
            }

            reservation.Settled = true;
            window.Pending--;
            if (confirmed)
            {
                window.Confirmed.Enqueue(time.GetUtcNow());
            }
            else if (window.IsEmpty)
            {
                _windows.Remove(reservation.AddressKey);
            }
        }
    }

    private void PruneAll(DateTimeOffset now)
    {
        var cutoff = now - RegistrationCapPolicy.Window;
        List<string>? empty = null;
        foreach (var (key, window) in _windows)
        {
            while (window.Confirmed.TryPeek(out var oldest) && oldest <= cutoff)
            {
                window.Confirmed.Dequeue();
            }

            if (window.IsEmpty)
            {
                (empty ??= []).Add(key);
            }
        }

        empty?.ForEach(key => _windows.Remove(key));
    }

    private sealed class AddressWindow
    {
        public Queue<DateTimeOffset> Confirmed { get; } = new();

        public int Pending { get; set; }

        public bool IsEmpty => Pending == 0 && Confirmed.Count == 0;
    }
}

/// <summary>A reserved registration-cap slot; settle it exactly once with Confirm or Release.</summary>
public sealed class RegistrationReservation
{
    internal RegistrationReservation(string addressKey) => AddressKey = addressKey;

    internal string AddressKey { get; }

    internal bool Settled { get; set; }
}
