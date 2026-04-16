using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DeviousTraps.src
{
    // curves work in a probablistic field of 0 to 1, with y being the # spawned
    internal class FlameTurretSpawnCurves
    {
        public static readonly AnimationCurve WeakCurve =
        new AnimationCurve(
        new Keyframe(0.00f, 0.0f * Plugin.FlameSpawnrate.Value),   // start: basically nothing
        new Keyframe(0.40f, 1f * Plugin.FlameSpawnrate.Value),   // tiny ramp
        new Keyframe(0.70f, 3f * Plugin.FlameSpawnrate.Value),   // slow growth
        new Keyframe(0.90f, 5.0f * Plugin.FlameSpawnrate.Value),   // bend upward
        new Keyframe(0.97f, 6f * Plugin.FlameSpawnrate.Value),  // sharp climb
        new Keyframe(1.00f, 8.0f * Plugin.FlameSpawnrate.Value)   // final spike
        );

        public static readonly AnimationCurve NormalCurve =
        new AnimationCurve(
        new Keyframe(0.00f, 0.0f * Plugin.FlameSpawnrate.Value),   // start: basically nothing
        new Keyframe(0.40f, 2f * Plugin.FlameSpawnrate.Value),   // tiny ramp
        new Keyframe(0.70f, 5f * Plugin.FlameSpawnrate.Value),   // slow growth
        new Keyframe(0.90f, 8.0f * Plugin.FlameSpawnrate.Value),   // bend upward
        new Keyframe(0.97f, 10f * Plugin.FlameSpawnrate.Value),  // sharp climb
        new Keyframe(1.00f, 15.0f * Plugin.FlameSpawnrate.Value)   // final spike
        );

        public static readonly AnimationCurve AggressiveCurve =
        new AnimationCurve(
        new Keyframe(0.00f, 0.0f * Plugin.FlameSpawnrate.Value),   // start: basically nothing
        new Keyframe(0.40f, 5f * Plugin.FlameSpawnrate.Value),   // tiny ramp
        new Keyframe(0.70f, 7f * Plugin.FlameSpawnrate.Value),   // slow growth
        new Keyframe(0.90f, 8.0f * Plugin.FlameSpawnrate.Value),   // bend upward
        new Keyframe(0.97f, 15f * Plugin.FlameSpawnrate.Value),  // sharp climb
        new Keyframe(1.00f, 35.0f * Plugin.FlameSpawnrate.Value)   // final spike
        );
    }
}
