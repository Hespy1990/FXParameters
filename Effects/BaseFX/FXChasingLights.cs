using UnityEngine;
using FX;

public class FXChasingLights : FXBaseWithEnabled, IFXTriggerable
{
    public Light[] lightsArray;
    public enum LightPattern { SineWave, EveryOther, MiddleOut, Random}
    public FXParameter<LightPattern> currentPattern = new FXParameter<LightPattern>(LightPattern.SineWave);

    public float spanProportion = 0.99f;
    public FXScaledParameter<float> chaseSpeed      = new FXScaledParameter<float>(1.0f, 0.0f, 10.0f);
    public FXParameter<bool> forwardDirection       = new FXParameter<bool>(true);
    public FXScaledParameter<float> fadeSpeed       = new FXScaledParameter<float>(0.05f, 1.0f, 100.0f);
    public FXScaledParameter<float> targetIntensity = new FXScaledParameter<float>(0.5f, 0.0f, 2.0f);
    public FXParameter<Color> colour = new FXParameter<Color>(Color.white);

    private int currentLeadIndex = 0;
    private float timeSinceLastTrigger = 0; 
    private float[] targetIntensities; 
    private float[] currentIntensities; 

    protected override void Awake()
    {
        base.Awake();
        chaseSpeed.OnValueChanged += SetChaseSpeed;
        colour.OnValueChanged += SetColour;

        if (lightsArray.Length == 0)
        {
            lightsArray = GetComponentsInChildren<Light>();
        }

        targetIntensities = new float[lightsArray.Length];
        currentIntensities = new float[lightsArray.Length];
        for (int i = 0; i < lightsArray.Length; i++)
        {
            currentIntensities[i] = lightsArray[i].intensity;
            targetIntensities[i] = 0; 
        }
    }

    private void Update()
    {
        if (!fxEnabled.Value) return;

        if (chaseSpeed.ScaledValue > 0)
        {
            timeSinceLastTrigger += Time.deltaTime;

            if (timeSinceLastTrigger > 1f / chaseSpeed.ScaledValue)
            {
                timeSinceLastTrigger = 0;
                FXTrigger();
            }
        }

        UpdateIntensities();

    }

    private void UpdateIntensities()
    {
        for (int i = 0; i < lightsArray.Length; i++)
        {
            if (currentIntensities[i] != targetIntensities[i])
            {
                currentIntensities[i] = Mathf.Lerp(currentIntensities[i], targetIntensities[i], fadeSpeed.ScaledValue * Time.deltaTime);
                lightsArray[i].intensity = currentIntensities[i];

                // If close enough to target, set it directly
                if (Mathf.Abs(currentIntensities[i] - targetIntensities[i]) < 0.01f)
                {
                    lightsArray[i].intensity = targetIntensities[i];
                    currentIntensities[i] = targetIntensities[i];
                    targetIntensities[i] = 0.0f;
                }
            }
        }
    }


    [FXMethod]
    public void FXTrigger()
    {
        if (!fxEnabled.Value) return;

        switch (currentPattern.Value)
        {
            case LightPattern.SineWave:
                float frequency = 2 * Mathf.PI / lightsArray.Length;
                for (int i = 0; i < lightsArray.Length; i++)
                {
                    float sinValue = 0.5f * Mathf.Sin(frequency * (i + currentLeadIndex)) + 0.5f;
                    targetIntensities[i] = sinValue > spanProportion ? targetIntensity.ScaledValue : 0f;
                }
                break;
            case LightPattern.EveryOther:
                for (int i = 0; i < lightsArray.Length; i++)
                {
                    if (i % 2 == currentLeadIndex % 2)
                    {
                        targetIntensities[i] = targetIntensity.ScaledValue;
                    }
                    else
                    {
                        targetIntensities[i] = 0f;
                    }
                }
                break;
            case LightPattern.MiddleOut:
                int middle = lightsArray.Length / 2;
                int offset = currentLeadIndex % middle;
                for (int i = 0; i < lightsArray.Length; i++)
                {
                    if (i == middle + offset || i == middle - offset)
                    {
                        targetIntensities[i] = targetIntensity.ScaledValue;
                    }
                    else
                    {
                        targetIntensities[i] = 0f;
                    }
                }
                break;

        }

        if (forwardDirection.Value)
        {
            currentLeadIndex++;
            if (currentLeadIndex >= lightsArray.Length) currentLeadIndex = 0;
        }
        else
        {
            currentLeadIndex--;
            if (currentLeadIndex < 0) currentLeadIndex = lightsArray.Length - 1;
        }
    }


    private void SetChaseSpeed(float value) 
    {
        if (value == 0.0f) 
        {
            for (int i = 0; i < lightsArray.Length; i++)
            {
                targetIntensities[i] = 0.0f;
            }
        }
    }

    private void SetColour(Color value)
    {

        for (int i = 0; i < lightsArray.Length; i++)
        {
            lightsArray[i].color = value;   
        }
      
    }

    protected override void OnFXEnabled(bool state)
    {

        foreach (Light light in lightsArray)
        {
            light.intensity = 0.0f;
            light.enabled = state;
        }
    }
}
