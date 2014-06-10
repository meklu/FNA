#region License
/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2014 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See LICENSE for details.
 */
#endregion

#region DISABLE_FAUXBACKBUFFER Option
// #define DISABLE_FAUXBACKBUFFER
/* If you want to debug GL without the extra FBO in your way, you can use this.
 * -flibit
 */
#endregion

#region Using Statements
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using OpenTK.Graphics.OpenGL;
#endregion

namespace Microsoft.Xna.Framework.Graphics
{
	internal class OpenGLDevice
	{
		#region The OpenGL Device Instance

		public static OpenGLDevice Instance
		{
			get;
			private set;
		}

		#endregion

		#region OpenGL State Container Class

		public class OpenGLState<T>
		{
			private T current;
			private T latch;

			public OpenGLState(T defaultValue)
			{
				current = defaultValue;
				latch = defaultValue;
			}

			public T Get()
			{
				return latch;
			}

			public T GetCurrent()
			{
				return current;
			}

			public void Set(T newValue)
			{
				latch = newValue;
			}

			public bool NeedsFlush()
			{
				return !current.Equals(latch);
			}

			public T Flush()
			{
				current = latch;
				return current;
			}
		}

		#endregion

		#region OpenGL Texture Container Class

		public class OpenGLTexture
		{
			public int Handle
			{
				get;
				private set;
			}

			public TextureTarget Target
			{
				get;
				private set;
			}

			public SurfaceFormat Format
			{
				get;
				private set;
			}

			public bool HasMipmaps
			{
				get;
				private set;
			}

			public OpenGLState<TextureAddressMode> WrapS;
			public OpenGLState<TextureAddressMode> WrapT;
			public OpenGLState<TextureAddressMode> WrapR;
			public OpenGLState<TextureFilter> Filter;
			public OpenGLState<float> Anistropy;
			public OpenGLState<int> MaxMipmapLevel;
			public OpenGLState<float> LODBias;

			public OpenGLTexture(TextureTarget target, SurfaceFormat format, bool hasMipmaps)
			{
				Handle = GL.GenTexture();
				Target = target;
				Format = format;
				HasMipmaps = hasMipmaps;

				WrapS = new OpenGLState<TextureAddressMode>(TextureAddressMode.Wrap);
				WrapT = new OpenGLState<TextureAddressMode>(TextureAddressMode.Wrap);
				WrapR = new OpenGLState<TextureAddressMode>(TextureAddressMode.Wrap);
				Filter = new OpenGLState<TextureFilter>(TextureFilter.Linear);
				Anistropy = new OpenGLState<float>(4.0f);
				MaxMipmapLevel = new OpenGLState<int>(0);
				LODBias = new OpenGLState<float>(0.0f);
			}

			public void Dispose()
			{
				GL.DeleteTexture(Handle);
				Handle = 0;
			}

			public bool NeedsFlush()
			{
				if (Handle == 0)
				{
					return false; // Nothing to modify!
				}

				return (	WrapS.NeedsFlush() ||
						WrapT.NeedsFlush() ||
						WrapR.NeedsFlush() ||
						Filter.NeedsFlush() ||
						Anistropy.NeedsFlush() ||
						MaxMipmapLevel.NeedsFlush() ||
						LODBias.NeedsFlush()	);
			}

			public void Flush(bool force)
			{
				if (Handle == 0)
				{
					return; // Nothing to modify!
				}

				if (force || WrapS.NeedsFlush())
				{
					GL.TexParameter(
						Target,
						TextureParameterName.TextureWrapS,
						(int) XNAToGL.Wrap[WrapS.Flush()]
					);
				}

				if (force || WrapT.NeedsFlush())
				{
					GL.TexParameter(
						Target,
						TextureParameterName.TextureWrapT,
						(int) XNAToGL.Wrap[WrapT.Flush()]
					);
				}

				if (force || WrapR.NeedsFlush())
				{
					GL.TexParameter(
						Target,
						TextureParameterName.TextureWrapR,
						(int) XNAToGL.Wrap[WrapR.Flush()]
					);
				}

				if (force || Filter.NeedsFlush() || Anistropy.NeedsFlush())
				{
					TextureFilter filter = Filter.Flush();
					float anistropy = Anistropy.Flush();
					GL.TexParameter(
						Target,
						TextureParameterName.TextureMagFilter,
						(int) XNAToGL.MagFilter[filter]
					);
					GL.TexParameter(
						Target,
						TextureParameterName.TextureMinFilter,
						(int) (HasMipmaps ? XNAToGL.MinMipFilter[filter] : XNAToGL.MinFilter[filter])
					);
					GL.TexParameter(
						Target,
						(TextureParameterName) ExtTextureFilterAnisotropic.TextureMaxAnisotropyExt,
						(filter == TextureFilter.Anisotropic) ? Math.Max(anistropy, 1.0f) : 1.0f
					);
				}

				if (force || MaxMipmapLevel.NeedsFlush())
				{
					GL.TexParameter(
						Target,
						TextureParameterName.TextureBaseLevel,
						MaxMipmapLevel.Flush()
					);
				}

				if (force || LODBias.NeedsFlush())
				{
					GL.TexParameter(
						Target,
						TextureParameterName.TextureLodBias,
						LODBias.Flush()
					);
				}
			}

			// We can't set a SamplerState Texture to null, so use this.
			private OpenGLTexture()
			{
				Handle = 0;
			}
			public static readonly OpenGLTexture NullTexture = new OpenGLTexture();
		}

		#endregion

		#region OpenGL Sampler State Container Class

		public class OpenGLSampler
		{
			public OpenGLState<OpenGLTexture> Texture;
			public OpenGLState<TextureTarget> Target;

			public OpenGLSampler()
			{
				Texture = new OpenGLState<OpenGLTexture>(OpenGLTexture.NullTexture);
				Target = new OpenGLState<TextureTarget>(TextureTarget.Texture2D);
			}
		}

		#endregion

		#region OpenGL Vertex Buffer Container Class

		public class OpenGLVertexBuffer
		{
			public int Handle
			{
				get;
				private set;
			}

			public BufferUsageHint Dynamic
			{
				get;
				private set;
			}

			public OpenGLVertexBuffer(bool dynamic, int vertexCount, int vertexStride)
			{
				Handle = GL.GenBuffer();
				Dynamic = dynamic ? BufferUsageHint.StreamDraw : BufferUsageHint.StaticDraw;

				OpenGLDevice.Instance.BindVertexBuffer(this);
				GL.BufferData(
					BufferTarget.ArrayBuffer,
					new IntPtr(vertexStride * vertexCount),
					IntPtr.Zero,
					Dynamic
				);
			}

			private OpenGLVertexBuffer()
			{
				Handle = 0;
			}
			public static readonly OpenGLVertexBuffer NullBuffer = new OpenGLVertexBuffer();
		}

		#endregion

		#region OpenGL Index Buffer Container Class

		public class OpenGLIndexBuffer
		{
			public int Handle
			{
				get;
				private set;
			}

			public BufferUsageHint Dynamic
			{
				get;
				private set;
			}

			public IntPtr BufferSize
			{
				get;
				private set;
			}

			public OpenGLIndexBuffer(bool dynamic, int indexCount, IndexElementSize elementSize)
			{
				Handle = GL.GenBuffer();
				Dynamic = dynamic ? BufferUsageHint.StreamDraw : BufferUsageHint.StaticDraw;
				BufferSize = (IntPtr) (indexCount * (elementSize == IndexElementSize.SixteenBits ? 2 : 4));

				OpenGLDevice.Instance.BindIndexBuffer(this);
				GL.BufferData(
					BufferTarget.ElementArrayBuffer,
					BufferSize,
					IntPtr.Zero,
					Dynamic
				);
			}

			private OpenGLIndexBuffer()
			{
				Handle = 0;
			}
			public static readonly OpenGLIndexBuffer NullBuffer = new OpenGLIndexBuffer();
		}

		#endregion

		#region OpenGL Vertex Attribute State Container Class

		public class OpenGLVertexAttribute
		{
			// Checked in FlushVertexAttributes
			public OpenGLState<int> Divisor;

			// Checked in VertexAttribPointer
			public int CurrentBuffer;
			public int CurrentSize;
			public VertexAttribPointerType CurrentType;
			public bool CurrentNormalized;
			public int CurrentStride;
			public IntPtr CurrentPointer;

			public OpenGLVertexAttribute()
			{
				Divisor = new OpenGLState<int>(0);
				CurrentBuffer = 0;
				CurrentSize = 4;
				CurrentType = VertexAttribPointerType.Float;
				CurrentNormalized = false;
				CurrentStride = 0;
				CurrentPointer = IntPtr.Zero;
			}
		}

		#endregion

		#region Alpha Blending State Variables

		public OpenGLState<bool> AlphaBlendEnable = new OpenGLState<bool>(false);

		public OpenGLState<Color> BlendColor = new OpenGLState<Color>(Color.TransparentBlack);

		public OpenGLState<Blend> SrcBlend = new OpenGLState<Blend>(Blend.One);

		public OpenGLState<Blend> DstBlend = new OpenGLState<Blend>(Blend.Zero);

		public OpenGLState<Blend> SrcBlendAlpha = new OpenGLState<Blend>(Blend.One);

		public OpenGLState<Blend> DstBlendAlpha = new OpenGLState<Blend>(Blend.Zero);

		public OpenGLState<BlendFunction> BlendOp = new OpenGLState<BlendFunction>(BlendFunction.Add);

		public OpenGLState<BlendFunction> BlendOpAlpha = new OpenGLState<BlendFunction>(BlendFunction.Add);

		#endregion

		#region Depth State Variables

		public OpenGLState<bool> ZEnable = new OpenGLState<bool>(false);

		public OpenGLState<bool> ZWriteEnable = new OpenGLState<bool>(false);

		public OpenGLState<CompareFunction> DepthFunc = new OpenGLState<CompareFunction>(CompareFunction.Less);

		public OpenGLState<float> DepthBias = new OpenGLState<float>(0.0f);

		public OpenGLState<float> SlopeScaleDepthBias = new OpenGLState<float>(0.0f);

		#endregion

		#region Stencil State Variables

		public OpenGLState<bool> StencilEnable = new OpenGLState<bool>(false);

		public OpenGLState<int> StencilWriteMask = new OpenGLState<int>(-1); // AKA 0xFFFFFFFF, ugh -flibit

		public OpenGLState<bool> SeparateStencilEnable = new OpenGLState<bool>(false);

		public OpenGLState<int> StencilRef = new OpenGLState<int>(0);

		public OpenGLState<int> StencilMask = new OpenGLState<int>(-1); // AKA 0xFFFFFFFF, ugh -flibit

		public OpenGLState<CompareFunction> StencilFunc = new OpenGLState<CompareFunction>(CompareFunction.Always);

		public OpenGLState<StencilOperation> StencilFail = new OpenGLState<StencilOperation>(StencilOperation.Keep);

		public OpenGLState<StencilOperation> StencilZFail = new OpenGLState<StencilOperation>(StencilOperation.Keep);

		public OpenGLState<StencilOperation> StencilPass = new OpenGLState<StencilOperation>(StencilOperation.Keep);

		public OpenGLState<CompareFunction> CCWStencilFunc = new OpenGLState<CompareFunction>(CompareFunction.Always);

		public OpenGLState<StencilOperation> CCWStencilFail = new OpenGLState<StencilOperation>(StencilOperation.Keep);

		public OpenGLState<StencilOperation> CCWStencilZFail = new OpenGLState<StencilOperation>(StencilOperation.Keep);

		public OpenGLState<StencilOperation> CCWStencilPass = new OpenGLState<StencilOperation>(StencilOperation.Keep);

		#endregion

		#region Miscellaneous Write State Variables

		public OpenGLState<bool> ScissorTestEnable = new OpenGLState<bool>(false);

		public OpenGLState<Rectangle> ScissorRectangle = new OpenGLState<Rectangle>(
			new Rectangle(
				0,
				0,
				GraphicsDeviceManager.DefaultBackBufferWidth,
				GraphicsDeviceManager.DefaultBackBufferHeight
			)
		);

		public OpenGLState<CullMode> CullFrontFace = new OpenGLState<CullMode>(CullMode.None);

		public OpenGLState<FillMode> GLFillMode = new OpenGLState<FillMode>(FillMode.Solid);

		public OpenGLState<ColorWriteChannels> ColorWriteEnable = new OpenGLState<ColorWriteChannels>(ColorWriteChannels.All);

		public OpenGLState<Rectangle> GLViewport = new OpenGLState<Rectangle>(
			new Rectangle(
				0,
				0,
				GraphicsDeviceManager.DefaultBackBufferWidth,
				GraphicsDeviceManager.DefaultBackBufferHeight
			)
		);

		public OpenGLState<float> DepthRangeMin = new OpenGLState<float>(0.0f);

		public OpenGLState<float> DepthRangeMax = new OpenGLState<float>(0.0f);

		#endregion

		#region Sampler State Variables

		public OpenGLSampler[] Samplers
		{
			get;
			private set;
		}

		#endregion

		#region Vertex Attribute State Variables

		public OpenGLVertexAttribute[] Attributes
		{
			get;
			private set;
		}

		public bool[] AttributeEnabled
		{
			get;
			private set;
		}

		private bool[] previousAttributeEnabled;

		#endregion

		#region Buffer Binding Cache Variables

		private int currentVertexBuffer = 0;
		private int currentIndexBuffer = 0;

		#endregion

		#region Render Target Cache Variables

		private int targetFramebuffer = 0;
		private int[] currentAttachments;
		private int currentDrawBuffers;
		private DrawBuffersEnum[] drawBuffersArray;
		private uint currentRenderbuffer;
		private DepthFormat currentDepthStencilFormat;

		#endregion

		#region Clear Cache Variables

		private Vector4 currentClearColor = new Vector4(0, 0, 0, 0);
		private float currentClearDepth = 1.0f;
		private int currentClearStencil = 0;

		#endregion

		#region Faux-Backbuffer Variable

		public FauxBackbuffer Backbuffer
		{
			get;
			private set;
		}

		#endregion

		#region OpenGL Extensions List, Device Capabilities Variables

		public string Extensions
		{
			get;
			private set;
		}

		public bool SupportsDxt1
		{
			get;
			private set;
		}

		public bool SupportsS3tc
		{
			get;
			private set;
		}

		public bool SupportsHardwareInstancing
		{
			get;
			private set;
		}

		public int MaxTextureSlots
		{
			get;
			private set;
		}

		public int MaxVertexAttributes
		{
			get;
			private set;
		}

		#endregion

		#region Public Initializer

		public static void Initialize()
		{
			// We should only have one of these!
			if (Instance != null)
			{
				throw new Exception("OpenGLDevice already created!");
			}

			Instance = new OpenGLDevice();
		}

		#endregion

		#region Private Constructor

		private OpenGLDevice()
		{
			/* This looks redundant, but Framebuffer needs this.
			 * Remove this when we have our own entry points.
			 * -flibit
			 */
			Instance = this;

			// Load OpenGL entry points
			GL.LoadAll();

			// Initialize ARB_debug_output callback
			DebugOutput.Initialize();

			// Initialize XNA->GL conversion Dictionaries
			XNAToGL.Initialize();

			// Print GL information
			System.Console.WriteLine("OpenGL Device: " + GL.GetString(StringName.Renderer));
			System.Console.WriteLine("OpenGL Driver: " + GL.GetString(StringName.Version));
			System.Console.WriteLine("OpenGL Vendor: " + GL.GetString(StringName.Vendor));
			
			// Load the extension list, initialize extension-dependent components
			Extensions = GL.GetString(StringName.Extensions);
			Framebuffer.Initialize();
			SupportsS3tc = (
				Extensions.Contains("GL_EXT_texture_compression_s3tc") ||
				Extensions.Contains("GL_OES_texture_compression_S3TC") ||
				Extensions.Contains("GL_EXT_texture_compression_dxt3") ||
				Extensions.Contains("GL_EXT_texture_compression_dxt5")
			);
			SupportsDxt1 = (
				SupportsS3tc ||
				Extensions.Contains("GL_EXT_texture_compression_dxt1")
			);
			SupportsHardwareInstancing = (
				Extensions.Contains("GL_ARB_draw_instanced") &&
				Extensions.Contains("GL_ARB_instanced_arrays")
			);

			/* So apparently OSX Lion likes to lie about hardware instancing support.
			 * This is incredibly stupid, but it works!
			 * -flibit
			 */
			if (SupportsHardwareInstancing) // TODO: Let's just load our own entry points.
			{
				SupportsHardwareInstancing = SDL2.SDL.SDL_GL_GetProcAddress("glVertexAttribDivisorARB") != IntPtr.Zero;
			}

			// Initialize the faux-backbuffer
			Backbuffer = new FauxBackbuffer(
				GraphicsDeviceManager.DefaultBackBufferWidth,
				GraphicsDeviceManager.DefaultBackBufferHeight,
				DepthFormat.Depth16
			);

			// Initialize sampler state array
			int numSamplers;
			GL.GetInteger(GetPName.MaxTextureImageUnits, out numSamplers);
			Samplers = new OpenGLSampler[numSamplers];
			for (int i = 0; i < numSamplers; i += 1)
			{
				Samplers[i] = new OpenGLSampler();
			}
			MaxTextureSlots = numSamplers;

			// Initialize vertex attribute state array
			int numAttributes;
			GL.GetInteger(GetPName.MaxVertexAttribs, out numAttributes);
			Attributes = new OpenGLVertexAttribute[numAttributes];
			AttributeEnabled = new bool[numAttributes];
			previousAttributeEnabled = new bool[numAttributes];
			for (int i = 0; i < numAttributes; i += 1)
			{
				Attributes[i] = new OpenGLVertexAttribute();
				AttributeEnabled[i] = false;
				previousAttributeEnabled[i] = false;
			}
			MaxVertexAttributes = numAttributes;

			// Initialize render target FBO and state arrays
			int numAttachments;
			GL.GetInteger(GetPName.MaxDrawBuffers, out numAttachments);
			currentAttachments = new int[numAttachments];
			drawBuffersArray = new DrawBuffersEnum[numAttachments];
			for (int i = 0; i < numAttachments; i += 1)
			{
				currentAttachments[i] = 0;
				drawBuffersArray[i] = DrawBuffersEnum.ColorAttachment0 + i;
			}
			currentDrawBuffers = 0;
			currentRenderbuffer = 0;
			currentDepthStencilFormat = DepthFormat.None;
			targetFramebuffer = Framebuffer.GenFramebuffer();

			// Flush GL state to default state
			FlushGLState(true);
		}

		#endregion

		#region Dispose Method

		public void Dispose()
		{
			Framebuffer.DeleteFramebuffer(targetFramebuffer);
			targetFramebuffer = 0;
			Backbuffer.Dispose();
			Backbuffer = null;

			XNAToGL.Clear();

			Instance = null;
		}

		#endregion

		#region Flush State Method

		public void FlushGLState(bool force = false)
		{
			// ALPHA BLENDING STATES

			if (force || AlphaBlendEnable.NeedsFlush())
			{
				ToggleGLState(EnableCap.Blend, AlphaBlendEnable.Flush());
			}

			if (AlphaBlendEnable.GetCurrent() && (force || BlendColor.NeedsFlush()))
			{
				Color color = BlendColor.Flush();
				GL.BlendColor(
					color.R / 255.0f,
					color.G / 255.0f,
					color.B / 255.0f,
					color.A / 255.0f
				);
			}

			if (	AlphaBlendEnable.GetCurrent() &&
				(	force ||
					SrcBlend.NeedsFlush() ||
					DstBlend.NeedsFlush() ||
					SrcBlendAlpha.NeedsFlush() ||
					DstBlendAlpha.NeedsFlush()	)	)
			{
				GL.BlendFuncSeparate(
					XNAToGL.BlendModeSrc[SrcBlend.Flush()],
					XNAToGL.BlendModeDst[DstBlend.Flush()],
					XNAToGL.BlendModeSrc[SrcBlendAlpha.Flush()],
					XNAToGL.BlendModeDst[DstBlendAlpha.Flush()]
				);
			}

			if (	AlphaBlendEnable.GetCurrent() &&
				(	force ||
					BlendOp.NeedsFlush() ||
					BlendOpAlpha.NeedsFlush()	)	)
			{
				GL.BlendEquationSeparate(
					XNAToGL.BlendEquation[BlendOp.Flush()],
					XNAToGL.BlendEquation[BlendOpAlpha.Flush()]
				);
			}

			// END ALPHA BLENDING STATES

			// DEPTH STATES

			if (force || ZEnable.NeedsFlush())
			{
				ToggleGLState(EnableCap.DepthTest, ZEnable.Flush());
			}

			if (ZEnable.GetCurrent() && (force || ZWriteEnable.NeedsFlush()))
			{
				GL.DepthMask(ZWriteEnable.Flush());
			}

			if (ZEnable.GetCurrent() && (force || DepthFunc.NeedsFlush()))
			{
				GL.DepthFunc(XNAToGL.DepthFunc[DepthFunc.Flush()]);
			}

			if (	ZEnable.GetCurrent() &&
				(	force ||
					DepthBias.NeedsFlush() ||
					SlopeScaleDepthBias.NeedsFlush()	)	)
			{
				float depthBias = DepthBias.Flush();
				float slopeScaleDepthBias = SlopeScaleDepthBias.Flush();
				if (depthBias == 0.0f && slopeScaleDepthBias == 0.0f)
				{
					ToggleGLState(EnableCap.PolygonOffsetFill, false);
				}
				else
				{
					ToggleGLState(EnableCap.PolygonOffsetFill, true);
					GL.PolygonOffset(slopeScaleDepthBias, depthBias);
				}
			}

			// END DEPTH STATES

			// STENCIL STATES

			if (force || StencilEnable.NeedsFlush())
			{
				ToggleGLState(EnableCap.StencilTest, StencilEnable.Flush());
			}

			if (StencilEnable.GetCurrent() && (force || StencilWriteMask.NeedsFlush()))
			{
				GL.StencilMask(StencilWriteMask.Flush());
			}

			if (	StencilEnable.GetCurrent() &&
				(	force ||
					SeparateStencilEnable.NeedsFlush() ||
					StencilRef.NeedsFlush() ||
					StencilMask.NeedsFlush() ||
					StencilFunc.NeedsFlush() ||
					CCWStencilFunc.NeedsFlush() ||
					StencilFail.NeedsFlush() ||
					StencilZFail.NeedsFlush() ||
					StencilPass.NeedsFlush() ||
					CCWStencilFail.NeedsFlush() ||
					CCWStencilZFail.NeedsFlush() ||
					CCWStencilPass.NeedsFlush()	)	)
			{
				// TODO: Can we split StencilFunc/StencilOp up nicely? -flibit
				if (SeparateStencilEnable.Flush())
				{
					GL.StencilFuncSeparate(
						(Version20) CullFaceMode.Front,
						XNAToGL.StencilFunc[StencilFunc.Flush()],
						StencilRef.Flush(),
						StencilMask.Flush()
					);
					GL.StencilFuncSeparate(
						(Version20) CullFaceMode.Back,
						XNAToGL.StencilFunc[CCWStencilFunc.Flush()],
						StencilRef.Flush(),
						StencilMask.Flush()
					);
					GL.StencilOpSeparate(
						StencilFace.Front,
						XNAToGL.GLStencilOp[StencilFail.Flush()],
						XNAToGL.GLStencilOp[StencilZFail.Flush()],
						XNAToGL.GLStencilOp[StencilPass.Flush()]
					);
					GL.StencilOpSeparate(
						StencilFace.Back,
						XNAToGL.GLStencilOp[CCWStencilFail.Flush()],
						XNAToGL.GLStencilOp[CCWStencilZFail.Flush()],
						XNAToGL.GLStencilOp[CCWStencilPass.Flush()]
					);
				}
				else
				{
					GL.StencilFunc(
						XNAToGL.StencilFunc[StencilFunc.Flush()],
						StencilRef.Flush(),
						StencilMask.Flush()
					);
					GL.StencilOp(
						XNAToGL.GLStencilOp[StencilFail.Flush()],
						XNAToGL.GLStencilOp[StencilZFail.Flush()],
						XNAToGL.GLStencilOp[StencilPass.Flush()]
					);
				}
			}

			// END STENCIL STATES

			// MISCELLANEOUS WRITE STATES

			if (force || ScissorTestEnable.NeedsFlush())
			{
				ToggleGLState(EnableCap.ScissorTest, ScissorTestEnable.Flush());
			}

			if (ScissorTestEnable.GetCurrent() && (force || ScissorRectangle.NeedsFlush()))
			{
				Rectangle scissorRect = ScissorRectangle.Flush();
				GL.Scissor(
					scissorRect.X,
					scissorRect.Y,
					scissorRect.Width,
					scissorRect.Height
				);
			}

			if (force || CullFrontFace.NeedsFlush())
			{
				CullMode current = CullFrontFace.GetCurrent();
				CullMode latched = CullFrontFace.Flush();
				if (force || (latched == CullMode.None) != (current == CullMode.None))
				{
					ToggleGLState(EnableCap.CullFace, latched != CullMode.None);
					if (latched != CullMode.None)
					{
						// FIXME: XNA/MonoGame-specific behavior? -flibit
						GL.CullFace(CullFaceMode.Back);
					}
				}
				if (latched != CullMode.None)
				{
					GL.FrontFace(XNAToGL.FrontFace[latched]);
				}
			}

			if (force || GLFillMode.NeedsFlush())
			{
				GL.PolygonMode(
					MaterialFace.FrontAndBack,
					XNAToGL.GLFillMode[GLFillMode.Flush()]
				);
			}

			if (force || ColorWriteEnable.NeedsFlush())
			{
				ColorWriteChannels write = ColorWriteEnable.Flush();
				GL.ColorMask(
					(write & ColorWriteChannels.Red) != 0,
					(write & ColorWriteChannels.Green) != 0,
					(write & ColorWriteChannels.Blue) != 0,
					(write & ColorWriteChannels.Alpha) != 0
				);
			}

			if (force || GLViewport.NeedsFlush())
			{
				Rectangle viewport = GLViewport.Flush();
				GL.Viewport(
					viewport.X,
					viewport.Y,
					viewport.Width,
					viewport.Height
				);
			}

			if (force || DepthRangeMin.NeedsFlush() || DepthRangeMax.NeedsFlush())
			{
				GL.DepthRange((double) DepthRangeMin.Flush(), (double) DepthRangeMax.Flush());
			}

			// END MISCELLANEOUS WRITE STATES

			// SAMPLER STATES

			int activeTexture = 0;
			for (int i = 0; i < Samplers.Length; i += 1)
			{
				OpenGLSampler sampler = Samplers[i];
				if (!(	force ||
					sampler.Target.NeedsFlush() ||
					sampler.Texture.NeedsFlush() ||
					sampler.Texture.GetCurrent().NeedsFlush()	))
				{
					// Nothing changed in this sampler, skip it.
					continue;
				}

				activeTexture = i;
				GL.ActiveTexture(TextureUnit.Texture0 + i);

				bool targetForce = force;
				if (force || sampler.Target.NeedsFlush())
				{
					force = true; // Reset the ENTIRE state when we change target!
					GL.BindTexture(sampler.Target.GetCurrent(), 0);
				}

				if (	force ||
					sampler.Texture.NeedsFlush() || 
					sampler.Texture.GetCurrent().NeedsFlush()	)
				{
					OpenGLTexture texture = sampler.Texture.Flush();
					GL.BindTexture(sampler.Target.Flush(), texture.Handle);
					texture.Flush(force);
				}

				// You didn't see nothin'.
				force = targetForce;
			}
			if (activeTexture != 0)
			{
				// Keep this state sane. -flibit
				GL.ActiveTexture(TextureUnit.Texture0);
			}

			// END SAMPLER STATES
		}

		#endregion

		#region Flush Vertex Attributes Method

		public void FlushGLVertexAttributes(bool force = false)
		{
			for (int i = 0; i < Attributes.Length; i += 1)
			{
				if (AttributeEnabled[i])
				{
					AttributeEnabled[i] = false;
					if (!previousAttributeEnabled[i])
					{
						GL.EnableVertexAttribArray(i);
						previousAttributeEnabled[i] = true;
					}
				}
				else if (previousAttributeEnabled[i])
				{
					GL.DisableVertexAttribArray(i);
					previousAttributeEnabled[i] = false;
				}

				if (force || Attributes[i].Divisor.NeedsFlush())
				{
					GL.VertexAttribDivisor(i, Attributes[i].Divisor.Flush());
				}
			}
		}

		#endregion

		#region glVertexAttribPointer Method

		public void VertexAttribPointer(
			int location,
			int size,
			VertexAttribPointerType type,
			bool normalized,
			int stride,
			IntPtr pointer
		) {
			if (	Attributes[location].CurrentBuffer != currentVertexBuffer ||
				Attributes[location].CurrentPointer != pointer ||
				Attributes[location].CurrentSize != size ||
				Attributes[location].CurrentType != type ||
				Attributes[location].CurrentNormalized != normalized ||
				Attributes[location].CurrentStride != stride	)
			{
				GL.VertexAttribPointer(
					location,
					size,
					type,
					normalized,
					stride,
					pointer
				);
				Attributes[location].CurrentBuffer = currentVertexBuffer;
				Attributes[location].CurrentPointer = pointer;
				Attributes[location].CurrentSize = size;
				Attributes[location].CurrentType = type;
				Attributes[location].CurrentNormalized = normalized;
				Attributes[location].CurrentStride = stride;
			}
		}

		#endregion

		#region glBindBuffer Methods

		public void BindVertexBuffer(OpenGLVertexBuffer buffer)
		{
			if (buffer.Handle != currentVertexBuffer)
			{
				GL.BindBuffer(BufferTarget.ArrayBuffer, buffer.Handle);
				currentVertexBuffer = buffer.Handle;
			}
		}

		public void BindIndexBuffer(OpenGLIndexBuffer buffer)
		{
			if (buffer.Handle != currentIndexBuffer)
			{
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, buffer.Handle);
				currentIndexBuffer = buffer.Handle;
			}
		}

		#endregion

		#region glSetBufferData Methods

		public void SetVertexBufferData<T>(
			OpenGLVertexBuffer handle,
			int bufferSize,
			int elementSizeInBytes,
			int offsetInBytes,
			T[] data,
			int startIndex,
			int elementCount,
			int vertexStride,
			SetDataOptions options
		) where T : struct {
			BindVertexBuffer(handle);

			if (options == SetDataOptions.Discard)
			{
				GL.BufferData(
					BufferTarget.ArrayBuffer,
					(IntPtr) bufferSize,
					IntPtr.Zero,
					handle.Dynamic
				);
			}

			GCHandle dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);

			GL.BufferSubData(
				BufferTarget.ArrayBuffer,
				(IntPtr) offsetInBytes,
				(IntPtr) (elementSizeInBytes * elementCount),
				(IntPtr) (dataHandle.AddrOfPinnedObject().ToInt64() + startIndex * elementSizeInBytes)
			);

			dataHandle.Free();
		}

		public void SetIndexBufferData<T>(
			OpenGLIndexBuffer handle,
			int offsetInBytes,
			T[] data,
			int startIndex,
			int elementCount,
			SetDataOptions options
		) where T : struct {
			BindIndexBuffer(handle);

			if (options == SetDataOptions.Discard)
			{
				GL.BufferData(
					BufferTarget.ElementArrayBuffer,
					handle.BufferSize,
					IntPtr.Zero,
					handle.Dynamic
				);
			}

			GCHandle dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);

			int elementSizeInBytes = Marshal.SizeOf(typeof(T));
			GL.BufferSubData(
				BufferTarget.ElementArrayBuffer,
				(IntPtr) offsetInBytes,
				(IntPtr) (elementSizeInBytes * elementCount),
				(IntPtr) (dataHandle.AddrOfPinnedObject().ToInt64() + startIndex * elementSizeInBytes)
			);

			dataHandle.Free();
		}

		#endregion

		#region glGetBufferData Methods

		public void GetVertexBufferData<T>(
			OpenGLVertexBuffer handle,
			int offsetInBytes,
			T[] data,
			int startIndex,
			int elementCount,
			int vertexStride
		) where T : struct {
			BindVertexBuffer(handle);

			IntPtr ptr = GL.MapBuffer(BufferTarget.ArrayBuffer, BufferAccess.ReadOnly);

			// Pointer to the start of data to read in the index buffer
			ptr = new IntPtr(ptr.ToInt64() + offsetInBytes);

			if (typeof(T) == typeof(byte))
			{
				/* If data is already a byte[] we can skip the temporary buffer.
				 * Copy from the vertex buffer to the destination array.
				 */
				byte[] buffer = data as byte[];
				Marshal.Copy(ptr, buffer, 0, buffer.Length);
			}
			else
			{
				// Temporary buffer to store the copied section of data
				byte[] buffer = new byte[elementCount * vertexStride - offsetInBytes];

				// Copy from the vertex buffer to the temporary buffer
				Marshal.Copy(ptr, buffer, 0, buffer.Length);

				GCHandle dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
				IntPtr dataPtr = (IntPtr) (dataHandle.AddrOfPinnedObject().ToInt64() + startIndex * Marshal.SizeOf(typeof(T)));

				// Copy from the temporary buffer to the destination array
				int dataSize = Marshal.SizeOf(typeof(T));
				if (dataSize == vertexStride)
				{
					Marshal.Copy(buffer, 0, dataPtr, buffer.Length);
				}
				else
				{
					// If the user is asking for a specific element within the vertex buffer, copy them one by one...
					for (int i = 0; i < elementCount; i += 1)
					{
						Marshal.Copy(buffer, i * vertexStride, dataPtr, dataSize);
						dataPtr = (IntPtr)(dataPtr.ToInt64() + dataSize);
					}
				}

				dataHandle.Free();
			}

			GL.UnmapBuffer(BufferTarget.ArrayBuffer);
		}

		public void GetIndexBufferData<T>(
			OpenGLIndexBuffer handle,
			int offsetInBytes,
			T[] data,
			int startIndex,
			int elementCount
		) where T : struct {
			BindIndexBuffer(handle);

			IntPtr ptr = GL.MapBuffer(BufferTarget.ElementArrayBuffer, BufferAccess.ReadOnly);

			// Pointer to the start of data to read in the index buffer
			ptr = new IntPtr(ptr.ToInt64() + offsetInBytes);

			/* If data is already a byte[] we can skip the temporary buffer.
			 * Copy from the index buffer to the destination array.
			 */
			if (typeof(T) == typeof(byte))
			{
				byte[] buffer = data as byte[];
				Marshal.Copy(ptr, buffer, 0, buffer.Length);
			}
			else
			{
				int elementSizeInBytes = Marshal.SizeOf(typeof(T));
				byte[] buffer = new byte[elementCount * elementSizeInBytes];
				Marshal.Copy(ptr, buffer, 0, buffer.Length);
				Buffer.BlockCopy(buffer, 0, data, startIndex * elementSizeInBytes, elementCount * elementSizeInBytes);
			}

			GL.UnmapBuffer(BufferTarget.ArrayBuffer);
		}

		#endregion

		#region glDeleteBuffers Methods

		public void DeleteVertexBuffer(OpenGLVertexBuffer buffer)
		{
			if (buffer.Handle == currentVertexBuffer)
			{
				GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
				currentVertexBuffer = 0;
			}
			GL.DeleteBuffer(0);
		}

		public void DeleteIndexBuffer(OpenGLIndexBuffer buffer)
		{
			if (buffer.Handle == currentIndexBuffer)
			{
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
				currentIndexBuffer = 0;
			}
			GL.DeleteBuffer(0);
		}

		#endregion

		#region glBindTexture Method

		public void BindTexture(OpenGLTexture texture)
		{
			Samplers[0].Target.Set(texture.Target);
			Samplers[0].Texture.Set(texture);
			if (Samplers[0].Target.NeedsFlush())
			{
				GL.BindTexture(
					Samplers[0].Target.GetCurrent(),
					0
				);
			}
			GL.BindTexture(
				Samplers[0].Target.Flush(),
				Samplers[0].Texture.Flush().Handle
			);
		}

		#endregion

		#region glDeleteTexture Method

		public void DeleteTexture(OpenGLTexture texture)
		{
			for (int i = 0; i < currentAttachments.Length; i += 1)
			{
				if (texture.Handle == currentAttachments[i])
				{
					// Force an attachment update, this no longer exists!
					currentAttachments[i] = -1;
				}
			}
			texture.Dispose();
		}

		#endregion

		#region glReadPixels Method

		/// <summary>
		/// Attempts to read the texture data directly from the FBO using GL.ReadPixels
		/// </summary>
		/// <typeparam name="T">Texture data type</typeparam>
		/// <param name="texture">The texture to read from</param>
		/// <param name="level">The texture level</param>
		/// <param name="data">The texture data array</param>
		/// <param name="rect">The portion of the image to read from</param>
		/// <returns>True if we successfully read the texture data</returns>
		public bool ReadTargetIfApplicable<T>(
			OpenGLTexture texture,
			int level,
			T[] data,
			Rectangle? rect
		) where T : struct {
			if (	currentDrawBuffers == 1 &&
				currentAttachments != null &&				
				currentAttachments[0] == texture.Handle	)
			{
				int oldReadFramebuffer = Framebuffer.CurrentReadFramebuffer;
				if (oldReadFramebuffer != targetFramebuffer)
				{
					Framebuffer.BindReadFramebuffer(targetFramebuffer);
				}

				/* glReadPixels should be faster than reading
				 * back from the render target if we are already bound.
				 */
				if (rect.HasValue)
				{
					GL.ReadPixels(
						rect.Value.Left,
						rect.Value.Top,
						rect.Value.Width,
						rect.Value.Height,
						PixelFormat.Rgba,
						PixelType.UnsignedByte,
						data
					);
				}
				else
				{
					// FIXME: Using two glGet calls here! D:
					int width = 0;
					int height = 0;
					OpenGLDevice.Instance.BindTexture(texture);
					GL.GetTexLevelParameter(
						texture.Target,
						level,
						GetTextureParameter.TextureWidth,
						out width
					);
					GL.GetTexLevelParameter(
						texture.Target,
						level,
						GetTextureParameter.TextureHeight,
						out height
					);

					GL.ReadPixels(
						0,
						0,
						width,
						height,
						PixelFormat.Rgba,
						PixelType.UnsignedByte,
						data
					);
				}
				Framebuffer.BindReadFramebuffer(oldReadFramebuffer);
				return true;
			}
			return false;
		}

		#endregion

		#region glDeleteRenderbuffer Method

		public void DeleteRenderbuffer(uint renderbuffer)
		{
			if (renderbuffer == currentRenderbuffer)
			{
				// Force a renderbuffer update, this no longer exists!
				currentRenderbuffer = uint.MaxValue;
			}
			Framebuffer.DeleteRenderbuffer(renderbuffer);
		}

		#endregion

		#region glEnable/glDisable Method

		private void ToggleGLState(EnableCap feature, bool enable)
		{
			if (enable)
			{
				GL.Enable(feature);
			}
			else
			{
				GL.Disable(feature);
			}
		}

		#endregion

		#region glClear Method

		public void Clear(ClearOptions options, Vector4 color, float depth, int stencil)
		{
			// Move some stuff around so the glClear works...
			if (ScissorTestEnable.GetCurrent())
			{
				GL.Disable(EnableCap.ScissorTest);
			}
			if (!ZWriteEnable.GetCurrent())
			{
				GL.DepthMask(true);
			}
			if (StencilWriteMask.GetCurrent() != Int32.MaxValue)
			{
				GL.StencilMask(Int32.MaxValue);
			}

			// Get the clear mask, set the clear properties if needed
			ClearBufferMask clearMask = 0;
			if ((options & ClearOptions.Target) == ClearOptions.Target)
			{
				clearMask |= ClearBufferMask.ColorBufferBit;
				if (!color.Equals(currentClearColor))
				{
					GL.ClearColor(
						color.X,
						color.Y,
						color.Z,
						color.W
					);
					currentClearColor = color;
				}
			}
			if ((options & ClearOptions.DepthBuffer) == ClearOptions.DepthBuffer)
			{
				clearMask |= ClearBufferMask.DepthBufferBit;
				if (depth != currentClearDepth)
				{
					GL.ClearDepth((double) depth);
					currentClearDepth = depth;
				}
			}
			if ((options & ClearOptions.Stencil) == ClearOptions.Stencil)
			{
				clearMask |= ClearBufferMask.StencilBufferBit;
				if (stencil != currentClearStencil)
				{
					GL.ClearStencil(stencil);
					currentClearStencil = stencil;
				}
			}

			// CLEAR!
			GL.Clear(clearMask);

			// Clean up after ourselves.
			if (ScissorTestEnable.Flush())
			{
				GL.Enable(EnableCap.ScissorTest);
			}
			if (!ZWriteEnable.Flush())
			{
				GL.DepthMask(false);
			}
			if (StencilWriteMask.Flush() != Int32.MaxValue)
			{
				GL.StencilMask(StencilWriteMask.Flush());
			}
		}

		#endregion

		#region SetRenderTargets Method

		public void SetRenderTargets(int[] attachments, uint renderbuffer, DepthFormat depthFormat)
		{
			// Bind the right framebuffer, if needed
			if (attachments == null)
			{
				Framebuffer.BindFramebuffer(Backbuffer.Handle);
				return;
			}
			else
			{
				Framebuffer.BindFramebuffer(targetFramebuffer);
			}

			// Update the color attachments, DrawBuffers state
			int i = 0;
			for (i = 0; i < attachments.Length; i += 1)
			{
				if (attachments[i] != currentAttachments[i])
				{
					Framebuffer.AttachColor(attachments[i], i);
					currentAttachments[i] = attachments[i];
				}
			}
			while (i < currentAttachments.Length)
			{
				if (currentAttachments[i] != 0)
				{
					Framebuffer.AttachColor(0, i);
					currentAttachments[i] = 0;
				}
				i += 1;
			}
			if (attachments.Length != currentDrawBuffers)
			{
				GL.DrawBuffers(attachments.Length, drawBuffersArray);
				currentDrawBuffers = attachments.Length;
			}

			// Update the depth/stencil attachment
			/* FIXME: Notice that we do separate attach calls for the stencil.
			 * We _should_ be able to do a single attach for depthstencil, but
			 * some drivers (like Mesa) cannot into GL_DEPTH_STENCIL_ATTACHMENT.
			 * Use XNAToGL.DepthStencilAttachment when this isn't a problem.
			 * -flibit
			 */
			if (renderbuffer != currentRenderbuffer)
			{
				if (currentDepthStencilFormat == DepthFormat.Depth24Stencil8)
				{
					Framebuffer.AttachDepthRenderbuffer(
						0,
						FramebufferAttachment.StencilAttachment
					);
				}
				currentDepthStencilFormat = depthFormat;
				Framebuffer.AttachDepthRenderbuffer(
					renderbuffer,
					FramebufferAttachment.DepthAttachment
				);
				if (currentDepthStencilFormat == DepthFormat.Depth24Stencil8)
				{
					Framebuffer.AttachDepthRenderbuffer(
						renderbuffer,
						FramebufferAttachment.StencilAttachment
					);
				}
				currentRenderbuffer = renderbuffer;
			}
		}

		#endregion

		#region XNA->GL Enum Conversion Class

		private static class XNAToGL
		{
			public static Dictionary<Blend, BlendingFactorSrc> BlendModeSrc
			{
				get;
				private set;
			}

			public static Dictionary<Blend, BlendingFactorDest> BlendModeDst
			{
				get;
				private set;
			}

			public static Dictionary<BlendFunction, BlendEquationMode> BlendEquation
			{
				get;
				private set;
			}

			public static Dictionary<CompareFunction, DepthFunction> DepthFunc
			{
				get;
				private set;
			}

			public static Dictionary<CompareFunction, StencilFunction> StencilFunc
			{
				get;
				private set;
			}

			public static Dictionary<StencilOperation, StencilOp> GLStencilOp
			{
				get;
				private set;
			}

			public static Dictionary<CullMode, FrontFaceDirection> FrontFace
			{
				get;
				private set;
			}

			public static Dictionary<FillMode, PolygonMode> GLFillMode
			{
				get;
				private set;
			}

			public static Dictionary<TextureAddressMode, TextureWrapMode> Wrap
			{
				get;
				private set;
			}

			public static Dictionary<TextureFilter, TextureMagFilter> MagFilter
			{
				get;
				private set;
			}

			public static Dictionary<TextureFilter, TextureMinFilter> MinMipFilter
			{
				get;
				private set;
			}

			public static Dictionary<TextureFilter, TextureMinFilter> MinFilter
			{
				get;
				private set;
			}

			public static Dictionary<DepthFormat, FramebufferAttachment> DepthStencilAttachment
			{
				get;
				private set;
			}

			public static void Initialize()
			{
				/* Ideally we would be using arrays, rather than Dictionaries.
				 * The problem is that we don't support every enum, and dealing
				 * with gaps would be a headache. So whatever, Dictionaries!
				 * -flibit
				 */

				BlendModeSrc = new Dictionary<Blend, BlendingFactorSrc>();
				BlendModeSrc.Add(Blend.DestinationAlpha,	BlendingFactorSrc.DstAlpha);
				BlendModeSrc.Add(Blend.DestinationColor,	BlendingFactorSrc.DstColor);
				BlendModeSrc.Add(Blend.InverseDestinationAlpha, BlendingFactorSrc.OneMinusDstAlpha);
				BlendModeSrc.Add(Blend.InverseDestinationColor, BlendingFactorSrc.OneMinusDstColor);
				BlendModeSrc.Add(Blend.InverseSourceAlpha,	BlendingFactorSrc.OneMinusSrcAlpha);
				BlendModeSrc.Add(Blend.InverseSourceColor,	(BlendingFactorSrc) All.OneMinusSrcColor); // Why -flibit
				BlendModeSrc.Add(Blend.One,			BlendingFactorSrc.One);
				BlendModeSrc.Add(Blend.SourceAlpha,		BlendingFactorSrc.SrcAlpha);
				BlendModeSrc.Add(Blend.SourceAlphaSaturation,	BlendingFactorSrc.SrcAlphaSaturate);
				BlendModeSrc.Add(Blend.SourceColor,		(BlendingFactorSrc) All.SrcColor); // Why -flibit
				BlendModeSrc.Add(Blend.Zero,			BlendingFactorSrc.Zero);

				BlendModeDst = new Dictionary<Blend, BlendingFactorDest>();
				BlendModeDst.Add(Blend.DestinationAlpha,	BlendingFactorDest.DstAlpha);
				BlendModeDst.Add(Blend.InverseDestinationAlpha,	BlendingFactorDest.OneMinusDstAlpha);
				BlendModeDst.Add(Blend.InverseSourceAlpha,	BlendingFactorDest.OneMinusSrcAlpha);
				BlendModeDst.Add(Blend.InverseSourceColor,	BlendingFactorDest.OneMinusSrcColor);
				BlendModeDst.Add(Blend.One,			BlendingFactorDest.One);
				BlendModeDst.Add(Blend.SourceAlpha,		BlendingFactorDest.SrcAlpha);
				BlendModeDst.Add(Blend.SourceColor,		BlendingFactorDest.SrcColor);
				BlendModeDst.Add(Blend.Zero,			BlendingFactorDest.Zero);

				BlendEquation = new Dictionary<BlendFunction, BlendEquationMode>();
				BlendEquation.Add(BlendFunction.Add,			BlendEquationMode.FuncAdd);
				BlendEquation.Add(BlendFunction.Max,			BlendEquationMode.Max);
				BlendEquation.Add(BlendFunction.Min,			BlendEquationMode.Min);
				BlendEquation.Add(BlendFunction.ReverseSubtract,	BlendEquationMode.FuncReverseSubtract);
				BlendEquation.Add(BlendFunction.Subtract,		BlendEquationMode.FuncSubtract);

				DepthFunc = new Dictionary<CompareFunction, DepthFunction>();
				DepthFunc.Add(CompareFunction.Always,		DepthFunction.Always);
				DepthFunc.Add(CompareFunction.Equal,		DepthFunction.Equal);
				DepthFunc.Add(CompareFunction.Greater,		DepthFunction.Greater);
				DepthFunc.Add(CompareFunction.GreaterEqual,	DepthFunction.Gequal);
				DepthFunc.Add(CompareFunction.Less,		DepthFunction.Less);
				DepthFunc.Add(CompareFunction.LessEqual,	DepthFunction.Lequal);
				DepthFunc.Add(CompareFunction.Never,		DepthFunction.Never);
				DepthFunc.Add(CompareFunction.NotEqual,		DepthFunction.Notequal);

				StencilFunc = new Dictionary<CompareFunction, StencilFunction>();
				StencilFunc.Add(CompareFunction.Always,		StencilFunction.Always);
				StencilFunc.Add(CompareFunction.Equal,		StencilFunction.Equal);
				StencilFunc.Add(CompareFunction.Greater,	StencilFunction.Greater);
				StencilFunc.Add(CompareFunction.GreaterEqual,	StencilFunction.Gequal);
				StencilFunc.Add(CompareFunction.Less,		StencilFunction.Less);
				StencilFunc.Add(CompareFunction.LessEqual,	StencilFunction.Lequal);
				StencilFunc.Add(CompareFunction.Never,		StencilFunction.Never);
				StencilFunc.Add(CompareFunction.NotEqual,	StencilFunction.Notequal);

				GLStencilOp = new Dictionary<StencilOperation, StencilOp>();
				GLStencilOp.Add(StencilOperation.Decrement,		StencilOp.DecrWrap);
				GLStencilOp.Add(StencilOperation.DecrementSaturation,	StencilOp.Decr);
				GLStencilOp.Add(StencilOperation.Increment,		StencilOp.IncrWrap);
				GLStencilOp.Add(StencilOperation.IncrementSaturation,	StencilOp.Incr);
				GLStencilOp.Add(StencilOperation.Invert,		StencilOp.Invert);
				GLStencilOp.Add(StencilOperation.Keep,			StencilOp.Keep);
				GLStencilOp.Add(StencilOperation.Replace,		StencilOp.Replace);
				GLStencilOp.Add(StencilOperation.Zero,			StencilOp.Zero);

				FrontFace = new Dictionary<CullMode, FrontFaceDirection>();
				FrontFace.Add(CullMode.CullClockwiseFace,		FrontFaceDirection.Cw);
				FrontFace.Add(CullMode.CullCounterClockwiseFace,	FrontFaceDirection.Ccw);

				GLFillMode = new Dictionary<FillMode, PolygonMode>();
				GLFillMode.Add(FillMode.Solid,		PolygonMode.Fill);
				GLFillMode.Add(FillMode.WireFrame,	PolygonMode.Line);

				Wrap = new Dictionary<TextureAddressMode, TextureWrapMode>();
				Wrap.Add(TextureAddressMode.Clamp,	TextureWrapMode.ClampToEdge);
				Wrap.Add(TextureAddressMode.Mirror,	TextureWrapMode.MirroredRepeat);
				Wrap.Add(TextureAddressMode.Wrap,	TextureWrapMode.Repeat);

				MagFilter = new Dictionary<TextureFilter, TextureMagFilter>();
				MagFilter.Add(TextureFilter.Point,			TextureMagFilter.Nearest);
				MagFilter.Add(TextureFilter.Linear,			TextureMagFilter.Linear);
				MagFilter.Add(TextureFilter.Anisotropic,		TextureMagFilter.Linear);
				MagFilter.Add(TextureFilter.LinearMipPoint,		TextureMagFilter.Linear);
				MagFilter.Add(TextureFilter.MinPointMagLinearMipPoint,	TextureMagFilter.Linear);
				MagFilter.Add(TextureFilter.MinPointMagLinearMipLinear,	TextureMagFilter.Linear);
				MagFilter.Add(TextureFilter.MinLinearMagPointMipPoint,	TextureMagFilter.Nearest);
				MagFilter.Add(TextureFilter.MinLinearMagPointMipLinear,	TextureMagFilter.Nearest);

				MinMipFilter = new Dictionary<TextureFilter, TextureMinFilter>();
				MinMipFilter.Add(TextureFilter.Point,				TextureMinFilter.NearestMipmapNearest);
				MinMipFilter.Add(TextureFilter.Linear,				TextureMinFilter.LinearMipmapLinear);
				MinMipFilter.Add(TextureFilter.Anisotropic,			TextureMinFilter.LinearMipmapLinear);
				MinMipFilter.Add(TextureFilter.LinearMipPoint,			TextureMinFilter.LinearMipmapNearest);
				MinMipFilter.Add(TextureFilter.MinPointMagLinearMipPoint,	TextureMinFilter.NearestMipmapNearest);
				MinMipFilter.Add(TextureFilter.MinPointMagLinearMipLinear,	TextureMinFilter.NearestMipmapLinear);
				MinMipFilter.Add(TextureFilter.MinLinearMagPointMipPoint,	TextureMinFilter.LinearMipmapNearest);
				MinMipFilter.Add(TextureFilter.MinLinearMagPointMipLinear,	TextureMinFilter.LinearMipmapLinear);

				MinFilter = new Dictionary<TextureFilter, TextureMinFilter>();
				MinFilter.Add(TextureFilter.Point,			TextureMinFilter.Nearest);
				MinFilter.Add(TextureFilter.Linear,			TextureMinFilter.Linear);
				MinFilter.Add(TextureFilter.Anisotropic,		TextureMinFilter.Linear);
				MinFilter.Add(TextureFilter.LinearMipPoint,		TextureMinFilter.Linear);
				MinFilter.Add(TextureFilter.MinPointMagLinearMipPoint,	TextureMinFilter.Nearest);
				MinFilter.Add(TextureFilter.MinPointMagLinearMipLinear,	TextureMinFilter.Nearest);
				MinFilter.Add(TextureFilter.MinLinearMagPointMipPoint,	TextureMinFilter.Linear);
				MinFilter.Add(TextureFilter.MinLinearMagPointMipLinear,	TextureMinFilter.Linear);

				DepthStencilAttachment = new Dictionary<DepthFormat, FramebufferAttachment>();
				DepthStencilAttachment.Add(DepthFormat.Depth16,		FramebufferAttachment.DepthAttachment);
				DepthStencilAttachment.Add(DepthFormat.Depth24,		FramebufferAttachment.DepthAttachment);
				DepthStencilAttachment.Add(DepthFormat.Depth24Stencil8,	FramebufferAttachment.DepthStencilAttachment);
			}

			public static void Clear()
			{
				BlendModeSrc.Clear();
				BlendModeSrc = null;
				BlendModeDst.Clear();
				BlendModeDst = null;
				BlendEquation.Clear();
				BlendEquation = null;
				DepthFunc.Clear();
				DepthFunc = null;
				StencilFunc.Clear();
				StencilFunc = null;
				GLStencilOp.Clear();
				GLStencilOp = null;
				FrontFace.Clear();
				FrontFace = null;
				GLFillMode.Clear();
				GLFillMode = null;
				Wrap.Clear();
				Wrap = null;
				MagFilter.Clear();
				MagFilter = null;
				MinMipFilter.Clear();
				MinMipFilter = null;
				DepthStencilAttachment.Clear();
				DepthStencilAttachment = null;
			}
		}

		#endregion

		#region Framebuffer ARB/EXT Wrapper Class

		public static class Framebuffer
		{
			private static int currentReadFramebuffer = 0;
			public static int CurrentReadFramebuffer
			{
				get
				{
					return currentReadFramebuffer;
				}
			}

			private static int currentDrawFramebuffer = 0;
			public static int CurrentDrawFramebuffer
			{
				get
				{
					return currentDrawFramebuffer;
				}
			}

			private static bool hasARB = false;

			public static void Initialize()
			{
				hasARB = (	SDL2.SDL.SDL_GL_GetProcAddress("glGenFramebuffers") != IntPtr.Zero &&
						SDL2.SDL.SDL_GL_GetProcAddress("glBlitFramebuffer") != IntPtr.Zero	);

				// If we don't have ARB_framebuffer_object, check for EXT as a fallback.
				if (!hasARB)
				{
					System.Console.WriteLine("ARB_framebuffer_object not found, falling back to EXT.");
					if (	SDL2.SDL.SDL_GL_GetProcAddress("glGenFramebuffersEXT") == IntPtr.Zero ||
						SDL2.SDL.SDL_GL_GetProcAddress("glBlitFramebufferEXT") == IntPtr.Zero	)
					{
						throw new NoSuitableGraphicsDeviceException("The graphics device does not support framebuffer objects.");
					}
				}
			}

			public static int GenFramebuffer()
			{
				int handle;
				if (hasARB)
				{
					GL.GenFramebuffers(1, out handle);
				}
				else
				{
					GL.Ext.GenFramebuffers(1, out handle);
				}
				return handle;
			}

			public static void DeleteFramebuffer(int handle)
			{
				if (hasARB)
				{
					GL.DeleteFramebuffers(1, ref handle);
				}
				else
				{
					GL.Ext.DeleteFramebuffers(1, ref handle);
				}
			}

			public static void BindFramebuffer(int handle)
			{
				if (currentReadFramebuffer != handle && currentDrawFramebuffer != handle)
				{
					if (hasARB)
					{
						GL.BindFramebuffer(
							FramebufferTarget.Framebuffer,
							handle
						);
					}
					else
					{
						GL.Ext.BindFramebuffer(
							FramebufferTarget.FramebufferExt,
							handle
						);
					}

					currentReadFramebuffer = handle;
					currentDrawFramebuffer = handle;
				}
				else if (currentReadFramebuffer != handle)
				{
					BindReadFramebuffer(handle);
				}
				else if (currentDrawFramebuffer != handle)
				{
					BindDrawFramebuffer(handle);
				}
			}

			public static void BindReadFramebuffer(int handle)
			{
				if (handle == currentReadFramebuffer)
				{
					return;
				}

				if (hasARB)
				{
					GL.BindFramebuffer(
						FramebufferTarget.ReadFramebuffer,
						handle
					);
				}
				else
				{
					GL.Ext.BindFramebuffer(
						FramebufferTarget.ReadFramebuffer,
						handle
					);
				}

				currentReadFramebuffer = handle;
			}

			public static void BindDrawFramebuffer(int handle)
			{
				if (handle == currentDrawFramebuffer)
				{
					return;
				}

				if (hasARB)
				{
					GL.BindFramebuffer(
						FramebufferTarget.DrawFramebuffer,
						handle
					);
				}
				else
				{
					GL.Ext.BindFramebuffer(
						FramebufferTarget.DrawFramebuffer,
						handle
					);
				}

				currentDrawFramebuffer = handle;
			}

			public static uint GenRenderbuffer(int width, int height, DepthFormat format)
			{
				uint handle;

				// DepthFormat->RenderbufferStorage
				RenderbufferStorage glFormat;
				if (format == DepthFormat.Depth16)
				{
					glFormat = RenderbufferStorage.DepthComponent16;
				}
				else if (format == DepthFormat.Depth24)
				{
					glFormat = RenderbufferStorage.DepthComponent24;
				}
				else if (format == DepthFormat.Depth24Stencil8)
				{
					glFormat = RenderbufferStorage.Depth24Stencil8;
				}
				else
				{
					throw new Exception("Unhandled DepthFormat: " + format.ToString());
				}

				// Actual GL calls!
				if (hasARB)
				{
					GL.GenRenderbuffers(1, out handle);
					GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, handle);
					GL.RenderbufferStorage(
						RenderbufferTarget.Renderbuffer,
						glFormat,
						width,
						height
					);
					GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
				}
				else
				{
					GL.Ext.GenRenderbuffers(1, out handle);
					GL.Ext.BindRenderbuffer(RenderbufferTarget.RenderbufferExt, handle);
					GL.Ext.RenderbufferStorage(
						RenderbufferTarget.RenderbufferExt,
						glFormat,
						width,
						height
					);
					GL.Ext.BindRenderbuffer(RenderbufferTarget.RenderbufferExt, 0);
				}
				return handle;
			}

			public static void DeleteRenderbuffer(uint handle)
			{
				if (hasARB)
				{
					GL.DeleteRenderbuffers(1, ref handle);
				}
				else
				{
					GL.Ext.DeleteRenderbuffers(1, ref handle);
				}
			}

			public static void AttachColor(int colorAttachment, int index)
			{
				if (hasARB)
				{
					GL.FramebufferTexture2D(
						FramebufferTarget.Framebuffer,
						FramebufferAttachment.ColorAttachment0 + index,
						TextureTarget.Texture2D,
						colorAttachment,
						0
					);
				}
				else
				{
					GL.Ext.FramebufferTexture2D(
						FramebufferTarget.FramebufferExt,
						FramebufferAttachment.ColorAttachment0Ext + index,
						TextureTarget.Texture2D,
						colorAttachment,
						0
					);
				}
			}

			public static void AttachDepthTexture(
				int depthAttachment,
				FramebufferAttachment depthFormat
			) {
				if (hasARB)
				{
					GL.FramebufferTexture2D(
						FramebufferTarget.Framebuffer,
						depthFormat,
						TextureTarget.Texture2D,
						depthAttachment,
						0
					);
				}
				else
				{
					GL.Ext.FramebufferTexture2D(
						FramebufferTarget.FramebufferExt,
						depthFormat,
						TextureTarget.Texture2D,
						depthAttachment,
						0
					);
				}
			}

			public static void AttachDepthRenderbuffer(
				uint renderbuffer,
				FramebufferAttachment depthFormat
			) {
				if (hasARB)
				{
					GL.FramebufferRenderbuffer(
						FramebufferTarget.Framebuffer,
						depthFormat,
						RenderbufferTarget.Renderbuffer,
						renderbuffer
					);
				}
				else
				{
					GL.Ext.FramebufferRenderbuffer(
						FramebufferTarget.FramebufferExt,
						depthFormat,
						RenderbufferTarget.RenderbufferExt,
						renderbuffer
					);
				}
			}

			public static void BlitToBackbuffer(
				int srcWidth,
				int srcHeight,
				int dstWidth,
				int dstHeight
			) {
#if !DISABLE_FAUXBACKBUFFER
				bool scissorTest = OpenGLDevice.Instance.ScissorTestEnable.GetCurrent();
				if (scissorTest)
				{
					GL.Disable(EnableCap.ScissorTest);
				}

				BindReadFramebuffer(OpenGLDevice.Instance.Backbuffer.Handle);
				BindDrawFramebuffer(0);

				if (hasARB)
				{
					GL.BlitFramebuffer(
						0, 0, srcWidth, srcHeight,
						0, 0, dstWidth, dstHeight,
						ClearBufferMask.ColorBufferBit,
						BlitFramebufferFilter.Linear
					);
				}
				else
				{
					GL.Ext.BlitFramebuffer(
						0, 0, srcWidth, srcHeight,
						0, 0, dstWidth, dstHeight,
						ClearBufferMask.ColorBufferBit,
						BlitFramebufferFilter.Linear
					);
				}

				BindFramebuffer(0);

				if (scissorTest)
				{
					GL.Enable(EnableCap.ScissorTest);
				}
#endif
			}
		}

		#endregion

		#region The Faux-Backbuffer

		public class FauxBackbuffer
		{
			public int Handle
			{
				get;
				private set;
			}

			public int Width
			{
				get;
				private set;
			}

			public int Height
			{
				get;
				private set;
			}

			private int colorAttachment;
			private int depthStencilAttachment;
			private DepthFormat depthStencilFormat;

			public FauxBackbuffer(int width, int height, DepthFormat depthFormat)
			{
#if DISABLE_FAUXBACKBUFFER
				Handle = 0;
				Width = width;
				Height = height;
#else
				Handle = Framebuffer.GenFramebuffer();
				colorAttachment = GL.GenTexture();
				depthStencilAttachment = GL.GenTexture();

				GL.BindTexture(TextureTarget.Texture2D, colorAttachment);
				GL.TexImage2D(
					TextureTarget.Texture2D,
					0,
					PixelInternalFormat.Rgba,
					width,
					height,
					0,
					PixelFormat.Rgba,
					PixelType.UnsignedByte,
					IntPtr.Zero
				);
				GL.BindTexture(TextureTarget.Texture2D, depthStencilAttachment);
				GL.TexImage2D(
					TextureTarget.Texture2D,
					0,
					PixelInternalFormat.DepthComponent16,
					width,
					height,
					0,
					PixelFormat.DepthComponent,
					PixelType.UnsignedByte,
					IntPtr.Zero
				);
				Framebuffer.BindFramebuffer(Handle);
				Framebuffer.AttachColor(
					colorAttachment,
					0
				);
				Framebuffer.AttachDepthTexture(
					depthStencilAttachment,
					FramebufferAttachment.DepthAttachment
				);
				GL.BindTexture(TextureTarget.Texture2D, 0);

				Width = width;
				Height = height;
#endif
			}

			public void Dispose()
			{
#if !DISABLE_FAUXBACKBUFFER
				Framebuffer.DeleteFramebuffer(Handle);
				GL.DeleteTexture(colorAttachment);
				GL.DeleteTexture(depthStencilAttachment);
				Handle = 0;
#endif
			}

			public void ResetFramebuffer(int width, int height, DepthFormat depthFormat)
			{
#if DISABLE_FAUXBACKBUFFER
				Width = width;
				Height = height;
#else
				// Update our color attachment to the new resolution.
				GL.BindTexture(TextureTarget.Texture2D, colorAttachment);
				GL.TexImage2D(
					TextureTarget.Texture2D,
					0,
					PixelInternalFormat.Rgba,
					width,
					height,
					0,
					PixelFormat.Rgba,
					PixelType.UnsignedByte,
					IntPtr.Zero
				);

				// Update the depth attachment based on the desired DepthFormat.
				PixelFormat depthPixelFormat;
				PixelInternalFormat depthPixelInternalFormat;
				PixelType depthPixelType;
				FramebufferAttachment depthAttachmentType;
				if (depthFormat == DepthFormat.Depth16)
				{
					depthPixelFormat = PixelFormat.DepthComponent;
					depthPixelInternalFormat = PixelInternalFormat.DepthComponent16;
					depthPixelType = PixelType.UnsignedByte;
					depthAttachmentType = FramebufferAttachment.DepthAttachment;
				}
				else if (depthFormat == DepthFormat.Depth24)
				{
					depthPixelFormat = PixelFormat.DepthComponent;
					depthPixelInternalFormat = PixelInternalFormat.DepthComponent24;
					depthPixelType = PixelType.UnsignedByte;
					depthAttachmentType = FramebufferAttachment.DepthAttachment;
				}
				else
				{
					depthPixelFormat = PixelFormat.DepthStencil;
					depthPixelInternalFormat = PixelInternalFormat.Depth24Stencil8;
					depthPixelType = PixelType.UnsignedInt248;
					depthAttachmentType = FramebufferAttachment.DepthStencilAttachment;
				}

				GL.BindTexture(TextureTarget.Texture2D, depthStencilAttachment);
				GL.TexImage2D(
					TextureTarget.Texture2D,
					0,
					depthPixelInternalFormat,
					width,
					height,
					0,
					depthPixelFormat,
					depthPixelType,
					IntPtr.Zero
				);

				// If the depth format changes, detach before reattaching!
				if (depthFormat != depthStencilFormat)
				{
					FramebufferAttachment attach;
					if (depthStencilFormat == DepthFormat.Depth24Stencil8)
					{
						attach = FramebufferAttachment.DepthStencilAttachment;
					}
					else
					{
						attach = FramebufferAttachment.DepthAttachment;
					}

					Framebuffer.BindFramebuffer(Handle);

					Framebuffer.AttachDepthTexture(
						0,
						attach
					);
					Framebuffer.AttachDepthTexture(
						depthStencilAttachment,
						depthAttachmentType
					);

					if (Game.Instance.GraphicsDevice.RenderTargetCount > 0)
					{
						Framebuffer.BindFramebuffer(
							OpenGLDevice.Instance.targetFramebuffer
						);
					}

					depthStencilFormat = depthFormat;
				}

				GL.BindTexture(
					TextureTarget.Texture2D,
					OpenGLDevice.Instance.Samplers[0].Texture.Get().Handle
				);

				Width = width;
				Height = height;
#endif
			}
		}

		#endregion

		#region Private ARB_debug_output Wrapper

		private static class DebugOutput
		{
			private enum GLenum : uint
			{
				// Hint Enum Value
				GL_DONT_CARE =				0x1100,
				// Source Enum Values
				GL_DEBUG_SOURCE_API_ARB =		0x8246,
				GL_DEBUG_SOURCE_WINDOW_SYSTEM_ARB =	0x8247,
				GL_DEBUG_SOURCE_SHADER_COMPILER_ARB =	0x8248,
				GL_DEBUG_SOURCE_THIRD_PARTY_ARB =	0x8249,
				GL_DEBUG_SOURCE_APPLICATION_ARB =	0x824A,
				GL_DEBUG_SOURCE_OTHER_ARB =		0x824B,
				// Type Enum Values
				GL_DEBUG_TYPE_ERROR_ARB =		0x824C,
				GL_DEBUG_TYPE_DEPRECATED_BEHAVIOR_ARB =	0x824D,
				GL_DEBUG_TYPE_UNDEFINED_BEHAVIOR_ARB =	0x824E,
				GL_DEBUG_TYPE_PORTABILITY_ARB =		0x824F,
				GL_DEBUG_TYPE_PERFORMANCE_ARB =		0x8250,
				GL_DEBUG_TYPE_OTHER_ARB =		0x8251,
				// Severity Enum Values
				GL_DEBUG_SEVERITY_HIGH_ARB =		0x9146,
				GL_DEBUG_SEVERITY_MEDIUM_ARB =		0x9147,
				GL_DEBUG_SEVERITY_LOW_ARB =		0x9148,
			}

			// Entry Points
			private delegate void DebugMessageCallback(
				DebugProc callback,
				IntPtr userParam
			);
			private delegate void DebugMessageControl(
				GLenum source,
				GLenum type,
				GLenum severity,
				IntPtr count, // GLsizei
				IntPtr ids, // const GLuint*
				bool enabled
			);
			private static DebugMessageCallback glDebugMessageCallbackARB;
			private static DebugMessageControl glDebugMessageControlARB;

			// Function pointer
			private delegate void DebugProc(
				GLenum source,
				GLenum type,
				uint id,
				GLenum severity,
				IntPtr length, // GLsizei
				IntPtr message, // const GLchar*
				IntPtr userParam // const GLvoid*
			);
			private static DebugProc DebugCall = DebugCallback;

			// Debug callback
			private static void DebugCallback(
				GLenum source,
				GLenum type,
				uint id,
				GLenum severity,
				IntPtr length, // GLsizei
				IntPtr message, // const GLchar*
				IntPtr userParam // const GLvoid*
			) {
				System.Console.WriteLine(
					"{0}\n\tSource: {1}\n\tType: {2}\n\tSeverity: {3}",
					Marshal.PtrToStringAnsi(message),
					source.ToString(),
					type.ToString(),
					severity.ToString()
				);
				if (type == GLenum.GL_DEBUG_TYPE_ERROR_ARB)
				{
					throw new Exception("ARB_debug_output found an error.");
				}
			}

			[System.Diagnostics.ConditionalAttribute("DEBUG")]
			public static void Initialize()
			{
				IntPtr messageCallback = SDL2.SDL.SDL_GL_GetProcAddress("glDebugMessageCallbackARB");
				IntPtr messageControl = SDL2.SDL.SDL_GL_GetProcAddress("glDebugMessageControlARB");
				if (messageCallback == IntPtr.Zero || messageControl == IntPtr.Zero)
				{
					System.Console.WriteLine("ARB_debug_output not supported!");
					return;
				}
				glDebugMessageCallbackARB = (DebugMessageCallback) Marshal.GetDelegateForFunctionPointer(
					messageCallback,
					typeof(DebugMessageCallback)
				);
				glDebugMessageControlARB = (DebugMessageControl) Marshal.GetDelegateForFunctionPointer(
					messageControl,
					typeof(DebugMessageControl)
				);
				glDebugMessageCallbackARB(DebugCall, IntPtr.Zero);
				glDebugMessageControlARB(
					GLenum.GL_DONT_CARE,
					GLenum.GL_DONT_CARE,
					GLenum.GL_DONT_CARE,
					IntPtr.Zero,
					IntPtr.Zero,
					true
				);
				glDebugMessageControlARB(
					GLenum.GL_DONT_CARE,
					GLenum.GL_DEBUG_TYPE_OTHER_ARB,
					GLenum.GL_DEBUG_SEVERITY_LOW_ARB,
					IntPtr.Zero,
					IntPtr.Zero,
					false
				);
			}
		}

		#endregion
	}
}
