using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.MathHelpers
{
    /// <summary>
    /// A structure encapsulating three Double precision doubleing point values and provides hardware accelerated methods.
    /// </summary>
    public partial struct Vector3Double : IEquatable<Vector3Double>, IFormattable
    {
        #region Public Static Properties
        /// <summary>
        /// Returns the vector (0,0,0).
        /// </summary>
        public static Vector3Double Zero { get { return new Vector3Double(); } }
        /// <summary>
        /// Returns the vector (1,1,1).
        /// </summary>
        public static Vector3Double One { get { return new Vector3Double(1.0f, 1.0f, 1.0f); } }
        /// <summary>
        /// Returns the vector (1,0,0).
        /// </summary>
        public static Vector3Double UnitX { get { return new Vector3Double(1.0f, 0.0f, 0.0f); } }
        /// <summary>
        /// Returns the vector (0,1,0).
        /// </summary>
        public static Vector3Double UnitY { get { return new Vector3Double(0.0f, 1.0f, 0.0f); } }
        /// <summary>
        /// Returns the vector (0,0,1).
        /// </summary>
        public static Vector3Double UnitZ { get { return new Vector3Double(0.0f, 0.0f, 1.0f); } }
        #endregion Public Static Properties

        #region Public Instance Methods

        /// <summary>
        /// Returns a boolean indicating whether the given Object is equal to this Vector3Double instance.
        /// </summary>
        /// <param name="obj">The Object to compare against.</param>
        /// <returns>True if the Object is equal to this Vector3Double; False otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (!(obj is Vector3Double))
                return false;
            return Equals((Vector3Double)obj);
        }

        /// <summary>
        /// Returns a String representing this Vector3Double instance.
        /// </summary>
        /// <returns>The string representation.</returns>
        public override string ToString()
        {
            return ToString("G", CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Returns a String representing this Vector3Double instance, using the specified format to format individual elements.
        /// </summary>
        /// <param name="format">The format of individual elements.</param>
        /// <returns>The string representation.</returns>
        public string ToString(string format)
        {
            return ToString(format, CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Returns a String representing this Vector3Double instance, using the specified format to format individual elements 
        /// and the given IFormatProvider.
        /// </summary>
        /// <param name="format">The format of individual elements.</param>
        /// <param name="formatProvider">The format provider to use when formatting elements.</param>
        /// <returns>The string representation.</returns>
        public string ToString(string format, IFormatProvider formatProvider)
        {
            StringBuilder sb = new StringBuilder();
            string separator = NumberFormatInfo.GetInstance(formatProvider).NumberGroupSeparator;
            sb.Append('<');
            sb.Append(((IFormattable)this.X).ToString(format, formatProvider));
            sb.Append(separator);
            sb.Append(' ');
            sb.Append(((IFormattable)this.Y).ToString(format, formatProvider));
            sb.Append(separator);
            sb.Append(' ');
            sb.Append(((IFormattable)this.Z).ToString(format, formatProvider));
            sb.Append('>');
            return sb.ToString();
        }

        /// <summary>
        /// Returns the length of the vector.
        /// </summary>
        /// <returns>The vector's length.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Length()
        {
            if (Vector.IsHardwareAccelerated)
            {
                double ls = Vector3Double.Dot(this, this);
                return (double)System.Math.Sqrt(ls);
            }
            else
            {
                double ls = X * X + Y * Y + Z * Z;
                return (double)System.Math.Sqrt(ls);
            }
        }

        /// <summary>
        /// Returns the length of the vector squared. This operation is cheaper than Length().
        /// </summary>
        /// <returns>The vector's length squared.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double LengthSquared()
        {
            if (Vector.IsHardwareAccelerated)
            {
                return Vector3Double.Dot(this, this);
            }
            else
            {
                return X * X + Y * Y + Z * Z;
            }
        }
        #endregion Public Instance Methods

        #region Public Static Methods
        /// <summary>
        /// Returns the Euclidean distance between the two given points.
        /// </summary>
        /// <param name="value1">The first point.</param>
        /// <param name="value2">The second point.</param>
        /// <returns>The distance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Distance(Vector3Double value1, Vector3Double value2)
        {
            if (Vector.IsHardwareAccelerated)
            {
                Vector3Double difference = value1 - value2;
                double ls = Vector3Double.Dot(difference, difference);
                return (double)System.Math.Sqrt(ls);
            }
            else
            {
                double dx = value1.X - value2.X;
                double dy = value1.Y - value2.Y;
                double dz = value1.Z - value2.Z;

                double ls = dx * dx + dy * dy + dz * dz;

                return (double)System.Math.Sqrt((double)ls);
            }
        }

        /// <summary>
        /// Returns the Euclidean distance squared between the two given points.
        /// </summary>
        /// <param name="value1">The first point.</param>
        /// <param name="value2">The second point.</param>
        /// <returns>The distance squared.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double DistanceSquared(Vector3Double value1, Vector3Double value2)
        {
            if (Vector.IsHardwareAccelerated)
            {
                Vector3Double difference = value1 - value2;
                return Vector3Double.Dot(difference, difference);
            }
            else
            {
                double dx = value1.X - value2.X;
                double dy = value1.Y - value2.Y;
                double dz = value1.Z - value2.Z;

                return dx * dx + dy * dy + dz * dz;
            }
        }

        /// <summary>
        /// Returns a vector with the same direction as the given vector, but with a length of 1.
        /// </summary>
        /// <param name="value">The vector to normalize.</param>
        /// <returns>The normalized vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double Normalize(Vector3Double value)
        {
            if (Vector.IsHardwareAccelerated)
            {
                double length = value.Length();
                return value / length;
            }
            else
            {
                double ls = value.X * value.X + value.Y * value.Y + value.Z * value.Z;
                double length = (double)System.Math.Sqrt(ls);
                return new Vector3Double(value.X / length, value.Y / length, value.Z / length);
            }
        }

        /// <summary>
        /// Computes the cross product of two vectors.
        /// </summary>
        /// <param name="vector1">The first vector.</param>
        /// <param name="vector2">The second vector.</param>
        /// <returns>The cross product.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double Cross(Vector3Double vector1, Vector3Double vector2)
        {
            return new Vector3Double(
                vector1.Y * vector2.Z - vector1.Z * vector2.Y,
                vector1.Z * vector2.X - vector1.X * vector2.Z,
                vector1.X * vector2.Y - vector1.Y * vector2.X);
        }

        /// <summary>
        /// Returns the reflection of a vector off a surface that has the specified normal.
        /// </summary>
        /// <param name="vector">The source vector.</param>
        /// <param name="normal">The normal of the surface being reflected off.</param>
        /// <returns>The reflected vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double Reflect(Vector3Double vector, Vector3Double normal)
        {
            if (Vector.IsHardwareAccelerated)
            {
                double dot = Vector3Double.Dot(vector, normal);
                Vector3Double temp = normal * dot * 2f;
                return vector - temp;
            }
            else
            {
                double dot = vector.X * normal.X + vector.Y * normal.Y + vector.Z * normal.Z;
                double tempX = normal.X * dot * 2f;
                double tempY = normal.Y * dot * 2f;
                double tempZ = normal.Z * dot * 2f;
                return new Vector3Double(vector.X - tempX, vector.Y - tempY, vector.Z - tempZ);
            }
        }

        /// <summary>
        /// Restricts a vector between a min and max value.
        /// </summary>
        /// <param name="value1">The source vector.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <returns>The restricted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double Clamp(Vector3Double value1, Vector3Double min, Vector3Double max)
        {
            // This compare order is very important!!!
            // We must follow HLSL behavior in the case user specified min value is bigger than max value.

            double x = value1.X;
            x = (x > max.X) ? max.X : x;
            x = (x < min.X) ? min.X : x;

            double y = value1.Y;
            y = (y > max.Y) ? max.Y : y;
            y = (y < min.Y) ? min.Y : y;

            double z = value1.Z;
            z = (z > max.Z) ? max.Z : z;
            z = (z < min.Z) ? min.Z : z;

            return new Vector3Double(x, y, z);
        }

        /// <summary>
        /// Linearly interpolates between two vectors based on the given weighting.
        /// </summary>
        /// <param name="value1">The first source vector.</param>
        /// <param name="value2">The second source vector.</param>
        /// <param name="amount">Value between 0 and 1 indicating the weight of the second source vector.</param>
        /// <returns>The interpolated vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double Lerp(Vector3Double value1, Vector3Double value2, double amount)
        {
            if (Vector.IsHardwareAccelerated)
            {
                Vector3Double firstInfluence = value1 * (1f - amount);
                Vector3Double secondInfluence = value2 * amount;
                return firstInfluence + secondInfluence;
            }
            else
            {
                return new Vector3Double(
                    value1.X + (value2.X - value1.X) * amount,
                    value1.Y + (value2.Y - value1.Y) * amount,
                    value1.Z + (value2.Z - value1.Z) * amount);
            }
        }

        /// <summary>
        /// Transforms a vector by the given matrix.
        /// </summary>
        /// <param name="position">The source vector.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <returns>The transformed vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double Transform(Vector3Double position, Matrix4x4 matrix)
        {
            return new Vector3Double(
                position.X * matrix.M11 + position.Y * matrix.M21 + position.Z * matrix.M31 + matrix.M41,
                position.X * matrix.M12 + position.Y * matrix.M22 + position.Z * matrix.M32 + matrix.M42,
                position.X * matrix.M13 + position.Y * matrix.M23 + position.Z * matrix.M33 + matrix.M43);
        }

        /// <summary>
        /// Transforms a vector normal by the given matrix.
        /// </summary>
        /// <param name="normal">The source vector.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <returns>The transformed vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double TransformNormal(Vector3Double normal, Matrix4x4 matrix)
        {
            return new Vector3Double(
                normal.X * matrix.M11 + normal.Y * matrix.M21 + normal.Z * matrix.M31,
                normal.X * matrix.M12 + normal.Y * matrix.M22 + normal.Z * matrix.M32,
                normal.X * matrix.M13 + normal.Y * matrix.M23 + normal.Z * matrix.M33);
        }

        /// <summary>
        /// Transforms a vector by the given Quaternion rotation value.
        /// </summary>
        /// <param name="value">The source vector to be rotated.</param>
        /// <param name="rotation">The rotation to apply.</param>
        /// <returns>The transformed vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double Transform(Vector3Double value, Quaternion rotation)
        {
            double x2 = rotation.X + rotation.X;
            double y2 = rotation.Y + rotation.Y;
            double z2 = rotation.Z + rotation.Z;

            double wx2 = rotation.W * x2;
            double wy2 = rotation.W * y2;
            double wz2 = rotation.W * z2;
            double xx2 = rotation.X * x2;
            double xy2 = rotation.X * y2;
            double xz2 = rotation.X * z2;
            double yy2 = rotation.Y * y2;
            double yz2 = rotation.Y * z2;
            double zz2 = rotation.Z * z2;

            return new Vector3Double(
                value.X * (1.0f - yy2 - zz2) + value.Y * (xy2 - wz2) + value.Z * (xz2 + wy2),
                value.X * (xy2 + wz2) + value.Y * (1.0f - xx2 - zz2) + value.Z * (yz2 - wx2),
                value.X * (xz2 - wy2) + value.Y * (yz2 + wx2) + value.Z * (1.0f - xx2 - yy2));
        }
        #endregion Public Static Methods

        #region Public operator methods

        // All these methods should be inlined as they are implemented
        // over JIT intrinsics

        /// <summary>
        /// Adds two vectors together.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The summed vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double Add(Vector3Double left, Vector3Double right)
        {
            return left + right;
        }

        /// <summary>
        /// Subtracts the second vector from the first.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The difference vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double Subtract(Vector3Double left, Vector3Double right)
        {
            return left - right;
        }

        /// <summary>
        /// Multiplies two vectors together.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The product vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double Multiply(Vector3Double left, Vector3Double right)
        {
            return left * right;
        }

        /// <summary>
        /// Multiplies a vector by the given scalar.
        /// </summary>
        /// <param name="left">The source vector.</param>
        /// <param name="right">The scalar value.</param>
        /// <returns>The scaled vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double Multiply(Vector3Double left, Double right)
        {
            return left * right;
        }

        /// <summary>
        /// Multiplies a vector by the given scalar.
        /// </summary>
        /// <param name="left">The scalar value.</param>
        /// <param name="right">The source vector.</param>
        /// <returns>The scaled vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double Multiply(Double left, Vector3Double right)
        {
            return left * right;
        }

        /// <summary>
        /// Divides the first vector by the second.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The vector resulting from the division.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double Divide(Vector3Double left, Vector3Double right)
        {
            return left / right;
        }

        /// <summary>
        /// Divides the vector by the given scalar.
        /// </summary>
        /// <param name="left">The source vector.</param>
        /// <param name="divisor">The scalar value.</param>
        /// <returns>The result of the division.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double Divide(Vector3Double left, Double divisor)
        {
            return left / divisor;
        }

        /// <summary>
        /// Negates a given vector.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <returns>The negated vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double Negate(Vector3Double value)
        {
            return -value;
        }
        #endregion Public operator methods

        /// <summary>
        /// The X component of the vector.
        /// </summary>
        public Double X;
        /// <summary>
        /// The Y component of the vector.
        /// </summary>
        public Double Y;
        /// <summary>
        /// The Z component of the vector.
        /// </summary>
        public Double Z;

        #region Constructors
        /// <summary>
        /// Constructs a vector whose elements are all the Double specified value.
        /// </summary>
        /// <param name="value">The element to fill the vector with.</param>
        
        public Vector3Double(Double value) : this(value, value, value) { }

        /// <summary>
        /// Constructs a Vector3Double from the given Vector2 and a third value.
        /// </summary>
        /// <param name="value">The Vector to extract X and Y components from.</param>
        /// <param name="z">The Z component.</param>
        public Vector3Double(Vector2Double value, double z) : this(value.X, value.Y, z) { }

        /// <summary>
        /// Constructs a vector with the given individual elements.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        /// <param name="z">The Z component.</param>
        
        public Vector3Double(Double x, Double y, Double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        #endregion Constructors

        #region Public Instance Methods
        /// <summary>
        /// Copies the contents of the vector into the given array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(Double[] array)
        {
            CopyTo(array, 0);
        }

        /// <summary>
        /// Copies the contents of the vector into the given array, starting from index.
        /// </summary>
        /// <exception cref="ArgumentNullException">If array is null.</exception>
        /// <exception cref="RankException">If array is multidimensional.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If index is greater than end of the array or index is less than zero.</exception>
        /// <exception cref="ArgumentException">If number of elements in source vector is greater than those available in destination array.</exception>
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(Double[] array, int index)
        {
            if (array == null)
            {
                // Match the JIT's exception type here. For perf, a NullReference is thrown instead of an ArgumentNull.
                throw new NullReferenceException("Arg_NullArgumentNullRef");
            }
            if (index < 0 || index >= array.Length)
            {
                throw new ArgumentOutOfRangeException("Arg_ArgumentOutOfRangeException");
            }
            if ((array.Length - index) < 3)
            {
                throw new ArgumentException(("Arg_ElementsInSourceIsGreaterThanDestination"));
            }
            array[index] = X;
            array[index + 1] = Y;
            array[index + 2] = Z;
        }

        /// <summary>
        /// Returns a boolean indicating whether the given Vector3Double is equal to this Vector3Double instance.
        /// </summary>
        /// <param name="other">The Vector3Double to compare this instance to.</param>
        /// <returns>True if the other Vector3Double is equal to this instance; False otherwise.</returns>


        public bool Equals(Vector3Double other)
        {
            return X == other.X &&
                   Y == other.Y &&
                   Z == other.Z;
        }
        #endregion Public Instance Methods

        #region Public Static Methods
        /// <summary>
        /// Returns the dot product of two vectors.
        /// </summary>
        /// <param name="vector1">The first vector.</param>
        /// <param name="vector2">The second vector.</param>
        /// <returns>The dot product.</returns>
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Dot(Vector3Double vector1, Vector3Double vector2)
        {
            return vector1.X * vector2.X +
                   vector1.Y * vector2.Y +
                   vector1.Z * vector2.Z;
        }

        /// <summary>
        /// Returns a vector whose elements are the minimum of each of the pairs of elements in the two source vectors.
        /// </summary>
        /// <param name="value1">The first source vector.</param>
        /// <param name="value2">The second source vector.</param>
        /// <returns>The minimized vector.</returns>
        
        public static Vector3Double Min(Vector3Double value1, Vector3Double value2)
        {
            return new Vector3Double(
                (value1.X < value2.X) ? value1.X : value2.X,
                (value1.Y < value2.Y) ? value1.Y : value2.Y,
                (value1.Z < value2.Z) ? value1.Z : value2.Z);
        }

        /// <summary>
        /// Returns a vector whose elements are the maximum of each of the pairs of elements in the two source vectors.
        /// </summary>
        /// <param name="value1">The first source vector.</param>
        /// <param name="value2">The second source vector.</param>
        /// <returns>The maximized vector.</returns>
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double Max(Vector3Double value1, Vector3Double value2)
        {
            return new Vector3Double(
                (value1.X > value2.X) ? value1.X : value2.X,
                (value1.Y > value2.Y) ? value1.Y : value2.Y,
                (value1.Z > value2.Z) ? value1.Z : value2.Z);
        }

        /// <summary>
        /// Returns a vector whose elements are the absolute values of each of the source vector's elements.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <returns>The absolute value vector.</returns>
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double Abs(Vector3Double value)
        {
            return new Vector3Double(Math.Abs(value.X), Math.Abs(value.Y), Math.Abs(value.Z));
        }

        /// <summary>
        /// Returns a vector whose elements are the square root of each of the source vector's elements.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <returns>The square root vector.</returns>
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double SquareRoot(Vector3Double value)
        {
            return new Vector3Double((Double)Math.Sqrt(value.X), (Double)Math.Sqrt(value.Y), (Double)Math.Sqrt(value.Z));
        }
        #endregion Public Static Methods

        #region Public Static Operators
        /// <summary>
        /// Adds two vectors together.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The summed vector.</returns>
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double operator +(Vector3Double left, Vector3Double right)
        {
            return new Vector3Double(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        /// <summary>
        /// Subtracts the second vector from the first.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The difference vector.</returns>
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double operator -(Vector3Double left, Vector3Double right)
        {
            return new Vector3Double(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        /// <summary>
        /// Multiplies two vectors together.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The product vector.</returns>
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double operator *(Vector3Double left, Vector3Double right)
        {
            return new Vector3Double(left.X * right.X, left.Y * right.Y, left.Z * right.Z);
        }

        /// <summary>
        /// Multiplies a vector by the given scalar.
        /// </summary>
        /// <param name="left">The source vector.</param>
        /// <param name="right">The scalar value.</param>
        /// <returns>The scaled vector.</returns>
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double operator *(Vector3Double left, Double right)
        {
            return left * new Vector3Double(right);
        }

        /// <summary>
        /// Multiplies a vector by the given scalar.
        /// </summary>
        /// <param name="left">The scalar value.</param>
        /// <param name="right">The source vector.</param>
        /// <returns>The scaled vector.</returns>
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double operator *(Double left, Vector3Double right)
        {
            return new Vector3Double(left) * right;
        }

        /// <summary>
        /// Divides the first vector by the second.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The vector resulting from the division.</returns>
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double operator /(Vector3Double left, Vector3Double right)
        {
            return new Vector3Double(left.X / right.X, left.Y / right.Y, left.Z / right.Z);
        }

        /// <summary>
        /// Divides the vector by the given scalar.
        /// </summary>
        /// <param name="value1">The source vector.</param>
        /// <param name="value2">The scalar value.</param>
        /// <returns>The result of the division.</returns>
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double operator /(Vector3Double value1, double value2)
        {
            double invDiv = 1.0f / value2;

            return new Vector3Double(
                value1.X * invDiv,
                value1.Y * invDiv,
                value1.Z * invDiv);
        }

        /// <summary>
        /// Negates a given vector.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <returns>The negated vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Double operator -(Vector3Double value)
        {
            return Zero - value;
        }

        /// <summary>
        /// Returns a boolean indicating whether the two given vectors are equal.
        /// </summary>
        /// <param name="left">The first vector to compare.</param>
        /// <param name="right">The second vector to compare.</param>
        /// <returns>True if the vectors are equal; False otherwise.</returns>
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vector3Double left, Vector3Double right)
        {
            return (left.X == right.X &&
                    left.Y == right.Y &&
                    left.Z == right.Z);
        }

        /// <summary>
        /// Returns a boolean indicating whether the two given vectors are not equal.
        /// </summary>
        /// <param name="left">The first vector to compare.</param>
        /// <param name="right">The second vector to compare.</param>
        /// <returns>True if the vectors are not equal; False if they are equal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vector3Double left, Vector3Double right)
        {
            return (left.X != right.X ||
                    left.Y != right.Y ||
                    left.Z != right.Z);
        }
        #endregion Public Static Operators
        public Vector3 ToVector3()
        {
            return new Vector3((float)X, (float)Y, (float)Z);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }
    }

    public static class Vector3DoubleExtension
    {
        public static Vector3Double ToVector3Double(this Vector3 v)
        {
            return new Vector3Double(v.X, v.Y, v.Z);
        }
    }
}
