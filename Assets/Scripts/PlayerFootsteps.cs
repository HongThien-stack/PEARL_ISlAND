using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Loops the matching footstep recording only while the player is actually moving.</summary>
[RequireComponent(typeof(PlayerMovement), typeof(Rigidbody2D))]
public sealed class PlayerFootsteps : MonoBehaviour
{
    [SerializeField] private AudioClip outdoorWalkGrass;
    [SerializeField] private AudioClip outdoorWalkMixedTerrain;
    [SerializeField] private AudioClip outdoorRunGrass;
    [SerializeField] private AudioClip indoorSteps;
    [SerializeField] private bool indoors = true;
    [Range(0f, 1f)] [SerializeField] private float volume = 1f;

    private PlayerMovement movement;
    private Rigidbody2D body;
    private AudioSource source;
    private Tilemap ground;
    private AudioClip pendingTerrainClip;
    private float pendingTerrainSince;

    public bool IsIndoors => indoors;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        body = GetComponent<Rigidbody2D>();
        GameObject groundObject = GameObject.Find("PAINT HERE - Y0 - GROUND - Terrain Map");
        if (groundObject != null) ground = groundObject.GetComponent<Tilemap>();
        GameObject audioObject = new GameObject("Footstep Audio");
        audioObject.transform.SetParent(transform, false);
        source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
    }

    private void Update()
    {
        bool moving = movement != null && movement.enabled && Time.timeScale > 0f &&
            !MovementLock.IsLocked && !QuestWindowUI.IsOpen && !VisualNovelDialogueUI.IsOpen &&
            body != null && body.linearVelocity.sqrMagnitude > 0.01f;
        if (!moving)
        {
            if (source.isPlaying) source.Stop();
            pendingTerrainClip = null;
            return;
        }

        bool running = movement.IsSprinting;
        AudioClip clip = indoors ? indoorSteps : running ? outdoorRunGrass :
            IsMixedTerrain() ? outdoorWalkMixedTerrain : outdoorWalkGrass;
        if (clip == null) clip = outdoorWalkGrass;
        if (clip == null)
        {
            if (source.isPlaying) source.Stop();
            return;
        }

        // Edge tiles may alternate between grass and road every frame. Let the new
        // terrain remain underfoot briefly before restarting a long recording.
        if (!indoors && !running && source.isPlaying && source.clip != clip)
        {
            if (pendingTerrainClip != clip)
            {
                pendingTerrainClip = clip;
                pendingTerrainSince = Time.time;
            }
            if (Time.time - pendingTerrainSince < 0.5f) clip = source.clip;
        }
        else if (source.clip == clip)
        {
            pendingTerrainClip = null;
        }

        if (source.clip != clip || !source.isPlaying)
        {
            source.Stop();
            source.clip = clip;
            source.Play();
        }
        source.pitch = indoors && running ? 1.35f : 1f;
        source.volume = volume * GameSettings.EffectsVolume;
    }

    private bool IsMixedTerrain()
    {
        if (ground == null) return false;
        Sprite sprite = ground.GetSprite(ground.WorldToCell(transform.position + Vector3.down * 0.25f));
        if (sprite == null || sprite.texture == null) return false;
        string textureName = sprite.texture.name.ToLowerInvariant();
        return textureName.Contains("dirt") || textureName.Contains("road") ||
            textureName.Contains("path") || textureName.Contains("gravel") ||
            textureName.Contains("stone");
    }

    public void SetIndoors(bool value)
    {
        if (indoors == value) return;
        indoors = value;
        if (source != null) source.Stop();
    }

    private void OnDisable()
    {
        if (source != null) source.Stop();
    }
}
