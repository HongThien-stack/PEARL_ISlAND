using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows the current coin balance in the top-right corner of gameplay.</summary>
public sealed class CoinHud : MonoBehaviour
{
    private Text amountText;

    public void Configure(Sprite coinIcon)
    {
        GameObject canvasObject = new GameObject("Coin Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;

        GameObject backgroundObject = new GameObject("Coin Balance", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = backgroundRect.anchorMax = Vector2.one;
        backgroundRect.pivot = Vector2.one;
        // Leave room for the Settings button at the upper-right corner.
        backgroundRect.anchoredPosition = new Vector2(-112f, -20f);
        backgroundRect.sizeDelta = new Vector2(150f, 42f);
        Image background = backgroundObject.GetComponent<Image>();
        background.color = new Color(.18f, .10f, .06f, .84f);
        background.raycastTarget = false;

        GameObject iconObject = new GameObject("Gold Coin", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(backgroundObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, .5f);
        iconRect.anchoredPosition = new Vector2(24f, 0f);
        iconRect.sizeDelta = new Vector2(30f, 30f);
        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = coinIcon;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        GameObject amountObject = new GameObject("Coin Count", typeof(RectTransform), typeof(Text));
        amountObject.transform.SetParent(backgroundObject.transform, false);
        RectTransform amountRect = amountObject.GetComponent<RectTransform>();
        amountRect.anchorMin = amountRect.anchorMax = new Vector2(.5f, .5f);
        amountRect.anchoredPosition = new Vector2(20f, 0f);
        amountRect.sizeDelta = new Vector2(96f, 36f);
        amountText = amountObject.GetComponent<Text>();
        amountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        amountText.fontSize = 21;
        amountText.fontStyle = FontStyle.Bold;
        amountText.alignment = TextAnchor.MiddleRight;
        amountText.color = new Color(1f, .91f, .7f);
        amountText.raycastTarget = false;
        UpdateBalance(CoinWallet.Balance);
        CoinWallet.BalanceChanged += UpdateBalance;
    }

    private void OnDestroy()
    {
        CoinWallet.BalanceChanged -= UpdateBalance;
    }

    private void UpdateBalance(int balance)
    {
        if (amountText != null) amountText.text = balance.ToString("N0");
    }
}
