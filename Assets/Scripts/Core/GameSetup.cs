using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bootstrap script — attach this to a single empty GameObject in the scene.
/// It configures the camera, creates the tower, and spawns all manager objects
/// so the game runs with zero manual scene setup.
/// </summary>
public class GameSetup : MonoBehaviour
{
    /// <summary>
    /// Shared 1×1 white square sprite used for all primitive visuals.
    /// </summary>
    public static Sprite WhiteSquareSprite { get; private set; }

    void Awake()
    {
        // ── Create the shared white-pixel sprite ─────────────────────
        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        WhiteSquareSprite = Sprite.Create(
            tex,
            new Rect(0, 0, 4, 4),
            new Vector2(0.5f, 0.5f),
            4f);       // 4 pixels-per-unit → 1 world-unit square

        // ── Configure the main camera ────────────────────────────────
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.backgroundColor = Color.black;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.transform.rotation = Quaternion.identity;

        // ── Create the player tower (white square at centre) ─────────
        GameObject tower = new GameObject("Tower");
        SpriteRenderer towerSr = tower.AddComponent<SpriteRenderer>();
        towerSr.sprite = WhiteSquareSprite;
        towerSr.color = Color.white;
        towerSr.sortingOrder = 0;
        tower.transform.position = Vector3.zero;
        tower.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

        // ── Spawn manager GameObjects ────────────────────────────────
        GameObject gm = new GameObject("GameManager");
        gm.AddComponent<GameManager>();

        GameObject spawner = new GameObject("EnemySpawner");
        spawner.AddComponent<EnemySpawner>();

        GameObject typing = new GameObject("TypingManager");
        typing.AddComponent<TypingManager>();

        // ── HUD Canvas ───────────────────────────────────────────────
        GameObject canvasObj = new GameObject("HUDCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        canvasObj.AddComponent<HUDManager>();

        // ── LLM & Roleplay managers ──────────────────────────────────
        GameObject llamaObj = new GameObject("LlamaServerManager");
        llamaObj.AddComponent<LlamaServerManager>();

        GameObject llmGenObj = new GameObject("LLMWordGenerator");
        llmGenObj.AddComponent<LLMWordGenerator>();

        GameObject roleplayObj = new GameObject("RoleplayManager");
        roleplayObj.AddComponent<RoleplayManager>();

        GameObject learnedWordsObj = new GameObject("LearnedWordsManager");
        learnedWordsObj.AddComponent<LearnedWordsManager>();

        // ── Ensure an EventSystem exists (needed for button clicks) ──
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }
}
