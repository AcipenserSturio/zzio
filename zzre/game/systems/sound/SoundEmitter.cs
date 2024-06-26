﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using Silk.NET.OpenAL;

namespace zzre.game.systems;

public sealed partial class SoundEmitter : AComponentSystem<float, components.SoundEmitter>
{
    private const int InitialSourceCount = 16;
    private readonly IAssetRegistry assetRegistry;
    private readonly OpenALDevice device;
    private readonly SoundContext context;
    private readonly GameConfigSection gameConfig;
    private readonly Queue<uint> sourcePool = new(InitialSourceCount);
    private readonly IDisposable? spawnEmitterSubscription;
    private readonly IDisposable? emitterRemovedSubscription;
    private readonly IDisposable? unpauseEmitterSubscription;

    public unsafe SoundEmitter(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>())
    {
        assetRegistry = diContainer.GetTag<IAssetRegistry>();
        diContainer.TryGetTag(out device);
        gameConfig = diContainer.GetTag<GameConfigSection>();
        if (!(IsEnabled = diContainer.TryGetTag(out context)))
            return;
        spawnEmitterSubscription = World.Subscribe<messages.SpawnSample>(HandleSpawnSample);
        emitterRemovedSubscription = World.SubscribeEntityComponentRemoved<components.SoundEmitter>(HandleEmitterRemoved);
        unpauseEmitterSubscription = World.Subscribe<messages.UnpauseEmitter>(HandleUnpauseEmitter);

        using var _ = context.EnsureIsCurrent();
        var sources = stackalloc uint[InitialSourceCount];
        device.AL.GenSources(InitialSourceCount, sources);
        for (int i = 0; i < InitialSourceCount; i++)
        {
            if (sources[i] == 0)
                throw new InvalidOperationException("Source was not generated");
            sourcePool.Enqueue(sources[i]);
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        if (!IsEnabled)
            return;

        // DefaultEcs.World does not dispose entities, thus also not remove emitter components
        // we have to do this ourselves
        var allEmitters = World
            .GetEntities()
            .With<components.SoundEmitter>()
            .AsEnumerable()
            .ToArray();
        foreach (var emitter in allEmitters)
            emitter.Dispose();
        
        // just to be safe: also delete all sources
        using (context.EnsureIsCurrent())
            device.AL.DeleteSources([.. sourcePool]);
        sourcePool.Clear();
        
        spawnEmitterSubscription?.Dispose();
        emitterRemovedSubscription?.Dispose();
        unpauseEmitterSubscription?.Dispose();
    }

    private unsafe void HandleSpawnSample(in messages.SpawnSample msg)
    {
        var entity = msg.AsEntity ?? World.CreateEntity();
        if (entity.World != World)
            throw new ArgumentException("Sample entity has to be created in UI World", nameof(msg));
        if (msg.Position.HasValue || msg.ParentLocation != null)
        {
            entity.Set(new Location()
            {
                LocalPosition = msg.Position ?? Vector3.Zero,
                Parent = msg.ParentLocation
            });
        }
        var handle = assetRegistry.LoadSound(entity, new zzio.FilePath(msg.SamplePath), msg.Priority);
        handle.Inner.Apply(&ApplySpawnSample, (this, entity, msg));
    }

    private static void ApplySpawnSample(AssetHandle handle,
        ref readonly (SoundEmitter, DefaultEcs.Entity, messages.SpawnSample) apply)
    {
        var (thiz, entity, msg) = apply;
        if (!entity.IsAlive)
            return;

        var context = thiz.context;
        var device = thiz.device;
        using var _ = context.EnsureIsCurrent();
        if (!thiz.sourcePool.TryDequeue(out var sourceId))
            sourceId = device.AL.GenSource();
        if (sourceId == 0)
            throw new InvalidOperationException("Source was not generated");
        bool is3D = msg.Position.HasValue || msg.ParentLocation != null;
        device.AL.SetSourceProperty(sourceId, SourceFloat.Gain, msg.Volume * thiz.gameConfig.SoundVolumeFactor);
        device.AL.SetSourceProperty(sourceId, SourceFloat.ReferenceDistance, msg.RefDistance);
        device.AL.SetSourceProperty(sourceId, SourceFloat.MaxDistance, msg.MaxDistance);
        device.AL.SetSourceProperty(sourceId, SourceFloat.MinGain, 0f);
        device.AL.SetSourceProperty(sourceId, SourceFloat.MaxGain, 1f);
        device.AL.SetSourceProperty(sourceId, SourceFloat.RolloffFactor, is3D ? 1f : 0f);
        device.AL.SetSourceProperty(sourceId, SourceInteger.Buffer, handle.Get<SoundAsset>().Buffer);
        device.AL.SetSourceProperty(sourceId, SourceBoolean.Looping, msg.Looping);
        device.AL.SetSourceProperty(sourceId, SourceBoolean.SourceRelative, false);
        if (!is3D)
            device.AL.SetSourceProperty(sourceId, SourceVector3.Position, Vector3.Zero);
        if (msg.Paused)
            device.AL.SourcePause(sourceId);
        else
            device.AL.SourcePlay(sourceId);
        device.AL.ThrowOnError();
        entity.Set(new components.SoundEmitter(sourceId, msg.Volume, msg.RefDistance, msg.MaxDistance, msg.IsMusic));

        device.Logger.Verbose("Spawned emitter for {Sample}", msg.SamplePath);
    }

    private void HandleEmitterRemoved(in DefaultEcs.Entity entity, in components.SoundEmitter emitter)
    {
        using var _ = context.EnsureIsCurrent();
        device.AL.ThrowOnError();
        device.AL.SourceStop(emitter.SourceId);
        device.AL.SetSourceProperty(emitter.SourceId, SourceInteger.Buffer, 0);
        sourcePool.Enqueue(emitter.SourceId);
        device.AL.ThrowOnError();
    }

    private void HandleUnpauseEmitter(in messages.UnpauseEmitter msg)
    {
        using var _ = context.EnsureIsCurrent();
        var emitter = msg.Emitter.Get<components.SoundEmitter>();
        device.AL.SourcePlay(emitter.SourceId);
    }

    protected override void Update(float elapsedTime, ref components.SoundEmitter emitter)
    {
        device.AL.SetSourceProperty(emitter.SourceId, SourceFloat.Gain, emitter.Volume *
            (emitter.IsMusic ? gameConfig.MusicVolumeFactor : gameConfig.SoundVolumeFactor));
        device.AL.ThrowOnError();
    }
}
