using UnityEngine;

namespace SYSTEM.Debug
{
    /// <summary>
    /// Debug component to test collision detection with world sphere (both prefab and procedural)
    /// </summary>
    public class WorldCollisionTester : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private bool enableTesting = true;
        [SerializeField] private float raycastDistance = 500f;
        [SerializeField] private LayerMask worldLayer = -1; // All layers by default
        
        [Header("Test Objects")]
        [SerializeField] private GameObject testProjectilePrefab;
        [SerializeField] private float projectileSpeed = 50f;
        [SerializeField] private KeyCode fireProjectileKey = KeyCode.F;
        
        [Header("Visual Debug")]
        [SerializeField] private bool showDebugRays = true;
        [SerializeField] private float debugRayDuration = 1f;
        [SerializeField] private Color hitColor = Color.green;
        [SerializeField] private Color missColor = Color.red;
        
        // References
        private WorldController centerWorldController;
        private Game.PrefabWorldController prefabWorldController;
        private Camera playerCamera;
        
        // Test results
        private int totalRaycasts = 0;
        private int successfulHits = 0;
        private float lastHitDistance = 0f;
        private Vector3 lastHitPoint;
        private Vector3 lastHitNormal;
        private string lastHitObjectName = "";
        
        void Start()
        {
            // Find world controllers
            centerWorldController = FindFirstObjectByType<WorldController>();
            prefabWorldController = FindFirstObjectByType<Game.PrefabWorldController>();
            
            // Find camera
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                playerCamera = FindFirstObjectByType<Camera>();
            }
            
            if (centerWorldController != null)
            {
                SystemDebug.Log(SystemDebug.Category.Performance, $"[WorldCollisionTester] Found WorldController with radius: {centerWorldController.Radius}");
            }
            if (prefabWorldController != null)
            {
                SystemDebug.Log(SystemDebug.Category.Performance, $"[WorldCollisionTester] Found PrefabWorldController with radius: {prefabWorldController.Radius}");
            }

            if (centerWorldController == null && prefabWorldController == null)
            {
                SystemDebug.LogWarning(SystemDebug.Category.Performance, "[WorldCollisionTester] No world controller found - collision testing may not work properly");
            }
        }
        
        void Update()
        {
            if (!enableTesting) return;
            
            // Test raycast on mouse click
            if (Input.GetMouseButtonDown(0))
            {
                TestRaycastFromCamera();
            }
            
            // Fire test projectile
            if (Input.GetKeyDown(fireProjectileKey))
            {
                FireTestProjectile();
            }
            
            // Test downward raycast from player position
            if (Input.GetKeyDown(KeyCode.G))
            {
                TestGroundRaycast();
            }
        }
        
        void TestRaycastFromCamera()
        {
            if (playerCamera == null) return;
            
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            totalRaycasts++;
            
            if (Physics.Raycast(ray, out hit, raycastDistance, worldLayer))
            {
                successfulHits++;
                lastHitDistance = hit.distance;
                lastHitPoint = hit.point;
                lastHitNormal = hit.normal;
                lastHitObjectName = hit.collider.gameObject.name;
                
                SystemDebug.Log(SystemDebug.Category.Performance, $"[WorldCollisionTester] HIT: {lastHitObjectName} at distance {lastHitDistance:F2}");
                SystemDebug.Log(SystemDebug.Category.Performance, $"[WorldCollisionTester] Hit Point: {lastHitPoint}, Normal: {lastHitNormal}");
                SystemDebug.Log(SystemDebug.Category.Performance, $"[WorldCollisionTester] Collider Type: {hit.collider.GetType().Name}");
                
                // Verify this is actually the world sphere
                bool isWorldSphere = hit.collider.name.Contains("WorldSphere") || 
                                   hit.collider.transform.parent?.GetComponent<WorldController>() != null ||
                                   hit.collider.transform.parent?.GetComponent<Game.PrefabWorldController>() != null;
                
                if (isWorldSphere)
                {
                    SystemDebug.Log(SystemDebug.Category.Performance, $"[WorldCollisionTester] Confirmed world sphere collision!");

                    // Test surface point calculation
                    float expectedRadius = GetWorldRadius();
                    float actualDistance = hit.point.magnitude;
                    float error = Mathf.Abs(actualDistance - expectedRadius);
                    SystemDebug.Log(SystemDebug.Category.Performance, $"[WorldCollisionTester] Surface accuracy: Expected radius {expectedRadius:F2}, Got {actualDistance:F2}, Error: {error:F2}");
                }
                
                if (showDebugRays)
                {
                    UnityEngine.Debug.DrawRay(ray.origin, ray.direction * hit.distance, hitColor, debugRayDuration);
                    UnityEngine.Debug.DrawRay(hit.point, hit.normal * 10f, Color.blue, debugRayDuration);
                }
            }
            else
            {
                SystemDebug.Log(SystemDebug.Category.Performance, $"[WorldCollisionTester] MISS: No collision detected");

                if (showDebugRays)
                {
                    UnityEngine.Debug.DrawRay(ray.origin, ray.direction * raycastDistance, missColor, debugRayDuration);
                }
            }

            SystemDebug.Log(SystemDebug.Category.Performance, $"[WorldCollisionTester] Hit Rate: {successfulHits}/{totalRaycasts} ({(float)successfulHits/totalRaycasts*100:F1}%)");
        }
        
        void TestGroundRaycast()
        {
            Vector3 origin = transform.position + Vector3.up * 10f;
            Ray ray = new Ray(origin, -transform.up);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, raycastDistance, worldLayer))
            {
                SystemDebug.Log(SystemDebug.Category.Performance, $"[WorldCollisionTester] Ground check HIT at distance: {hit.distance:F2}");
                SystemDebug.Log(SystemDebug.Category.Performance, $"[WorldCollisionTester] Player height above surface: {hit.distance - 10f:F2}");

                if (showDebugRays)
                {
                    UnityEngine.Debug.DrawRay(origin, -transform.up * hit.distance, hitColor, debugRayDuration);
                }
            }
            else
            {
                SystemDebug.Log(SystemDebug.Category.Performance, $"[WorldCollisionTester] Ground check MISS - player may be floating!");

                if (showDebugRays)
                {
                    UnityEngine.Debug.DrawRay(origin, -transform.up * raycastDistance, missColor, debugRayDuration);
                }
            }
        }
        
        void FireTestProjectile()
        {
            if (playerCamera == null) return;
            
            GameObject projectile;
            if (testProjectilePrefab != null)
            {
                projectile = Instantiate(testProjectilePrefab, playerCamera.transform.position, playerCamera.transform.rotation);
            }
            else
            {
                // Create simple sphere projectile
                projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                projectile.transform.position = playerCamera.transform.position;
                projectile.transform.localScale = Vector3.one * 0.5f;
                projectile.GetComponent<Renderer>().material.color = Color.yellow;
            }
            
            projectile.name = "TestProjectile";
            
            // Add physics
            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = projectile.AddComponent<Rigidbody>();
            }
            rb.useGravity = false;
            rb.linearVelocity = playerCamera.transform.forward * projectileSpeed;
            
            // Add collision detection script
            var detector = projectile.AddComponent<CollisionDetector>();
            detector.worldTester = this;
            
            // Auto-destroy after 10 seconds
            Destroy(projectile, 10f);
            
            SystemDebug.Log(SystemDebug.Category.Performance, "[WorldCollisionTester] Fired test projectile");
        }
        
        float GetWorldRadius()
        {
            if (centerWorldController != null)
                return centerWorldController.Radius;
            if (prefabWorldController != null)
                return prefabWorldController.Radius;
            return 300f; // Default
        }
        
        public void ReportCollision(string objectName, Vector3 point, Vector3 normal)
        {
            SystemDebug.Log(SystemDebug.Category.Performance, $"[WorldCollisionTester] COLLISION DETECTED: {objectName}");
            SystemDebug.Log(SystemDebug.Category.Performance, $"[WorldCollisionTester] Impact point: {point}, Normal: {normal}");
            SystemDebug.Log(SystemDebug.Category.Performance, $"[WorldCollisionTester] Distance from center: {point.magnitude:F2}");

            float expectedRadius = GetWorldRadius();
            float error = Mathf.Abs(point.magnitude - expectedRadius);
            SystemDebug.Log(SystemDebug.Category.Performance, $"[WorldCollisionTester] Collision accuracy: Error from expected radius: {error:F2}");
        }
        
        void OnGUI()
        {
            if (!enableTesting) return;
            
            int y = 100;
            GUI.Label(new Rect(10, y, 400, 20), "=== World Collision Tester ===");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), $"World Type: {(centerWorldController != null ? "CenterWorld" : prefabWorldController != null ? "PrefabWorld" : "None")}");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), $"World Radius: {GetWorldRadius():F1}");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), $"Raycasts: {successfulHits}/{totalRaycasts}");
            y += 20;
            if (successfulHits > 0)
            {
                GUI.Label(new Rect(10, y, 400, 20), $"Last Hit: {lastHitObjectName} at {lastHitDistance:F1}m");
                y += 20;
            }
            GUI.Label(new Rect(10, y, 400, 20), "Controls: Left Click = Raycast, F = Fire Projectile, G = Ground Check");
        }
        
        // Helper class for projectile collision detection
        class CollisionDetector : MonoBehaviour
        {
            public WorldCollisionTester worldTester;
            
            void OnCollisionEnter(Collision collision)
            {
                if (worldTester != null)
                {
                    ContactPoint contact = collision.contacts[0];
                    worldTester.ReportCollision(collision.gameObject.name, contact.point, contact.normal);
                }
                
                // Visual feedback
                GetComponent<Renderer>().material.color = Color.red;
                Destroy(gameObject, 1f);
            }
        }
    }
}