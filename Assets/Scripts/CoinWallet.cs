using System;
using UnityEngine;

/// <summary>Tracks the player's coin balance for the current game session.</summary>
public static class CoinWallet
{
    public static int Balance { get; private set; }
    public static event Action<int> BalanceChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewGameSession()
    {
        Balance = 0;
        BalanceChanged = null;
    }

    public static void Add(int amount)
    {
        if (amount <= 0) return;
        Balance = (int)Math.Min((long)Balance + amount, int.MaxValue);
        BalanceChanged?.Invoke(Balance);
    }

    public static bool TrySpend(int amount)
    {
        if (amount < 0 || Balance < amount) return false;
        Balance -= amount;
        BalanceChanged?.Invoke(Balance);
        return true;
    }

    public static void Restore(int amount)
    {
        Balance = Math.Max(0, amount);
        BalanceChanged?.Invoke(Balance);
    }
}
