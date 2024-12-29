using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MockHandWaving : MonoBehaviour
{
    public float movementPeriodSec = 1f;
    public float zRotationAmplitudeDeg = 30f;

    public float xTranslationAmplitudeM = -0.1f;


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float timeSinceStart = Time.time;

        int cycles = (int)Mathf.Floor(timeSinceStart / movementPeriodSec);
        float timeInCycle = timeSinceStart - (cycles * movementPeriodSec);
        float timeInCycleNormalized = timeInCycle / movementPeriodSec;

        float zRotAmplitudeNormalized = Mathf.Sin(timeInCycleNormalized * Mathf.PI * 2);
        float zRot = zRotAmplitudeNormalized * zRotationAmplitudeDeg;

        Vector3 localRot = transform.localRotation.eulerAngles;
        transform.localRotation = Quaternion.Euler(localRot.x, localRot.y, zRot);

        float xTransAmplitudeNormalized = Mathf.Sin(timeInCycleNormalized * Mathf.PI * 2);
        float xTrans = xTransAmplitudeNormalized * xTranslationAmplitudeM;

        Vector3 localPos = transform.localPosition;
        transform.localPosition = new Vector3(xTrans, localPos.y, localPos.z);

    }
}
