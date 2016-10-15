/*
 * Author: Viacheslav Soroka
 * 
 * This file is part of IGE <https://github.com/destrofer/IGE>.
 * 
 * IGE is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * IGE is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public License
 * along with IGE.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Drawing;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;

using IGE.Input;
using IGE.Graphics;
using IGE.Graphics.OpenGL;
using IGE.Platform;

namespace IGE {
	/// <summary>
	/// Main class used to open game window and startup the game itself.
	/// </summary>
	public static partial class GameEngine {
		public static event LoadEventHandler LoadEvent;
		public static event LoadEventHandler ConfigureEvent;
		public static event UnloadEventHandler UnloadEvent;
		public static event HandlePhysicsEventHandler HandlePhysicsEvent;
		public static event HandleInputEventHandler HandleInputEvent;
		public static event RendererEventHandler RenderEvent;
		public static event ResizeEventHandler ResizeEvent;

		public static bool ExitGame = false;
		
		// Game window related
		private static IOpenGLWindow m_Window = null;
		public static IOpenGLWindow Window { get { return m_Window; } }
		
		private static IKeyboardDevice m_Keyboard;
		public static IKeyboardDevice Keyboard { get { return m_Keyboard; } set { m_Keyboard = value; } }
		
		public static IMouseDevice m_Mouse;
		public static IMouseDevice Mouse { get { return m_Mouse; } set { m_Mouse = value; } }
		
		private static readonly Thread m_MainThread;
		
		// It is recommended to use only en-US culture, since it is affects how digits are parsed in XML files and other places.
		public static readonly CultureInfo DefaultCulture = new CultureInfo("en-US");
		private static CultureInfo m_CurrentCulture;
		public static CultureInfo CurrentCulture {
			get { return m_CurrentCulture; }
			set { m_MainThread.CurrentCulture = m_CurrentCulture = value; }
		}
		
		static GameEngine() {
			// Thread, that accesses GameEngine first is considered to be main. If that is a background thread,
			// then, once it ends, GameEngine will not be pointing to a correct thread, which might end in
			// unpredictable results.
			m_MainThread = Thread.CurrentThread;
			CurrentCulture = DefaultCulture;
		}

		public static void Exit() {
			Application.Exit();
		}
		
		public static void Run() {
			int initialVSyncSetting = 0;
			try {
				initialVSyncSetting = GL.GetBufferSwapInterval();
				GameConfig.Load();

				GameDebugger.EngineLog(LogLevel.Debug, "Running application specific configuration event handlers");
				if( ConfigureEvent != null )
					ConfigureEvent();
				
				GameDebugger.EngineLog(LogLevel.Debug, "Opening display device '{0}'", GameConfig.DisplayDevice);
				IDisplayDevice cur_dev = API.GetDisplayDevice(GameConfig.DisplayDevice);
				// GameDebugger.Log("Primary display device {0}", cur_dev.ToString());
				if( GameConfig.DisplayDevice != null && !GameConfig.DisplayDevice.Equals(cur_dev.Id) )
					GameConfig.DisplayDevice = null; // needed to save a "primary display device" setting since the one in config was not found
				
				List<IMonitor> monitors = cur_dev.GetMonitors();
				if( monitors == null || monitors.Count == 0 )
					throw new UserFriendlyException("No monitors could be found on the selected device. Could there be a bug somewhere?", "Failed initializing graphics");
				IMonitor first_monitor = monitors[0];
				// foreach( Rectangle mon in monitors )
				//	GameDebugger.Log("\tMonitor at {0}", mon.ToString());
				
				// Thanks to someone in Microsoft determining current refresh rate and bits per pixel of a monitor is quite a pain in the ass
				int current_width = first_monitor.Width;
				int current_height = first_monitor.Height;
				int current_bpp = first_monitor.BitsPerPixel;
				int current_refresh_rate = first_monitor.RefreshRate;

				int cfg_width = ( GameConfig.ResX <= 0 || GameConfig.ResY <= 0 ) ? current_width : GameConfig.ResX;
				int cfg_height = ( GameConfig.ResX <= 0 || GameConfig.ResY <= 0 ) ? current_height : GameConfig.ResY;
				int cfg_bpp = ( GameConfig.BitsPerPixel <= 0 ) ? current_bpp : GameConfig.BitsPerPixel;
				int cfg_refresh_rate = ( GameConfig.RefreshRate <= 0 ) ? current_refresh_rate : GameConfig.RefreshRate;
				
				// GameDebugger.Log("Searching for {0}bit {1}x{2} @{3}", GameConfig.BitsPerPixel, GameConfig.ResX, GameConfig.ResY, GameConfig.RefreshRate);
				
				if( GameConfig.FullScreen ) {
					List<IDisplayMode> modes = cur_dev.GetModes();
					if( modes == null || modes.Count == 0 )
						throw new Exception(String.Format("Device {0} is invalid and has no modes suitable for graphics", cur_dev.ToString()));
					
					IDisplayMode selected_mode = null;
					foreach( IDisplayMode mode in modes ) {
						// GameDebugger.Log("\t{0}", mode.ToString()); // Uncomment this to log all supported video modes for the chosen display device
						if( cfg_width == mode.Width
						&& cfg_height == mode.Height
						&& cfg_bpp == mode.BitsPerPixel
						&& cfg_refresh_rate == mode.RefreshRate ) {
							selected_mode = mode;
							// GameDebugger.Log("Selected mode: {0}", selected_mode.ToString());
							//break;
						}
					}
					
					if( selected_mode == null ) {
						// in case the mode in the config is specified incorrectly we try to use current screen settings. also reset config so later it won't try this bothersome thing
						GameConfig.ResX = -1;
						GameConfig.ResY = -1;
						GameConfig.BitsPerPixel = -1;
						GameConfig.RefreshRate = -1;
						foreach( IDisplayMode mode in modes ) {
							//GameDebugger.Log("\t{0}", mode.ToString());
							if( current_width == mode.Width
							&& current_height == mode.Height
							&& current_bpp == mode.BitsPerPixel
							&& current_refresh_rate == mode.RefreshRate ) {
								selected_mode = mode;
								break;
							}
						}
					}
					
					if( selected_mode == null ) {
						// this should not happen but still ... if no current resolution we deperately search for any 1024x768x32 mode (should be present on all computers by now)
						foreach( IDisplayMode mode in modes ) {
							//GameDebugger.Log("\t{0}", mode.ToString());
							if( 1024 == mode.Width
							&& 768 == mode.Height
							&& 32 == mode.BitsPerPixel ) {
								selected_mode = mode;
								break;
							}
						}
					}
					
					// 
					if( selected_mode == null ) {
						// OMG this is totally fucked up! just pick out the first one :|
						selected_mode = modes[0];
					}
	
					if( selected_mode.Width != current_width
					|| selected_mode.Height != current_height
					|| selected_mode.BitsPerPixel != current_bpp
					|| selected_mode.RefreshRate != current_refresh_rate ) {
						// TODO: add support for non full screen modes
						if( selected_mode.Set(true) ) {
							current_width = selected_mode.Width;
							current_height = selected_mode.Height;
							current_bpp = selected_mode.BitsPerPixel;
							current_refresh_rate = selected_mode.RefreshRate;
						}
						
						// After changing resolution monitor positions will change so we have to read them again!
 						monitors = cur_dev.GetMonitors();
						if( monitors == null || monitors.Count == 0 )
							throw new Exception("No monitors could be found on the selected device. Could there be a bug somewhere?");
						first_monitor = monitors[0];
					}
				}
				else {
					Size2 size = API.AdjustWindowSize(cfg_width, cfg_height);
					current_width = size.Width;
					current_height = size.Height;
				}
				
				// GameDebugger.Log("{0},{1}-{2},{3}", first_monitor.X, first_monitor.Y, current_width, current_height);

				using(IOpenGLWindow window = GL.CreateWindow(null, first_monitor.X, first_monitor.Y, current_width, current_height)) {
					m_Window = window;
					window.BeforeRenderFrameEvent += OnBeforeFrame;
					window.RenderFrameEvent += OnFrame;
					window.AfterRenderFrameEvent += OnAfterFrame;
					window.ResizeEvent += OnResize;

					m_Keyboard = IL.FirstKeyboardDevice;
					
					Mouse = IL.FirstMouseDevice;
					Mouse.Window = window; // mouse must be attached to window before showing since it watches move and resize events
					Mouse.Visible = GameConfig.ShowHardwareCursor;
					Mouse.Clipped = GameConfig.ClipMouseCursor;

					window.Show();
					
					GL.SetBufferSwapInterval(GameConfig.VSync ? 1 : 0);
					GameConfig.VSyncChangedEvent += OnVSyncSettingChanged;
					
					OnLoad();
					if( LoadEvent != null )
						LoadEvent();
					// GameDebugger.Log("Running main loop at last");
					Application.Run();
					// GameDebugger.Log("ByeBye!");
				}
			}

			catch(Exception ex) {
				GameDebugger.EngineLog(LogLevel.Error, "IGE or application has crashed!");
				GameDebugger.EngineLog(LogLevel.Error, ex);
			}

			finally {
				try {
					CacheableObject.DisposeAll();
				}
				catch(Exception ex) {
					GameDebugger.EngineLog(LogLevel.Error, "IGE automatic resource unloading has crashed!");
					GameDebugger.EngineLog(LogLevel.Error, ex);
				}
				
				try {
					if( UnloadEvent != null )
						UnloadEvent();
				}
				catch(Exception ex) {
					GameDebugger.Log(LogLevel.Error, "Application has crashed on UnloadEvent!");
					GameDebugger.Log(LogLevel.Error, ex);
				}

				try {
					OnUnload();
				}
				catch(Exception ex) {
					GameDebugger.EngineLog(LogLevel.Error, "IGE final unloading has crashed!");
					GameDebugger.EngineLog(LogLevel.Error, ex);
				}

				try {
					GameConfig.Save();
				}
				catch(Exception ex) {
					GameDebugger.EngineLog(LogLevel.Error, "IGE could not automatically save the configuration!");
					GameDebugger.EngineLog(LogLevel.Error, ex);
				}
				
				try { GameConfig.VSyncChangedEvent -= OnVSyncSettingChanged; } catch {}
				
				GL.SetBufferSwapInterval(initialVSyncSetting);
				m_Window = null;
			}
		}
		
		private static void OnVSyncSettingChanged(bool vsync) {
			GL.SetBufferSwapInterval(vsync ? 1 : 0);
		}
		
		// initialization related
		public static Color4 BackgroundColor = new Color4(0.05f, 0.20f, 0.25f, 1.0f);
		
		public static void ApplyDefaultGraphicsSettings() {
			
			View.ResetView();
			
			GL.ClearColor(ref BackgroundColor);

			GL.ClearDepth(1.0);
			GL.DepthFunc(DepthFunction.Lequal);
		
			GL.ShadeModel(ShadeModel.Smooth);
		
			GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);
			
			GL.BlendFunc(BlendFuncSrc.SrcAlpha, BlendFuncDst.OneMinusSrcAlpha);
			
			GL.FrontFace(FrontFaceDirection.Ccw);
			GL.Enable(EnableCap.CullFace);
		
			GL.Enable(EnableCap.ColorMaterial);
			GL.ColorMaterial(FaceName.FrontAndBack, ColorMaterialMode.Diffuse);
			
			GL.Enable(EnableCap.Normalize);
			
			View.SetMode3D();
		}

		private static int m_FPSFrames = 0;
		private static float m_FPS = 0.0f;
		private static GameTimer m_FPSTimer = new GameTimer();
		
		/// <summary>
		/// FPS counting timer. Gets reset every second.
		/// </summary>
		public static double FPSTime { get { return m_FPSTimer.Time; } }

		/// <summary>
		/// Frames rendered per last second
		/// </summary>
		public static float FPS { get { return m_FPS; } }

		public static void OnLoad() {
			m_FPSTimer.Reset();
			ApplyDefaultGraphicsSettings();
		}
		
		public static void OnUnload() {
			
		}
		
		public static void OnBeforeFrame() {
			m_FPSTimer.OnFrame();
			m_FPSFrames++;
			if( m_FPSTimer.Time >= 1.0 ) {
				m_FPS = (float)m_FPSFrames / (float)m_FPSTimer.Time;
				m_FPSTimer.Reset();
				m_FPSFrames = 0;
			}
		}

		private static void OnFrame() {
			if( m_Window == null || m_Window.Disposed )
				return;
			if( HandlePhysicsEvent != null )
				HandlePhysicsEvent();
			if( HandleInputEvent != null )
				HandleInputEvent();
			if( RenderEvent != null )
				RenderEvent();
		}
		
		private static void OnAfterFrame() {
		
		}
		
		
		private static void OnResize(ResizeEventArgs args) {
			if( GameConfig.RecalcViewportOnResize )
				View.SetView(m_Window.GetClientRect());
			
			if( ResizeEvent != null )
				ResizeEvent(args);
		}
		
		public static void StartFrame() {
			GL.Clear(GameConfig.ClearEachFrame ? (ClearBufferBits.DepthBufferBit | ClearBufferBits.ColorBufferBit) : ClearBufferBits.DepthBufferBit);
			GL.LoadIdentity();
		}
		
		public static void EndFrame() {
			
		}
		
		#region Shared OpenGL resource threading
		
		public static Thread StartSharedResourceThread(ThreadStart threadFunc) {
			Thread thread = new Thread(SharedResourceThread);
			thread.Start(new SharedResourceThreadData {
				Context = Window.ResourceContext.CreateSharedContext(),
				ThreadFunc = threadFunc,
				ThreadParam = null,
			});
			return thread;
		}
		
		public static Thread StartSharedResourceThread(ParameterizedThreadStart threadFunc, object threadParam) {
			Thread thread = new Thread(SharedResourceThread);
			thread.Start(new SharedResourceThreadData {
				Context = Window.ResourceContext.CreateSharedContext(),
				ThreadFunc = threadFunc,
				ThreadParam = threadParam,
			});
			return thread;
		}
		
		private static void SharedResourceThread(object threadData) {
			SharedResourceThreadData data = (SharedResourceThreadData)threadData;
			data.Context.Activate();
			try {
				Thread.CurrentThread.CurrentCulture = CurrentCulture;
				if( data.ThreadFunc is ThreadStart )
					((ThreadStart)data.ThreadFunc).Invoke();
				else
					((ParameterizedThreadStart)data.ThreadFunc).Invoke(data.ThreadParam);
			}
			finally {
				GL.ResetContext();
				data.Context.Dispose();
			}
		}
		
		internal struct SharedResourceThreadData {
			public IResourceContext Context;
			public object ThreadFunc;
			public object ThreadParam;
		}

		#endregion Shared OpenGL resource threading

		/// <summary>
		/// DO NOT REMOVE THIS METHOD! IT IS NEEDED TO ENSURE THAT PLATFORM SPECIFIC
		/// LIBRARIES ARE ALSO COPIED TO THE PROJECT OUTPUT DIRECTORY ON PROJECT
		/// BUILD.
		/// 
		/// These libraries (drivers) are not copied since they are loaded into
		/// running assembly dinamically and their instances are created only via
		/// reflection. In other words there are no other direct references to main
		/// driver classes.
		/// </summary>
		private static void EnsureReferencesCopied() {
			Console.WriteLine("{0}", typeof(IGE.Platform.Unix.API).FullName);
			Console.WriteLine("{0}", typeof(IGE.Platform.Win32.API).FullName);
			Console.WriteLine("{0}", typeof(IGE.Platform.Win32.NativeInput).FullName);
			Console.WriteLine("{0}", typeof(IGE.Platform.Win32.NativeMultimedia).FullName);
			Console.WriteLine("{0}", typeof(IGE.Platform.Win32.OpenAL).FullName);
			Console.WriteLine("{0}", typeof(IGE.Platform.Win32.OpenGL).FullName);
		}
	}

	public delegate void LoadEventHandler();
	public delegate void UnloadEventHandler();
	public delegate void HandlePhysicsEventHandler();
	public delegate void HandleInputEventHandler();
	public delegate void RendererEventHandler();
}