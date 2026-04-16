using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DeviousTraps.src.SoundCannon
{
    // qualifies as a listener effect, thus it is overridable by ImpactFX
    public class DeafnessListenerFilter : MonoBehaviour
    {
        public float Deafness = 0;          // 0..1

        void OnAudioFilterRead(float[] data, int channels)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] *= 1 - Math.Min(1, Deafness);
        }
    }

}
