using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MazeCamera : MonoBehaviour
{
    const float TerrainPixelsPerUnit = 64;
    [SerializeField, Min(0.5f)] float viewSize = 2.5f;
    Camera sceneCamera;
    PlayerController hero;
    Material effects;
    Transform target;
    Vector2 focus;

    void Awake()
    {
        sceneCamera = GetComponent<Camera>();
        sceneCamera.orthographic = true;
        sceneCamera.orthographicSize = viewSize;
        sceneCamera.nearClipPlane = 0.1f;
        sceneCamera.farClipPlane = 30;
        sceneCamera.allowHDR = false;
        sceneCamera.allowMSAA = false;
        sceneCamera.transparencySortMode = TransparencySortMode.CustomAxis;
        sceneCamera.transparencySortAxis = Vector3.up;
        sceneCamera.backgroundColor = new Color(0.025f, 0.025f, 0.03f);
        focus = transform.position;
    }

    public void Follow(Transform subject)
    {
        target = subject;
        LateUpdate();
    }

    public void SetEffectSource(PlayerController player)
    {
        hero = player;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (hero != null && !hero.Dead && (hero.FearActive || hero.Empowered))
        {
            if (effects == null)
            {
                Shader shader = Resources.Load<Shader>("Shaders/CombatScreen");
                if (shader != null && shader.isSupported) effects = new Material(shader);
            }
            if (effects != null)
            {
                effects.SetFloat("_Fear", hero.FearActive ? 1 : 0);
                effects.SetFloat("_Rage", hero.Empowered && !hero.FearActive ? 1 : 0);
                Graphics.Blit(source, destination, effects);
                return;
            }
        }
        Graphics.Blit(source, destination);
    }

    void OnDestroy()
    {
        if (effects != null) Destroy(effects);
    }

    public void LookAt(Vector2 position)
    {
        target = null;
        focus = position;
        LateUpdate();
    }

    void LateUpdate()
    {
        if (sceneCamera.pixelHeight <= 0) return;
        float zoom = Mathf.Max(1, Mathf.Round(sceneCamera.pixelHeight / (2 * viewSize * TerrainPixelsPerUnit)));
        float pixelsPerUnit = TerrainPixelsPerUnit * zoom;
        sceneCamera.orthographicSize = sceneCamera.pixelHeight / (2 * pixelsPerUnit);
        if (target != null) focus = target.position;
        transform.position = new Vector3(
            Mathf.Round(focus.x * pixelsPerUnit) / pixelsPerUnit,
            Mathf.Round(focus.y * pixelsPerUnit) / pixelsPerUnit, -10);
    }
}
