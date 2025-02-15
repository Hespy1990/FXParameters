using System.Collections.Generic;
using UnityEngine;

namespace FX.Patterns
{
    public class OscillatorPattern : PatternBase
    {

        [HideInInspector]
        public List<float> pattern = new List<float>();
        float beatDuration;
        float frequency;
        private float previousValue = 0f;
        public enum OscillatorType
        {
            Sine,
            Square,
            Triangle,
            Sawtooth
        }


        private OscillatorType oscillatorType = OscillatorType.Sine;
        public OscillatorType Oscillator
        {
            get { return oscillatorType; }
            set 
            {
                if (oscillatorType != value) 
                {
                    oscillatorType = value;
                    NotifyPropertyChanged();
                }
                GeneratePattern();
            }
        }

        public override void Start()
        {
            base.Start();
            GeneratePattern();

        }

        public override void GeneratePattern()
        {
            beatDuration = 60f / bpm;
            frequency = 1f / (beatDuration * numBeats);
            int steps = 20;
            pattern = new List<float>();
            for (int i = 0; i < steps; i++)
            {
                float phase = (float)i / steps;
                pattern.Add(GetCurrentValue(oscillatorType, phase));
            }
        }

        private void Update()
        {
            phase += Time.deltaTime * frequency;
            phase %= 1f;
            _currentValue = GetCurrentValue(oscillatorType, phase);
        }

        public float GetCurrentValue(OscillatorType oscillatorType, float phase)
        {
            float currentValue = 0f;
            switch (oscillatorType)
            {
                case OscillatorType.Sine:
                    float sinValue = (Mathf.Sin((phase * 2.0f * Mathf.PI) - (Mathf.PI * 0.5f)) + 1f) / 2f;
                    currentValue = sinValue;
                    break;
                case OscillatorType.Square:
                    float squareValue = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * phase));
                    currentValue = (squareValue + 1f) / 2f;
                    if (previousValue != currentValue) Trigger();
                    break;
                case OscillatorType.Triangle:
                    float triangleValue = Mathf.Abs(2f * (phase - Mathf.Floor(0.5f + phase)));
                    currentValue = triangleValue;
                    break;
                case OscillatorType.Sawtooth:
                    float sawtoothValue = phase - Mathf.Floor(phase);
                    currentValue = sawtoothValue;
                    break;
                default:
                    Debug.LogError("Invalid oscillator type!");
                    break;
            }
            previousValue = currentValue;
            return currentValue;
        }

        public float Map(float value, float inputMin, float inputMax, float outputMin, float outputMax)
        {
            return (value - inputMin) * (outputMax - outputMin) / (inputMax - inputMin) + outputMin;
        }

        public override void HandleBpmChange(float number)
        {
            base.HandleBpmChange(number);
            beatDuration = 60f / bpm;
            GeneratePattern();
        }

    }
}

