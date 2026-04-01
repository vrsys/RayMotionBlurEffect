using UnityEngine;

public class MockHandWaving : MonoBehaviour
{
    public float movementPeriodSec = 1f;
    public float zRotationAmplitudeDeg = 30f;
    public float xTranslationAmplitudeM = -0.1f;

    [Header("Pause at Extremes")]
    public bool pauseAtExtremes = false;
    public float pauseDurationSec = 0.3f;

    private float _phase = 0f;
    private float _pauseTimer = 0f;
    private bool _isPausing = false;

    void Update()
    {
        float sineValue;

        if (pauseAtExtremes)
        {
            if (_isPausing)
            {
                _pauseTimer -= Time.deltaTime;
                if (_pauseTimer <= 0f)
                    _isPausing = false;
            }

            if (!_isPausing)
            {
                float prevPhase = _phase;
                _phase += (Time.deltaTime / movementPeriodSec) * Mathf.PI * 2f;

                // Extremes of sin(x) occur at x = π/2 + k*π for integer k.
                // Detect a crossing by checking if the bucket index changed.
                int prevBucket = Mathf.FloorToInt((prevPhase - Mathf.PI * 0.5f) / Mathf.PI);
                int currBucket = Mathf.FloorToInt((_phase - Mathf.PI * 0.5f) / Mathf.PI);
                if (currBucket > prevBucket)
                {
                    _isPausing = true;
                    _pauseTimer = pauseDurationSec;
                }
            }

            sineValue = Mathf.Sin(_phase);
        }
        else
        {
            sineValue = Mathf.Sin((Time.time / movementPeriodSec) * Mathf.PI * 2f);
        }

        Vector3 localRot = transform.localRotation.eulerAngles;
        transform.localRotation = Quaternion.Euler(localRot.x, localRot.y, sineValue * zRotationAmplitudeDeg);

        Vector3 localPos = transform.localPosition;
        transform.localPosition = new Vector3(sineValue * xTranslationAmplitudeM, localPos.y, localPos.z);
    }
}
