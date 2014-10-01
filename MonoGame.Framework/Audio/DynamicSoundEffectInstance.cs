#region License
/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2014 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See LICENSE for details.
 */
#endregion

#region Using Statements
using System;
using System.Collections.Generic;

using OpenTK.Audio.OpenAL;
#endregion

namespace Microsoft.Xna.Framework.Audio
{
	// http://msdn.microsoft.com/en-us/library/microsoft.xna.framework.audio.dynamicsoundeffectinstance.aspx
	public sealed class DynamicSoundEffectInstance : SoundEffectInstance
	{
		#region Public Properties

		public int PendingBufferCount
		{
			get;
			private set;
		}

		#endregion

		#region Private XNA Variables

		private int sampleRate;
		private AudioChannels channels;

		#endregion

		#region Private OpenAL Variables

		private Queue<int> queuedBuffers;
		private Queue<int> buffersToQueue;
		private Queue<int> availableBuffers;

		#endregion

		#region Private Static XNA->AL Dictionaries

		private static readonly Dictionary<AudioChannels, ALFormat> XNAToShort = new Dictionary<AudioChannels, ALFormat>
		{
			{ AudioChannels.Mono, ALFormat.Mono16 },
			{ AudioChannels.Stereo, ALFormat.Stereo16 }
		};

		private static readonly Dictionary<AudioChannels, ALFormat> XNAToFloat = new Dictionary<AudioChannels, ALFormat>
		{
			{ AudioChannels.Mono, ALFormat.MonoFloat32Ext },
			{ AudioChannels.Stereo, ALFormat.StereoFloat32Ext }
		};

		#endregion

		#region BufferNeeded Event

		public event EventHandler<EventArgs> BufferNeeded;

		#endregion

		#region Public Constructor

		public DynamicSoundEffectInstance(int sampleRate, AudioChannels channels) : base(null)
		{
			this.sampleRate = sampleRate;
			this.channels = channels;

			PendingBufferCount = 0;

			queuedBuffers = new Queue<int>();
			buffersToQueue = new Queue<int>();
			availableBuffers = new Queue<int>();
		}

		#endregion

		#region Destructor

		~DynamicSoundEffectInstance()
		{
			Dispose();
		}

		#endregion

		#region Public Dispose Method

		public override void Dispose()
		{
			if (!IsDisposed)
			{
				base.Dispose(); // Will call Stop(true);

				// Delete all known buffer objects
				while (queuedBuffers.Count > 0)
				{
					AL.DeleteBuffer(queuedBuffers.Dequeue());
				}
				queuedBuffers = null;
				while (availableBuffers.Count > 0)
				{
					AL.DeleteBuffer(availableBuffers.Dequeue());
				}
				availableBuffers = null;
				while (buffersToQueue.Count > 0)
				{
					AL.DeleteBuffer(buffersToQueue.Dequeue());
				}
				buffersToQueue = null;

				IsDisposed = true;
			}
		}

		#endregion

		#region Public Time/Sample Information Methods

		public TimeSpan GetSampleDuration(int sizeInBytes)
		{
			return SoundEffect.GetSampleDuration(
				sizeInBytes,
				sampleRate,
				channels
			);
		}

		public int GetSampleSizeInBytes(TimeSpan duration)
		{
			return SoundEffect.GetSampleSizeInBytes(
				duration,
				sampleRate,
				channels
			);
		}

		#endregion

		#region Public SubmitBuffer Methods

		public void SubmitBuffer(byte[] buffer)
		{
			this.SubmitBuffer(buffer, 0, buffer.Length);
		}

		public void SubmitBuffer(byte[] buffer, int offset, int count)
		{
			// Generate a buffer if we don't have any to use.
			if (availableBuffers.Count == 0)
			{
				availableBuffers.Enqueue(AL.GenBuffer());
			}

			// Push the data to OpenAL.
			int newBuf = availableBuffers.Dequeue();
			AL.BufferData(
				newBuf,
				XNAToShort[channels],
				buffer, // TODO: offset -flibit
				count,
				sampleRate
			);

			// If we're already playing, queue immediately.
			if (State == SoundState.Playing)
			{
				AL.SourceQueueBuffer(INTERNAL_alSource, newBuf);
				queuedBuffers.Enqueue(newBuf);
			}
			else
			{
				buffersToQueue.Enqueue(newBuf);
			}

			PendingBufferCount += 1;
		}

		#endregion

		#region Public Play Method

		public override void Play()
		{
			if (State != SoundState.Stopped)
			{
				return; // No-op if we're already playing.
			}

			if (INTERNAL_alSource != -1)
			{
				// The sound has stopped, but hasn't cleaned up yet...
				AL.SourceStop(INTERNAL_alSource);
				AL.DeleteSource(INTERNAL_alSource);
				INTERNAL_alSource = -1;
			}
			while (queuedBuffers.Count > 0)
			{
				availableBuffers.Enqueue(queuedBuffers.Dequeue());
			}

			INTERNAL_alSource = AL.GenSource();
			if (INTERNAL_alSource == 0)
			{
				System.Console.WriteLine("WARNING: AL SOURCE WAS NOT AVAILABLE. SKIPPING.");
				return;
			}

			// Queue the buffers to this source
			while (buffersToQueue.Count > 0)
			{
				int nextBuf = buffersToQueue.Dequeue();
				queuedBuffers.Enqueue(nextBuf);
				AL.SourceQueueBuffer(INTERNAL_alSource, nextBuf);
			}

			// Apply Pan/Position
			if (INTERNAL_positionalAudio)
			{
				INTERNAL_positionalAudio = false;
				AL.Source(INTERNAL_alSource, ALSource3f.Position, position.X, position.Y, position.Z);
			}
			else
			{
				Pan = Pan;
			}

			// Reassign Properties, in case the AL properties need to be applied.
			Volume = Volume;
			IsLooped = IsLooped;
			Pitch = Pitch;

			// Finally.
			AL.SourcePlay(INTERNAL_alSource);
			OpenALDevice.Instance.dynamicInstancePool.Add(this);

			// ... but wait! What if we need moar buffers?
			if (PendingBufferCount <= 2 && BufferNeeded != null)
			{
				BufferNeeded(this, null);
			}
		}

		#endregion

		#region Internal Update Method

		internal bool Update()
		{
			if (State == SoundState.Stopped)
			{
				/* If we've stopped, remove ourselves from the list.
				 * Do NOT do anything else, Play/Stop/Dispose do this!
				 * -flibit
				 */
				return false;
			}

			// Get the processed buffers.
			int finishedBuffers;
			AL.GetSource(INTERNAL_alSource, ALGetSourcei.BuffersProcessed, out finishedBuffers);
			if (finishedBuffers == 0)
			{
				// Nothing to do... yet.
				return true;
			}

			int[] bufs = AL.SourceUnqueueBuffers(INTERNAL_alSource, finishedBuffers);
			PendingBufferCount -= finishedBuffers;
			if (BufferNeeded != null)
			{
				// PendingBufferCount changed during playback, trigger now!
				BufferNeeded(this, null);
			}

			// Error check our queuedBuffers list.
			for (int i = 0; i < finishedBuffers; i += 1)
			{
				int newBuf = queuedBuffers.Dequeue();
				if (newBuf != bufs[i])
				{
					throw new Exception("Buffer desync!");
				}
				availableBuffers.Enqueue(newBuf);
			}

			// Notify the application that we need moar buffers!
			if (PendingBufferCount <= 2 && BufferNeeded != null)
			{
				BufferNeeded(this, null);
			}

			return true;
		}

		#endregion

		#region Public FNA Extension Methods

		/* THIS IS AN EXTENSION OF THE XNA4 API! */
		public void SubmitFloatBufferEXT(float[] buffer)
		{
			/* Float samples are the typical format received from decoders.
			 * We currently use this for the VideoPlayer.
			 * -flibit
			 */

			// Generate a buffer if we don't have any to use.
			if (availableBuffers.Count == 0)
			{
				availableBuffers.Enqueue(AL.GenBuffer());
			}

			// Push the data to OpenAL.
			int newBuf = availableBuffers.Dequeue();
			AL.BufferData(
				newBuf,
				XNAToFloat[channels],
				buffer,
				buffer.Length * 4,
				sampleRate
			);

			// If we're already playing, queue immediately.
			if (State == SoundState.Playing)
			{
				AL.SourceQueueBuffer(INTERNAL_alSource, newBuf);
				queuedBuffers.Enqueue(newBuf);
			}
			else
			{
				buffersToQueue.Enqueue(newBuf);
			}

			PendingBufferCount += 1;
		}

		#endregion
	}
}
