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
 * Additionally, if you always use the desktop resolution in fullscreen mode,
 * you can use this to optimize your game and even lower the GL requirements.
 * -flibit
 */
#endregion

#region THREADED_GL Option
// #define THREADED_GL
/* Ah, so I see you've run into some issues with threaded GL...
 *
 * This class is designed to handle rendering coming from multiple threads, but
 * if you're too wreckless with how many threads are calling the GL, this will
 * hang.
 *
 * With THREADED_GL we instead allow you to run threaded rendering using
 * multiple GL contexts. This is more flexible, but much more dangerous.
 *
 * Also note that this affects Threading.cs and SDL2/SDL2_GamePlatform.cs!
 * Check THREADED_GL there too.
 *
 * Basically, if you have to enable this, you should feel very bad.
 * -flibit
 */
#endregion

#region Using Statements
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SDL2;
#endregion

namespace Microsoft.Xna.Framework.Graphics
{
	internal class OpenGLDevice
	{
		#region OpenGL Texture Container Class

		public class OpenGLTexture
		{
			public uint Handle
			{
				get;
				private set;
			}

			public GLenum Target
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

			public TextureAddressMode WrapS;
			public TextureAddressMode WrapT;
			public TextureAddressMode WrapR;
			public TextureFilter Filter;
			public float Anistropy;
			public int MaxMipmapLevel;
			public float LODBias;

			public OpenGLTexture(
				uint handle,
				Type target,
				SurfaceFormat format,
				bool hasMipmaps
			) {
				Handle = handle;
				Target = XNAToGL.TextureType[target];
				Format = format;
				HasMipmaps = hasMipmaps;

				WrapS = TextureAddressMode.Wrap;
				WrapT = TextureAddressMode.Wrap;
				WrapR = TextureAddressMode.Wrap;
				Filter = TextureFilter.Linear;
				Anistropy = 4.0f;
				MaxMipmapLevel = 0;
				LODBias = 0.0f;
			}

			// We can't set a SamplerState Texture to null, so use this.
			private OpenGLTexture()
			{
				Handle = 0;
				Target = GLenum.GL_TEXTURE_2D; // FIXME: Assumption! -flibit
			}
			public static readonly OpenGLTexture NullTexture = new OpenGLTexture();
		}

		#endregion

		#region OpenGL Vertex Buffer Container Class

		public class OpenGLVertexBuffer
		{
			public uint Handle
			{
				get;
				private set;
			}

			public GLenum Dynamic
			{
				get;
				private set;
			}

			public OpenGLVertexBuffer(
				GraphicsDevice graphicsDevice,
				bool dynamic,
				int vertexCount,
				int vertexStride
			) {
				uint handle;
				graphicsDevice.GLDevice.glGenBuffers((IntPtr) 1, out handle);
				Handle = handle;
				Dynamic = dynamic ? GLenum.GL_STREAM_DRAW : GLenum.GL_STATIC_DRAW;

				graphicsDevice.GLDevice.BindVertexBuffer(this);
				graphicsDevice.GLDevice.glBufferData(
					GLenum.GL_ARRAY_BUFFER,
					(IntPtr) (vertexStride * vertexCount),
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
			public uint Handle
			{
				get;
				private set;
			}

			public GLenum Dynamic
			{
				get;
				private set;
			}

			public IntPtr BufferSize
			{
				get;
				private set;
			}

			public OpenGLIndexBuffer(
				GraphicsDevice graphicsDevice,
				bool dynamic,
				int indexCount,
				IndexElementSize elementSize
			) {
				uint handle;
				graphicsDevice.GLDevice.glGenBuffers((IntPtr) 1, out handle);
				Handle = handle;
				Dynamic = dynamic ? GLenum.GL_STREAM_DRAW : GLenum.GL_STATIC_DRAW;
				BufferSize = (IntPtr) (indexCount * (elementSize == IndexElementSize.SixteenBits ? 2 : 4));

				graphicsDevice.GLDevice.BindIndexBuffer(this);
				graphicsDevice.GLDevice.glBufferData(
					GLenum.GL_ELEMENT_ARRAY_BUFFER,
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
			public int Divisor;

			// Checked in VertexAttribPointer
			public uint CurrentBuffer;
			public int CurrentSize;
			public VertexElementFormat CurrentType;
			public bool CurrentNormalized;
			public int CurrentStride;
			public IntPtr CurrentPointer;

			public OpenGLVertexAttribute()
			{
				Divisor = 0;
				CurrentBuffer = 0;
				CurrentSize = 4;
				CurrentType = VertexElementFormat.Single;
				CurrentNormalized = false;
				CurrentStride = 0;
				CurrentPointer = IntPtr.Zero;
			}
		}

		#endregion

		#region Alpha Blending State Variables

		internal bool alphaBlendEnable = false;
		private Color blendColor = Color.Transparent;
		private BlendFunction blendOp = BlendFunction.Add;
		private BlendFunction blendOpAlpha = BlendFunction.Add;
		private Blend srcBlend = Blend.One;
		private Blend dstBlend = Blend.Zero;
		private Blend srcBlendAlpha = Blend.One;
		private Blend dstBlendAlpha = Blend.Zero;
		private ColorWriteChannels colorWriteEnable = ColorWriteChannels.All;

		#endregion

		#region Depth State Variables

		internal bool zEnable = false;
		private bool zWriteEnable = false;
		private CompareFunction depthFunc = CompareFunction.Less;

		#endregion

		#region Stencil State Variables

		private bool stencilEnable = false;
		private int stencilWriteMask = -1; // AKA 0xFFFFFFFF, ugh -flibit
		private bool separateStencilEnable = false;
		private int stencilRef = 0;
		private int stencilMask = -1; // AKA 0xFFFFFFFF, ugh -flibit
		private CompareFunction stencilFunc = CompareFunction.Always;
		private StencilOperation stencilFail = StencilOperation.Keep;
		private StencilOperation stencilZFail = StencilOperation.Keep;
		private StencilOperation stencilPass = StencilOperation.Keep;
		private CompareFunction ccwStencilFunc = CompareFunction.Always;
		private StencilOperation ccwStencilFail = StencilOperation.Keep;
		private StencilOperation ccwStencilZFail = StencilOperation.Keep;
		private StencilOperation ccwStencilPass = StencilOperation.Keep;

		#endregion

		#region Rasterizer State Variables

		internal bool scissorTestEnable = false;
		internal CullMode cullFrontFace = CullMode.None;
		private FillMode fillMode = FillMode.Solid;
		private float depthBias = 0.0f;
		private float slopeScaleDepthBias = 0.0f;

		#endregion

		#region Viewport State Variables

		/* These two aren't actually empty rects by default in OpenGL,
		 * but we don't _really_ know the starting window size, so
		 * force apply this when the GraphicsDevice is initialized.
		 * -flibit
		 */
		private Rectangle scissorRectangle =  new Rectangle(
			0,
			0,
			0,
			0
		);
		private Rectangle viewport = new Rectangle(
			0,
			0,
			0,
			0
		);
		private float depthRangeMin = 0.0f;
		private float depthRangeMax = 1.0f;

		#endregion

		#region Texture Collection Variables

		// FIXME: This doesn't need to be public. Blame VideoPlayer. -flibit
		public OpenGLTexture[] Textures
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
		private int[] previousAttributeDivisor;

		#endregion

		#region Buffer Binding Cache Variables

		private uint currentVertexBuffer = 0;
		private uint currentIndexBuffer = 0;

		#endregion

		#region Render Target Cache Variables

		private uint currentReadFramebuffer = 0;
		public uint CurrentReadFramebuffer
		{
			get
			{
				return currentReadFramebuffer;
			}
		}

		private uint currentDrawFramebuffer = 0;
		public uint CurrentDrawFramebuffer
		{
			get
			{
				return currentDrawFramebuffer;
			}
		}

		private uint targetFramebuffer = 0;
		private uint[] currentAttachments;
		private GLenum[] currentAttachmentFaces;
		private int currentDrawBuffers;
		private GLenum[] drawBuffersArray;
		private uint currentRenderbuffer;
		private DepthFormat currentDepthStencilFormat;

		#endregion

		#region Clear Cache Variables

		private Vector4 currentClearColor = new Vector4(0, 0, 0, 0);
		private float currentClearDepth = 1.0f;
		private int currentClearStencil = 0;

		#endregion

		#region Private OpenGL Context Variable

		private IntPtr glContext;

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

		#endregion

		#region Public Constructor

		public OpenGLDevice(
			PresentationParameters presentationParameters
		) {
			// Create OpenGL context
			glContext = SDL.SDL_GL_CreateContext(
				presentationParameters.DeviceWindowHandle
			);

#if THREADED_GL
			// Create a background context
			SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_SHARE_WITH_CURRENT_CONTEXT, 1);
			Threading.WindowInfo = presentationParameters.DeviceWindowHandle;
			Threading.BackgroundContext = new Threading.GL_ContextHandle()
			{
				context = SDL.SDL_GL_CreateContext(
					presentationParameters.DeviceWindowHandle
				)
			};

			// Make the foreground context current.
			SDL.SDL_GL_MakeCurrent(presentationParameters.DeviceWindowHandle, glContext);
#endif

			// Initialize entry points
			LoadGLEntryPoints();

			// Print GL information
			System.Console.WriteLine("OpenGL Device: " + glGetString(GLenum.GL_RENDERER));
			System.Console.WriteLine("OpenGL Driver: " + glGetString(GLenum.GL_VERSION));
			System.Console.WriteLine("OpenGL Vendor: " + glGetString(GLenum.GL_VENDOR));
			
			// Load the extension list, initialize extension-dependent components
			Extensions = glGetString(GLenum.GL_EXTENSIONS);
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
				this,
				GraphicsDeviceManager.DefaultBackBufferWidth,
				GraphicsDeviceManager.DefaultBackBufferHeight,
				DepthFormat.Depth16
			);

			// Initialize texture collection array
			int numSamplers;
			glGetIntegerv(GLenum.GL_MAX_TEXTURE_UNITS, out numSamplers);
			Textures = new OpenGLTexture[numSamplers];
			for (int i = 0; i < numSamplers; i += 1)
			{
				Textures[i] = OpenGLTexture.NullTexture;
			}
			MaxTextureSlots = numSamplers;

			// Initialize vertex attribute state array
			int numAttributes;
			glGetIntegerv(GLenum.GL_MAX_VERTEX_ATTRIBS, out numAttributes);
			Attributes = new OpenGLVertexAttribute[numAttributes];
			AttributeEnabled = new bool[numAttributes];
			previousAttributeEnabled = new bool[numAttributes];
			previousAttributeDivisor = new int[numAttributes];
			for (int i = 0; i < numAttributes; i += 1)
			{
				Attributes[i] = new OpenGLVertexAttribute();
				AttributeEnabled[i] = false;
				previousAttributeEnabled[i] = false;
				previousAttributeDivisor[i] = 0;
			}

			// Initialize render target FBO and state arrays
			int numAttachments;
			glGetIntegerv(GLenum.GL_MAX_DRAW_BUFFERS, out numAttachments);
			currentAttachments = new uint[numAttachments];
			currentAttachmentFaces = new GLenum[numAttachments];
			drawBuffersArray = new GLenum[numAttachments];
			for (int i = 0; i < numAttachments; i += 1)
			{
				currentAttachments[i] = 0;
				currentAttachmentFaces[i] = GLenum.GL_TEXTURE_2D;
				drawBuffersArray[i] = GLenum.GL_COLOR_ATTACHMENT0 + i;
			}
			currentDrawBuffers = 0;
			currentRenderbuffer = 0;
			currentDepthStencilFormat = DepthFormat.None;
			glGenFramebuffers((IntPtr) 1, out targetFramebuffer);
		}

		#endregion

		#region Dispose Method

		public void Dispose()
		{
			glDeleteFramebuffers((IntPtr) 1, ref targetFramebuffer);
			targetFramebuffer = 0;
			Backbuffer.Dispose();
			Backbuffer = null;

#if THREADED_GL
			SDL.SDL_GL_DeleteContext(Threading.BackgroundContext.context);
#endif
			SDL.SDL_GL_DeleteContext(glContext);
		}

		#endregion

		#region Window SwapBuffers Method

		public void SwapBuffers(IntPtr overrideWindowHandle)
		{
#if !DISABLE_FAUXBACKBUFFER
			int windowWidth, windowHeight;
			SDL.SDL_GetWindowSize(
				overrideWindowHandle,
				out windowWidth,
				out windowHeight
			);

			if (scissorTestEnable)
			{
				glDisable(GLenum.GL_SCISSOR_TEST);
			}

			BindReadFramebuffer(Backbuffer.Handle);
			BindDrawFramebuffer(0);

			glBlitFramebuffer(
				0, 0, Backbuffer.Width, Backbuffer.Height,
				0, 0, windowWidth, windowHeight,
				GLenum.GL_COLOR_BUFFER_BIT,
				GLenum.GL_LINEAR
			);

			BindFramebuffer(0);

			if (scissorTestEnable)
			{
				glEnable(GLenum.GL_SCISSOR_TEST);
			}
#endif

			SDL.SDL_GL_SwapWindow(
				overrideWindowHandle
			);
			BindFramebuffer(Backbuffer.Handle);
		}

		#endregion

		#region State Management Methods

		public void SetViewport(Viewport vp, bool renderTargetBound)
		{
			// Flip viewport when target is not bound
			if (!renderTargetBound)
			{
				vp.Y = Backbuffer.Height - vp.Y - vp.Height;
			}

			if (vp.Bounds != viewport)
			{
				viewport = vp.Bounds;
				glViewport(
					viewport.X,
					viewport.Y,
					(IntPtr) viewport.Width,
					(IntPtr) viewport.Height
				);
			}

			if (vp.MinDepth != depthRangeMin || vp.MaxDepth != depthRangeMax)
			{
				depthRangeMin = vp.MinDepth;
				depthRangeMax = vp.MaxDepth;
				glDepthRange((double) depthRangeMin, (double) depthRangeMax);
			}
		}

		public void SetScissorRect(
			Rectangle scissorRect,
			bool renderTargetBound
		) {
			// Flip rectangle when target is not bound
			if (!renderTargetBound)
			{
				scissorRect.Y = viewport.Height - scissorRect.Y - scissorRect.Height;
			}

			if (scissorRect != scissorRectangle)
			{
				scissorRectangle = scissorRect;
				glScissor(
					scissorRectangle.X,
					scissorRectangle.Y,
					(IntPtr) scissorRectangle.Width,
					(IntPtr) scissorRectangle.Height
				);
			}
		}

		public void SetBlendState(BlendState blendState)
		{
			bool newEnable = (
				!(	blendState.ColorSourceBlend == Blend.One &&
					blendState.ColorDestinationBlend == Blend.Zero &&
					blendState.AlphaSourceBlend == Blend.One &&
					blendState.AlphaDestinationBlend == Blend.Zero	)
			);
			if (newEnable != alphaBlendEnable)
			{
				alphaBlendEnable = newEnable;
				ToggleGLState(GLenum.GL_BLEND, alphaBlendEnable);
			}

			if (alphaBlendEnable)
			{
				if (blendState.BlendFactor != blendColor)
				{
					blendColor = blendState.BlendFactor;
					glBlendColor(
						blendColor.R / 255.0f,
						blendColor.G / 255.0f,
						blendColor.B / 255.0f,
						blendColor.A / 255.0f
					);
				}

				if (	blendState.ColorSourceBlend != srcBlend ||
					blendState.ColorDestinationBlend != dstBlend ||
					blendState.AlphaSourceBlend != srcBlendAlpha ||
					blendState.AlphaDestinationBlend != dstBlendAlpha	)
				{
					srcBlend = blendState.ColorSourceBlend;
					dstBlend = blendState.ColorDestinationBlend;
					srcBlendAlpha = blendState.AlphaSourceBlend;
					dstBlendAlpha = blendState.AlphaDestinationBlend;
					glBlendFuncSeparate(
						XNAToGL.BlendMode[srcBlend],
						XNAToGL.BlendMode[dstBlend],
						XNAToGL.BlendMode[srcBlendAlpha],
						XNAToGL.BlendMode[dstBlendAlpha]
					);
				}

				if (	blendState.ColorBlendFunction != blendOp ||
					blendState.AlphaBlendFunction != blendOpAlpha	)
				{
					blendOp = blendState.ColorBlendFunction;
					blendOpAlpha = blendState.AlphaBlendFunction;
					glBlendEquationSeparate(
						XNAToGL.BlendEquation[blendOp],
						XNAToGL.BlendEquation[blendOpAlpha]
					);
				}

				if (blendState.ColorWriteChannels != colorWriteEnable)
				{
					colorWriteEnable = blendState.ColorWriteChannels;
					glColorMask(
						(colorWriteEnable & ColorWriteChannels.Red) != 0,
						(colorWriteEnable & ColorWriteChannels.Green) != 0,
						(colorWriteEnable & ColorWriteChannels.Blue) != 0,
						(colorWriteEnable & ColorWriteChannels.Alpha) != 0
					);
				}
			}
		}

		public void SetDepthStencilState(DepthStencilState depthStencilState)
		{
			if (depthStencilState.DepthBufferEnable != zEnable)
			{
				zEnable = depthStencilState.DepthBufferEnable;
				ToggleGLState(GLenum.GL_DEPTH_TEST, zEnable);
			}

			if (zEnable)
			{
				if (depthStencilState.DepthBufferWriteEnable != zWriteEnable)
				{
					zWriteEnable = depthStencilState.DepthBufferWriteEnable;
					glDepthMask(zWriteEnable);
				}

				if (depthStencilState.DepthBufferFunction != depthFunc)
				{
					depthFunc = depthStencilState.DepthBufferFunction;
					glDepthFunc(XNAToGL.CompareFunc[depthFunc]);
				}
			}

			if (depthStencilState.StencilEnable != stencilEnable)
			{
				stencilEnable = depthStencilState.StencilEnable;
				ToggleGLState(GLenum.GL_STENCIL_TEST, stencilEnable);
			}

			if (stencilEnable)
			{
				if (depthStencilState.StencilWriteMask != stencilWriteMask)
				{
					stencilWriteMask = depthStencilState.StencilWriteMask;
					glStencilMask(stencilWriteMask);
				}

				// TODO: Can we split StencilFunc/StencilOp up nicely? -flibit
				if (	depthStencilState.TwoSidedStencilMode != separateStencilEnable ||
					depthStencilState.ReferenceStencil != stencilRef ||
					depthStencilState.StencilMask != stencilMask ||
					depthStencilState.StencilFunction != stencilFunc ||
					depthStencilState.CounterClockwiseStencilFunction != ccwStencilFunc ||
					depthStencilState.StencilFail != stencilFail ||
					depthStencilState.StencilDepthBufferFail != stencilZFail ||
					depthStencilState.StencilPass != stencilPass ||
					depthStencilState.CounterClockwiseStencilFail != ccwStencilFail ||
					depthStencilState.CounterClockwiseStencilDepthBufferFail != ccwStencilZFail ||
					depthStencilState.CounterClockwiseStencilPass != ccwStencilPass	)
				{
					separateStencilEnable = depthStencilState.TwoSidedStencilMode;
					stencilRef = depthStencilState.ReferenceStencil;
					stencilMask = depthStencilState.StencilMask;
					stencilFunc = depthStencilState.StencilFunction;
					stencilFail = depthStencilState.StencilFail;
					stencilZFail = depthStencilState.StencilDepthBufferFail;
					stencilPass = depthStencilState.StencilPass;
					if (separateStencilEnable)
					{
						ccwStencilFunc = depthStencilState.CounterClockwiseStencilFunction;
						ccwStencilFail = depthStencilState.CounterClockwiseStencilFail;
						ccwStencilZFail = depthStencilState.CounterClockwiseStencilDepthBufferFail;
						ccwStencilPass = depthStencilState.CounterClockwiseStencilPass;
						glStencilFuncSeparate(
							GLenum.GL_FRONT,
							XNAToGL.CompareFunc[stencilFunc],
							stencilRef,
							stencilMask
						);
						glStencilFuncSeparate(
							GLenum.GL_BACK,
							XNAToGL.CompareFunc[ccwStencilFunc],
							stencilRef,
							stencilMask
						);
						glStencilOpSeparate(
							GLenum.GL_FRONT,
							XNAToGL.GLStencilOp[stencilFail],
							XNAToGL.GLStencilOp[stencilZFail],
							XNAToGL.GLStencilOp[stencilPass]
						);
						glStencilOpSeparate(
							GLenum.GL_BACK,
							XNAToGL.GLStencilOp[ccwStencilFail],
							XNAToGL.GLStencilOp[ccwStencilZFail],
							XNAToGL.GLStencilOp[ccwStencilPass]
						);
					}
					else
					{
						glStencilFunc(
							XNAToGL.CompareFunc[stencilFunc],
							stencilRef,
							stencilMask
						);
						glStencilOp(
							XNAToGL.GLStencilOp[stencilFail],
							XNAToGL.GLStencilOp[stencilZFail],
							XNAToGL.GLStencilOp[stencilPass]
						);
					}
				}
			}
		}

		public void ApplyRasterizerState(
			RasterizerState rasterizerState,
			bool renderTargetBound
		) {
			if (rasterizerState.ScissorTestEnable != scissorTestEnable)
			{
				scissorTestEnable = rasterizerState.ScissorTestEnable;
				ToggleGLState(GLenum.GL_SCISSOR_TEST, scissorTestEnable);
			}

			CullMode actualMode;
			if (renderTargetBound)
			{
				actualMode = rasterizerState.CullMode;
			}
			else
			{
				// When not rendering offscreen the faces change order.
				if (rasterizerState.CullMode == CullMode.None)
				{
					actualMode = rasterizerState.CullMode;
				}
				else
				{
					actualMode = (
						rasterizerState.CullMode == CullMode.CullClockwiseFace ?
							CullMode.CullCounterClockwiseFace :
							CullMode.CullClockwiseFace
					);
				}
			}
			if (actualMode != cullFrontFace)
			{
				if ((actualMode == CullMode.None) != (cullFrontFace == CullMode.None))
				{
					ToggleGLState(GLenum.GL_CULL_FACE, actualMode != CullMode.None);
					if (actualMode != CullMode.None)
					{
						// FIXME: XNA/FNA-specific behavior? -flibit
						glCullFace(GLenum.GL_BACK);
					}
				}
				cullFrontFace = actualMode;
				if (cullFrontFace != CullMode.None)
				{
					glFrontFace(XNAToGL.FrontFace[cullFrontFace]);
				}
			}

			if (rasterizerState.FillMode != fillMode)
			{
				fillMode = rasterizerState.FillMode;
				glPolygonMode(
					GLenum.GL_FRONT_AND_BACK,
					XNAToGL.GLFillMode[fillMode]
				);
			}

			if (zEnable)
			{
				if (	rasterizerState.DepthBias != depthBias ||
					rasterizerState.SlopeScaleDepthBias != slopeScaleDepthBias	)
				{
					depthBias = rasterizerState.DepthBias;
					slopeScaleDepthBias = rasterizerState.SlopeScaleDepthBias;
					if (depthBias == 0.0f && slopeScaleDepthBias == 0.0f)
					{
						glDisable(GLenum.GL_POLYGON_OFFSET_FILL);
					}
					else
					{
						glEnable(GLenum.GL_POLYGON_OFFSET_FILL);
						glPolygonOffset(slopeScaleDepthBias, depthBias);
					}
				}
			}
		}

		public void VerifySampler(int index, Texture texture, SamplerState sampler)
		{
			if (texture == null)
			{
				if (Textures[index] != OpenGLTexture.NullTexture)
				{
					if (index != 0)
					{
						glActiveTexture(GLenum.GL_TEXTURE0 + index);
					}
					glBindTexture(Textures[index].Target, 0);
					if (index != 0)
					{
						// Keep this state sane. -flibit
						glActiveTexture(GLenum.GL_TEXTURE0);
					}
					Textures[index] = OpenGLTexture.NullTexture;
				}
				return;
			}

			if (	texture.texture == Textures[index] &&
				sampler.AddressU == texture.texture.WrapS &&
				sampler.AddressV == texture.texture.WrapT &&
				sampler.AddressW == texture.texture.WrapR &&
				sampler.Filter == texture.texture.Filter &&
				sampler.MaxAnisotropy == texture.texture.Anistropy &&
				sampler.MaxMipLevel == texture.texture.MaxMipmapLevel &&
				sampler.MipMapLevelOfDetailBias == texture.texture.LODBias	)
			{
				// Nothing's changing, forget it.
				return;
			}

			// Set the active texture slot
			if (index != 0)
			{
				glActiveTexture(GLenum.GL_TEXTURE0 + index);
			}

			// Bind the correct texture
			if (texture.texture != Textures[index])
			{
				if (texture.texture.Target != Textures[index].Target)
				{
					// If we're changing targets, unbind the old texture first!
					glBindTexture(Textures[index].Target, 0);
				}
				glBindTexture(texture.texture.Target, texture.texture.Handle);
				Textures[index] = texture.texture;
			}

			// Apply the sampler states to the GL texture
			if (sampler.AddressU != texture.texture.WrapS)
			{
				texture.texture.WrapS = sampler.AddressU;
				glTexParameteri(
					texture.texture.Target,
					GLenum.GL_TEXTURE_WRAP_S,
					(int) XNAToGL.Wrap[texture.texture.WrapS]
				);
			}
			if (sampler.AddressV != texture.texture.WrapT)
			{
				texture.texture.WrapT = sampler.AddressV;
				glTexParameteri(
					texture.texture.Target,
					GLenum.GL_TEXTURE_WRAP_T,
					(int) XNAToGL.Wrap[texture.texture.WrapT]
				);
			}
			if (sampler.AddressW != texture.texture.WrapR)
			{
				texture.texture.WrapR = sampler.AddressW;
				glTexParameteri(
					texture.texture.Target,
					GLenum.GL_TEXTURE_WRAP_R,
					(int) XNAToGL.Wrap[texture.texture.WrapR]
				);
			}
			if (	sampler.Filter != texture.texture.Filter ||
				sampler.MaxAnisotropy != texture.texture.Anistropy	)
			{
				texture.texture.Filter = sampler.Filter;
				texture.texture.Anistropy = sampler.MaxAnisotropy;
				glTexParameteri(
					texture.texture.Target,
					GLenum.GL_TEXTURE_MAG_FILTER,
					(int) XNAToGL.MagFilter[texture.texture.Filter]
				);
				glTexParameteri(
					texture.texture.Target,
					GLenum.GL_TEXTURE_MIN_FILTER,
					(int) (
						texture.texture.HasMipmaps ?
							XNAToGL.MinMipFilter[texture.texture.Filter] :
							XNAToGL.MinFilter[texture.texture.Filter]
					)
				);
				glTexParameterf(
					texture.texture.Target,
					GLenum.GL_TEXTURE_MAX_ANISOTROPY_EXT,
					(texture.texture.Filter == TextureFilter.Anisotropic) ?
						Math.Max(texture.texture.Anistropy, 1.0f) :
						1.0f
				);
			}
			if (sampler.MaxMipLevel != texture.texture.MaxMipmapLevel)
			{
				texture.texture.MaxMipmapLevel = sampler.MaxMipLevel;
				glTexParameteri(
					texture.texture.Target,
					GLenum.GL_TEXTURE_BASE_LEVEL,
					texture.texture.MaxMipmapLevel
				);
			}
			if (sampler.MipMapLevelOfDetailBias != texture.texture.LODBias)
			{
				texture.texture.LODBias = sampler.MipMapLevelOfDetailBias;
				glTexParameterf(
					texture.texture.Target,
					GLenum.GL_TEXTURE_LOD_BIAS,
					texture.texture.LODBias
				);
			}

			if (index != 0)
			{
				// Keep this state sane. -flibit
				glActiveTexture(GLenum.GL_TEXTURE0);
			}
		}

		#endregion

		#region Flush Vertex Attributes Method

		public void FlushGLVertexAttributes()
		{
			for (int i = 0; i < Attributes.Length; i += 1)
			{
				if (AttributeEnabled[i])
				{
					AttributeEnabled[i] = false;
					if (!previousAttributeEnabled[i])
					{
						glEnableVertexAttribArray(i);
						previousAttributeEnabled[i] = true;
					}
				}
				else if (previousAttributeEnabled[i])
				{
					glDisableVertexAttribArray(i);
					previousAttributeEnabled[i] = false;
				}

				if (Attributes[i].Divisor != previousAttributeDivisor[i])
				{
					glVertexAttribDivisor(i, Attributes[i].Divisor);
					previousAttributeDivisor[i] = Attributes[i].Divisor;
				}
			}
		}

		#endregion

		#region glVertexAttribPointer Method

		public void VertexAttribPointer(
			int location,
			int size,
			VertexElementFormat type,
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
				glVertexAttribPointer(
					location,
					size,
					XNAToGL.PointerType[type],
					normalized,
					(IntPtr) stride,
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
				glBindBuffer(GLenum.GL_ARRAY_BUFFER, buffer.Handle);
				currentVertexBuffer = buffer.Handle;
			}
		}

		public void BindIndexBuffer(OpenGLIndexBuffer buffer)
		{
			if (buffer.Handle != currentIndexBuffer)
			{
				glBindBuffer(GLenum.GL_ELEMENT_ARRAY_BUFFER, buffer.Handle);
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
				glBufferData(
					GLenum.GL_ARRAY_BUFFER,
					(IntPtr) bufferSize,
					IntPtr.Zero,
					handle.Dynamic
				);
			}

			GCHandle dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);

			glBufferSubData(
				GLenum.GL_ARRAY_BUFFER,
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
				glBufferData(
					GLenum.GL_ELEMENT_ARRAY_BUFFER,
					handle.BufferSize,
					IntPtr.Zero,
					handle.Dynamic
				);
			}

			GCHandle dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);

			int elementSizeInBytes = Marshal.SizeOf(typeof(T));
			glBufferSubData(
				GLenum.GL_ELEMENT_ARRAY_BUFFER,
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

			IntPtr ptr = glMapBuffer(GLenum.GL_ARRAY_BUFFER, GLenum.GL_READ_ONLY);

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

			glUnmapBuffer(GLenum.GL_ARRAY_BUFFER);
		}

		public void GetIndexBufferData<T>(
			OpenGLIndexBuffer handle,
			int offsetInBytes,
			T[] data,
			int startIndex,
			int elementCount
		) where T : struct {
			BindIndexBuffer(handle);

			IntPtr ptr = glMapBuffer(GLenum.GL_ELEMENT_ARRAY_BUFFER, GLenum.GL_READ_ONLY);

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

			glUnmapBuffer(GLenum.GL_ELEMENT_ARRAY_BUFFER);
		}

		#endregion

		#region glDeleteBuffers Methods

		public void DeleteVertexBuffer(OpenGLVertexBuffer buffer)
		{
			if (buffer.Handle == currentVertexBuffer)
			{
				glBindBuffer(GLenum.GL_ARRAY_BUFFER, 0);
				currentVertexBuffer = 0;
			}
			uint handle = buffer.Handle;
			glDeleteBuffers((IntPtr) 1, ref handle);
		}

		public void DeleteIndexBuffer(OpenGLIndexBuffer buffer)
		{
			if (buffer.Handle == currentIndexBuffer)
			{
				glBindBuffer(GLenum.GL_ELEMENT_ARRAY_BUFFER, 0);
				currentIndexBuffer = 0;
			}
			uint handle = buffer.Handle;
			glDeleteBuffers((IntPtr) 1, ref handle);
		}

		#endregion

		#region glCreateTexture Method

		public OpenGLTexture CreateTexture(Type target, SurfaceFormat format, bool hasMipmaps)
		{
			uint handle;
			glGenTextures((IntPtr) 1, out handle);
			OpenGLTexture result = new OpenGLTexture(
				handle,
				target,
				format,
				hasMipmaps
			);
			BindTexture(result);
			glTexParameteri(
				result.Target,
				GLenum.GL_TEXTURE_WRAP_S,
				(int) XNAToGL.Wrap[result.WrapS]
			);
			glTexParameteri(
				result.Target,
				GLenum.GL_TEXTURE_WRAP_T,
				(int) XNAToGL.Wrap[result.WrapT]
			);
			glTexParameteri(
				result.Target,
				GLenum.GL_TEXTURE_WRAP_R,
				(int) XNAToGL.Wrap[result.WrapR]
			);
			glTexParameteri(
				result.Target,
				GLenum.GL_TEXTURE_MAG_FILTER,
				(int) XNAToGL.MagFilter[result.Filter]
			);
			glTexParameteri(
				result.Target,
				GLenum.GL_TEXTURE_MIN_FILTER,
				(int) (result.HasMipmaps ? XNAToGL.MinMipFilter[result.Filter] : XNAToGL.MinFilter[result.Filter])
			);
			glTexParameterf(
				result.Target,
				GLenum.GL_TEXTURE_MAX_ANISOTROPY_EXT,
				(result.Filter == TextureFilter.Anisotropic) ? Math.Max(result.Anistropy, 1.0f) : 1.0f
			);
			glTexParameteri(
				result.Target,
				GLenum.GL_TEXTURE_BASE_LEVEL,
				result.MaxMipmapLevel
			);
			glTexParameterf(
				result.Target,
				GLenum.GL_TEXTURE_LOD_BIAS,
				result.LODBias
			);
			return result;
		}

		#endregion

		#region glBindTexture Method

		public void BindTexture(OpenGLTexture texture)
		{
			if (texture.Target != Textures[0].Target)
			{
				glBindTexture(Textures[0].Target, 0);
			}
			if (texture != Textures[0])
			{
				glBindTexture(
					texture.Target,
					texture.Handle
				);
			}
			Textures[0] = texture;
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
					currentAttachments[i] = uint.MaxValue;
				}
			}
			uint handle = texture.Handle;
			glDeleteTextures((IntPtr) 1, ref handle);
		}

		#endregion

		#region glReadPixels Method

		/// <summary>
		/// Attempts to read the texture data directly from the FBO using glReadPixels
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
				uint oldReadFramebuffer = CurrentReadFramebuffer;
				if (oldReadFramebuffer != targetFramebuffer)
				{
					BindReadFramebuffer(targetFramebuffer);
				}

				/* glReadPixels should be faster than reading
				 * back from the render target if we are already bound.
				 */
				GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
				// FIXME: Try/Catch with the GCHandle -flibit
				if (rect.HasValue)
				{
					glReadPixels(
						rect.Value.Left,
						rect.Value.Top,
						(IntPtr) rect.Value.Width,
						(IntPtr) rect.Value.Height,
						GLenum.GL_RGBA, // FIXME: Assumption!
						GLenum.GL_UNSIGNED_BYTE,
						handle.AddrOfPinnedObject()
					);
				}
				else
				{
					// FIXME: Using two glGet calls here! D:
					int width = 0;
					int height = 0;
					BindTexture(texture);
					glGetTexLevelParameteriv(
						texture.Target,
						level,
						GLenum.GL_TEXTURE_WIDTH,
						out width
					);
					glGetTexLevelParameteriv(
						texture.Target,
						level,
						GLenum.GL_TEXTURE_HEIGHT,
						out height
					);

					glReadPixels(
						0,
						0,
						(IntPtr) width,
						(IntPtr) height,
						GLenum.GL_RGBA, // FIXME: Assumption
						GLenum.GL_UNSIGNED_BYTE,
						handle.AddrOfPinnedObject()
					);
				}
				handle.Free();
				BindReadFramebuffer(oldReadFramebuffer);
				return true;
			}
			return false;
		}

		#endregion

		#region Framebuffer Methods

		public void BindFramebuffer(uint handle)
		{
			if (	currentReadFramebuffer != handle &&
				currentDrawFramebuffer != handle	)
			{
				glBindFramebuffer(
					GLenum.GL_FRAMEBUFFER,
					handle
				);
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

		public void BindReadFramebuffer(uint handle)
		{
			if (handle == currentReadFramebuffer)
			{
				return;
			}

			glBindFramebuffer(
				GLenum.GL_READ_FRAMEBUFFER,
				handle
			);

			currentReadFramebuffer = handle;
		}

		public void BindDrawFramebuffer(uint handle)
		{
			if (handle == currentDrawFramebuffer)
			{
				return;
			}

			glBindFramebuffer(
				GLenum.GL_DRAW_FRAMEBUFFER,
				handle
			);

			currentDrawFramebuffer = handle;
		}

		#endregion

		#region Renderbuffer Methods

		public uint GenRenderbuffer(int width, int height, DepthFormat format)
		{
			uint handle;
			glGenRenderbuffers((IntPtr) 1, out handle);
			glBindRenderbuffer(
				GLenum.GL_RENDERBUFFER,
				handle
			);
			glRenderbufferStorage(
				GLenum.GL_RENDERBUFFER,
				XNAToGL.DepthStorage[format],
				(IntPtr) width,
				(IntPtr) height
			);
			glBindRenderbuffer(
				GLenum.GL_RENDERBUFFER,
				0
			);
			return handle;
		}

		public void DeleteRenderbuffer(uint renderbuffer)
		{
			if (renderbuffer == currentRenderbuffer)
			{
				// Force a renderbuffer update, this no longer exists!
				currentRenderbuffer = uint.MaxValue;
			}
			glDeleteRenderbuffers((IntPtr) 1, ref renderbuffer);
		}

		#endregion

		#region glEnable/glDisable Method

		private void ToggleGLState(GLenum feature, bool enable)
		{
			if (enable)
			{
				glEnable(feature);
			}
			else
			{
				glDisable(feature);
			}
		}

		#endregion

		#region glClear Method

		public void Clear(ClearOptions options, Vector4 color, float depth, int stencil)
		{
			// Move some stuff around so the glClear works...
			if (scissorTestEnable)
			{
				glDisable(GLenum.GL_SCISSOR_TEST);
			}
			if (!zWriteEnable)
			{
				glDepthMask(true);
			}
			if (stencilWriteMask != -1)
			{
				// AKA 0xFFFFFFFF, ugh -flibit
				glStencilMask(-1);
			}

			// Get the clear mask, set the clear properties if needed
			GLenum clearMask = GLenum.GL_ZERO;
			if ((options & ClearOptions.Target) == ClearOptions.Target)
			{
				clearMask |= GLenum.GL_COLOR_BUFFER_BIT;
				if (!color.Equals(currentClearColor))
				{
					glClearColor(
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
				clearMask |= GLenum.GL_DEPTH_BUFFER_BIT;
				if (depth != currentClearDepth)
				{
					glClearDepth((double) depth);
					currentClearDepth = depth;
				}
			}
			if ((options & ClearOptions.Stencil) == ClearOptions.Stencil)
			{
				clearMask |= GLenum.GL_STENCIL_BUFFER_BIT;
				if (stencil != currentClearStencil)
				{
					glClearStencil(stencil);
					currentClearStencil = stencil;
				}
			}

			// CLEAR!
			glClear(clearMask);

			// Clean up after ourselves.
			if (scissorTestEnable)
			{
				glEnable(GLenum.GL_SCISSOR_TEST);
			}
			if (!zWriteEnable)
			{
				glDepthMask(false);
			}
			if (stencilWriteMask != -1) // AKA 0xFFFFFFFF, ugh -flibit
			{
				glStencilMask(stencilWriteMask);
			}
		}

		#endregion

		#region SetRenderTargets Method

		public void SetRenderTargets(
			uint[] attachments,
			GLenum[] textureTargets,
			uint renderbuffer,
			DepthFormat depthFormat
		) {
			// Bind the right framebuffer, if needed
			if (attachments == null)
			{
				BindFramebuffer(Backbuffer.Handle);
				return;
			}
			else
			{
				BindFramebuffer(targetFramebuffer);
			}

			// Update the color attachments, DrawBuffers state
			int i = 0;
			for (i = 0; i < attachments.Length; i += 1)
			{
				if (	attachments[i] != currentAttachments[i] ||
					textureTargets[i] != currentAttachmentFaces[i]	)
				{
					glFramebufferTexture2D(
						GLenum.GL_FRAMEBUFFER,
						GLenum.GL_COLOR_ATTACHMENT0 + i,
						textureTargets[i],
						attachments[i],
						0
					);
					currentAttachments[i] = attachments[i];
					currentAttachmentFaces[i] = textureTargets[i];
				}
			}
			while (i < currentAttachments.Length)
			{
				if (currentAttachments[i] != 0)
				{
					glFramebufferTexture2D(
						GLenum.GL_FRAMEBUFFER,
						GLenum.GL_COLOR_ATTACHMENT0 + i,
						GLenum.GL_TEXTURE_2D,
						0,
						0
					);
					currentAttachments[i] = 0;
					currentAttachmentFaces[i] = GLenum.GL_TEXTURE_2D;
				}
				i += 1;
			}
			if (attachments.Length != currentDrawBuffers)
			{
				glDrawBuffers((IntPtr) attachments.Length, drawBuffersArray);
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
					glFramebufferRenderbuffer(
						GLenum.GL_FRAMEBUFFER,
						GLenum.GL_STENCIL_ATTACHMENT,
						GLenum.GL_RENDERBUFFER,
						renderbuffer
					);
				}
				currentDepthStencilFormat = depthFormat;
				glFramebufferRenderbuffer(
					GLenum.GL_FRAMEBUFFER,
					GLenum.GL_DEPTH_ATTACHMENT,
					GLenum.GL_RENDERBUFFER,
					renderbuffer
				);
				if (currentDepthStencilFormat == DepthFormat.Depth24Stencil8)
				{
					glFramebufferRenderbuffer(
						GLenum.GL_FRAMEBUFFER,
						GLenum.GL_STENCIL_ATTACHMENT,
						GLenum.GL_RENDERBUFFER,
						renderbuffer
					);
				}
				currentRenderbuffer = renderbuffer;
			}
		}

		#endregion

		#region XNA->GL Enum Conversion Class

		private static class XNAToGL
		{
			/* Ideally we would be using arrays, rather than Dictionaries.
			 * The problem is that we don't support every enum, and dealing
			 * with gaps would be a headache. So whatever, Dictionaries!
			 * -flibit
			 */

			public static readonly Dictionary<Type, GLenum> TextureType = new Dictionary<Type, GLenum>()
			{
				{ typeof(Texture2D), GLenum.GL_TEXTURE_2D },
				{ typeof(Texture3D), GLenum.GL_TEXTURE_3D },
				{ typeof(TextureCube), GLenum.GL_TEXTURE_CUBE_MAP }
			};

			public static readonly Dictionary<Blend, GLenum> BlendMode = new Dictionary<Blend, GLenum>()
			{
				{ Blend.DestinationAlpha,		GLenum.GL_DST_ALPHA },
				{ Blend.DestinationColor,		GLenum.GL_DST_COLOR },
				{ Blend.InverseDestinationAlpha,	GLenum.GL_ONE_MINUS_DST_ALPHA },
				{ Blend.InverseDestinationColor,	GLenum.GL_ONE_MINUS_DST_COLOR },
				{ Blend.InverseSourceAlpha,		GLenum.GL_ONE_MINUS_SRC_ALPHA },
				{ Blend.InverseSourceColor,		GLenum.GL_ONE_MINUS_SRC_COLOR },
				{ Blend.One,				GLenum.GL_ONE },
				{ Blend.SourceAlpha,			GLenum.GL_SRC_ALPHA },
				{ Blend.SourceAlphaSaturation,		GLenum.GL_SRC_ALPHA_SATURATE },
				{ Blend.SourceColor,			GLenum.GL_SRC_COLOR },
				{ Blend.Zero,				GLenum.GL_ZERO }
			};

			public static readonly Dictionary<BlendFunction, GLenum> BlendEquation = new Dictionary<BlendFunction, GLenum>()
			{
				{ BlendFunction.Add,			GLenum.GL_FUNC_ADD },
				{ BlendFunction.Max,			GLenum.GL_MAX },
				{ BlendFunction.Min,			GLenum.GL_MIN },
				{ BlendFunction.ReverseSubtract,	GLenum.GL_FUNC_REVERSE_SUBTRACT },
				{ BlendFunction.Subtract,		GLenum.GL_FUNC_SUBTRACT }
			};

			public static readonly Dictionary<CompareFunction, GLenum> CompareFunc = new Dictionary<CompareFunction, GLenum>()
			{
				{ CompareFunction.Always,	GLenum.GL_ALWAYS },
				{ CompareFunction.Equal,	GLenum.GL_EQUAL },
				{ CompareFunction.Greater,	GLenum.GL_GREATER },
				{ CompareFunction.GreaterEqual,	GLenum.GL_GEQUAL },
				{ CompareFunction.Less,		GLenum.GL_LESS },
				{ CompareFunction.LessEqual,	GLenum.GL_LEQUAL },
				{ CompareFunction.Never,	GLenum.GL_NEVER },
				{ CompareFunction.NotEqual,	GLenum.GL_NOTEQUAL }
			};

			public static readonly Dictionary<StencilOperation, GLenum> GLStencilOp = new Dictionary<StencilOperation, GLenum>()
			{
				{ StencilOperation.Decrement,		GLenum.GL_DECR_WRAP },
				{ StencilOperation.DecrementSaturation,	GLenum.GL_DECR },
				{ StencilOperation.Increment,		GLenum.GL_INCR_WRAP },
				{ StencilOperation.IncrementSaturation,	GLenum.GL_INCR },
				{ StencilOperation.Invert,		GLenum.GL_INVERT },
				{ StencilOperation.Keep,		GLenum.GL_KEEP },
				{ StencilOperation.Replace,		GLenum.GL_REPLACE },
				{ StencilOperation.Zero,		GLenum.GL_ZERO }
			};

			public static readonly Dictionary<CullMode, GLenum> FrontFace = new Dictionary<CullMode, GLenum>()
			{
				{ CullMode.CullClockwiseFace,		GLenum.GL_CW },
				{ CullMode.CullCounterClockwiseFace,	GLenum.GL_CCW }
			};

			public static readonly Dictionary<FillMode, GLenum> GLFillMode = new Dictionary<FillMode, GLenum>()
			{
				{ FillMode.Solid,	GLenum.GL_FILL },
				{ FillMode.WireFrame,	GLenum.GL_LINE }
			};

			public static readonly Dictionary<TextureAddressMode, GLenum> Wrap = new Dictionary<TextureAddressMode, GLenum>()
			{
				{ TextureAddressMode.Clamp,	GLenum.GL_CLAMP_TO_EDGE },
				{ TextureAddressMode.Mirror,	GLenum.GL_MIRRORED_REPEAT },
				{ TextureAddressMode.Wrap,	GLenum.GL_REPEAT }
			};

			public static readonly Dictionary<TextureFilter, GLenum> MagFilter = new Dictionary<TextureFilter, GLenum>()
			{
				{ TextureFilter.Point,				GLenum.GL_NEAREST },
				{ TextureFilter.Linear,				GLenum.GL_LINEAR },
				{ TextureFilter.Anisotropic,			GLenum.GL_LINEAR },
				{ TextureFilter.LinearMipPoint,			GLenum.GL_LINEAR },
				{ TextureFilter.MinPointMagLinearMipPoint,	GLenum.GL_LINEAR },
				{ TextureFilter.MinPointMagLinearMipLinear,	GLenum.GL_LINEAR },
				{ TextureFilter.MinLinearMagPointMipPoint,	GLenum.GL_NEAREST },
				{ TextureFilter.MinLinearMagPointMipLinear,	GLenum.GL_NEAREST }
			};

			public static readonly Dictionary<TextureFilter, GLenum> MinMipFilter = new Dictionary<TextureFilter, GLenum>()
			{
				{ TextureFilter.Point,				GLenum.GL_NEAREST_MIPMAP_NEAREST },
				{ TextureFilter.Linear,				GLenum.GL_LINEAR_MIPMAP_LINEAR },
				{ TextureFilter.Anisotropic,			GLenum.GL_LINEAR_MIPMAP_LINEAR },
				{ TextureFilter.LinearMipPoint,			GLenum.GL_LINEAR_MIPMAP_NEAREST },
				{ TextureFilter.MinPointMagLinearMipPoint,	GLenum.GL_NEAREST_MIPMAP_NEAREST },
				{ TextureFilter.MinPointMagLinearMipLinear,	GLenum.GL_NEAREST_MIPMAP_LINEAR },
				{ TextureFilter.MinLinearMagPointMipPoint,	GLenum.GL_LINEAR_MIPMAP_NEAREST },
				{ TextureFilter.MinLinearMagPointMipLinear,	GLenum.GL_LINEAR_MIPMAP_LINEAR }
			};

			public static readonly Dictionary<TextureFilter, GLenum> MinFilter = new Dictionary<TextureFilter, GLenum>()
			{
				{ TextureFilter.Point,				GLenum.GL_NEAREST },
				{ TextureFilter.Linear,				GLenum.GL_LINEAR },
				{ TextureFilter.Anisotropic,			GLenum.GL_LINEAR },
				{ TextureFilter.LinearMipPoint,			GLenum.GL_LINEAR },
				{ TextureFilter.MinPointMagLinearMipPoint,	GLenum.GL_NEAREST },
				{ TextureFilter.MinPointMagLinearMipLinear,	GLenum.GL_NEAREST },
				{ TextureFilter.MinLinearMagPointMipPoint,	GLenum.GL_LINEAR },
				{ TextureFilter.MinLinearMagPointMipLinear,	GLenum.GL_LINEAR }
			};

			public static readonly Dictionary<DepthFormat, GLenum> DepthStencilAttachment = new Dictionary<DepthFormat, GLenum>()
			{
				{ DepthFormat.Depth16,		GLenum.GL_DEPTH_ATTACHMENT },
				{ DepthFormat.Depth24,		GLenum.GL_DEPTH_ATTACHMENT },
				{ DepthFormat.Depth24Stencil8,	GLenum.GL_DEPTH_STENCIL_ATTACHMENT }
			};

			public static readonly Dictionary<DepthFormat, GLenum> DepthStorage = new Dictionary<DepthFormat, GLenum>()
			{
				{ DepthFormat.Depth16,		GLenum.GL_DEPTH_COMPONENT16 },
				{ DepthFormat.Depth24,		GLenum.GL_DEPTH_COMPONENT24 },
				{ DepthFormat.Depth24Stencil8,	GLenum.GL_DEPTH24_STENCIL8 }
			};

			public static readonly Dictionary<VertexElementFormat, GLenum> PointerType = new Dictionary<VertexElementFormat, GLenum>()
			{
				{ VertexElementFormat.Single,		GLenum.GL_FLOAT },
				{ VertexElementFormat.Vector2,		GLenum.GL_FLOAT },
				{ VertexElementFormat.Vector3,		GLenum.GL_FLOAT },
				{ VertexElementFormat.Vector4,		GLenum.GL_FLOAT },
				{ VertexElementFormat.Color,		GLenum.GL_UNSIGNED_BYTE },
				{ VertexElementFormat.Byte4,		GLenum.GL_UNSIGNED_BYTE },
				{ VertexElementFormat.Short2,		GLenum.GL_SHORT },
				{ VertexElementFormat.Short4,		GLenum.GL_SHORT },
				{ VertexElementFormat.NormalizedShort2,	GLenum.GL_SHORT },
				{ VertexElementFormat.NormalizedShort4,	GLenum.GL_SHORT },
				{ VertexElementFormat.HalfVector2,	GLenum.GL_HALF_FLOAT },
				{ VertexElementFormat.HalfVector4,	GLenum.GL_HALF_FLOAT }
			};
		}

		#endregion

		#region The Faux-Backbuffer

		public class FauxBackbuffer
		{
			public uint Handle
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

			private uint colorAttachment;
			private uint depthStencilAttachment;
			private DepthFormat depthStencilFormat;
			private OpenGLDevice glDevice;

			public FauxBackbuffer(
				OpenGLDevice device,
				int width,
				int height,
				DepthFormat depthFormat
			) {
#if DISABLE_FAUXBACKBUFFER
				Handle = 0;
				Width = width;
				Height = height;
#else
				glDevice = device;
				uint handle;
				glDevice.glGenFramebuffers((IntPtr) 1, out handle);
				Handle = handle;
				glDevice.glGenTextures((IntPtr) 1, out colorAttachment);
				glDevice.glGenTextures((IntPtr) 1, out depthStencilAttachment);

				glDevice.glBindTexture(GLenum.GL_TEXTURE_2D, colorAttachment);
				glDevice.glTexImage2D(
					GLenum.GL_TEXTURE_2D,
					0,
					(int) GLenum.GL_RGBA,
					(IntPtr) width,
					(IntPtr) height,
					0,
					GLenum.GL_RGBA,
					GLenum.GL_UNSIGNED_BYTE,
					IntPtr.Zero
				);
				glDevice.glBindTexture(GLenum.GL_TEXTURE_2D, depthStencilAttachment);
				glDevice.glTexImage2D(
					GLenum.GL_TEXTURE_2D,
					0,
					(int) GLenum.GL_DEPTH_COMPONENT16,
					(IntPtr) width,
					(IntPtr) height,
					0,
					GLenum.GL_DEPTH_COMPONENT,
					GLenum.GL_UNSIGNED_BYTE,
					IntPtr.Zero
				);
				glDevice.glBindFramebuffer(
					GLenum.GL_FRAMEBUFFER,
					Handle
				);
				glDevice.glFramebufferTexture2D(
					GLenum.GL_FRAMEBUFFER,
					GLenum.GL_COLOR_ATTACHMENT0,
					GLenum.GL_TEXTURE_2D,
					colorAttachment,
					0
				);
				glDevice.glFramebufferTexture2D(
					GLenum.GL_FRAMEBUFFER,
					GLenum.GL_DEPTH_ATTACHMENT,
					GLenum.GL_TEXTURE_2D,
					depthStencilAttachment,
					0
				);
				glDevice.glBindTexture(GLenum.GL_TEXTURE_2D, 0);

				Width = width;
				Height = height;
#endif
			}

			public void Dispose()
			{
#if !DISABLE_FAUXBACKBUFFER
				uint handle = Handle;
				glDevice.glDeleteFramebuffers((IntPtr) 1, ref handle);
				glDevice.glDeleteTextures((IntPtr) 1, ref colorAttachment);
				glDevice.glDeleteTextures((IntPtr) 1, ref depthStencilAttachment);
				glDevice = null;
				Handle = 0;
#endif
			}

			public void ResetFramebuffer(
				GraphicsDevice graphicsDevice,
				int width,
				int height,
				DepthFormat depthFormat
			) {
#if DISABLE_FAUXBACKBUFFER
				Width = width;
				Height = height;
#else
				// Update our color attachment to the new resolution.
				glDevice.glBindTexture(GLenum.GL_TEXTURE_2D, colorAttachment);
				glDevice.glTexImage2D(
					GLenum.GL_TEXTURE_2D,
					0,
					(int) GLenum.GL_RGBA,
					(IntPtr) width,
					(IntPtr) height,
					0,
					GLenum.GL_RGBA,
					GLenum.GL_UNSIGNED_BYTE,
					IntPtr.Zero
				);

				// Update the depth attachment based on the desired DepthFormat.
				GLenum depthPixelFormat;
				GLenum depthPixelInternalFormat;
				GLenum depthPixelType;
				GLenum depthAttachmentType;
				if (depthFormat == DepthFormat.Depth16)
				{
					depthPixelFormat = GLenum.GL_DEPTH_COMPONENT;
					depthPixelInternalFormat = GLenum.GL_DEPTH_COMPONENT16;
					depthPixelType = GLenum.GL_UNSIGNED_BYTE;
					depthAttachmentType = GLenum.GL_DEPTH_ATTACHMENT;
				}
				else if (depthFormat == DepthFormat.Depth24)
				{
					depthPixelFormat = GLenum.GL_DEPTH_COMPONENT;
					depthPixelInternalFormat = GLenum.GL_DEPTH_COMPONENT24;
					depthPixelType = GLenum.GL_UNSIGNED_BYTE;
					depthAttachmentType = GLenum.GL_DEPTH_ATTACHMENT;
				}
				else
				{
					depthPixelFormat = GLenum.GL_DEPTH_STENCIL;
					depthPixelInternalFormat = GLenum.GL_DEPTH24_STENCIL8;
					depthPixelType = GLenum.GL_UNSIGNED_INT_24_8;
					depthAttachmentType = GLenum.GL_DEPTH_STENCIL_ATTACHMENT;
				}

				glDevice.glBindTexture(GLenum.GL_TEXTURE_2D, depthStencilAttachment);
				glDevice.glTexImage2D(
					GLenum.GL_TEXTURE_2D,
					0,
					(int) depthPixelInternalFormat,
					(IntPtr) width,
					(IntPtr) height,
					0,
					depthPixelFormat,
					depthPixelType,
					IntPtr.Zero
				);

				// If the depth format changes, detach before reattaching!
				if (depthFormat != depthStencilFormat)
				{
					GLenum attach;
					if (depthStencilFormat == DepthFormat.Depth24Stencil8)
					{
						attach = GLenum.GL_DEPTH_STENCIL_ATTACHMENT;
					}
					else
					{
						attach = GLenum.GL_DEPTH_ATTACHMENT;
					}

					glDevice.glBindFramebuffer(
						GLenum.GL_FRAMEBUFFER,
						Handle
					);

					glDevice.glFramebufferTexture2D(
						GLenum.GL_FRAMEBUFFER,
						attach,
						GLenum.GL_TEXTURE_2D,
						0,
						0
					);
					glDevice.glFramebufferTexture2D(
						GLenum.GL_FRAMEBUFFER,
						depthAttachmentType,
						GLenum.GL_TEXTURE_2D,
						depthStencilAttachment,
						0
					);

					if (graphicsDevice.RenderTargetCount > 0)
					{
						glDevice.glBindFramebuffer(
							GLenum.GL_FRAMEBUFFER,
							graphicsDevice.GLDevice.targetFramebuffer
						);
					}

					depthStencilFormat = depthFormat;
				}

				glDevice.glBindTexture(
					GLenum.GL_TEXTURE_2D,
					graphicsDevice.GLDevice.Textures[0].Handle
				);

				Width = width;
				Height = height;
#endif
			}
		}

		#endregion

		#region Private OpenGL Entry Points

		public enum GLenum : int
		{
			// Hint Enum Value
			GL_DONT_CARE =				0x1100,
			// 0/1
			GL_ZERO =				0x0000,
			GL_ONE =				0x0001,
			// Types
			GL_BYTE =				0x1400,
			GL_UNSIGNED_BYTE =			0x1401,
			GL_SHORT =				0x1402,
			GL_UNSIGNED_SHORT =			0x1402,
			GL_FLOAT =				0x1406,
			GL_HALF_FLOAT =				0x140B,
			GL_UNSIGNED_SHORT_4_4_4_4 =		0x8033,
			GL_UNSIGNED_SHORT_5_5_5_1 =		0x8034,
			GL_UNSIGNED_INT_10_10_10_2 =		0x8036,
			GL_UNSIGNED_SHORT_5_6_5 =		0x8363,
			GL_UNSIGNED_INT_24_8 =			0x84FA,
			// Strings
			GL_VENDOR =				0x1F00,
			GL_RENDERER =				0x1F01,
			GL_VERSION =				0x1F02,
			GL_EXTENSIONS =				0x1F03,
			// Clear Mask
			GL_COLOR_BUFFER_BIT =			0x4000,
			GL_DEPTH_BUFFER_BIT =			0x0100,
			GL_STENCIL_BUFFER_BIT =			0x0400,
			// Enable Caps
			GL_SCISSOR_TEST =			0x0C10,
			GL_DEPTH_TEST =				0x0B71,
			GL_STENCIL_TEST =			0x0B90,
			// Polygons
			GL_LINE =				0x1B01,
			GL_FILL =				0x1B02,
			GL_CW =					0x0900,
			GL_CCW =				0x0901,
			GL_FRONT =				0x0404,
			GL_BACK =				0x0405,
			GL_FRONT_AND_BACK =			0x0408,
			GL_CULL_FACE =				0x0B44,
			GL_POLYGON_OFFSET_FILL =		0x8037,
			// Texture Type
			GL_TEXTURE_2D =				0x0DE1,
			GL_TEXTURE_3D =				0x806F,
			GL_TEXTURE_CUBE_MAP =			0x8513,
			GL_TEXTURE_CUBE_MAP_POSITIVE_X =	0x8515,
			// Blend Mode
			GL_BLEND =				0x0BE2,
			GL_SRC_COLOR =				0x0300,
			GL_ONE_MINUS_SRC_COLOR =		0x0301,
			GL_SRC_ALPHA =				0x0302,
			GL_ONE_MINUS_SRC_ALPHA =		0x0303,
			GL_DST_ALPHA =				0x0304,
			GL_ONE_MINUS_DST_ALPHA =		0x0305,
			GL_DST_COLOR =				0x0306,
			GL_ONE_MINUS_DST_COLOR =		0x0307,
			GL_SRC_ALPHA_SATURATE =			0x0308,
			// Equations
			GL_MIN =				0x8007,
			GL_MAX =				0x8008,
			GL_FUNC_ADD =				0x8006,
			GL_FUNC_SUBTRACT =			0x800A,
			GL_FUNC_REVERSE_SUBTRACT =		0x800B,
			// Comparisons
			GL_NEVER =				0x0200,
			GL_LESS =				0x0201,
			GL_EQUAL =				0x0202,
			GL_LEQUAL =				0x0203,
			GL_GREATER =				0x0204,
			GL_NOTEQUAL =				0x0205,
			GL_GEQUAL =				0x0206,
			GL_ALWAYS =				0x0207,
			// Stencil Operations
			GL_INVERT =				0x150A,
			GL_KEEP =				0x1E00,
			GL_REPLACE =				0x1E01,
			GL_INCR =				0x1E02,
			GL_DECR =				0x1E03,
			GL_INCR_WRAP =				0x8507,
			GL_DECR_WRAP =				0x8508,
			// Wrap Modes
			GL_REPEAT =				0x2901,
			GL_CLAMP_TO_EDGE =			0x812F,
			GL_MIRRORED_REPEAT =			0x8370,
			// Filters
			GL_NEAREST =				0x2600,
			GL_LINEAR =				0x2601,
			GL_NEAREST_MIPMAP_NEAREST =		0x2700,
			GL_NEAREST_MIPMAP_LINEAR =		0x2702,
			GL_LINEAR_MIPMAP_NEAREST =		0x2701,
			GL_LINEAR_MIPMAP_LINEAR =		0x2702,
			// Attachments
			GL_COLOR_ATTACHMENT0 =			0x8CE0,
			GL_DEPTH_ATTACHMENT =			0x8D00,
			GL_STENCIL_ATTACHMENT =			0x8D20,
			GL_DEPTH_STENCIL_ATTACHMENT =		0x821A,
			// Texture Formats
			GL_RED =				0x1903,
			GL_RGB =				0x1907,
			GL_RGBA =				0x1908,
			GL_LUMINANCE =				0x1909,
			GL_RGBA4 =				0x8056,
			GL_RGB10_A2_EXT =			0x8059,
			GL_BGR =				0x80E0,
			GL_BGRA =				0x80E1,
			GL_DEPTH_COMPONENT16 =			0x81A5,
			GL_DEPTH_COMPONENT24 =			0x81A6,
			GL_RG =					0x8227,
			GL_R16F =				0x822D,
			GL_R32F =				0x822E,
			GL_RG16F =				0x822F,
			GL_RG32F =				0x8230,
			GL_RG8I =				0x8237,
			GL_RG16UI =				0x823A,
			GL_RGBA32F =				0x8814,
			GL_RGBA16F =				0x881A,
			GL_DEPTH24_STENCIL8 =			0x88F0,
			GL_RGBA16UI =				0x8D76,
			GL_RGBA8I =				0x8D8E,
			GL_COMPRESSED_TEXTURE_FORMATS =		0x86A3,
			GL_COMPRESSED_RGBA_S3TC_DXT1_EXT =	0x83F1,
			GL_COMPRESSED_RGBA_S3TC_DXT3_EXT =	0x83F2,
			GL_COMPRESSED_RGBA_S3TC_DXT5_EXT =	0x83F3,
			// Texture Internal Formats
			GL_DEPTH_COMPONENT =			0x1902,
			GL_DEPTH_STENCIL =			0x8F49,
			// Textures
			GL_TEXTURE_WRAP_S =			0x2802,
			GL_TEXTURE_WRAP_T =			0x2803,
			GL_TEXTURE_WRAP_R =			0x8072,
			GL_TEXTURE_MAG_FILTER =			0x2800,
			GL_TEXTURE_MIN_FILTER =			0x2801,
			GL_TEXTURE_MAX_ANISOTROPY_EXT =		0x84FE,
			GL_TEXTURE_BASE_LEVEL =			0x813C,
			GL_TEXTURE_MAX_LEVEL =			0x813D,
			GL_TEXTURE_LOD_BIAS =			0x8501,
			GL_UNPACK_ALIGNMENT =			0x0CF5,
			// Multitexture
			GL_TEXTURE0 =				0x84C0,
			GL_MAX_TEXTURE_UNITS =			0x84E2,
			// Texture Queries
			GL_TEXTURE_WIDTH =			0x1000,
			GL_TEXTURE_HEIGHT =			0x1001,
			// Buffer objects
			GL_ARRAY_BUFFER =			0x8892,
			GL_ELEMENT_ARRAY_BUFFER =		0x8893,
			GL_STREAM_DRAW =			0x88E0,
			GL_STATIC_DRAW =			0x88E4,
			GL_READ_ONLY =				0x88B8,
			GL_MAX_VERTEX_ATTRIBS =			0x8869,
			// Render targets
			GL_FRAMEBUFFER =			0x8D40,
			GL_READ_FRAMEBUFFER =			0x8CA8,
			GL_DRAW_FRAMEBUFFER =			0x8CA9,
			GL_RENDERBUFFER =			0x8D41,
			GL_MAX_DRAW_BUFFERS =			0x8824,
			// Draw Primitives
			GL_TRIANGLE_STRIP =			0x0005,
			// Query Objects
			GL_QUERY_RESULT =			0x8866,
			GL_QUERY_RESULT_AVAILABLE =		0x8867,
			GL_SAMPLES_PASSED =			0x8914,
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
			// Stupid dumk stuff that's stupid
			GL_CURRENT_PROGRAM =			0x8B8D,
			GL_FRAGMENT_SHADER =			0x8B30,
			GL_VERTEX_SHADER =			0x8B31
		}

		// Entry Points

		/* BEGIN GET FUNCTIONS */

		private delegate string GetString(GLenum pname);
		private GetString glGetString;

		public delegate void GetIntegerv(GLenum pname, out int param);
		public GetIntegerv glGetIntegerv;

		/* END GET FUNCTIONS */

		/* BEGIN ENABLE/DISABLE FUNCTIONS */

		public delegate void Enable(GLenum cap);
		public Enable glEnable;

		public delegate void Disable(GLenum cap);
		public Disable glDisable;

		/* END ENABLE/DISABLE FUNCTIONS */

		/* BEGIN VIEWPORT/SCISSOR FUNCTIONS */

		public delegate void G_Viewport(
			int x,
			int y,
			IntPtr width,
			IntPtr height
		);
		public G_Viewport glViewport;

		private delegate void DepthRange(
			double near_val,
			double far_val
		);
		private DepthRange glDepthRange;

		private delegate void Scissor(
			int x,
			int y,
			IntPtr width,
			IntPtr height
		);
		private Scissor glScissor;

		/* END VIEWPORT/SCISSOR FUNCTIONS */

		/* BEGIN BLEND STATE FUNCTIONS */

		private delegate void BlendColor(
			double red,
			double green,
			double blue,
			double alpha
		);
		private BlendColor glBlendColor;

		private delegate void BlendFuncSeparate(
			GLenum srcRGB,
			GLenum dstRGB,
			GLenum srcAlpha,
			GLenum dstAlpha
		);
		private BlendFuncSeparate glBlendFuncSeparate;

		private delegate void BlendEquationSeparate(
			GLenum modeRGB,
			GLenum modeAlpha
		);
		private BlendEquationSeparate glBlendEquationSeparate;

		private delegate void ColorMask(
			bool red,
			bool green,
			bool blue,
			bool alpha
		);
		private ColorMask glColorMask;

		/* END BLEND STATE FUNCTIONS */

		/* BEGIN DEPTH/STENCIL STATE FUNCTIONS */

		private delegate void DepthMask(bool flag);
		private DepthMask glDepthMask;

		private delegate void DepthFunc(GLenum func);
		private DepthFunc glDepthFunc;

		private delegate void StencilMask(int mask);
		private StencilMask glStencilMask;

		private delegate void StencilFuncSeparate(
			GLenum face,
			GLenum func,
			int reference,
			int mask
		);
		private StencilFuncSeparate glStencilFuncSeparate;

		private delegate void StencilOpSeparate(
			GLenum face,
			GLenum sfail,
			GLenum dpfail,
			GLenum dppass
		);
		private StencilOpSeparate glStencilOpSeparate;

		private delegate void StencilFunc(
			GLenum fail,
			int reference,
			int mask
		);
		private StencilFunc glStencilFunc;

		private delegate void StencilOp(
			GLenum fail,
			GLenum zfail,
			GLenum zpass
		);
		private StencilOp glStencilOp;

		/* END DEPTH/STENCIL STATE FUNCTIONS */

		/* BEGIN RASTERIZER STATE FUNCTIONS */

		private delegate void CullFace(GLenum mode);
		private CullFace glCullFace;

		private delegate void FrontFace(GLenum mode);
		private FrontFace glFrontFace;

		private delegate void PolygonMode(GLenum face, GLenum mode);
		private PolygonMode glPolygonMode;

		private delegate void PolygonOffset(float factor, float units);
		private PolygonOffset glPolygonOffset;

		/* END RASTERIZER STATE FUNCTIONS */

		/* BEGIN TEXTURE FUNCTIONS */

		public delegate void GenTextures(IntPtr n, out uint textures);
		public GenTextures glGenTextures;

		public delegate void DeleteTextures(
			IntPtr n,
			ref uint textures
		);
		public DeleteTextures glDeleteTextures;

		public delegate void G_BindTexture(GLenum target, uint texture);
		public G_BindTexture glBindTexture;

		public delegate void TexImage2D(
			GLenum target,
			int level,
			int internalFormat,
			IntPtr width,
			IntPtr height,
			int border,
			GLenum format,
			GLenum type,
			IntPtr pixels
		);
		public TexImage2D glTexImage2D;

		public delegate void TexSubImage2D(
			GLenum target,
			int level,
			int xoffset,
			int yoffset,
			IntPtr width,
			IntPtr height,
			GLenum format,
			GLenum type,
			IntPtr pixels
		);
		public TexSubImage2D glTexSubImage2D;

		public delegate void CompressedTexImage2D(
			GLenum target,
			int level,
			int internalFormat,
			IntPtr width,
			IntPtr height,
			int border,
			IntPtr imageSize,
			IntPtr pixels
		);
		public CompressedTexImage2D glCompressedTexImage2D;

		public delegate void CompressedTexSubImage2D(
			GLenum target,
			int level,
			int xoffset,
			int yoffset,
			IntPtr width,
			IntPtr height,
			GLenum format,
			IntPtr imageSize,
			IntPtr pixels
		);
		public CompressedTexSubImage2D glCompressedTexSubImage2D;

		public delegate void TexImage3D(
			GLenum target,
			int level,
			int internalFormat,
			IntPtr width,
			IntPtr height,
			IntPtr depth,
			int border,
			GLenum format,
			GLenum type,
			IntPtr pixels
		);
		public TexImage3D glTexImage3D;

		public delegate void TexSubImage3D(
			GLenum target,
			int level,
			int xoffset,
			int yoffset,
			int zoffset,
			IntPtr width,
			IntPtr height,
			IntPtr depth,
			GLenum format,
			GLenum type,
			IntPtr pixels
		);
		public TexSubImage3D glTexSubImage3D;

		public delegate void GetTexImage(
			GLenum target,
			int level,
			GLenum format,
			GLenum type,
			IntPtr pixels
		);
		public GetTexImage glGetTexImage;

		public delegate void TexParameteri(
			GLenum target,
			GLenum pname,
			int param
		);
		public TexParameteri glTexParameteri;

		private delegate void TexParameterf(
			GLenum target,
			GLenum pname,
			float param
		);
		private TexParameterf glTexParameterf;

		public delegate void ActiveTexture(GLenum texture);
		public ActiveTexture glActiveTexture;

		private delegate void GetTexLevelParameteriv(
			GLenum target,
			int level,
			GLenum pname,
			out int param
		);
		private GetTexLevelParameteriv glGetTexLevelParameteriv;

		public delegate void PixelStorei(GLenum pname, int param);
		public PixelStorei glPixelStorei;

		/* END TEXTURE FUNCTIONS */

		/* BEGIN BUFFER FUNCTIONS */

		public delegate void GenBuffers(IntPtr n, out uint buffers);
		public GenBuffers glGenBuffers;

		private delegate void DeleteBuffers(
			IntPtr n,
			ref uint buffers
		);
		private DeleteBuffers glDeleteBuffers;

		private delegate void BindBuffer(GLenum target, uint buffer);
		private BindBuffer glBindBuffer;

		public delegate void BufferData(
			GLenum target,
			IntPtr size,
			IntPtr data,
			GLenum usage
		);
		public BufferData glBufferData;

		private delegate void BufferSubData(
			GLenum target,
			IntPtr offset,
			IntPtr size,
			IntPtr data
		);
		private BufferSubData glBufferSubData;

		private delegate IntPtr MapBuffer(GLenum target, GLenum access);
		private MapBuffer glMapBuffer;

		private delegate void UnmapBuffer(GLenum target);
		private UnmapBuffer glUnmapBuffer;

		/* END BUFFER FUNCTIONS */

		/* BEGIN VERTEX ATTRIBUTE FUNCTIONS */

		private delegate void EnableVertexAttribArray(int index);
		private EnableVertexAttribArray glEnableVertexAttribArray;

		private delegate void DisableVertexAttribArray(int index);
		private DisableVertexAttribArray glDisableVertexAttribArray;

		private delegate void VertexAttribDivisor(
			int index,
			int divisor
		);
		private VertexAttribDivisor glVertexAttribDivisor;

		private delegate void G_VertexAttribPointer(
			int index,
			int size,
			GLenum type,
			bool normalized,
			IntPtr stride,
			IntPtr pointer
		);
		private G_VertexAttribPointer glVertexAttribPointer;

		/* END VERTEX ATTRIBUTE FUNCTIONS */

		/* BEGIN CLEAR FUNCTIONS */

		private delegate void ClearColor(
			double red,
			double green,
			double blue,
			double alpha
		);
		private ClearColor glClearColor;

		private delegate void ClearDepth(double depth);
		private ClearDepth glClearDepth;

		private delegate void ClearStencil(int s);
		private ClearStencil glClearStencil;

		private delegate void G_Clear(GLenum mask);
		private G_Clear glClear;

		/* END CLEAR FUNCTIONS */

		/* BEGIN FRAMEBUFFER FUNCTIONS */

		private delegate void DrawBuffers(IntPtr n, GLenum[] bufs);
		private DrawBuffers glDrawBuffers;

		private delegate void ReadPixels(
			int x,
			int y,
			IntPtr width,
			IntPtr height,
			GLenum format,
			GLenum type,
			IntPtr pixels
		);
		private ReadPixels glReadPixels;

		public delegate void GenFramebuffers(
			IntPtr n,
			out uint framebuffers
		);
		public GenFramebuffers glGenFramebuffers;

		public delegate void DeleteFramebuffers(
			IntPtr n,
			ref uint framebuffers
		);
		public DeleteFramebuffers glDeleteFramebuffers;

		public delegate void G_BindFramebuffer(
			GLenum target,
			uint framebuffer
		);
		public G_BindFramebuffer glBindFramebuffer;

		public delegate void FramebufferTexture2D(
			GLenum target,
			GLenum attachment,
			GLenum textarget,
			uint texture,
			int level
		);
		public FramebufferTexture2D glFramebufferTexture2D;

		public delegate void FramebufferRenderbuffer(
			GLenum target,
			GLenum attachment,
			GLenum renderbuffertarget,
			uint renderbuffer
		);
		public FramebufferRenderbuffer glFramebufferRenderbuffer;

		public delegate void BlitFramebuffer(
			int srcX0,
			int srcY0,
			int srcX1,
			int srcY1,
			int dstX0,
			int dstY0,
			int dstX1,
			int dstY1,
			GLenum mask,
			GLenum filter
		);
		public BlitFramebuffer glBlitFramebuffer;

		public delegate void GenRenderbuffers(
			IntPtr n,
			out uint renderbuffers
		);
		public GenRenderbuffers glGenRenderbuffers;

		public delegate void DeleteRenderbuffers(
			IntPtr n,
			ref uint renderbuffers
		);
		public DeleteRenderbuffers glDeleteRenderbuffers;

		public delegate void BindRenderbuffer(
			GLenum target,
			uint renderbuffer
		);
		public BindRenderbuffer glBindRenderbuffer;

		public delegate void RenderbufferStorage(
			GLenum target,
			GLenum internalformat,
			IntPtr width,
			IntPtr height
		);
		public RenderbufferStorage glRenderbufferStorage;

		/* END FRAMEBUFFER FUNCTIONS */

		/* BEGIN DRAWING FUNCTIONS */

		public delegate void DrawArrays(
			GLenum mode,
			int first,
			IntPtr count
		);
		public DrawArrays glDrawArrays;

		/* END DRAWING FUNCTIONS */

		/* BEGIN QUERY FUNCTIONS */

		public delegate void GenQueries(IntPtr n, out uint ids);
		public GenQueries glGenQueries;

		public delegate void DeleteQueries(IntPtr n, ref uint ids);
		public DeleteQueries glDeleteQueries;

		public delegate void BeginQuery(GLenum target, uint id);
		public BeginQuery glBeginQuery;

		public delegate void EndQuery(GLenum target);
		public EndQuery glEndQuery;

		public delegate void GetQueryObjectiv(
			uint id,
			GLenum pname,
			out int param
		);
		public GetQueryObjectiv glGetQueryObjectiv;

		/* END QUERY FUNCTIONS */

		/* BEGIN SHADER FUNCTIONS */

		public delegate uint CreateShader(GLenum type);
		public CreateShader glCreateShader;

		public delegate void DeleteShader(uint shader);
		public DeleteShader glDeleteShader;

		public delegate void ShaderSource(
			uint shader,
			IntPtr count,
			ref string source,
			ref int length
		);
		public ShaderSource glShaderSource;

		public delegate void CompileShader(uint shader);
		public CompileShader glCompileShader;

		public delegate uint CreateProgram();
		public CreateProgram glCreateProgram;

		public delegate void DeleteProgram(uint program);
		public DeleteProgram glDeleteProgram;

		public delegate void AttachShader(uint program, uint shader);
		public AttachShader glAttachShader;

		public delegate void LinkProgram(uint program);
		public LinkProgram glLinkProgram;

		public delegate void UseProgram(uint program);
		public UseProgram glUseProgram;

		public delegate void Uniform1i(int location, int v0);
		public Uniform1i glUniform1i;

		public delegate int GetUniformLocation(
			uint program,
			string name
		);
		public GetUniformLocation glGetUniformLocation;

		public delegate void BindAttribLocation(
			uint program,
			uint index,
			string name
		);
		public BindAttribLocation glBindAttribLocation;

		/* END SHADER FUNCTIONS */

		/* BEGIN STUPID THREADED GL FUNCTIONS */

		public delegate void Flush();
		public Flush glFlush;

		/* END STUPID THREADED GL FUNCTIONS */

		/* BEGIN DEBUG OUTPUT FUNCTIONS */

		private delegate void DebugMessageCallback(
			DebugProc callback,
			IntPtr userParam
		);
		private DebugMessageCallback glDebugMessageCallbackARB;

		private delegate void DebugMessageControl(
			GLenum source,
			GLenum type,
			GLenum severity,
			IntPtr count, // GLsizei
			IntPtr ids, // const GLuint*
			bool enabled
		);
		private DebugMessageControl glDebugMessageControlARB;

		// ARB_debug_output callback
		private delegate void DebugProc(
			GLenum source,
			GLenum type,
			uint id,
			GLenum severity,
			IntPtr length, // GLsizei
			IntPtr message, // const GLchar*
			IntPtr userParam // const GLvoid*
		);
		private DebugProc DebugCall = DebugCallback;
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

		/* END DEBUG OUTPUT FUNCTIONS */

		public void LoadGLEntryPoints()
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

		#endregion
	}
}
