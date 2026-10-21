using UnityEngine;

/// <summary>
/// Tự động kiểm tra và fix Physics 2D collision setup khi game khởi động.
/// Đảm bảo MC va chạm đúng với NPC, đồ vật, tường.
///
/// Gắn script này vào bất kỳ GameObject nào trong scene (ví dụ: GameManager).
/// </summary>
public class CollisionSetupFixer : MonoBehaviour
{
    [Header("Auto-fix Settings")]
    [Tooltip("Tự động fix Layer Collision Matrix khi Start?")]
    [SerializeField] private bool autoFixOnStart = true;

    [Tooltip("Ghi log chi tiết ra Console?")]
    [SerializeField] private bool verboseLog = true;

    private void Start()
    {
        if (autoFixOnStart)
        {
            FixAllCollisions();
        }
    }

    /// <summary>
    /// Đảm bảo tất cả layer KHÔNG bị ignore lẫn nhau trong Physics2D.
    /// Sau đó fix tất cả collider trong scene.
    /// </summary>
    [ContextMenu("Fix All Collisions Now")]
    public void FixAllCollisions()
    {
        int fixedLayers = 0;
        int fixedColliders = 0;

        // ===== 1. Fix Layer Collision Matrix =====
        // Lấy layer của MC
        GameObject mc = GameObject.Find("MC");
        int mcLayer = mc != null ? mc.layer : 0;

        if (verboseLog)
            Debug.Log($"[CollisionFixer] MC Layer: {mcLayer} ('{LayerMask.LayerToName(mcLayer)}')");

        // Đảm bảo tất cả các layer có tên đều va chạm với MC layer
        for (int i = 0; i < 32; i++)
        {
            string layerName = LayerMask.LayerToName(i);
            if (string.IsNullOrEmpty(layerName)) continue;

            bool wasIgnored = Physics2D.GetIgnoreLayerCollision(mcLayer, i);

            // Chỉ bỏ ignore — không tạo ignore mới
            if (wasIgnored)
            {
                Physics2D.IgnoreLayerCollision(mcLayer, i, false);
                fixedLayers++;
                Debug.LogWarning($"[CollisionFixer] ✅ Fixed: Layer '{layerName}'({i}) không còn bị ignore với MC layer!");
            }
        }

        // Đảm bảo Default <-> Default không bị ignore
        if (Physics2D.GetIgnoreLayerCollision(0, 0))
        {
            Physics2D.IgnoreLayerCollision(0, 0, false);
            fixedLayers++;
            Debug.LogWarning("[CollisionFixer] ✅ Fixed: Default layer tự va chạm với chính nó.");
        }

        // ===== 2. Kiểm tra tất cả Collider2D trong scene =====
        Collider2D[] allColliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);

        foreach (var col in allColliders)
        {
            if (col == null || col.gameObject == null) continue;

            string objName = col.gameObject.name;

            // Bỏ qua những trigger hợp lệ (teleporter, interaction zone, hitbox, hurtbox...)
            bool isIntentionalTrigger =
                col.gameObject.GetComponent<MapTeleporter>() != null ||
                col.gameObject.GetComponent<Hitbox>() != null ||
                col.gameObject.GetComponent<Hurtbox>() != null ||
                objName.Contains("Trigger") ||
                objName.Contains("Zone") ||
                objName.Contains("Hitbox") ||
                objName.Contains("Hurtbox") ||
                objName.Contains("Sensor") ||
                objName.Contains("FishMinigame") ||
                objName.Contains("Spawner");

            if (isIntentionalTrigger) continue;

            // Vật thể có tên gợi ý là solid wall/NPC/object nhưng isTrigger = true → fix
            bool looksLikeSolidObject =
                objName.Contains("NPC") ||
                objName.Contains("Wall") ||
                objName.Contains("Tree") ||
                objName.Contains("Rock") ||
                objName.Contains("Fence") ||
                objName.Contains("House") ||
                objName.Contains("Building") ||
                objName.Contains("Chef") ||
                objName.Contains("Goblin") ||
                objName.Contains("Customer") ||
                objName.Contains("Farmer") ||
                objName.Contains("Table") ||
                objName.Contains("Counter") ||
                objName.Contains("Box") ||
                objName.Contains("Barrel") ||
                objName.Contains("Door");

            if (looksLikeSolidObject && col.isTrigger)
            {
                if (verboseLog)
                    Debug.LogWarning($"[CollisionFixer] ⚠️ '{objName}' có isTrigger=true nhưng trông như vật thể rắn. Kiểm tra thủ công!");
            }

            // Kiểm tra: collider disabled thì không có tác dụng
            if (!col.enabled && looksLikeSolidObject)
            {
                if (verboseLog)
                    Debug.LogWarning($"[CollisionFixer] ⚠️ '{objName}' có Collider2D bị DISABLED!");
            }
        }

        // ===== 3. Kiểm tra MC cụ thể =====
        if (mc != null)
        {
            Rigidbody2D rb = mc.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                // Đảm bảo không kinematic (kinematic sẽ không phản ứng collision vật lý)
                if (rb.isKinematic)
                {
                    Debug.LogWarning("[CollisionFixer] ⚠️ MC Rigidbody2D là KINEMATIC — đổi về Dynamic để có va chạm vật lý!");
                }

                // Đảm bảo gravityScale = 0 cho top-down
                if (rb.gravityScale != 0f)
                {
                    rb.gravityScale = 0f;
                    Debug.Log("[CollisionFixer] ✅ MC gravityScale reset về 0 (top-down game).");
                }

                // Đảm bảo FreezeRotation
                if ((rb.constraints & RigidbodyConstraints2D.FreezeRotation) == 0)
                {
                    rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
                    Debug.Log("[CollisionFixer] ✅ MC FreezeRotation đã được bật.");
                }

                // Nếu Collision Detection là Discrete và có vẻ cần Continuous
                if (rb.collisionDetectionMode == CollisionDetectionMode2D.Discrete)
                {
                    rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                    Debug.Log("[CollisionFixer] ✅ MC CollisionDetection đổi sang Continuous để tránh xuyên vật thể tốc độ cao.");
                }
            }

            // Kiểm tra MC có BoxCollider2D không
            var mcCol = mc.GetComponent<BoxCollider2D>();
            if (mcCol == null)
            {
                Debug.LogError("[CollisionFixer] ❌ MC THIẾU BoxCollider2D! Thêm BoxCollider2D vào MC ngay!");
            }
            else if (mcCol.isTrigger)
            {
                mcCol.isTrigger = false;
                fixedColliders++;
                Debug.LogWarning("[CollisionFixer] ✅ Fixed: MC BoxCollider2D bị isTrigger=true, đã sửa về false.");
            }
            else
            {
                if (verboseLog)
                    Debug.Log($"[CollisionFixer] ✅ MC BoxCollider2D OK: size={mcCol.size}, isTrigger=false");
            }
        }

        // ===== 4. Summary =====
        Debug.Log($"[CollisionFixer] 🔧 Hoàn thành! Fixed {fixedLayers} layer collisions, {fixedColliders} colliders. Kiểm tra Console để xem chi tiết.");

        // ===== 5. Log toàn bộ Layer Matrix cho MC =====
        if (verboseLog && mc != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"[CollisionFixer] Layer Matrix cho MC (layer {mc.layer}):");
            for (int i = 0; i < 32; i++)
            {
                string lname = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(lname)) continue;
                bool ignored = Physics2D.GetIgnoreLayerCollision(mc.layer, i);
                sb.AppendLine($"  vs '{lname}'({i}): {(ignored ? "IGNORED ❌" : "COLLIDES ✅")}");
            }
            Debug.Log(sb.ToString());
        }
    }
}
