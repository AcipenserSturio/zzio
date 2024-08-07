﻿using System;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.rwbs;

namespace zzre;

public static class ZZIOExtensions
{
    public static Vector4 ToNumerics(this FColor c) => new(c.r, c.g, c.b, c.a);
    public static void CopyFromNumerics(this ref FColor c, Vector4 v)
    {
        c.r = v.X;
        c.g = v.Y;
        c.b = v.Z;
        c.a = v.W;
    }

    public static Quaternion ToZZRotation(this Vector3 v)
    {
        v *= -1f;
        var up = Vector3.UnitY;
        if (Vector3.Cross(v, up).LengthSquared() < 0.001f)
            up = Vector3.UnitZ;
        return Quaternion.Conjugate(Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateLookAt(Vector3.Zero, v, up)));
    }

    public static Vector3 ToZZRotationVector(this Quaternion rotation) =>
        Vector3.Transform(-Vector3.UnitZ, rotation) * -1f;

    public static RgbaByte ToVeldrid(this IColor c) => new(c.r, c.g, c.b, c.a);
    public static RgbaFloat ToVeldrid(this FColor c) => new(c.r, c.g, c.b, c.a);
    public static FColor ToFColor(this Vector4 v) => new(v.X, v.Y, v.Z, v.W);
    public static RgbaFloat ToRgbaFloat(this Vector4 v) => new(v);

    public static Vector3 ToNormal(this CollisionSectorType sectorType) => sectorType switch
    {
        CollisionSectorType.X => Vector3.UnitX,
        CollisionSectorType.Y => Vector3.UnitY,
        CollisionSectorType.Z => Vector3.UnitZ,
        _ => throw new ArgumentOutOfRangeException($"Unknown collision sector type {sectorType}")
    };
}
