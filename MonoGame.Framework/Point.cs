#region License
/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2014 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See LICENSE for details.
 */

/*
MIT License
Copyright (C) 2006 The Mono.Xna Team

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
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
