﻿using ImGuiNET;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using static ImGuiNET.ImGui;

namespace zzre.imgui
{
    public class WindowContainer : BaseDisposable, IReadOnlyCollection<BaseWindow>
    {
        private GraphicsDevice Device { get; }
        private ResourceFactory Factory => Device.ResourceFactory;
        private List<BaseWindow> windows = new List<BaseWindow>();
        private List<Fence> onceFences = new List<Fence>();
        private CommandList commandList;
        private Fence fence;
        private IntPtr glyphRangePtr = IntPtr.Zero;

        public BaseWindow? FocusedWindow { get; private set; } = null;
        public int Count => windows.Count;
        public ImGuiRenderer ImGuiRenderer { get; }
        public MenuBar MenuBar { get; } = new MenuBar();
        private bool isInUpdateEnumeration = false;
        private Action onceAfterUpdate = () => { }; // used for deferred modification of windows list

        public WindowContainer(GraphicsDevice device)
        {
            Device = device;

            var fb = device.MainSwapchain.Framebuffer;
            ImGuiRenderer = new ImGuiRenderer(device, fb.OutputDescription, (int)fb.Width, (int)fb.Height);
            commandList = Factory.CreateCommandList();
            fence = Factory.CreateFence(true);

            LoadForkAwesomeFont();
            ImGuiRenderer.Start();
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            foreach (var window in this.ToArray())
                window.Dispose();
            ImGuiRenderer.Dispose();
            commandList.Dispose();
            fence.Dispose();
        }

        protected override void DisposeNative()
        {
            base.DisposeNative();
            if (glyphRangePtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(glyphRangePtr);
                glyphRangePtr = IntPtr.Zero;
            }
        }

        private void AddSafelyToWindows(BaseWindow window)
        {
            if (isInUpdateEnumeration)
                onceAfterUpdate += () => windows.Add(window);
            else
                windows.Add(window);
        }

        public Window NewWindow(string title = "Window")
        {
            var window = new Window(this, title);
            AddSafelyToWindows(window);
            return window;
        }

        public Modal NewModal(string title = "Modal")
        {
            var modal = new Modal(this, title);
            AddSafelyToWindows(modal);
            return modal;
        }

        public void Update(GameTime time, InputSnapshot input)
        {
            ImGuiRenderer.Update(time.Delta, input);

            var viewport = GetMainViewport();
            SetNextWindowPos(viewport.Pos);
            SetNextWindowSize(viewport.Size);
            SetNextWindowViewport(viewport.ID);
            PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
            PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            Begin("Master",
                ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoBringToFrontOnFocus |
                ImGuiWindowFlags.NoNavFocus |
                ImGuiWindowFlags.MenuBar);
            PopStyleVar(3);
            DockSpace(GetID("MasterDockSpace"));
            MenuBar.Update();
            End();

            ShowDemoWindow();
            FocusedWindow = null;
            isInUpdateEnumeration = true;
            foreach (var window in this)
            {
                window.Update();
                if (window.IsFocused)
                    FocusedWindow = window;
            }
            isInUpdateEnumeration = false;
            onceAfterUpdate();
            onceAfterUpdate = () => { };
        }

        public void Render()
        {
            foreach (var window in this)
                window.HandleRender();
            if (onceFences.Count > 0)
                Device.WaitForFences(onceFences.ToArray(), true, TimeSpan.FromSeconds(10000.0)); // timeout is a workaround
            onceFences.Clear();

            fence.Reset();
            commandList.Begin();
            commandList.SetFramebuffer(Device.MainSwapchain.Framebuffer);
            commandList.ClearColorTarget(0, RgbaFloat.Cyan);
            ImGuiRenderer.Render(Device, commandList);
            commandList.End();
            Device.SubmitCommands(commandList, fence);
        }

        public void HandleKeyEvent(Key sym, bool isDown)
        {
            if (!GetIO().WantCaptureKeyboard)
                FocusedWindow?.HandleKeyEvent(sym, isDown);
        }

        public void HandleResize(int width, int height)
        {
            ImGuiRenderer.WindowResized(width, height);
        }

        public void RemoveWindow(BaseWindow window) => windows.Remove(window);
        public void AddFenceOnce(Fence fence) => onceFences.Add(fence);
        public BaseWindow? WithTag<TTag>() where TTag : class => windows.FirstOrDefault(w => w.HasTag<TTag>());
        public IEnumerable<BaseWindow> AllWithTag<TTag>() where TTag : class => windows.Where(w => w.HasTag<TTag>());
        public IEnumerator<BaseWindow> GetEnumerator() => windows.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => windows.GetEnumerator();

        private unsafe void LoadForkAwesomeFont()
        {
            var assembly = typeof(WindowContainer).Assembly;
            using var stream = assembly.GetManifestResourceStream("zzre.core.assets.forkawesome-webfont.ttf");
            if (stream == null)
                throw new FileNotFoundException("Could not find embedded ForkAwesome font");
            var data = new byte[stream.Length];
            stream.Read(data, 0, data.Length);
            stream.Close();

            glyphRangePtr = Marshal.AllocHGlobal(sizeof(ushort) * 3);
            var fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();
            fontConfig->MergeMode = 1;
            fontConfig->GlyphMinAdvanceX = 13.0f;
            fontConfig->FontDataOwnedByAtlas = 0;
            fixed (byte* fontPtr = data)
            {
                ushort* glyphRanges = (ushort*)glyphRangePtr.ToPointer();
                glyphRanges[0] = IconFonts.ForkAwesome.IconMin;
                glyphRanges[1] = IconFonts.ForkAwesome.IconMax;
                glyphRanges[2] = 0;
                GetIO().Fonts.AddFontFromMemoryTTF(new IntPtr(fontPtr), data.Length, 15.0f, fontConfig, glyphRangePtr);
            }
            ImGuiRenderer.RecreateFontDeviceTexture();
            ImGuiNative.ImFontConfig_destroy(fontConfig);
        }
    }
}
