using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static RayMotionBlur;

using UnityEngine.Profiling;

public class RayMotionBlur : MonoBehaviour
{

    [SerializeField]
    private GameObject cameraGameObject;

    private Camera centerEyeCamera;

    private Transform handTransform;


    [SerializeField]
    private GameObject noBlurRayGameObject;
    private MeshRenderer noBlurRayMeshRenderer;

    [SerializeField]
    private GameObject rayQuadGeometryGameObject;
    
    //[SerializeField]
    //public Material activeRayProxyTrianglesMaterialTimeBased;
    [SerializeField]
    public Material activeRayProxyTrianglesMaterialOpacityFunction;

    private MeshRenderer rayProxyMeshRenderer;

    public bool writeExecutionTimeToFile = false;
    private System.Diagnostics.Stopwatch stopwatch;
    private long[] executionTimes = new long[500];
    private int numFramesMeasured = 0;
    private string executionTimeFilename = "rayMotionBlurUpdateExecutionTime.csv";
    private CustomSampler sampler;

    //Recorder profilingRecorder;

    [SerializeField]
    public int NumTimeIntervalsForBlur
    {
        get
        {
            return _numTimeIntervalsForBlur;
        }
        set
        {
            _numTimeIntervalsForBlur = Mathf.Min(Mathf.Max(value, 1), 6);            
            OnNumTimeIntervalsForBlurChanged();
        }
    }

    private int _numTimeIntervalsForBlur = 4;

    private int targetFrameRate = 72;

    [SerializeField]
    private bool createDebugGeometry = false;

    private float opacityPerSecond;

    private Vector3[] rayGeometryVertices = new Vector3[4];

    [SerializeField]
    private bool useRealTimeStamps;


    [Header("Blur activation parameters")]

    [SerializeField]
    [Range(0, 50)]
    private float blurTransitionStartSpeedMSec = 0.5f;

    [SerializeField]
    [Range(0, 50)]
    private float blurTransitionEndSpeedMSec = 9f;

    [SerializeField]
    [Range(0, 1)]
    private float blurDistanceFromAnchorFadeStart = 0.05f;
    [SerializeField]
    [Range(0, 1)]
    private float blurDistanceFromAnchorFadeEnd = 0.6f;


    [Header("Opacity function parameters")]


    [SerializeField]
    [Range(0, 1)]
    private float opacityFunctionFadeInEnd = 0.85f;

    [SerializeField]
    [Range(0, 1)]
    private float opacityFunctionFadeOutStart = 0.75f;


    [SerializeField]
    [Range(0, 10000)]
    private float speedBasedOpacityFadeMinSpeedMSec = 0.5f;
    [SerializeField]
    [Range(0, 10000)]
    private float speedBasedOpacityFadeMaxSpeedMSec = 9f;


    //private GameObject rayProxyAABBGameObject;
    private GameObject rayProxyTrianglesGameObject;
    private Mesh rayProxyTrianglesMesh;
    private MeshFilter rayProxyTrianglesMeshFilter;
    private int[] rayProxyTriangleMeshIndices_Ray;
    private int[] rayProxyTriangleMeshIndices_FillRightMotion;
    private int[] rayProxyTriangleMeshIndices_FillLeftMotion;

    private Vector3 lastPos;
    private Quaternion lastRot;
    private Vector3 lastScale;

    private int currentFrameVerticesSubBufferIdx = 0;
    Vector3[] allTransformedVertices;
    Vector3[] allTransformedVerticesTemporallyOrdered;

    float[] allTimestamps;
    float[] allTimestampsOrdered;

    private bool rayMovesRight = true;

    private ComputeBuffer rayVerticesComputeBuffer;

    public enum MotionBlurRayMode
    {
        RayOff,
        NoBlur,
        //TimeBased,
        OpacityEnvelope
    }
    MotionBlurRayMode motionBlurMode = MotionBlurRayMode.NoBlur;

    public bool autoOn = false;

    // Start is called before the first frame update
    void Awake()
    {
        if (rayQuadGeometryGameObject == null)
        {
            Debug.LogError("rayGeometryGameObject not given!");
        }

        if (noBlurRayGameObject == null)
        {
            Debug.LogError("noBlurRayGameObject not given!");
        }

        if (cameraGameObject == null)
        {
            Debug.LogError("cameraGameObject not given!");
        }

        centerEyeCamera = cameraGameObject.GetComponent<Camera>();

        handTransform = transform.parent;

        noBlurRayMeshRenderer = noBlurRayGameObject.GetComponent<MeshRenderer>();
        noBlurRayMeshRenderer.enabled = false;

        Mesh rayGeometryMesh = rayQuadGeometryGameObject.GetComponent<MeshFilter>().mesh;
        List<Vector3> rayVerticesNoTransform = new List<Vector3>();
        rayGeometryMesh.GetVertices(rayVerticesNoTransform);
        rayQuadGeometryGameObject.SetActive(false);


        if (rayVerticesNoTransform.Count != 4)
        {
            Debug.LogError("Ray should have 4 vertices!");
        }

        // get ray vertices in local object space
        for (int i = 0; i < 4; i++)
        {
            // get world space vertex pos
            rayGeometryVertices[i] = rayQuadGeometryGameObject.transform.TransformPoint(rayVerticesNoTransform[i]);
            // convert to local space of this object
            rayGeometryVertices[i] = transform.InverseTransformPoint(rayGeometryVertices[i]);
        }

        // swap vertices 1 and 2 to get the vertices of each ray edge next to each other 
        Vector3 temp = rayGeometryVertices[1];
        rayGeometryVertices[1] = rayGeometryVertices[2];
        rayGeometryVertices[2] = temp;

        //Debug.Log("ray geometry vertices in model space");
        //foreach (var v in rayGeometryVertices)
        //{
        //    Debug.Log("Ray geometry vertex : " + v);
        //}


        rayProxyTrianglesGameObject = new GameObject("RayProxyTriangles");
        rayProxyTrianglesMeshFilter = rayProxyTrianglesGameObject.AddComponent<MeshFilter>();
        rayProxyMeshRenderer = rayProxyTrianglesGameObject.AddComponent<MeshRenderer>();
        //rayProxyMeshRenderer.material = activeRayProxyTrianglesMaterial;

        rayProxyTrianglesMesh = new Mesh();
        rayProxyTrianglesMesh.name = "rayProxyTriangles";

        opacityPerSecond = (float)targetFrameRate / NumTimeIntervalsForBlur; // 72 FPS is target refresh rate


        InitializeRayProxyTopology();

        InitializeVertexBuffers();





        // initialize vertex buffer with starting position
        for (currentFrameVerticesSubBufferIdx = 0; currentFrameVerticesSubBufferIdx <= NumTimeIntervalsForBlur; ++currentFrameVerticesSubBufferIdx)
        {
            ApplyCurrentTransformToVertices();
        }
        currentFrameVerticesSubBufferIdx = 0;


        // set MB on to initialize shader
        SetRayMode(MotionBlurRayMode.OpacityEnvelope);
        SetRayMode(MotionBlurRayMode.NoBlur);

        if (autoOn)
        {
            //rayQuadGeometryGameObject.SetActive(true);
            SetRayMode(MotionBlurRayMode.OpacityEnvelope);
        }

        if (writeExecutionTimeToFile)
        {
            //    profilingRecorder = Recorder.Get("BehaviourUpdate");
            //    profilingRecorder.CollectFromAllThreads();
            //    profilingRecorder.enabled = true;
            //}
            sampler = CustomSampler.Create("RayBlurUpdateSampler");
        }
    }

        public MotionBlurRayMode GetRayMode()
    {
        return motionBlurMode;
    }

    public void SetRayMode(MotionBlurRayMode _motionBlurMode)
    {
        motionBlurMode = _motionBlurMode;

        switch (motionBlurMode)
        {
            case MotionBlurRayMode.RayOff:
                noBlurRayMeshRenderer.enabled = false;
                rayProxyMeshRenderer.enabled = false;
                break;

            case MotionBlurRayMode.NoBlur:
                noBlurRayMeshRenderer.enabled = true;
                rayProxyMeshRenderer.enabled = false;
                break;

            //case MotionBlurMode.TimeBased:
            //    noBlurRayMeshRenderer.enabled = false;
            //    rayProxyMeshRenderer.enabled = true;
            //    rayProxyMeshRenderer.material = activeRayProxyTrianglesMaterialTimeBased;
            //    SetConstantUniformProperties();
            //    break;

            case MotionBlurRayMode.OpacityEnvelope:
                noBlurRayMeshRenderer.enabled = false;
                rayProxyMeshRenderer.enabled = true;
                rayProxyMeshRenderer.material = activeRayProxyTrianglesMaterialOpacityFunction;
                SetConstantUniformProperties();
                break;

            default:
                break;
        }
    }

    void OnNumTimeIntervalsForBlurChanged()
    {
        Debug.Log("OnNumTimeIntervalsForBlurChanged called");
        
        opacityPerSecond = (float)targetFrameRate / NumTimeIntervalsForBlur; // 72 FPS is target refresh rate
        rayProxyMeshRenderer.material.SetFloat("_OpacityPerSecond", opacityPerSecond);

        InitializeRayProxyTopology();
        InitializeVertexBuffers();
    }

    void InitializeRayProxyTopology()
    {
        // create index buffer for ray's current position
        rayProxyTriangleMeshIndices_Ray = new int[6] { 0, 1, 3, 3, 2, 0 };
        // offset indices so that ray is formed from vertices at the end of the buffer (latest time step)
        for (int i = 0; i < rayProxyTriangleMeshIndices_Ray.Length; ++i)
        {
            rayProxyTriangleMeshIndices_Ray[i] += (4 * NumTimeIntervalsForBlur);
        }

        // create proxy indices for when ray is moving left and right
        // the following indices create patches between adjacent time steps
        // they should be repeated when multiple time steps are used to create the ray
        int[] proxyPatchLeftMotion = new int[6] { 6, 7, 3, 3, 2, 6 };
        int[] proxyPatchRightMotion = new int[6] { 0, 1, 5, 5, 4, 0 };

        rayProxyTriangleMeshIndices_FillLeftMotion = new int[6 * NumTimeIntervalsForBlur];
        rayProxyTriangleMeshIndices_FillRightMotion = new int[6 * NumTimeIntervalsForBlur];
        // for each time interval, add a patch that connects instances of the ray's trailing edge
        for (int t = 0; t < NumTimeIntervalsForBlur; t++)
        {
            for (int i = 0; i < 6; ++i)
            {
                rayProxyTriangleMeshIndices_FillLeftMotion[t * 6 + i] = proxyPatchLeftMotion[i] + t * 4;
                rayProxyTriangleMeshIndices_FillRightMotion[t * 6 + i] = proxyPatchRightMotion[i] + t * 4;
            }
        }
    }

    void InitializeVertexBuffers()
    {
        int rayVerticesComputeBufferNumElements = rayGeometryVertices.Length * (NumTimeIntervalsForBlur + 1);
        int rayVerticesComputeBufferStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector4));
        rayVerticesComputeBuffer = new ComputeBuffer(rayVerticesComputeBufferNumElements, rayVerticesComputeBufferStride, ComputeBufferType.Default);

        allTransformedVertices = new Vector3[4 * (1 + NumTimeIntervalsForBlur)];
        allTransformedVerticesTemporallyOrdered = new Vector3[4 * (1 + NumTimeIntervalsForBlur)];

        allTimestamps = new float[1 + NumTimeIntervalsForBlur];
        allTimestampsOrdered = new float[1 + NumTimeIntervalsForBlur];
    }

    void StoreTransformsAndVertices()
    {
        lastPos = transform.position;
        lastRot = transform.rotation;
        lastScale = transform.localScale;
    }

    void ApplyCurrentTransformToVertices()
    {
        // apply current transform to ray vertices
        // store in allTransformedVertices at current write position
        for (int i = 0; i < 4; ++i)
        {
            allTransformedVertices[currentFrameVerticesSubBufferIdx * 4 + i] = transform.TransformPoint(rayGeometryVertices[i]);
        }

        // get current time stamp, that will be associated with vertices
        allTimestamps[currentFrameVerticesSubBufferIdx] = Time.time;
    }

    void DEBUG_CreateFakeVertices()
    {
        for (int t = 0; t <= NumTimeIntervalsForBlur; ++t)
        {
            Vector3 posShift = new Vector3(0.15f * (t), 0f, 0f);    
            Quaternion rotShift = Quaternion.Euler(0, 10 * t, 0);

            //Vector3 posShift = new Vector3(0f, 0f, 0f);
            //Quaternion rotShift = Quaternion.Euler(0, 0, 0);

            for (int i = 0; i < 4; ++i)
            {
                Vector3 v = transform.TransformPoint(rayGeometryVertices[i]);


                //Vector3 rotationCentre = rayGeometryVertices[0];
                Vector3 rotationCentre = transform.position;

                v -= rotationCentre;
                v = rotShift * v;
                v += rotationCentre;
                v += posShift;
                allTransformedVerticesTemporallyOrdered[t * 4 + i] = v;
            }

            //allTimestampsOrdered[t] = t / 72f;
            allTimestampsOrdered[t] = (t / (float)targetFrameRate);// + Random.Range(0, (t / 72f) * 0.2f);
        }
    }

    // all vertices for ray positions over time are already stored in allTransformedVertices
    // this function updates a buffer which stores vertices in temporal order (oldest to newest)
    void UpdateTemporallyOrderedVertexBuffer()
    {
        // loop from current frame to oldest frame
        for (int t = NumTimeIntervalsForBlur; t >= 0; --t)
        {
            // get subbuffer index where transformed vertices are stored in allTransformedVertices
            int readSubBufferIdx = currentFrameVerticesSubBufferIdx - (NumTimeIntervalsForBlur - t);
            if (readSubBufferIdx < 0)
            {
                readSubBufferIdx += NumTimeIntervalsForBlur + 1;
            }
        
            int writeSubBufferIdx = t;

            // copy vertices from allTransformedVertices to allTransformedVerticesTemporallyOrdered
            // copy 4 vertices at a time
            for (int i = 0; i < 4; ++i)
            {
                allTransformedVerticesTemporallyOrdered[writeSubBufferIdx * 4 + i] = allTransformedVertices[readSubBufferIdx * 4 + i];
            }

            // order time stamps
            allTimestampsOrdered[writeSubBufferIdx] = allTimestamps[readSubBufferIdx];
        }

        ////print temporally ordered time stamps
        //string ts = "";
        //for (int i = 0; i < allTimestampsOrdered.Length; ++i)
        //{
        //    ts += allTimestampsOrdered[i].ToString() + " | ";
        //}
        //Debug.Log("Timestamps: " + ts);

        // normalize time stamps
        //float earliestTime = allTimestampsOrdered[0];
        //float timeRange = allTimestampsOrdered[NumTimeIntervalsForBlur] - earliestTime;
        //for (int i = 0; i < allTimestampsOrdered.Length; i++)
        //{
        //    allTimestampsOrdered[i] = Mathf.Clamp( (allTimestampsOrdered[i] - earliestTime) / timeRange, 0f, 1f);
        //}

        ////print temporally ordered time stamps
        //string tsn = "";
        //for (int i = 0; i < allTimestampsOrdered.Length; ++i)
        //{
        //    tsn += allTimestampsOrdered[i].ToString() + " | ";
        //}
        //Debug.Log("Timestamps normalized: " + tsn);

        // print temporally ordered vertices in groups of 4
        //string allTransformedVerticesString = "";
        //for (int i = 0; i < allTransformedVerticesTemporallyOrdered.Length; ++i)
        //{
        //    allTransformedVerticesString += allTransformedVerticesTemporallyOrdered[i].ToString() + " | ";
        //    if ((i + 1) % 4 == 0)
        //    {
        //        Debug.Log("all transformed vertices temporally ordered " + i + ": " + allTransformedVerticesString);
        //        allTransformedVerticesString = "";
        //    }
        //}

        // dump buffer to file 
        //if (++numFrames > 300)
        //{
        //    Debug.Log("Frame " + numFrames + ", pos " + allTransformedVerticesTemporallyOrdered[allTransformedVerticesTemporallyOrdered.Length - 1]);

        //    string filepath = "temporallyOrderedVertices_" + (numFrames).ToString() + ".bin";
        //    using (BinaryWriter writer = new BinaryWriter(File.Open(filepath, FileMode.Create)))
        //    {
        //        for (int i = 0; i < allTransformedVerticesTemporallyOrdered.Length; i++)
        //        {
        //            writer.Write(allTransformedVerticesTemporallyOrdered[i].x);
        //            writer.Write(allTransformedVerticesTemporallyOrdered[i].y);
        //            writer.Write(allTransformedVerticesTemporallyOrdered[i].z);
        //        }
        //    }

        //}

    }

    // gets centroid of ray for a given time step
    // where oldest time step has index 0 and current time step has index [numTimeIntervalsForBlur]
    Vector3 GetRayCentroidForTimeStep(int timeStep)
    {
        // get offset into allTransformedVerticesTemporallyOrdered buffer
        int subBufferIdx = timeStep * 4;
        // calculate centroid of ray for this time step
        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < 4; ++i)
        {
            centroid += allTransformedVerticesTemporallyOrdered[subBufferIdx + i];
        }
        return centroid /= 4f;
    }

    // creates a set of triangles formed by the ray and path between the last and current rays
    void UpdateProxyTriangles()
    {

        // get new indices for proxy mesh based on motion direction
        // calculate whether ray centroid has moved left or right WRT camera
        Transform camTransform = cameraGameObject.transform;

        Vector3 lastCentroid = GetRayCentroidForTimeStep(NumTimeIntervalsForBlur - 1);
        Vector3 newCentroid = GetRayCentroidForTimeStep(NumTimeIntervalsForBlur);
        Vector3 lastCentroidCamSpace = camTransform.InverseTransformPoint(lastCentroid);
        Vector3 newCentroidCamSpace = camTransform.InverseTransformPoint(newCentroid);

        Vector3 rayPlaneNormal = transform.up;

        int[] allIndices;

        // concatenate indices which form the ray with those that form the trailing patches
        // order of triangles in index buffer is not important

        rayMovesRight = (newCentroidCamSpace.x > lastCentroidCamSpace.x);
        if (Vector3.Dot(camTransform.up, rayPlaneNormal) < 0)
        {
            rayMovesRight = !rayMovesRight;
        }

        //allIndices = rayProxyTriangleMeshIndices_Ray;

        //if x is larger in current centroid, we say the ray moved to the right in view
        if (rayMovesRight)
        {
            allIndices = rayProxyTriangleMeshIndices_Ray.Concat(rayProxyTriangleMeshIndices_FillRightMotion).ToArray();
        }
        else
        {
            allIndices = rayProxyTriangleMeshIndices_Ray.Concat(rayProxyTriangleMeshIndices_FillLeftMotion).ToArray();
        }

        // update proxy mesh vertices and triangles indices
        rayProxyTrianglesMesh.Clear();
        rayProxyTrianglesMesh.vertices = allTransformedVerticesTemporallyOrdered;
        rayProxyTrianglesMesh.triangles = allIndices; // updates bounds automatically
        rayProxyTrianglesMeshFilter.mesh = rayProxyTrianglesMesh;

        // print all indices in groups of 6
        //string allIndicesString = "";
        //for (int i = 0; i < allIndices.Length; ++i)
        //{
        //    allIndicesString += allIndices[i].ToString() + " | ";
        //    if ((i + 1) % 6 == 0)
        //    {
        //        Debug.Log("all indices " + i + ": " + allIndicesString);
        //        allIndicesString = "";
        //    }
        //}

    }


    // ray edge data is uploaded to the shader in a compute buffer
    // each time step is described by a set of vertices
    // in the basic case where a ray is represented by two lines, there are four vertices per time step
    private void UploadVertexDataToShader()
    {
        // convert vertices to vector4 for upload
        Vector4[] allRayPointsv4 = new Vector4 [4 * (NumTimeIntervalsForBlur + 1)];
        // for each point in temporally ordered vertex buffer, convert to vector4 in allRay Points
        for (int i = 0; i < allTransformedVerticesTemporallyOrdered.Length; ++i)
        {
            // get time stamp that this vertex is associated with and add to 4th element
            float t = allTimestampsOrdered[i / 4];

            allRayPointsv4[i] = new Vector4(allTransformedVerticesTemporallyOrdered[i].x, allTransformedVerticesTemporallyOrdered[i].y, allTransformedVerticesTemporallyOrdered[i].z, t);
        }

        rayVerticesComputeBuffer.SetData(allRayPointsv4);
        rayProxyMeshRenderer.material.SetBuffer("RayVertices", rayVerticesComputeBuffer);

        ////print temporally ordered vec4s in groups of 4
        //string allRayPointsv4String = "";
        //for (int i = 0; i < allRayPointsv4.Length; ++i)
        //{
        //    allRayPointsv4String += allRayPointsv4[i].ToString() + " | ";
        //    if ((i + 1) % 4 == 0)
        //    {
        //        Debug.Log("all transformed vec4 temporally ordered " + i + ": " + allRayPointsv4String);
        //        allRayPointsv4String = "";
        //    }
        //}
    }

    private float CalculateRaySpeedPxPerSec()
    {
        // calculate ray speed in image space
        // use movement of end point (vertex 1) of ray to calculate speed

        // TODO moving average using ring buffer to reduce computation

        float distanceInPixels = 0f;

        Vector2 lastScreenPos = centerEyeCamera.WorldToScreenPoint(allTransformedVerticesTemporallyOrdered[1]);

        for (int t = 1; t < NumTimeIntervalsForBlur + 1; t++)
        {
            Vector3 rayEndPosWS = allTransformedVerticesTemporallyOrdered[t * 4 + 1];
            Vector2 screenPos = centerEyeCamera.WorldToScreenPoint(rayEndPosWS);

            float intervalDistanceInPixels = Vector2.Distance(screenPos, lastScreenPos);
            distanceInPixels += intervalDistanceInPixels;

            lastScreenPos = screenPos;
        }
        float speed = (targetFrameRate * distanceInPixels) / _numTimeIntervalsForBlur;

        return speed;
    }

    private float CalculateHorizontalSpeedInRaySpace()
    {
        // get average movement of ray end point on x axis in ray's current coordinate system

        // TODO moving average using ring buffer to reduce computation

        // TODO use absolute distance or allow movement in opposite directions to cancel out?

        float distanceMoved = 0f;

        Vector3 lastPos = transform.InverseTransformPoint(allTransformedVerticesTemporallyOrdered[1]);

        for (int t = 1; t < NumTimeIntervalsForBlur + 1; t++)
        {
            Vector3 rayEndPosWS = allTransformedVerticesTemporallyOrdered[t * 4 + 1];
            Vector3 pos = transform.InverseTransformPoint(rayEndPosWS);

            distanceMoved += Mathf.Abs(pos.x - lastPos.x);
            lastPos = pos;
        }
        float speed = (targetFrameRate * Mathf.Abs(distanceMoved)) / _numTimeIntervalsForBlur;

        return speed;
    }

    // some actual constants will be moved to shader, but remain uniforms for now for easier tweaking
    private void SetConstantUniformProperties()
    {
        rayProxyMeshRenderer.material.SetFloat("_OpacityPerSecond", opacityPerSecond);
        rayProxyMeshRenderer.material.SetInt("_NumTimeIntervals", NumTimeIntervalsForBlur);
        rayProxyMeshRenderer.material.SetInt("_TargetFrameRate", targetFrameRate);
        rayProxyMeshRenderer.material.SetInt("_UseRealTime", useRealTimeStamps ? 1 : 0);


        SetEditorInteractableUniformProperties();
    }

    // set uniforms that are constant except for manual changes in the editor
    private void SetEditorInteractableUniformProperties()
    {
        rayProxyMeshRenderer.material.SetFloat("_OpacityFunctionFadeOutStart", opacityFunctionFadeOutStart);
        rayProxyMeshRenderer.material.SetFloat("_OpacityFunctionFadeInEnd", opacityFunctionFadeInEnd);

        rayProxyMeshRenderer.material.SetFloat("_BlurTransitionStartSpeed", blurTransitionStartSpeedMSec);
        rayProxyMeshRenderer.material.SetFloat("_BlurTransitionEndSpeed", blurTransitionEndSpeedMSec);

        rayProxyMeshRenderer.material.SetFloat("_BlurDistanceFromAnchorFadeStart", blurDistanceFromAnchorFadeStart);
        rayProxyMeshRenderer.material.SetFloat("_BlurDistanceFromAnchorFadeEnd", blurDistanceFromAnchorFadeEnd);

        rayProxyMeshRenderer.material.SetFloat("_SpeedBasedOpacityFadeMinSpeed", speedBasedOpacityFadeMinSpeedMSec);
        rayProxyMeshRenderer.material.SetFloat("_SpeedBasedOpacityFadeMaxSpeed", speedBasedOpacityFadeMaxSpeedMSec);
    }

    private void UpdateVariableUniformProperties()
    {
#if UNITY_EDITOR
        SetEditorInteractableUniformProperties();
#endif

        //float speed_px_per_sec = CalculateRaySpeedPxPerSec();
        float horizontal_speed_in_ray_space = CalculateHorizontalSpeedInRaySpace();
        rayProxyMeshRenderer.material.SetFloat("_RaySpeed", horizontal_speed_in_ray_space);

        rayProxyMeshRenderer.material.SetInt("_RayMovesRight", rayMovesRight ? 1 : 0);

    }


    // rotate quad that represents ray around its Z axis, so that angle between quad normal and viewing vector is minimized
    void RotateRayUp()
    {
        Vector3 rayUp = handTransform.up;
        Vector3 rayForward = handTransform.forward;
        Vector3 rayToEye = cameraGameObject.transform.position - handTransform.position;

        // project ray up vector and ray-to-eye vector onto plane with normal = hand forward vector
        Vector3 projectedRayUp = Vector3.ProjectOnPlane(rayUp, rayForward);
        Vector3 projectedRayToEye = Vector3.ProjectOnPlane(rayToEye, rayForward);
        // get angle between projected vectors
        float angle = Vector3.SignedAngle(projectedRayUp, projectedRayToEye, rayForward);
        // rotate ray around this angle
        transform.localRotation = Quaternion.Euler(0, 0, angle);
    }

    // Update is called once per frame
    void Update()
    {
        if (writeExecutionTimeToFile)
        {
            stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            sampler.Begin();
        }

        RotateRayUp();

        ApplyCurrentTransformToVertices();
        UpdateTemporallyOrderedVertexBuffer();

        if (createDebugGeometry)
        {
            DEBUG_CreateFakeVertices();
        }

        UpdateProxyTriangles();
        UploadVertexDataToShader();
        UpdateVariableUniformProperties();

        ++currentFrameVerticesSubBufferIdx;
        if (currentFrameVerticesSubBufferIdx > NumTimeIntervalsForBlur)
        {
            currentFrameVerticesSubBufferIdx = 0;
        }

        if (writeExecutionTimeToFile)
        {
            sampler.End();


            stopwatch.Stop();
            if (numFramesMeasured < executionTimes.Length)
            {
                executionTimes[numFramesMeasured] = stopwatch.Elapsed.Ticks;
                Debug.Log("time " + stopwatch.Elapsed);
                Debug.Log("ticks" + stopwatch.Elapsed.Ticks);
            }
            if (numFramesMeasured == executionTimes.Length)
            {
                using (var writer = new StreamWriter(executionTimeFilename, false))
                {
                    for (int i = 0; i < executionTimes.Length; i++)
                    {
                        writer.Write(executionTimes[i] + "\n");
                    }
                }
                Debug.Log("Wrote execution time data to file " + executionTimeFilename);
            }
            //Debug.Log("Timer: " + stopwatch.Elapsed);
            stopwatch.Reset();
            ++numFramesMeasured;

            //if (profilingRecorder.isValid)
            //    Debug.Log("BehaviourUpdate time: " + profilingRecorder.elapsedNanoseconds);

        }

    }

    private void OnDestroy()
    {
        rayVerticesComputeBuffer.Release();
    }
}
