#region License
/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2014 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See LICENSE for details.
 */

/* Derived from code by the Mono.Xna Team (Copyright 2006).
 * Released under the MIT License. See monoxna.LICENSE for details.
 */
#endregion

#region Using Statements
using System;
using System.ComponentModel;
using System.Runtime.Serialization;

using Microsoft.Xna.Framework.Design;
#endregion

namespace Microsoft.Xna.Framework
{
	[DataContract]
	[TypeConverter(typeof(PointTypeConverter))]
	public struct Point : IEquatable<Point>
	{
		#region Public Static Properties

		/// <summary>
		/// Returns a <see>Point</see> with coordinates 0, 0.
		/// </summary>
		public static Point Zero
		{
			get
			{
				return zeroPoint;
			}
		}

		#endregion

		#region Public Fields

		[DataMember]
		public int X;

		[DataMember]
		public int Y;

		#endregion

		#region Private Static Variables

		private static Point zeroPoint = new Point();

		#endregion

		#region Public Constructors

		/// <summary>
		/// Creates a <see>Point</see> with the provided coordinates.
		/// </summary>
		/// <param name="x">The x coordinate of the <see>Point</see> to create.</param>
		/// <param name="y">The y coordinate of the <see>Point</see> to create.</param>
		public Point(int x, int y)
		{
			this.X = x;
			this.Y = y;
		}

		#endregion

		#region Public Methods

		public bool Equals(Point other)
		{
			return ((X == other.X) && (Y == other.Y));
		}

		public override bool Equals(object obj)
		{
			return (obj is Point) ? Equals((Point) obj) : false;
		}

		public override int GetHashCode()
		{
			return X ^ Y;
		}

		/// <summary>
		/// Returns a String representation of this Point in the format:
		/// X:[x] Y:[y]
		/// </summary>
		public override string ToString()
		{
			return string.Format("{{X:{0} Y:{1}}}", X, Y);
		}

		#endregion

		#region Public Static Operators

		public static Point operator +(Point a, Point b)
		{
			return new Point(a.X + b.X, a.Y + b.Y);
		}

		public static Point operator -(Point a, Point b)
		{
			return new Point(a.X - b.X, a.Y - b.Y);
		}

		public static Point operator *(Point a, Point b)
		{
			return new Point(a.X * b.X, a.Y * b.Y);
		}

		public static Point operator /(Point a, Point b)
		{
			return new Point(a.X / b.X, a.Y / b.Y);
		}

		public static bool operator ==(Point a, Point b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(Point a, Point b)
		{
			return !a.Equals(b);
		}

		#endregion
	}
}
