#region License
/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2014 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See LICENSE for details.
 */
#endregion

#if DEBUG

#region Using Statements
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
#endregion

namespace Microsoft.Xna.Framework
{

	internal class PerformanceItem
	{
		#region Public Properties

		public long PreviousTime
		{
			get;
			set;
		}

		public long TotalTime
		{
			get;
			set;
		}

		public long MaxTime
		{
			get;
			set;
		}

		public long HitCount
		{
			get;
			set;
		}

		public string Name
		{
			get;
			set;
		}

		#endregion

		#region Public Methods

		public void Dump()
		{
			Debug.WriteLine(ToString());
		}

		public override string ToString()
		{
			return string.Format(
				"[{0}({1}%)\t HitCount={2}\t TotalTime={3}ms\t " +
					"MaxTime={4}ms\t AverageTime={5}ms]",
				Name,
				(100*TotalTime) / PerformanceCounter.ElapsedTime,
				HitCount,
				TotalTime,
				MaxTime,
				TotalTime/HitCount
			);
		}

		#endregion
	}

	public static class PerformanceCounter
	{
		#region Public Static Properties

		public static long ElapsedTime
		{
			get
			{
				return _endTime - _startTime;
			}
		}

		#endregion

		#region Private Variables

		private static Dictionary<string, PerformanceItem> _list =
			new Dictionary<string, PerformanceItem>();
		private static long _startTime = DateTime.UtcNow.Ticks;
		private static long _endTime;

		#endregion

		#region Public Static Methods

		public static void Dump()
		{
			_endTime = DateTime.UtcNow.Ticks;

			Debug.WriteLine("Performance count results");
			Debug.WriteLine("=========================");
			Debug.WriteLine("Execution Time: " + ElapsedTime.ToString() + "ms.");

			foreach (PerformanceItem item in _list.Values)
			{
				item.Dump();
			}

			Debug.WriteLine("=========================");
		}

		public static void Begin()
		{
			_startTime = DateTime.UtcNow.Ticks;
		}

		public static void BeginMensure(string Name)
		{
			PerformanceItem item;
			if (_list.ContainsKey(Name))
			{
				item = _list[Name];
				item.PreviousTime = DateTime.UtcNow.Ticks;
			}
			else
			{
				item = new PerformanceItem();

				StackTrace stackTrace = new StackTrace();
				StackFrame stackFrame = stackTrace.GetFrame(1);
				MethodBase methodBase = stackFrame.GetMethod();

				item.Name = (
					"ID: " +
					Name +
					" In " +
					methodBase.ReflectedType.ToString() +
					"::" +
					methodBase.Name
				);

				item.PreviousTime = DateTime.UtcNow.Ticks;
				_list.Add(Name, item);
			}
		}

		public static void EndMensure(string Name)
		{
			PerformanceItem item = _list[Name];
			long elapsedTime = DateTime.UtcNow.Ticks - item.PreviousTime;
			if (item.MaxTime < elapsedTime)
			{
				item.MaxTime = elapsedTime;
			}
			item.TotalTime += elapsedTime;
			item.HitCount += 1;
		}

		#endregion
	}
}

#endif
