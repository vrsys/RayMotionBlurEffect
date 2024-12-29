using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuadMesh : MonoBehaviour
{

    Material rayMaterial;

    Vector3[] vertices = {
            new Vector3(0.1f,  0.1f, 0.0f),
            new Vector3(0.9f,  0.0f, -0.1f),
            new Vector3(-0.2f,  1.1f, 0.3f),
            new Vector3(1.0f,  1.0f, 0.5f)

            // vertices repeated so that (vx_id % 3) can be used to determine
            // which vertices lie on 'sides' of quad
            //new Vector3(0.1f,  0.1f, 0.0f),
            //new Vector3(-0.2f,  1.1f, 0.3f),
            //new Vector3(0.9f,  0.0f, -0.1f),
            //new Vector3(1.0f,  1.0f, 0.0f),
            //new Vector3(0.9f,  0.0f, -0.1f),
            //new Vector3(-0.2f,  1.1f, 0.3f)
        };

    Vector2[] uvs =
    {
        new Vector2(0.0f, 0.0f),
        new Vector2(1.0f, 0.0f),
        new Vector2(0.0f, 1.0f),
        new Vector2(1.0f, 1.0f)
    };

    int[] indices = {
            0, 2, 1, 3, 1, 2
            //0,1,2,3,4,5
        };




    //Vector3 rayOrigin;
    //Vector3 rayEnd;
    //Vector3 lastRayOrigin;
    //Vector3 lastRayEnd;


    void Start()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        Mesh mesh = new Mesh();
        meshFilter.mesh = mesh;
        mesh.vertices = vertices;
        mesh.triangles = indices;
        mesh.uv = uvs;
        rayMaterial = GetComponent<Renderer>().material;


        //lastRayOrigin = vertices[0];
        //lastRayEnd = vertices[2];
        //rayOrigin = vertices[1];
        //rayEnd = vertices[3];
    }

    private void Update()
    {
        // update shader
        //rayMaterial.SetVector("_RayOriginWS", rayOrigin);
        //rayMaterial.SetVector("_RayEndWS", rayEnd);
        //rayMaterial.SetVector("_LastRayOriginWS", lastRayOrigin);
        //rayMaterial.SetVector("_LastRayEndWS", lastRayEnd);
    }

}
