using UnityEngine;

/// <summary>
/// Pixel explosion effect. Spawns small square fragments that fly outward,
/// spin, and fade out. Attach to individual fragment GameObjects.
/// </summary>
public class PixelExplosion : MonoBehaviour
{
    private Vector2 velocity;
    private float angularVelocity;
    private float lifetime;
    private float elapsed;
    private Color startColor;
    private SpriteRenderer sr;

    /// <summary>
    /// Spawn an explosion of pixel fragments at the given position.
    /// </summary>
    public static void Spawn(Vector3 position, Color color, int fragmentCount = 20)
    {
        for (int i = 0; i < fragmentCount; i++)
        {
            CreateFragment(position, color);
        }
    }

    private static void CreateFragment(Vector3 position, Color color)
    {
        GameObject frag = new GameObject("Fragment");
        frag.transform.position = position;

        SpriteRenderer sr = frag.AddComponent<SpriteRenderer>();
        sr.sprite = GameSetup.WhiteSquareSprite;
        sr.color = color;
        sr.sortingOrder = 5;

        float size = Random.Range(0.06f, 0.16f);
        frag.transform.localScale = new Vector3(size, size, 1f);

        PixelExplosion px = frag.AddComponent<PixelExplosion>();
        px.Initialize(
            Random.insideUnitCircle.normalized * Random.Range(3f, 12f),
            Random.Range(-720f, 720f),
            Random.Range(0.4f, 0.9f),
            color
        );
    }

    public void Initialize(Vector2 vel, float angVel, float life, Color col)
    {
        velocity = vel;
        angularVelocity = angVel;
        lifetime = life;
        startColor = col;
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / lifetime;

        // Move
        transform.position += (Vector3)(velocity * Time.deltaTime);

        // Spin
        transform.Rotate(0f, 0f, angularVelocity * Time.deltaTime);

        // Fade out
        Color c = startColor;
        c.a = Mathf.Lerp(1f, 0f, t);
        if (sr != null) sr.color = c;

        // Slow down
        velocity *= 1f - Time.deltaTime * 3f;

        // Destroy when done
        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
