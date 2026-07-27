using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MathE
{
    public static Vector2 ConvertEulerAngleToVector2(float angle, bool isRadian)
    {
        /*
        MathE Angle Axis
                0
                |
        270 <--  --> 90
                |
                180
        */
        Vector2 result = Vector2.zero;
        if (!isRadian)
            angle = angle * (Mathf.PI / 180);
        result.x = Mathf.Sin(angle);
        result.y = Mathf.Cos(angle);

        return result;
    }
    public static float ConvertVector2ToEulerAngle(Vector2 vector)
    {
        /*
        MathE Angle Axis
                0
                |
        270 <--  --> 90
                |
                180
        */
        if (vector.x < 0)
        {
            return 360 - (Mathf.Atan2(vector.x, vector.y) * Mathf.Rad2Deg * -1);
        }
        else
        {
            return Mathf.Atan2(vector.x, vector.y) * Mathf.Rad2Deg;
        }
    }
    public static Vector2 RotateVector2(Vector2 vector, float howMuchToRotate)
    {
        float magnitude = vector.magnitude;
        float vectorAngle = ConvertVector2ToEulerAngle(vector);
        vectorAngle += howMuchToRotate;
        return ConvertEulerAngleToVector2(vectorAngle, false) * magnitude;
    }
    public static float ReturnMirrorEulerAngle(float angle)
    {
        if (angle == 180 || angle == 360 || angle == 0)
            return angle;

        if (angle > 180 && angle < 360)
            return 180 - (angle - 180);
        else
            return (180 - angle) + 180;
    }
    public static float ConvertMathEAngleAxisToUnityZAngleAxis(float angle)
    {

        /*
        
        From -

        MathE Angle Axis
                0
                |
        270 <--  --> 90
                |
                180 
        
        To -

        Unity Z Angle Axis
               90
                |
        180 <--  --> 0
                |
               270
        */

        angle = ReturnMirrorEulerAngle(angle);
        angle += 90;
        if (angle > 360)
            angle -= 360;

        return angle;
    }
    public static float ConvertUnityZAngleAxisToMathEAngleAxis(float angle)
    {

        /*
        
        From -

        Unity Z Angle Axis
               90
                |
        180 <--  --> 0
                |
               270

        To -

        MathE Angle Axis
                0
                |
        270 <--  --> 90
                |
                180 
        
        */

        angle -= 90;
        if (angle < 0)
            angle += 360;
        angle = ReturnMirrorEulerAngle(angle);
        return angle;
    }
    public static float CalculateTriangleArea(Vector2 v0, Vector2 v1, Vector2 v2)
    {   /*
                     v1
                     /\
                    /| \
                   / |  \ C
                  / H|   \ 
               v0/___|__|a\v2
                     B

            B = base = v02 magnitude
            a = angle between B and C = angle between v12 and v02
            H = height = sin(a) * C
            Triangle Area --> (H * B) / 2 = S
        */

        float S = 0;
        Vector2 v02 = v0 - v2;
        float B = v02.magnitude;

        Vector2 v12 = v1 - v2;
        float a = Vector2.Angle(v12, v02);
        if (a > 90)
            a = 180 - a;

        float H = Mathf.Sin(Mathf.Deg2Rad * a) * v12.magnitude;
        S = (H * B) / 2;

        return S;
    }
}
