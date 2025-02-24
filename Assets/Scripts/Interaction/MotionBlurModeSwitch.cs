using Oculus.Interaction;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static RayMotionBlur;

public class MotionBlurModeSwitch : MonoBehaviour
{
    [SerializeField]
    GrabInteractor InteractorView;


    [SerializeField]
    RayMotionBlur rayMotionBlurEffect;

    [SerializeField]
    TMP_Text textDisplay;

    
    private int numModes = 0;
    private int mode = 0;

    public bool hideModeIdentity = true;


    // Start is called before the first frame update
    void Awake()
    {
        if (rayMotionBlurEffect == null)
        {
            Debug.LogError("rayMotionBlurEffect is null");
        }
        if (textDisplay == null)
        {
            Debug.LogError("text is null");
        }
        if (InteractorView == null)
        {
            Debug.LogError("InteractorView is null");
        }

        //numModes = RayMotionBlur.MotionBlurRayMode.GetNames(typeof(RayMotionBlur.MotionBlurRayMode)).Length;
        numModes = 2;

    }

    private void Start()
    {
        UpdateVisual();

    }


    protected virtual void OnEnable()
    {
        InteractorView.WhenStateChanged += SwitchModeState;
    }

    protected virtual void OnDisable()
    {
        InteractorView.WhenStateChanged -= SwitchModeState;
    }

    private void UpdateVisual()
    {
        if (hideModeIdentity)
        {
            textDisplay.text = "Mode " + (mode + 1);
        }
        else
        {
            switch (mode)
            {
                case 0:
                    textDisplay.text = "Motion Blur Effect\nOFF\n(Toggle with trigger)";
                    break;
                case 1:
                    textDisplay.text = "Motion Blur Effect\nON\n(Toggle with trigger)";
                    break;
                default:
                    textDisplay.text = "Ray Off";
                    break;
            }
        }

    }


    private void SwitchModeState(InteractorStateChangeArgs args) => SwitchMode();

    public void SwitchMode()
    {
        if (InteractorView.State == InteractorState.Select)
        { 
            mode = numModes > 0 ? ((mode + 1) % numModes) : 0;

            Debug.Log("Switch to Mode " + (mode+1));

            RayMotionBlur.MotionBlurRayMode motionBlurMode;
            switch (mode)
            {
                case 0:
                    motionBlurMode = RayMotionBlur.MotionBlurRayMode.NoBlur;
                    break;
                case 1:
                    motionBlurMode = RayMotionBlur.MotionBlurRayMode.OpacityEnvelope;
                    break;
                default:
                    motionBlurMode = RayMotionBlur.MotionBlurRayMode.RayOff;
                    break;
            }
            rayMotionBlurEffect.SetRayMode(motionBlurMode);

            UpdateVisual();
        }

    }

}
