using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.Numeric
{
    public struct Int2
    {
        public int X = 0;
        public int Y = 0;

        public Int2(int X, int Y)
        {
            this.X = X;
            this.Y = Y;
        }

        public Int2(Vector2 v)
        {
            this.X = (int)v.X;
            this.Y = (int)v.Y;
        }

        public static Int2 operator+(Int2 v1, Int2 v2)
        {
            return new(v1.X + v2.X, v1.Y + v2.Y);
        }

        public Vector2 ToVector2() 
        { 
            return new Vector2(X, Y); 
        }
    }
}
