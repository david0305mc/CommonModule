using UnityEngine;

public static class MathUtil
{
    public static float Remap(float value, float from1, float to1, float from2, float to2)
    {
        return from2 + (value - from1) * (to2 - from2) / (to1 - from1);
    }

    public static float Clamp01(float value)
    {
        return Mathf.Clamp01(value);
    }

    public static bool Approximately(float a, float b, float tolerance = 0.0001f)
    {
        return Mathf.Abs(a - b) <= tolerance;
    }

    public static int Sign(int value)
    {
        return value >= 0 ? 1 : -1;
    }
}