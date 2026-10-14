using System;
using UnityEngine;

/// <summary>
/// Controls the in-game clock and drives the ClockUI analog hands (Kim gio, Kim phut).
/// Pure time simulation: Does NOT modify screen colors, lighting, or tileset colors.
/// Features:
/// - 24-hour in-game clock with customizable day duration
/// - Smooth rotation of hour hand (Kim gio) and minute hand (Kim phut)
/// - Clean events for time and day transitions
/// </summary>
public class DayNightClockSystem : MonoBehaviour
{
    public static DayNightClockSystem Instance { get; private set; }

    [Header("Time Settings")]
    [Tooltip("Total real-time seconds for one complete 24-hour in-game day (e.g. 240s = 4 minutes).")]
    [SerializeField] private float dayDurationInSeconds = 240f;

    [Tooltip("Starting hour of the day (0 to 24). e.g. 6 = 6:00 AM.")]
    [Range(0f, 24f)]
    [SerializeField] private float timeOfDay = 6.0f; // 6:00 AM

    [Tooltip("Day counter (Day 1, Day 2...).")]
    [SerializeField] private int currentDay = 1;

    [Tooltip("Pause or resume time progression.")]
    [SerializeField] private bool isTimePaused = false;

    [Header("Clock UI Hand References")]
    [Tooltip("Transform of the hour hand (Kim gio).")]
    [SerializeField] private Transform hourHand;

    [Tooltip("Transform of the minute hand (Kim phut).")]
    [SerializeField] private Transform minuteHand;

    [Tooltip("Initial rotation offset for hour hand if sprite points sideways.")]
    [SerializeField] private float hourHandRotationOffset = 0f;

    [Tooltip("Initial rotation offset for minute hand if sprite points sideways.")]
    [SerializeField] private float minuteHandRotationOffset = 0f;

    [Header("Camera & UI HUD Mode (Optional)")]
    [Tooltip("If true, ClockUI follows the Camera so it stays visible on screen.")]
    [SerializeField] private bool followCamera = false;
    [SerializeField] private Vector3 screenAnchorOffset = new Vector3(7f, 4f, 10f);

    public event Action<int, int> OnTimeChanged; // (hour, minute)
    public event Action<int> OnDayChanged;        // (day)

    private int lastHour = -1;
    private int lastMinute = -1;

    public float TimeOfDay => timeOfDay;
    public int CurrentHour => Mathf.FloorToInt(timeOfDay);
    public int CurrentMinute => Mathf.FloorToInt((timeOfDay - CurrentHour) * 60f);
    public int CurrentDay => currentDay;
    public string FormattedTime => $"{CurrentHour:D2}:{CurrentMinute:D2}";

    private void Awake()
    {
        if (Instance == null) Instance = this;

        // Auto-find hands on children if not assigned
        if (hourHand == null)
        {
            Transform t = transform.Find("Kim gio");
            if (t != null) hourHand = t;
        }

        if (minuteHand == null)
        {
            Transform t = transform.Find("Kim phut");
            if (t != null) minuteHand = t;
        }
    }

    private void Update()
    {
        // 1. Advance in-game time
        if (!isTimePaused && dayDurationInSeconds > 0f)
        {
            float hoursPerSecond = 24f / dayDurationInSeconds;
            timeOfDay += hoursPerSecond * Time.deltaTime;

            if (timeOfDay >= 24f)
            {
                timeOfDay -= 24f;
                currentDay++;
                OnDayChanged?.Invoke(currentDay);
                Debug.Log($"[DayNightSystem] 🌅 Chuyển sang Ngày mới: Ngày {currentDay}!");
            }
        }

        int hour = CurrentHour;
        int minute = CurrentMinute;

        if (hour != lastHour || minute != lastMinute)
        {
            lastHour = hour;
            lastMinute = minute;
            OnTimeChanged?.Invoke(hour, minute);
        }

        // 2. Update Clock hands rotation
        UpdateClockHands();

        // 3. Follow Camera if enabled
        if (followCamera)
        {
            UpdateFollowCamera();
        }
    }

    /// <summary>
    /// Rotates the hour hand and minute hand based on the current in-game time.
    /// In an analog clock:
    /// - Minute hand: 360 degrees in 60 minutes = 6 deg/min.
    /// - Hour hand: 360 degrees in 12 hours = 30 deg/hour (+ 0.5 deg/min).
    /// </summary>
    private void UpdateClockHands()
    {
        float totalMinutes = (timeOfDay * 60f);

        if (minuteHand != null)
        {
            // Clockwise rotation is negative Z in Unity
            float minuteAngle = -(totalMinutes % 60f) * 6f + minuteHandRotationOffset;
            minuteHand.localEulerAngles = new Vector3(0f, 0f, minuteAngle);
        }

        if (hourHand != null)
        {
            float hourAngle = -((timeOfDay % 12f) * 30f) + hourHandRotationOffset;
            hourHand.localEulerAngles = new Vector3(0f, 0f, hourAngle);
        }
    }

    private void UpdateFollowCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            Vector3 targetPos = cam.transform.position + screenAnchorOffset;
            targetPos.z = 0f;
            transform.position = targetPos;
        }
    }

    /// <summary>
    /// Manually set the in-game time (e.g. for testing or sleeping).
    /// </summary>
    public void SetTime(float hour)
    {
        timeOfDay = Mathf.Repeat(hour, 24f);
        UpdateClockHands();
    }
}
