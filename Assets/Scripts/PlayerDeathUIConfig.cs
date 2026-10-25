using UnityEngine;

/// <summary>Sprites and home entrance offset for the player death screen.</summary>
public sealed class PlayerDeathUIConfig : ScriptableObject
{
    public Sprite youDiedSprite;
    public Sprite replaySprite;
    public Vector3 homeSpawnOffset = new Vector3(0f, 2.04f, 0f);
}
