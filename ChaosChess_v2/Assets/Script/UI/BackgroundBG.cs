using UnityEngine;

public class BackgroundBG : MonoBehaviour
{
    [SerializeField] private Material material;
    [SerializeField] private float baseTileX = 0.85f;
    [SerializeField] private float baseTileY = 2f;
    [SerializeField] private float referenceAspect = 9f / 16f;

    private RectTransform _rect;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    private void Start()
    {
        UpdateTileOffset();
    }

    private void OnRectTransformDimensionsChange()
    {
        UpdateTileOffset();
    }

    private void UpdateTileOffset()
    {
        if (material == null || _rect == null) return;
        Rect r = _rect.rect;
        if (r.width <= 0 || r.height <= 0) return;

        float currentAspect = r.width / r.height;
        float tileX = baseTileX * (currentAspect / referenceAspect);
        material.SetVector("_TileOffset", new Vector4(tileX, baseTileY, 0f, 0f));
    }
}
