using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.ImGuiMethods
{
    public class Box<T>
    {
        public T Value;

        public Box(T value)
        {
            Value = value;
        }
    }
}
