using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.MathHelpers
{
    public static class MathHelper
    {
        public static Vector3 RotateWorldPoint(Vector3 origin, float angle, Vector3 p)
        {
            if (angle == 0f) return p;
            var s = (float)Math.Sin(angle);
            var c = (float)Math.Cos(angle);

            // translate point back to origin:
            p.X -= origin.X;
            p.Z -= origin.Z;

            // rotate point
            float xnew = p.X * c - p.Z * s;
            float ynew = p.X * s + p.Z * c;

            // translate point back:
            p.X = xnew + origin.X;
            p.Z = ynew + origin.Z;
            return p;
        }

        public static float Float(this int i)
        {
            return (float)i;
        }

        public static Vector2 ToVector2(this Vector3 vector3)
        {
            return new Vector2(vector3.X, vector3.Z);
        }

        public static Vector3 ToVector3(this Vector2 vector2)
        {
            return vector2.ToVector3(Svc.ClientState.LocalPlayer?.Position.Y ?? 0);
        }

        public static Vector3 ToVector3(this Vector2 vector2, float Y)
        {
            return new Vector3(vector2.X, Y, vector2.Y);
        }

        public static float GetRelativeAngle(Vector3 origin, Vector3 target)
        {
            return GetRelativeAngle(origin.ToVector2(), target.ToVector2());
        }

        public static float GetRelativeAngle(Vector2 origin, Vector2 target)
        {
            var vector2 = target - origin;
            var vector1 = new Vector2(0, 1);
            return ((MathF.Atan2(vector2.Y, vector2.X) - MathF.Atan2(vector1.Y, vector1.X)) * (180 / MathF.PI) + 360 + 180) % 360;
        }

        public static float RadToDeg(this float f)
        {
            return (f * (180 / MathF.PI) + 360) % 360;
        }

        public static CardinalDirection GetCardinalDirection(Vector3 origin, Vector3 target)
        {
            return GetCardinalDirection(GetRelativeAngle(origin, target));
        }

        public static CardinalDirection GetCardinalDirection(Vector2 origin, Vector2 target)
        {
            return GetCardinalDirection(GetRelativeAngle(origin, target));
        }

        public static CardinalDirection GetCardinalDirection(float angle)
        {
            if (angle.InRange(45, 135)) return CardinalDirection.East;
            if (angle.InRange(135, 225)) return CardinalDirection.South;
            if (angle.InRange(225, 315)) return CardinalDirection.West;
            return CardinalDirection.North;
        }

        public static bool InRange(this double f, double inclusiveStart, double exclusiveEnd)
        {
            return f >= inclusiveStart && f < exclusiveEnd;
        }

        public static bool InRange(this float f, float inclusiveStart, float exclusiveEnd)
        {
            return f >= inclusiveStart && f < exclusiveEnd;
        }

        public static bool InRange(this long f, long inclusiveStart, long exclusiveEnd)
        {
            return f >= inclusiveStart && f < exclusiveEnd;
        }

        public static bool InRange(this int f, int inclusiveStart, int exclusiveEnd)
        {
            return f >= inclusiveStart && f < exclusiveEnd;
        }

        public static bool InRange(this short f, short inclusiveStart, short exclusiveEnd)
        {
            return f >= inclusiveStart && f < exclusiveEnd;
        }

        public static bool InRange(this byte f, byte inclusiveStart, byte exclusiveEnd)
        {
            return f >= inclusiveStart && f < exclusiveEnd;
        }

        public static bool InRange(this ulong f, ulong inclusiveStart, ulong exclusiveEnd)
        {
            return f >= inclusiveStart && f < exclusiveEnd;
        }

        public static bool InRange(this uint f, uint inclusiveStart, uint exclusiveEnd)
        {
            return f >= inclusiveStart && f < exclusiveEnd;
        }

        public static bool InRange(this ushort f, ushort inclusiveStart, ushort exclusiveEnd)
        {
            return f >= inclusiveStart && f < exclusiveEnd;
        }

        public static bool InRange(this sbyte f, sbyte inclusiveStart, sbyte exclusiveEnd)
        {
            return f >= inclusiveStart && f < exclusiveEnd;
        }
    }
}
