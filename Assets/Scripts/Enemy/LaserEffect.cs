using UnityEngine;

/// <summary>
/// Draws a quick laser beam from the tower (centre) to a target position,
/// then fades out over a short duration. Uses a LineRenderer.
/// </summary>
public class LaserEffect : MonoBehaviour
{
    private LineRenderer lr;
    private float duration;
    private float timer;
    private Color startColor;

    /// <summary>
    /// Spawn a laser beam from origin (0,0,0) to the given world position.
    /// </summary>
    public static void Spawn(Vector3 targetPos, Color color, float duration = 0.15f)
    {
        GameObject obj = new GameObject("Laser");
        LaserEffect laser = obj.AddComponent<LaserEffect>();
        laser.Init(targetPos, color, duration);
    }

    private void Init(Vector3 targetPos, Color color, float dur)
    {
        duration = dur;
        timer = dur;
        startColor = color;

        lr = gameObject.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, Vector3.zero);
        lr.SetPosition(1, targetPos);

        // Use a simple unlit material
        lr.material = new Material(Shader.Find("Sprites/Default"));

        lr.startWidth = 0.08f;
        lr.endWidth = 0.04f;
        lr.startColor = color;
        lr.endColor = color;
        lr.sortingOrder = 10;
        lr.useWorldSpace = true;
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // Fade out
        float alpha = timer / duration;
        Color c = startColor;
        c.a = alpha;
        lr.startColor = c;
        lr.endColor = new Color(c.r, c.g, c.b, alpha * 0.6f);

        // Shrink width as it fades
        lr.startWidth = 0.08f * alpha;
        lr.endWidth = 0.04f * alpha;
    }
}
