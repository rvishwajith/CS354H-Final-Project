using UnityEngine;

public static class OceanGeometryGenerator
{
    public static Mesh CreatePlaneMesh(int width, int height, float lengthScale, Seams seams = Seams.None, int trianglesShift = 0)
    {
        var mesh = new Mesh();
        mesh.name = "Clipmap plane";
        if ((width + 1) * (height + 1) >= 256 * 256)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        Vector3[] vertices = new Vector3[(width + 1) * (height + 1)];
        int[] triangles = new int[width * height * 2 * 3];
        Vector3[] normals = new Vector3[(width + 1) * (height + 1)];

        for (int i = 0; i < height + 1; i++)
        {
            for (int j = 0; j < width + 1; j++)
            {
                int x = j;
                int z = i;

                if ((i == 0 && seams.HasFlag(Seams.Bottom)) || (i == height && seams.HasFlag(Seams.Top)))
                    x = x / 2 * 2;
                if ((j == 0 && seams.HasFlag(Seams.Left)) || (j == width && seams.HasFlag(Seams.Right)))
                    z = z / 2 * 2;

                vertices[j + i * (width + 1)] = new Vector3(x, 0, z) * lengthScale;
                normals[j + i * (width + 1)] = Vector3.up;
            }
        }

        int tris = 0;
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                int k = j + i * (width + 1);
                if ((i + j + trianglesShift) % 2 == 0)
                {
                    triangles[tris++] = k;
                    triangles[tris++] = k + width + 1;
                    triangles[tris++] = k + width + 2;

                    triangles[tris++] = k;
                    triangles[tris++] = k + width + 2;
                    triangles[tris++] = k + 1;
                }
                else
                {
                    triangles[tris++] = k;
                    triangles[tris++] = k + width + 1;
                    triangles[tris++] = k + 1;

                    triangles[tris++] = k + 1;
                    triangles[tris++] = k + width + 1;
                    triangles[tris++] = k + width + 2;
                }
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.normals = normals;
        return mesh;
    }

    public static Mesh CreateSkirtMesh(int k, float outerBorderScale)
    {
        var mesh = new Mesh();
        mesh.name = "Clipmap skirt";
        CombineInstance[] combine = new CombineInstance[8];

        Mesh quad = OceanGeometryGenerator.CreatePlaneMesh(1, 1, 1);
        Mesh hStrip = OceanGeometryGenerator.CreatePlaneMesh(k, 1, 1);
        Mesh vStrip = OceanGeometryGenerator.CreatePlaneMesh(1, k, 1);

        Vector3 cornerQuadScale = new Vector3(outerBorderScale, 1, outerBorderScale);
        Vector3 midQuadScaleVert = new Vector3(1f / k, 1, outerBorderScale);
        Vector3 midQuadScaleHor = new Vector3(outerBorderScale, 1, 1f / k);

        combine[0].transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, cornerQuadScale);
        combine[0].mesh = quad;

        combine[1].transform = Matrix4x4.TRS(Vector3.right * outerBorderScale, Quaternion.identity, midQuadScaleVert);
        combine[1].mesh = hStrip;

        combine[2].transform = Matrix4x4.TRS(Vector3.right * (outerBorderScale + 1), Quaternion.identity, cornerQuadScale);
        combine[2].mesh = quad;

        combine[3].transform = Matrix4x4.TRS(Vector3.forward * outerBorderScale, Quaternion.identity, midQuadScaleHor);
        combine[3].mesh = vStrip;

        combine[4].transform = Matrix4x4.TRS(Vector3.right * (outerBorderScale + 1)
            + Vector3.forward * outerBorderScale, Quaternion.identity, midQuadScaleHor);
        combine[4].mesh = vStrip;

        combine[5].transform = Matrix4x4.TRS(Vector3.forward * (outerBorderScale + 1), Quaternion.identity, cornerQuadScale);
        combine[5].mesh = quad;

        combine[6].transform = Matrix4x4.TRS(Vector3.right * outerBorderScale
            + Vector3.forward * (outerBorderScale + 1), Quaternion.identity, midQuadScaleVert);
        combine[6].mesh = hStrip;

        combine[7].transform = Matrix4x4.TRS(Vector3.right * (outerBorderScale + 1)
            + Vector3.forward * (outerBorderScale + 1), Quaternion.identity, cornerQuadScale);
        combine[7].mesh = quad;
        mesh.CombineMeshes(combine, true);
        return mesh;
    }

    public static Mesh CreateTrimMesh(int k, float lengthScale)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Clipmap trim";
        CombineInstance[] combine = new CombineInstance[2];

        combine[0].mesh = OceanGeometryGenerator.CreatePlaneMesh(k + 1, 1, lengthScale, Seams.None, 1);
        combine[0].transform = Matrix4x4.TRS(new Vector3(-k - 1, 0, -1) * lengthScale, Quaternion.identity, Vector3.one);

        combine[1].mesh = OceanGeometryGenerator.CreatePlaneMesh(1, k, lengthScale, Seams.None, 1);
        combine[1].transform = Matrix4x4.TRS(new Vector3(-1, 0, -k - 1) * lengthScale, Quaternion.identity, Vector3.one);

        mesh.CombineMeshes(combine, true);
        return mesh;
    }

    public static Mesh CreateRingMesh(int k, float lengthScale)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Clipmap ring";
        if ((2 * k + 1) * (2 * k + 1) >= 256 * 256)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        CombineInstance[] combine = new CombineInstance[4];

        combine[0].mesh = OceanGeometryGenerator.CreatePlaneMesh(2 * k, (k - 1) / 2, lengthScale, Seams.Bottom | Seams.Right | Seams.Left);
        combine[0].transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

        combine[1].mesh = OceanGeometryGenerator.CreatePlaneMesh(2 * k, (k - 1) / 2, lengthScale, Seams.Top | Seams.Right | Seams.Left);
        combine[1].transform = Matrix4x4.TRS(new Vector3(0, 0, k + 1 + (k - 1) / 2) * lengthScale, Quaternion.identity, Vector3.one);

        combine[2].mesh = OceanGeometryGenerator.CreatePlaneMesh((k - 1) / 2, k + 1, lengthScale, Seams.Left);
        combine[2].transform = Matrix4x4.TRS(new Vector3(0, 0, (k - 1) / 2) * lengthScale, Quaternion.identity, Vector3.one);

        combine[3].mesh = OceanGeometryGenerator.CreatePlaneMesh((k - 1) / 2, k + 1, lengthScale, Seams.Right);
        combine[3].transform = Matrix4x4.TRS(new Vector3(k + 1 + (k - 1) / 2, 0, (k - 1) / 2) * lengthScale, Quaternion.identity, Vector3.one);

        mesh.CombineMeshes(combine, true);
        return mesh;
    }
}