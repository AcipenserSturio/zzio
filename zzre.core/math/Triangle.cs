﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static zzre.MathEx;

namespace zzre
{
    public readonly struct Triangle
    {
        public readonly Vector3 A, B, C;
        public Line AB => new Line(A, B);
        public Line BC => new Line(B, C);
        public Line CA => new Line(C, A);
        public Line AC => new Line(A, C);
        public Vector3 NormalUn => Vector3.Cross(AB.Vector, AC.Vector);
        public Vector3 Normal => Vector3.Normalize(NormalUn);
        public Plane Plane => new Plane(Normal, Vector3.Dot(A, Normal));

        public Triangle(Vector3 a, Vector3 b, Vector3 c) => (A, B, C) = (a, b, c);

        public IEnumerable<Vector3> Corners()
        {
            yield return A;
            yield return B;
            yield return C;
        }

        public Vector3 ClosestPoint(Vector3 point)
        {
            var closest = Plane.ClosestPoint(point);
            if (Intersects(closest))
                return closest;

            return new[] { AB, BC, AC }
                .Select(edge => edge.ClosestPoint(point))
                .OrderBy(p => (point - p).LengthSquared())
                .First();
        }

        public bool Intersects(Vector3 point)
        {
            var (localA, localB, localC) = (A - point, B - point, C - point);
            var normalPAB = Vector3.Cross(localA, localB);
            var normalPBC = Vector3.Cross(localB, localC);
            var normalPCA = Vector3.Cross(localC, localA);
            return
                Vector3.Dot(normalPBC, normalPAB) < 0 ||
                Vector3.Dot(normalPBC, normalPCA) < 0;
        }

        public bool Intersects(Box box) => Intersects(box, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        public bool Intersects(Box box, Location boxLoc)
        {
            var (newBox, boxRot) = box.TransformToWorld(boxLoc);
            var (boxRight, boxUp, boxForward) = boxRot.UnitVectors();
            return Intersects(newBox, boxRight, boxUp, boxForward);
        }
        private bool Intersects(Box box, Vector3 boxRight, Vector3 boxUp, Vector3 boxForward) =>
            SATIntersects(box.Corners(), Corners(), new[]
            {
                boxRight, boxUp, boxForward, Normal,
                Vector3.Cross(boxRight, AB.Vector),
                Vector3.Cross(boxRight, BC.Vector),
                Vector3.Cross(boxRight, CA.Vector),
                Vector3.Cross(boxUp, AB.Vector),
                Vector3.Cross(boxUp, BC.Vector),
                Vector3.Cross(boxUp, CA.Vector),
                Vector3.Cross(boxForward, AB.Vector),
                Vector3.Cross(boxForward, BC.Vector),
                Vector3.Cross(boxForward, CA.Vector),
            });

        public bool Intersects(Triangle other) => SATIntersects(Corners(), other.Corners(), new[]
        {
            Normal, other.Normal,
            SATCrossEdge(AB, other.AB),
            SATCrossEdge(BC, other.AB),
            SATCrossEdge(CA, other.AB),
            SATCrossEdge(AB, other.BC),
            SATCrossEdge(BC, other.BC),
            SATCrossEdge(CA, other.BC),
            SATCrossEdge(AB, other.CA),
            SATCrossEdge(BC, other.CA),
            SATCrossEdge(CA, other.CA),
        });

        public bool Intersects(Plane plane)
        {
            var da = plane.SignedDistanceTo(A);
            var db = plane.SignedDistanceTo(B);
            var dc = plane.SignedDistanceTo(C);
            return
                (CmpZero(da) && CmpZero(db) && CmpZero(dc)) ||
                !(da < 0 && db < 0 && dc < 0) ||
                !(da > 0 && db > 0 && dc > 0);
        }
    }
}
