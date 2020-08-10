﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzio.utils;
using zzio.vfs;
using zzre.materials;
using zzre.rendering;
using static ImGuiNET.ImGui;

namespace zzre.tools
{
    public partial class ActorEditor
    {
        private class Part : ListDisposable
        {
            private readonly ActorEditor parent;
            private readonly TextureLoader textureLoader;
            private readonly GameTime gameTime;
            private readonly string modelName; // used as ImGui ID
            private bool isPlaying = false;
            private int currentAnimationI = -1;

            public RWGeometryBuffers geometry;
            public ModelSkinnedMaterial[] materials;
            public Skeleton skeleton;
            public DebugSkeletonRenderer skeletonRenderer;
            public (AnimationType type, SkeletalAnimation ani)[] animations;
            
            public Part(ActorEditor parent, string modelName, (AnimationType type, string filename)[] animationNames)
            {
                this.parent = parent;
                this.modelName = modelName;
                textureLoader = parent.diContainer.GetTag<TextureLoader>();
                gameTime = parent.diContainer.GetTag<GameTime>();
                var modelPath = new FilePath("resources/models/actorsex/").Combine(modelName);
                var texturePath = textureLoader.GetTexturePathFromModel(modelPath);

                using var contentStream = parent.resourcePool.FindAndOpen(modelPath);
                if (contentStream == null)
                    throw new IOException($"Could not open model at {modelPath.ToPOSIXString()}");
                var clump = Section.ReadNew(contentStream);
                if (clump.sectionId != SectionId.Clump)
                    throw new InvalidDataException($"Expected a root clump section, got a {clump.sectionId}");
                var skin = (RWSkinPLG)clump.FindChildById(SectionId.SkinPLG);
                if (skin == null)
                    throw new InvalidDataException($"Attached actor part model does not have a skin");

                geometry = new RWGeometryBuffers(parent.diContainer, (RWClump)clump);
                AddDisposable(geometry);

                skeleton = new Skeleton(skin);
                skeletonRenderer = new DebugSkeletonRenderer(parent.diContainer, geometry, skeleton);
                skeletonRenderer.BoneMaterial.Transformation.Buffer = parent.editor.Transform.Buffer;
                skeletonRenderer.SkinMaterial.Transformation.Buffer = parent.editor.Transform.Buffer;
                skeletonRenderer.SkinHighlightedMaterial.Transformation.Buffer = parent.editor.Transform.Buffer;
                AddDisposable(skeletonRenderer);

                materials = new ModelSkinnedMaterial[geometry.SubMeshes.Count];
                foreach (var (rwMaterial, index) in geometry.SubMeshes.Select(s => s.Material).Indexed())
                {
                    var material = materials[index] = new ModelSkinnedMaterial(parent.diContainer);
                    (material.MainTexture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(texturePath, rwMaterial);
                    material.Transformation.Buffer = parent.editor.Transform.Buffer;
                    material.Uniforms.Ref = ModelStandardMaterialUniforms.Default;
                    material.Uniforms.Ref.vertexColorFactor = 0.0f;
                    material.Uniforms.Ref.tint = rwMaterial.color.ToFColor();
                    material.Pose.Skeleton = skeleton;
                    AddDisposable(material);
                }

                SkeletalAnimation LoadAnimation(string filename)
                {
                    var animationPath = new FilePath("resources/models/actorsex/").Combine(filename);
                    using var contentStream = parent.resourcePool.FindAndOpen(animationPath);
                    if (contentStream == null)
                        throw new IOException($"Could not open animation at {animationPath.ToPOSIXString()}");
                    var animation = SkeletalAnimation.ReadNew(contentStream);
                    if (animation.BoneCount != skeleton.BoneCount)
                        throw new InvalidDataException($"Animation {filename} is incompatible with actor skeleton {modelName}");
                    return animation;
                }
                animations = animationNames.Select(t => (t.type, LoadAnimation(t.filename))).ToArray();
                skeleton.ResetToBinding();
            }

            public void Render(CommandList cl)
            {
                geometry.SetBuffers(cl);
                foreach (var (subMesh, index) in geometry.SubMeshes.Indexed())
                {
                    (materials[index] as IMaterial).Apply(cl);
                    geometry.SetSkinBuffer(cl); // TODO: find a solution for the pipeline problem
                    cl.DrawIndexed(
                        indexStart: (uint)subMesh.IndexOffset,
                        indexCount: (uint)subMesh.IndexCount,
                        instanceCount: 1,
                        vertexOffset: 0,
                        instanceStart: 0);
                }
            }

            public void RenderDebug(CommandList cl) => skeletonRenderer.Render(cl);

            public bool PlaybackContent()
            {
                bool hasChanged = isPlaying;
                if (isPlaying)
                {
                    skeleton.AddTime(gameTime.Delta);
                    foreach (var mat in materials)
                        mat.Pose.MarkPoseDirty();
                }

                PushID(modelName);
                if (BeginCombo("Animation", currentAnimationI < 0 ? "" : animations[currentAnimationI].type.ToString()))
                {
                    foreach (var ((type, ani), index) in animations.Indexed())
                    {
                        PushID((int)type);
                        if (Selectable(type.ToString(), index == currentAnimationI))
                        {
                            currentAnimationI = index;
                            skeleton.JumpToAnimation(ani);
                        }
                        PopID();
                    }
                    EndCombo();
                }

                float time = skeleton.AnimationTime;
                if (skeleton.CurrentAnimation == null)
                {
                    SliderFloat("Time", ref time, 0.0f, 0.0f);
                    SmallButton("|<");
                    SameLine();
                    SmallButton("|>");
                    SameLine();
                    SmallButton(">|");
                }
                else
                {
                    if (SliderFloat("Time", ref time, 0.0f, skeleton.CurrentAnimation.duration))
                    {
                        skeleton.AnimationTime = time;
                        foreach (var mat in materials)
                            mat.Pose.MarkPoseDirty();
                        hasChanged = true;
                    }
                    if (SmallButton("|<"))
                        skeleton.JumpToAnimation(skeleton.CurrentAnimation);
                    SameLine();
                    if (isPlaying && SmallButton("||"))
                        isPlaying = false;
                    else if (!isPlaying && SmallButton("|>"))
                        isPlaying = true;
                    SameLine();
                    if (SmallButton(">|"))
                        skeleton.JumpToAnimation(skeleton.CurrentAnimation);
                }

                PopID();
                return hasChanged;
            }

            public bool AnimationsContent()
            {
                return false;
            }
        }
    }
}