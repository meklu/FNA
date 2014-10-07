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

using OpenAL;
#endregion

namespace Microsoft.Xna.Framework.Audio
{
	/* This class is meant to be a compact container for platform-specific
	 * effect work. Keep general XACT stuff out of here.
	 * -flibit
	 */
	internal abstract class DSPEffect
	{
		#region Public Properties

		public uint Handle
		{
			get;
			private set;
		}

		#endregion

		#region Protected Variables

		protected uint effectHandle;

		#endregion

		#region Public Constructor

		public DSPEffect()
		{
			// Generate the EffectSlot and Effect
			uint handle;
			EFX.alGenAuxiliaryEffectSlots((IntPtr) 1, out handle);
			Handle = handle;
			EFX.alGenEffects((IntPtr) 1, out effectHandle);
		}

		#endregion

		#region Public Dispose Method

		public void Dispose()
		{
			// Delete EFX data
			uint handle = Handle;
			EFX.alDeleteAuxiliaryEffectSlots((IntPtr) 1, ref handle);
			EFX.alDeleteEffects((IntPtr) 1, ref effectHandle);
		}

		#endregion
	}

	internal class DSPReverbEffect : DSPEffect
	{
		#region Public Constructor

		public DSPReverbEffect(DSPParameter[] parameters) : base()
		{
			// Set up the Reverb Effect
			EFX.alEffecti(
				effectHandle,
				EFX.AL_EFFECT_TYPE,
				EFX.AL_EFFECT_EAXREVERB
			);

			// TODO: Use DSP Parameters on EAXReverb Effect. They don't bind very cleanly. :/

			// Bind the Effect to the EffectSlot. XACT will use the EffectSlot.
			EFX.alAuxiliaryEffectSloti(
				Handle,
				EFX.AL_EFFECTSLOT_EFFECT,
				(int) effectHandle
			);
		}

		#endregion

		#region Public Methods

		public void SetGain(float value)
		{
			// Apply the value to the effect
			EFX.alEffectf(
				effectHandle,
				EFX.AL_EAXREVERB_GAIN,
				value
			);

			// Apply the newly modified effect to the effect slot
			EFX.alAuxiliaryEffectSloti(
				Handle,
				EFX.AL_EFFECTSLOT_EFFECT,
				(int) effectHandle
			);
		}

		public void SetDecayTime(float value)
		{
			// TODO
		}

		public void SetDensity(float value)
		{
			// TODO
		}

		#endregion
	}
}
