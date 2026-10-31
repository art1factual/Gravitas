
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class SurfaceGrid : MonoBehaviour
{

    [Range(2, 64)]
    public int cells = 20;

    [Range(1, 8)]
    public int lineThickness = 2;

    public Color lineColor = new Color(0.4f, 1f, 0.8f);

    public Color backgroundColor = new Color(0.025f, 0.05f, 0.07f);

    const int PixelsPerCell = 32;

    Texture2D gridTexture;

    Material gridMaterial;
    Material originalMaterial;
    MeshRenderer surfaceRenderer;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        surfaceRenderer = GetComponent<MeshRenderer>();
        originalMaterial = surfaceRenderer.sharedMaterial;

        if(originalMaterial == null)
        {
            Debug.LogError("Assign a material first.", this);
            return;
        }

        cells = Mathf.Clamp(cells, 2, 64);
        lineThickness = Mathf.Clamp(lineThickness, 1, 8);

        int size = cells * PixelsPerCell;

        gridTexture = new Texture2D(size, size, TextureFormat.RGBA32, true);

        gridTexture.name = "Generated Surfaced Grid";

        gridTexture.wrapMode = TextureWrapMode.Repeat;
        gridTexture.filterMode = FilterMode.Trilinear;
        gridTexture.anisoLevel = 8;
        
        Color32[] pixels = new Color32[size * size];

        Color32 line = lineColor;
        Color32 background = backgroundColor;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool onLine = x % PixelsPerCell < lineThickness || y % PixelsPerCell < lineThickness;

                pixels[y * size + x] = onLine ? line : background;
            }
        }

        gridTexture.SetPixels32(pixels);

        gridTexture.Apply(true, true);

        gridMaterial = new Material(originalMaterial);
        gridMaterial.name = "Surface Grid Material";

        gridMaterial.mainTexture = gridTexture;
        gridMaterial.mainTextureScale = Vector2.one;
        gridMaterial.mainTextureOffset = Vector2.zero;

        surfaceRenderer.sharedMaterial = gridMaterial;
    
    
    }

    void OnDestroy()
    {
            if (surfaceRenderer != null && gridMaterial != null && surfaceRenderer.sharedMaterial == gridMaterial)
        {
            surfaceRenderer.sharedMaterial = originalMaterial;
        }

        if (gridMaterial != null)
        Destroy(gridMaterial);

        if(gridTexture != null)
        Destroy(gridTexture);
    

    }
}
