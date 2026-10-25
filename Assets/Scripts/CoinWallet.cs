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
}
