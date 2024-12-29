using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class RotateRayUp : MonoBehaviour
{
    [SerializeField]
    private Transform eyeTransform;

    private Transform handTransform;

    // Start is called before the first frame update
    void Start()
    {
        if (eyeTransform == null)
        {
            eyeTransform = GameObject.Find("CenterEyeAnchor").transform;
        }

        handTransform = transform.parent;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 rayUp = handTransform.up;
        Vector3 rayForward = handTransform.forward;
        Vector3 rayToEye = eyeTransform.position - handTransform.position;

        // get rotation from ray up vector to ray to eye vector
        //Quaternion quaternionUpToHandEyeRotationWS = Quaternion.FromToRotation(Vector3.up, Vector3.Normalize(rayToEye));
        //rayOriginToEyeObjectTransform.position = handTransform.position;
        //rayOriginToEyeObjectTransform.rotation = (quaternionUpToHandEyeRotationWS);
    
        // project ray up vector and ray-to-eye vector onto plane defined by hand forward vector
        Vector3 projectedRayUp = Vector3.ProjectOnPlane(rayUp, rayForward);
        Vector3 projectedRayToEye = Vector3.ProjectOnPlane(rayToEye, rayForward);
        // get angle between projected vectors
        float angle = Vector3.SignedAngle(projectedRayUp, projectedRayToEye, rayForward);
        // rotate ray around this angle
        transform.localRotation = Quaternion.Euler(0, 0, angle);


    }
}
