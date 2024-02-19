﻿using System;
using DefaultEcs.Resource;
using Silk.NET.SDL;
using Veldrid;
using zzio;
using zzio.vfs;
using zzre.materials;
using zzre.rendering;
using PixelFormat = Veldrid.PixelFormat;

namespace zzre.game.resources;

public readonly record struct RawMaskedBitmapInfo(string colorFile, string maskFile);

public class UIRawMaskedBitmap : AResourceManager<RawMaskedBitmapInfo, UIMaterial>
{
    private static readonly FilePath BasePath = new("resources/bitmaps");

    private readonly ITagContainer diContainer;
    private readonly Sdl sdl;
    private readonly UI ui;
    private readonly GraphicsDevice graphicsDevice;
    private readonly ResourceFactory resourceFactory;
    private readonly IResourcePool resourcePool;

    public UIRawMaskedBitmap(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
        sdl = diContainer.GetTag<Sdl>();
        ui = diContainer.GetTag<UI>();
        graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        resourceFactory = diContainer.GetTag<ResourceFactory>();
        resourcePool = diContainer.GetTag<IResourcePool>();
        Manage(diContainer.GetTag<DefaultEcs.World>());
    }

    protected override UIMaterial Load(RawMaskedBitmapInfo info)
    {
        using var bitmap = UIBitmap.LoadBitmap(sdl, resourcePool, info.colorFile, suffix: "", withAlpha: true) ??
            throw new System.IO.FileNotFoundException($"Could not open bitmap {info.colorFile}");
        var mainTexture = bitmap.ToTexture(graphicsDevice);
        mainTexture.Name = "UIRawMaskedBitmap " + info.colorFile;

        var mask = LoadMask(info.maskFile, bitmap.Width, bitmap.Height);
        var maskTexture = resourceFactory.CreateTexture(new(
            (uint)bitmap.Width, (uint)bitmap.Height, depth: 1,
            mipLevels: 1, arrayLayers: 1,
            format: PixelFormat.R8_UInt,
            usage: TextureUsage.Sampled,
            type: TextureType.Texture2D));
        graphicsDevice.UpdateTexture(maskTexture, mask, 0, 0, 0, maskTexture.Width, maskTexture.Height, 1, 0, 0);

        var material = new UIMaterial(diContainer) { HasMask = true };
        material.MainTexture.Texture = mainTexture;
        material.MainSampler.Sampler = graphicsDevice.LinearSampler;
        material.MaskTexture.Texture = maskTexture;
        material.MaskSampler.Sampler = graphicsDevice.PointSampler;
        material.ScreenSize.Buffer = ui.ProjectionBuffer;
        return material;
    }

    protected override void OnResourceLoaded(in DefaultEcs.Entity entity, RawMaskedBitmapInfo info, UIMaterial resource)
    {
        entity.Set(resource);
    }

    protected override void Unload(RawMaskedBitmapInfo info, UIMaterial resource)
    {
        resource.MainTexture.Texture?.Dispose();
        resource.MaskTexture.Texture?.Dispose();
        resource.Dispose();
    }

    private byte[] LoadMask(string maskFile, int width, int height)
    {
        using var stream = resourcePool.FindAndOpen(BasePath.Combine(maskFile)) ??
            throw new System.IO.FileNotFoundException($"Could not open mask {maskFile}");
        if (stream.Length != width * height)
            throw new System.IO.InvalidDataException($"Expected {width * height} bytes for a bitmap mask, but got {stream.Length}");
        var mask = new byte[width * height];
        stream.ReadExactly(mask.AsSpan());
        return mask;
    }
}
