using UnityEngine;

/// <summary>Loops the sound of the animal pen nearest to the player.</summary>
public sealed class AnimalPenAudio : MonoBehaviour
{
    [System.Serializable]
    private struct PenSound
    {
        public Transform pen;
        public Vector2 localCenter;
        public AudioClip clip;
    }

    [SerializeField] private PenSound cow;
    [SerializeField] private PenSound chicken;
    [SerializeField] private PenSound pig;
    [SerializeField, Min(0f)] private float hearingDistance = 11f;
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

    private AudioSource source;

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = volume * GameSettings.EffectsVolume;
    }

    private void Update()
    {
        source.volume = volume * GameSettings.EffectsVolume;
        float closestSqrDistance = hearingDistance * hearingDistance;
        AudioClip closestClip = null;

        CheckPen(cow, ref closestSqrDistance, ref closestClip);
        CheckPen(chicken, ref closestSqrDistance, ref closestClip);
        CheckPen(pig, ref closestSqrDistance, ref closestClip);

        if (source.clip != closestClip)
        {
            source.Stop();
            source.clip = closestClip;
        }

        if (closestClip != null && !source.isPlaying)
            source.Play();
    }

    private void CheckPen(PenSound sound, ref float closestSqrDistance, ref AudioClip closestClip)
    {
        if (sound.pen == null || sound.clip == null)
            return;

        Vector2 penCenter = (Vector2)sound.pen.TransformPoint(sound.localCenter);
        float sqrDistance = ((Vector2)transform.position - penCenter).sqrMagnitude;
        if (sqrDistance > closestSqrDistance)
            return;

        closestSqrDistance = sqrDistance;
        closestClip = sound.clip;
    }
}
