using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

#if !SDL2
using System.Windows.Forms;
#endif

using OpenTK.Audio;
using OpenTK.Audio.OpenAL;

namespace Microsoft.Xna.Framework.Audio
{
    internal sealed class OpenALSoundController : IDisposable
    {
        const int PreallocatedBuffers = 256;
        const int PreallocatedSources = 64;
        const int ExpandSize = 32;
        const float BufferTimeout = 10; // in seconds

        public const float EmitterDepth = 2.0f;

        class BufferAllocation
        {
            public int BufferId;
            public int SourceCount;
            public float SinceUnused;
        }

        public static void Initialize()
        {
            //Log("Initializing locks");

            ActiveLock = new ReaderWriterLockSlim();
            FilteringLock = new ReaderWriterLockSlim();
            AllocationsLock = new ReaderWriterLockSlim();

            //Log("Initializing controller");

            instance = new OpenALSoundController();
        }
        static OpenALSoundController instance;
        public static OpenALSoundController Instance
        {
            get { return instance; }
        }

        readonly AudioContext context;

        readonly ConcurrentStack<int> freeSources;
        readonly ConcurrentStack<int> freeBuffers;
        readonly Dictionary<SoundEffect, BufferAllocation> allocatedBuffers;

        readonly HashSet<int> filteredSources;
        readonly List<SoundEffectInstance> activeSoundEffects;

        static ReaderWriterLockSlim ActiveLock;
        static ReaderWriterLockSlim FilteringLock;
        static ReaderWriterLockSlim AllocationsLock;

        readonly int filterId;

        int totalSources, totalBuffers;
        float lowpassGainHf = 1;

        static void Log(string message)
        {
            try
            {
                Console.WriteLine("({0}) [{1}] {2}", DateTime.Now.ToString("HH:mm:ss.fff"), "OpenAL", message);
                string filePath;
                if (    Environment.OSVersion.Platform == PlatformID.MacOSX ||
                        Environment.OSVersion.Platform == PlatformID.Unix       )
                {
                    filePath = Storage.StorageDevice.StorageRoot + "/FEZ/Debug Log.txt";
                }
                else
                {
                    filePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    filePath += "\\FEZ\\Debug Log.txt";
                }
                using (var stream = File.Open(filePath, FileMode.Append))
                {
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.WriteLine("({0}) [{1}] {2}",
                            DateTime.Now.ToString("HH:mm:ss.fff"), "OpenAL", message);
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show("Error writing log (" + ex.ToString() + "), wanted to write : " + message);
                // NOT THAT BIG A DEAL GUYS
            }
        }

        private OpenALSoundController()
        {
            try
            {
                context = new AudioContext();
            }
            catch (Exception ex)
            {
                Log(ex.ToString());

#if SDL2
                SDL2.SDL.SDL_ShowSimpleMessageBox(
                    SDL2.SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR,
                    "OpenAL Error",
                    "Error initializing audio subsystem. Game will now exit.\n" +
                    "(see debug log for more details)",
                    Game.Instance.Window.Handle
                );
#else
                Log("Last error in enumerator is " + AudioDeviceEnumerator.LastError);

                MessageBox.Show("Error initializing audio subsystem. Game will now exit.\n" +
                                "(see debug log for more details)", "OpenAL Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
#endif
                throw;
            }

            Log("Context created");

            // log how many sources we have access to
            var attributesSize = new int[1];
            var device = Alc.GetContextsDevice(Alc.GetCurrentContext());
            Alc.GetInteger(device, AlcGetInteger.AttributesSize, 1, attributesSize);
            var attributes = new int[attributesSize[0]];
            Alc.GetInteger(device, AlcGetInteger.AllAttributes, attributesSize[0], attributes);
            for (int i = 0; i < attributes.Length; i++)
                if (attributes[i] == (int) AlcContextAttributes.MonoSources)
                {
                    Log("Available mono sources : " + attributes[i + 1]);
                    break;
                }

            filterId = ALHelper.Efx.GenFilter();
            ALHelper.Efx.Filter(filterId, EfxFilteri.FilterType, (int)EfxFilterType.Lowpass);
            ALHelper.Efx.Filter(filterId, EfxFilterf.LowpassGain, 1);
            ALHelper.Efx.Filter(filterId, EfxFilterf.LowpassGainHF, 1);
            ALHelper.Check();

            AL.DistanceModel(ALDistanceModel.None);
            ALHelper.Check();

            // listener settings
            AL.Listener(ALListener3f.Position, 0, 0, 0);
            AL.Listener(ALListener3f.Velocity, 0, 0, 0);
            var orientation = new float[] { 0, 0, -1, 0, 1, 0 };
            AL.Listener(ALListenerfv.Orientation, ref orientation);

            freeBuffers = new ConcurrentStack<int>();
            ExpandBuffers(PreallocatedBuffers);

            allocatedBuffers = new Dictionary<SoundEffect, BufferAllocation>();
            staleAllocations = new List<KeyValuePair<SoundEffect, BufferAllocation>>();

            filteredSources = new HashSet<int>();
            activeSoundEffects = new List<SoundEffectInstance>();
            freeSources = new ConcurrentStack<int>();
            ExpandSources(PreallocatedSources);

            Log("Sound manager initialized!");
        }

        public int RegisterSfxInstance(SoundEffectInstance instance, bool forceNoFilter = false)
        {
            ActiveLock.EnterWriteLock();
            activeSoundEffects.Add(instance);
            ActiveLock.ExitWriteLock();

            var doFilter = !forceNoFilter &&
                           !instance.SoundEffect.Name.Contains("Ui") && !instance.SoundEffect.Name.Contains("Warp") &&
                           !instance.SoundEffect.Name.Contains("Zoom") && !instance.SoundEffect.Name.Contains("Trixel");
            return TakeSourceFor(instance.SoundEffect, doFilter);
        }

        readonly List<KeyValuePair<SoundEffect, BufferAllocation>> staleAllocations;
        public void Update(GameTime gameTime)
        {
            ActiveLock.EnterUpgradeableReadLock();
            for (int i = activeSoundEffects.Count - 1; i >= 0; i--)
            {
                var sfx = activeSoundEffects[i];
                if (sfx.RefreshState() || sfx.IsDisposed)
                {
                    ActiveLock.EnterWriteLock();
                    if (!sfx.IsDisposed) sfx.Dispose();
                    activeSoundEffects.RemoveAt(i);
                    ActiveLock.ExitWriteLock();
                }
            }
            ActiveLock.ExitUpgradeableReadLock();

            var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
            AllocationsLock.EnterUpgradeableReadLock();
            foreach (var kvp in allocatedBuffers)
                if (kvp.Value.SourceCount == 0)
                {
                    kvp.Value.SinceUnused += elapsedSeconds;
                    if (kvp.Value.SinceUnused >= BufferTimeout)
                        staleAllocations.Add(kvp);
                }

            foreach (var kvp in staleAllocations)
            {
                //Trace.WriteLine("[OpenAL] Deleting buffer for " + kvp.Key.Name);
                AllocationsLock.EnterWriteLock();
                allocatedBuffers.Remove(kvp.Key);
                AllocationsLock.ExitWriteLock();
                freeBuffers.Push(kvp.Value.BufferId);
            }
            AllocationsLock.ExitUpgradeableReadLock();

            TidySources();
            TidyBuffers();

            staleAllocations.Clear();
        }

//        public void RegisterSoundEffect(SoundEffect soundEffect)
//        {
//            if (allocatedBuffers.ContainsKey(soundEffect)) return;
//
//            if (freeBuffers.Count == 0) ExpandBuffers();
//            Trace.WriteLine("[OpenAL] Pre-allocating buffer for " + soundEffect.Name);
//            BufferAllocation allocation;
//            allocatedBuffers.Add(soundEffect, allocation = new BufferAllocation { BufferId = freeBuffers.Pop(), SinceUnused = -1 });
//            //lock (bufferDataMutex)
//            AL.BufferData(allocation.BufferId, soundEffect.Format, soundEffect._data, soundEffect.Size, soundEffect.Rate);
//            ALHelper.Check();
//        }

        public void DestroySoundEffect(SoundEffect soundEffect)
        {
            BufferAllocation allocation;
            AllocationsLock.EnterUpgradeableReadLock();
            if (!allocatedBuffers.TryGetValue(soundEffect, out allocation))
            {
                AllocationsLock.ExitUpgradeableReadLock();
                return;
            }

            bool foundActive = false;
            ActiveLock.EnterUpgradeableReadLock();
            for (int i = activeSoundEffects.Count - 1; i >= 0; i--)
            {
                var sfx = activeSoundEffects[i];
                if (sfx.SoundEffect == soundEffect)
                {
                    ActiveLock.EnterWriteLock();
                    if (!sfx.IsDisposed)
                    {
                        foundActive = true;
                        sfx.Stop();
                        sfx.Dispose();
                    }
                    activeSoundEffects.RemoveAt(i);
                    ActiveLock.ExitWriteLock();
                }
            }
            ActiveLock.ExitUpgradeableReadLock();

            if (foundActive)
                Trace.WriteLine("[OpenAL] Delete active sources & buffer for " + soundEffect.Name);

            Debug.Assert(allocation.SourceCount == 0);

            AllocationsLock.EnterWriteLock();
            allocatedBuffers.Remove(soundEffect);
            AllocationsLock.ExitWriteLock();

            freeBuffers.Push(allocation.BufferId);
            AllocationsLock.ExitUpgradeableReadLock();
        }

        int TakeSourceFor(SoundEffect soundEffect, bool filter = false)
        {
            int sourceId;
            while (!freeSources.TryPop(out sourceId))
                ExpandSources();

            if (filter && ALHelper.Efx.IsInitialized)
            {
                ALHelper.Efx.Filter(filterId, EfxFilterf.LowpassGainHF, MathHelper.Clamp(lowpassGainHf, 0, 1));
                ALHelper.Efx.BindFilterToSource(sourceId, filterId);
                FilteringLock.EnterWriteLock();
                filteredSources.Add(sourceId);
                FilteringLock.ExitWriteLock();
            }

            BufferAllocation allocation;
            AllocationsLock.EnterUpgradeableReadLock();
            if (!allocatedBuffers.TryGetValue(soundEffect, out allocation))
            {
                //Trace.WriteLine("[OpenAL] Allocating buffer for " + soundEffect.Name);
                allocation = new BufferAllocation();
                while (!freeBuffers.TryPop(out allocation.BufferId))
                    ExpandBuffers();
                AllocationsLock.EnterWriteLock();
                allocatedBuffers.Add(soundEffect, allocation);
                AllocationsLock.ExitWriteLock();
                AL.BufferData(allocation.BufferId, soundEffect.Format, soundEffect._data, soundEffect.Size, soundEffect.Rate);
                ALHelper.Check();
            }
            allocation.SourceCount++;

            AL.BindBufferToSource(sourceId, allocation.BufferId);
            ALHelper.Check();
            AllocationsLock.ExitUpgradeableReadLock();

            return sourceId;
        }

        public void ReturnSourceFor(SoundEffect soundEffect, int sourceId)
        {
            BufferAllocation allocation;
            AllocationsLock.EnterReadLock();
            if (allocatedBuffers.TryGetValue(soundEffect, out allocation))
            {
                allocation.SourceCount--;
                if (allocation.SourceCount == 0) allocation.SinceUnused = 0;
                Debug.Assert(allocation.SourceCount >= 0);
            }
            AllocationsLock.ExitReadLock();

            ReturnSource(sourceId);
        }

        public int[] TakeBuffers(int count)
        {
            if (count == 0)
                throw new ArgumentException("Attempting to take 0 OpenAL buffers -- why?");

            var buffersIds = new int[count];
            int popped = 0;
            while (popped < count)
            {
                while (!freeBuffers.TryPop(out buffersIds[popped]))
                    ExpandBuffers();
                popped++;
            }

            return buffersIds;
        }

        public int TakeSource()
        {
            int sourceId;
            while (!freeSources.TryPop(out sourceId))
                ExpandSources();

            if (ALHelper.Efx.IsInitialized)
            {
                FilteringLock.EnterWriteLock();
                filteredSources.Add(sourceId);
                ALHelper.Efx.Filter(filterId, EfxFilterf.LowpassGainHF, MathHelper.Clamp(lowpassGainHf, 0, 1));
                ALHelper.Efx.BindFilterToSource(sourceId, filterId);
                FilteringLock.ExitWriteLock();
            }

            return sourceId;
        }

        public void SetSourceFiltered(int sourceId, bool filtered)
        {
            if (!ALHelper.Efx.IsInitialized) return;

            FilteringLock.EnterWriteLock();
            if (!filtered && filteredSources.Remove(sourceId))
            {
                ALHelper.Efx.Filter(filterId, EfxFilterf.LowpassGainHF, 1);
                ALHelper.Efx.BindFilterToSource(sourceId, 0);
            }
            else if (filtered && !filteredSources.Contains(sourceId))
            {
                filteredSources.Add(sourceId);
                ALHelper.Efx.Filter(filterId, EfxFilterf.LowpassGainHF, MathHelper.Clamp(lowpassGainHf, 0, 1));
                ALHelper.Efx.BindFilterToSource(sourceId, filterId);
            }
            FilteringLock.ExitWriteLock();
        }

        public void ReturnBuffers(int[] bufferIds)
        {
            freeBuffers.PushRange(bufferIds);

            //AL.DeleteBuffers(bufferIds);
            //freeBuffers.PushRange(AL.GenBuffers(bufferIds.Length));

            //Console.WriteLine("Returned " + bufferIds.Length + " buffers, now " + freeBuffers.Count);
        }

        public void ReturnSource(int sourceId)
        {
            ResetSource(sourceId);
            
            //AL.DeleteSource(sourceId);
            //freeSources.Push(AL.GenSource());
        }

        void ResetSource(int sourceId)
        {
            AL.Source(sourceId, ALSourceb.Looping, false);
            ALHelper.Check();
            AL.Source(sourceId, ALSource3f.Position, 0, 0.0f, -EmitterDepth);
            ALHelper.Check();
            AL.Source(sourceId, ALSourcef.Pitch, 1);
            ALHelper.Check();
            AL.Source(sourceId, ALSourcef.Gain, 1);
            ALHelper.Check();
            AL.SourceStop(sourceId);
            ALHelper.Check();
            AL.Source(sourceId, ALSourcei.Buffer, 0);
            ALHelper.Check();

            if (ALHelper.Efx.IsInitialized)
            {
                FilteringLock.EnterWriteLock();
                if (filteredSources.Remove(sourceId))
                    ALHelper.Efx.BindFilterToSource(sourceId, 0);
                FilteringLock.ExitWriteLock();
            }

            ALHelper.Check();

            freeSources.Push(sourceId);
        }

        void ExpandBuffers(int expandSize = ExpandSize)
        {
            totalBuffers += expandSize;
            Trace.WriteLine("[OpenAL] Expanding buffers to " + totalBuffers);

            var newBuffers = AL.GenBuffers(expandSize);
            ALHelper.Check();

            if (ALHelper.XRam.IsInitialized)
            {
                ALHelper.XRam.SetBufferMode(newBuffers.Length, ref newBuffers[0], XRamExtension.XRamStorage.Hardware);
                ALHelper.Check();
            }
            Array.Reverse(newBuffers);
            freeBuffers.PushRange(newBuffers);
        }

        void ExpandSources(int expandSize = ExpandSize)
        {
            totalSources += expandSize;
            Trace.WriteLine("[OpenAL] Expanding sources to " + totalSources);

            var newSources = AL.GenSources(expandSize);
            ALHelper.Check();

            for (int i = newSources.Length - 1; i >= 0; i--)
                ResetSource(newSources[i]);
        }

        public float LowPassHFGain
        {
            set
            {
                if (ALHelper.Efx.IsInitialized)
                {
                    FilteringLock.EnterReadLock();
                    foreach (var s in filteredSources)
                    {
                        ALHelper.Efx.Filter(filterId, EfxFilterf.LowpassGainHF, MathHelper.Clamp(value, 0, 1));
                        ALHelper.Efx.BindFilterToSource(s, filterId);
                        ALHelper.Check();
                    }
                    FilteringLock.ExitReadLock();

                    lowpassGainHf = value;
                }
            }
        }

        void TidySources()
        {
            bool tidiedUp = false;
            int sourceId;
            if (freeSources.Count > 2 * PreallocatedSources && freeSources.TryPop(out sourceId))
            {
                AL.DeleteSource(sourceId);
                ALHelper.Check();
                totalSources--;
                tidiedUp = true;
            }
            if (tidiedUp)
                Trace.WriteLine("[OpenAL] Tidied sources down to " + totalSources);
        }
        void TidyBuffers()
        {
            bool tidiedUp = false;
            int bufferId;
            if (freeBuffers.Count > 2 * PreallocatedBuffers && freeBuffers.TryPop(out bufferId))
            {
                AL.DeleteBuffer(bufferId);
                ALHelper.Check();
                totalBuffers--;
                tidiedUp = true;
            }
            if (tidiedUp)
                Trace.WriteLine("[OpenAL] Tidied buffers down to " + totalBuffers);
        }

        public void Dispose()
        {
            if (ALHelper.Efx.IsInitialized)
                ALHelper.Efx.DeleteFilter(filterId);

            int id;
            while (freeSources.TryPop(out id)) AL.DeleteSource(id);
            while (freeBuffers.TryPop(out id)) AL.DeleteBuffer(id);

            context.Dispose();
            instance = null;
        }
    }
}

