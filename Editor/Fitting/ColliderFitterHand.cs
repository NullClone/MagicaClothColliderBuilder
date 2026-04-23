using MagicaCloth2;
using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public static partial class ColliderFitter
    {
        public static bool TryFitFinger(ColliderGenerationJob job, Vector3 childHint, BoneFitRole boneRole, ref FitResult fitResult)
        {
            var limbSettings = job.Property.LimbFitProperty;
            var fingerAxis = childHint.normalized;
            float jointDistance = childHint.magnitude;

            if (jointDistance <= 1.0e-5f) return false;

            FitMode fitMode = ResolveFitMode(job, boneRole);

            float radiusPercentile = limbSettings.GetRadiusPercentile(fitMode);
            var fingerRotation = Quaternion.FromToRotation(Vector3.up, fingerAxis);

            if (!TryFitOnY(
                job.Vertices,
                Quaternion.Inverse(fingerRotation),
                radiusPercentile,
                jointDistance,
                boneRole,
                true,
                true,
                fitMode,
                job.Property,
                out Vector3 fingerCenter,
                out float _,
                out float fingerStartRadius,
                out float fingerEndRadius,
                out float _))
            {
                return false;
            }

            fingerStartRadius *= limbSettings.RadiusScale;
            fingerEndRadius *= limbSettings.RadiusScale;

            float maxFingerRadius = Mathf.Max(0.0025f, jointDistance * 0.32f);
            fingerStartRadius = Mathf.Min(fingerStartRadius, maxFingerRadius);
            fingerEndRadius = Mathf.Min(fingerEndRadius, maxFingerRadius);

            fitResult.LocalRotation = fingerRotation;
            fitResult.Direction = MagicaCapsuleCollider.Direction.Y;
            fitResult.Center = new Vector3(fingerCenter.x, jointDistance * 0.5f, fingerCenter.z);
            fitResult.Length = jointDistance;
            fitResult.RadiusAtMin = fingerStartRadius;
            fitResult.RadiusAtMax = fingerEndRadius;
            fitResult.ReverseDirection = false;

            return true;
        }

        public static bool TryFitPalm(ColliderGenerationJob job, ref FitResult fitResult)
        {
            if (job == null ||
                job.TargetBone == null ||
                job.Property == null ||
                job.Vertices == null ||
                job.Vertices.Length < 4 ||
                !TryPalmHint(job.Animator, job.TargetBone.transform, out Vector3 palmAxis, out float palmLength))
            {
                return false;
            }

            palmLength = Mathf.Max(palmLength, job.Property.LimbFitProperty.MinJointDistance);

            Vector3 axis = palmAxis.normalized;
            Quaternion palmRotation = Quaternion.FromToRotation(Vector3.up, axis);
            Quaternion inverseRotation = Quaternion.Inverse(palmRotation);

            var rotated = new Vector3[job.Vertices.Length];
            var palmX = new List<float>();
            var palmZ = new List<float>();
            float minY = -palmLength * 0.10f;
            float maxY = palmLength * 0.82f;
            float sampleMinY = -palmLength * 0.18f;
            float sampleMaxY = palmLength * 0.95f;

            for (int i = 0; i < job.Vertices.Length; ++i)
            {
                Vector3 rv = inverseRotation * job.Vertices[i];
                rotated[i] = rv;

                if (rv.y < sampleMinY || rv.y > sampleMaxY) continue;

                palmX.Add(rv.x);
                palmZ.Add(rv.z);
            }

            if (palmX.Count == 0) return false;

            float centerX = Percentile(palmX, 50.0f);
            float centerZ = Percentile(palmZ, 50.0f);
            float centerY = (minY + maxY) * 0.5f;
            float length = Mathf.Max(job.Property.GenericFitProperty.MinLength, maxY - minY);
            float endWindow = Mathf.Max(length * 0.30f, 0.004f);
            var allRadii = new List<float>();
            var wristRadii = new List<float>();
            var knuckleRadii = new List<float>();

            for (int i = 0; i < rotated.Length; ++i)
            {
                Vector3 v = rotated[i];

                if (v.y < sampleMinY || v.y > sampleMaxY) continue;

                float dx = v.x - centerX;
                float dz = v.z - centerZ;
                float radial = Mathf.Sqrt((dx * dx) + (dz * dz));
                allRadii.Add(radial);

                if (v.y <= minY + endWindow)
                {
                    wristRadii.Add(radial);
                }

                if (v.y >= maxY - endWindow)
                {
                    knuckleRadii.Add(radial);
                }
            }

            if (allRadii.Count == 0)
            {
                return false;
            }

            if (wristRadii.Count == 0)
            {
                wristRadii.AddRange(allRadii);
            }

            if (knuckleRadii.Count == 0)
            {
                knuckleRadii.AddRange(allRadii);
            }

            FitMode fitMode = ResolveFitMode(job, BoneFitRole.Default);
            float radiusPercentile = job.Property.LimbFitProperty.GetRadiusPercentile(fitMode);
            float globalRadius = Percentile(allRadii, Mathf.Min(radiusPercentile + 6.0f, 58.0f));
            float wristRadius = Mathf.Min(Percentile(wristRadii, radiusPercentile), globalRadius);
            float knuckleRadius = Mathf.Min(Percentile(knuckleRadii, radiusPercentile), globalRadius);
            float maxAllowedRadius = Mathf.Max(0.008f, palmLength * 0.48f) * job.Property.LimbFitProperty.GetRadiusCapScale(fitMode);
            float minRadius = job.Property.GenericFitProperty.MinRadius;

            wristRadius = Mathf.Clamp(wristRadius * job.Property.LimbFitProperty.RadiusScale, minRadius, maxAllowedRadius);
            knuckleRadius = Mathf.Clamp(knuckleRadius * job.Property.LimbFitProperty.RadiusScale, minRadius, maxAllowedRadius);

            fitResult.LocalRotation = palmRotation;
            fitResult.Direction = MagicaCapsuleCollider.Direction.Y;
            fitResult.Center = new Vector3(centerX, centerY, centerZ);
            fitResult.Length = length;
            fitResult.RadiusAtMin = wristRadius;
            fitResult.RadiusAtMax = knuckleRadius;
            fitResult.ReverseDirection = false;

            return true;
        }
    }
}