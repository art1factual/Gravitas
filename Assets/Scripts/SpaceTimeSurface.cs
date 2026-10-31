
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]

public class SpaceTimeSurface : MonoBehaviour
{

    public int resolution = 50;
    public float width = 20f;

    public float heightScale = 0.15f;


    Mesh mesh;


    Vector3[] vertices;
    Vector3[] flatVertices;
  

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        resolution = Mathf.Clamp(resolution, 2, 200);
        width = Mathf.Max(0.1f, width);

        float outerRadius = width / 2f;
        int rowLength = resolution +1;

        int vertexCount = 1 + resolution * rowLength;

        vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];

        int triangleCount = resolution + 2 * resolution * (resolution - 1);
        
        int[] triangles = new int[triangleCount * 3];

        vertices[0] = Vector3.zero;
        normals[0] = Vector3.up;
        uv[0] = new Vector2(0.5f, 0.5f);

        for (int ring = 1 ; ring <= resolution; ring++)
        {
            float radius = outerRadius * ring / resolution;

            float height = 0f;

            for (int x = 0; x <= resolution; x++)
            {
                float angle = x / (float)resolution * Mathf.PI * 2f;

                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                int index = 1 + (ring - 1) * rowLength + x;

                vertices[index] = new Vector3(radius * cos, height, radius * sin);

                normals[index] = Vector3.up;
            
                uv[index] = new Vector2(radius * cos / width + 0.5f, radius * sin / width + 0.5f);

            }
        }

        int triangleIndex = 0;

        for (int x = 0; x < resolution; x++)
        {
            triangles[triangleIndex++] = 0;
            triangles[triangleIndex++] = x + 2;
            triangles[triangleIndex++] = x + 1;

        }
        
        for (int ring = 1; ring < resolution; ring++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int corner = 1 + (ring - 1) * rowLength + x;

                triangles[triangleIndex++] = corner;
                triangles[triangleIndex++] = corner + 1;
                triangles[triangleIndex++] = corner + rowLength;

                triangles[triangleIndex++] = corner + 1;
                triangles[triangleIndex++] = corner + rowLength + 1;
                triangles[triangleIndex++] = corner + rowLength;

            }
        }
        
       flatVertices = (Vector3[])vertices.Clone();

       mesh = new Mesh();
       mesh.name = "Moving Mass Surface";
       mesh.MarkDynamic();


       mesh.vertices = vertices;
       mesh.triangles = triangles;
       mesh.normals = normals;
       mesh.uv = uv;

       mesh.RecalculateBounds();

       GetComponent<MeshFilter>().sharedMesh = mesh;
    }  

    void LateUpdate()
    {
        if (mesh == null || flatVertices == null)
        return;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPosition = transform.TransformPoint(flatVertices[i]);
            
            float potential = MassBody.PotentialAt(worldPosition);

            vertices[i] = flatVertices[i];

            vertices[i].y = potential * heightScale;

        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void OnDestroy()
    {
        if (mesh != null)
            Destroy(mesh);
    }
}