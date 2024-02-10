﻿using System;
using System.IO;
using System.Linq;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.CompilerServices;
using Veldrid;
using Veldrid.Sdl2;
using zzio.vfs;
using zzre.rendering;

namespace zzre;

internal partial class Program
{
    private static readonly Option<string[]> OptionPools = new(
        new[] { "--pool", "-p" },
        () => new[]
        {
            // as if run from a directory like zanzarah/zzre or zanzarah/system
            Path.Combine(Environment.CurrentDirectory, "..", "Resources", "DATA_0.PAK"),
            Path.Combine(Environment.CurrentDirectory, "..")
        },
        "Adds a resource pool to use (later pools overwrite previous ones).\nCurrently directories and PAK archives are supported");

    private static readonly Option<bool> OptionDebugLayers = new(
        new[] { "--debug-layers" },
#if DEBUG
        () => true,
#else
        () => false,
#endif
        "Enable Vulkan debug layers");

    private static void Main(string[] args)
    {
        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        var rootCommand = new RootCommand("zzre - Engine reimplementation and modding tools for Zanzarah");
        rootCommand.AddGlobalOption(OptionPools);
        rootCommand.AddGlobalOption(OptionDebugLayers);
        AddGlobalRenderDocOption(rootCommand);
        AddInDevCommand(rootCommand);
        rootCommand.Invoke(args);
    }

    private static void CommonStartupBeforeWindow(InvocationContext ctx)
    {
        LoadRenderDoc(ctx);
    }

    private static ITagContainer CommonStartupAfterWindow(Sdl2Window window, InvocationContext ctx)
    {
        SetupRenderDocKeys(window);
        var graphicsDevice = CreateGraphicsDevice(window, ctx);

        var diContainer = new TagContainer();
        return diContainer
            .AddTag(ctx)
            .AddTag(window)
            .AddTag(graphicsDevice)
            .AddTag(graphicsDevice.ResourceFactory)
            .AddTag(new ShaderVariantCollection(graphicsDevice,
                typeof(Program).Assembly.GetManifestResourceStream("shaders.mlss")
                ?? throw new InvalidDataException("Shader set is not compiled into zzre")))
            .AddTag(new GameTime())
            .AddTag(CreateResourcePool(ctx))
            .AddTag<IAssetLoader<Texture>>(new TextureAssetLoader(diContainer));
    }

    private static GraphicsDevice CreateGraphicsDevice(Sdl2Window window, InvocationContext ctx)
    {
        var options = new GraphicsDeviceOptions()
        {
            Debug = ctx.ParseResult.GetValueForOption(OptionDebugLayers),
            HasMainSwapchain = true,
            PreferDepthRangeZeroToOne = true,
            PreferStandardClipSpaceYDirection = true,
            SyncToVerticalBlank = true
        };
        SwapchainDescription scDesc = new SwapchainDescription(
            GetSwapchainSource(window),
            (uint)window.Width,
            (uint)window.Height,
            options.SwapchainDepthFormat,
            options.SyncToVerticalBlank,
            colorSrgb : false);
        return GraphicsDevice.CreateVulkan(options, scDesc);
    }

    private static unsafe SwapchainSource GetSwapchainSource(Sdl2Window window)
    {
        IntPtr sdlHandle = window.SdlWindowHandle;
        SDL_SysWMinfo sysWmInfo;
        Sdl2Native.SDL_GetVersion(&sysWmInfo.version);
        Sdl2Native.SDL_GetWMWindowInfo(sdlHandle, &sysWmInfo);
        switch (sysWmInfo.subsystem)
        {
            case SysWMType.Windows:
                Win32WindowInfo w32Info = Unsafe.Read<Win32WindowInfo>(&sysWmInfo.info);
                return SwapchainSource.CreateWin32(w32Info.Sdl2Window, w32Info.hinstance);
            case SysWMType.X11:
                X11WindowInfo x11Info = Unsafe.Read<X11WindowInfo>(&sysWmInfo.info);
                return SwapchainSource.CreateXlib(
                    x11Info.display,
                    x11Info.Sdl2Window);
            case SysWMType.Wayland:
                WaylandWindowInfo wlInfo = Unsafe.Read<WaylandWindowInfo>(&sysWmInfo.info);
                return SwapchainSource.CreateWayland(wlInfo.display, wlInfo.surface);
            case SysWMType.Cocoa:
                CocoaWindowInfo cocoaInfo = Unsafe.Read<CocoaWindowInfo>(&sysWmInfo.info);
                IntPtr nsWindow = cocoaInfo.Window;
                return SwapchainSource.CreateNSWindow(nsWindow);
            default:
                throw new PlatformNotSupportedException("Cannot create a SwapchainSource for " + sysWmInfo.subsystem + ".");
        }
    }

    private static IResourcePool CreateResourcePool(InvocationContext ctx)
    {
        var pools = ctx.ParseResult.GetValueForOption(OptionPools) ?? Array.Empty<string>();
        return pools.Length switch
        {
            0 => new InMemoryResourcePool(),
            1 => CreateSingleResourcePool(pools.Single()),
            _ => new CombinedResourcePool(pools.Select(CreateSingleResourcePool).ToArray())
        };
    }

    private static IResourcePool CreateSingleResourcePool(string poolName)
    {
        // just to normalize
        var path = Path.Combine(Environment.CurrentDirectory, poolName);
        var ext = Path.GetExtension(path).ToLowerInvariant() ?? "";
        switch(ext)
        {
            case ".pak": return new PAKResourcePool(new FileStream(path, FileMode.Open, FileAccess.Read));
            case "": return new FileResourcePool(path);
            default:
                Console.WriteLine($"Warning: Ignored resource pool {poolName} due to unsupported extension {ext}");
                return new InMemoryResourcePool();
        }
    }

    private static void CommonCleanup(ITagContainer diContainer)
    {
        // dispose graphics device last, otherwise Vulkan will crash
        diContainer.TryGetTag(out GraphicsDevice graphicsDevice);
        diContainer.RemoveTag<GraphicsDevice>(dispose: false);
        diContainer.Dispose();
        graphicsDevice?.Dispose();
    }

#if DEBUG
    private static readonly Option<bool> OptionRenderDoc = new(
        "--renderdoc",
        () => true,
        "Whether RenderDoc is to be loaded at start.\nIf RenderDoc loading makes problems set this option to \"false\"");

    private static RenderDoc? RenderDoc = null;

    private static void AddGlobalRenderDocOption(RootCommand command) =>
        command.Add(OptionRenderDoc);

    private static void LoadRenderDoc(InvocationContext ctx)
    {
        var shouldLoad = ctx.ParseResult.GetValueForOption(OptionRenderDoc);
        if (!shouldLoad)
            return;
        if (RenderDoc.Load(out RenderDoc))
        {
            RenderDoc.APIValidation = true;
            RenderDoc.OverlayEnabled = false;
            RenderDoc.RefAllResources = true;
            Console.WriteLine("Info: RenderDoc was loaded, use the PrintScreen key to capture the next frame");
        }
        else
            Console.WriteLine("Warning: Could not load RenderDoc");
    }

    private static void SetupRenderDocKeys(Sdl2Window window)
    {
        if (RenderDoc == null)
            return;
        window.KeyDown += ev =>
        {
            if (ev.Repeat || ev.Key != Key.PrintScreen)
                return;
            if (!RenderDoc.IsTargetControlConnected())
                RenderDoc.LaunchReplayUI();
        };
    }
#else
    private static void AddGlobalRenderDocOption(RootCommand _) { }
    private static void LoadRenderDoc(InvocationContext _) { }
    private static void SetupRenderDocKeys(Sdl2Window _) { }
#endif
}
