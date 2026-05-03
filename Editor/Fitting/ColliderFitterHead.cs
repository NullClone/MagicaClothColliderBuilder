using System.Collections.Generic;
using MagicaCloth2;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public static partial class ColliderFitter
    {
        public static bool TryFitHead(ColliderGenerationJob job, out FitResult fitResult)
        {
            fitResult = default;

            var vertices = job.Vertices;

            if (vertices == null || vertices.Length < 4 || job.TargetBone == null)
            {
                return false;
            }

            var settings = job.Property.HeadFitProperty;
            var headTransform = job.TargetBone.transform;

            var fitMode = ResolveFitMode(job, BoneFitRole.Head);
            var localUp = ResolveHeadLocalUp(headTransform);

            Quaternion localRotation = Quaternion.FromToRotation(Vector3.up, localUp);
            Quaternion inverseRotation = Quaternion.Inverse(localRotation);

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float minZ = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            float maxZ = float.MinValue;
            var xValues = new List<float>(vertices.Length);
            var yValues = new List<float>(vertices.Length);
            var zValues = new List<float>(vertices.Length);

            for (int i = 0; i < vertices.Length; ++i)
            {
                var v = inverseRotation * vertices[i];
                xValues.Add(v.x);
                yValues.Add(v.y);
                zValues.Add(v.z);

                if (v.x < minX) minX = v.x;
                if (v.y < minY) minY = v.y;
                if (v.z < minZ) minZ = v.z;
                if (v.x > maxX) maxX = v.x;
                if (v.y > maxY) maxY = v.y;
                if (v.z > maxZ) maxZ = v.z;
            }

            if (fitMode != FitMode.Outer)
            {
                float lower = fitMode == FitMode.Inner ? 8.0f : 3.0f;
                float upper = fitMode == FitMode.Inner ? 92.0f : 97.0f;

                minX = Percentile(xValues, lower);
                maxX = Percentile(xValues, upper);
                minY = Percentile(yValues, lower);
                maxY = Percentile(yValues, upper);
                minZ = Percentile(zValues, lower);
                maxZ = Percentile(zValues, upper);
            }

            var center = new Vector3(
                (minX + maxX) * 0.5f,
                (minY + maxY) * 0.5f,
                (minZ + maxZ) * 0.5f);

            float halfWidthX = Mathf.Max(0.0f, (maxX - minX) * 0.5f);
            float halfWidthZ = Mathf.Max(0.0f, (maxZ - minZ) * 0.5f);
            float baseRadius = Mathf.Max(halfWidthX, halfWidthZ);
            float modeScale = fitMode switch
            {
                FitMode.Inner => 0.88f,
                FitMode.Outer => 1.0f,
                _ => 0.95f,
            };
            float radius = Mathf.Clamp(baseRadius * settings.RadiusScale * modeScale, settings.MinRadius, settings.MaxRadius);
            float length = Mathf.Clamp(radius * settings.LengthRatio, 0.005f, radius * 0.6f);

            if (settings.AnchorOuterStartToHeadTransform)
            {
                center = new Vector3(0.0f, (length * 0.5f) + radius, 0.0f);
            }
            else if (settings.UseFaceForwardOffsetWhenNotAnchored)
            {
                Vector3 faceDir = inverseRotation * ResolveHeadLocalForward(headTransform);
                Vector3 upDir = inverseRotation * localUp;
                center += (faceDir.normalized * settings.ForwardOffset) + (upDir.normalized * settings.UpOffset);
            }

            fitResult = new FitResult
            {
                LocalRotation = localRotation,
                Direction = MagicaCapsuleCollider.Direction.Y,
                Center = center,
                Length = length,
                RadiusAtMin = radius,
                RadiusAtMax = radius,
                ReverseDirection = false,
            };

            return true;
        }


        private static Vector3 ResolveHeadLocalUp(Transform headTransform)
        {
            if (headTransform == null)
            {
                return Vector3.up;
            }

            Vector3 worldUp = headTransform.root != null ? headTransform.root.up : Vector3.up;
            Vector3 localUp = headTransform.InverseTransformDirection(worldUp);

            if (localUp.sqrMagnitude <= 1.0e-8f)
            {
                localUp = Vector3.up;
            }

            return localUp.normalized;
        }

        private static Vector3 ResolveHeadLocalForward(Transform headTransform)
        {
            if (headTransform == null)
            {
                return Vector3.forward;
            }

            Vector3 worldForward = headTransform.root != null ? headTransform.root.forward : Vector3.forward;
            Vector3 localForward = headTransform.InverseTransformDirection(worldForward);

            if (localForward.sqrMagnitude <= 1.0e-8f)
            {
                localForward = Vector3.forward;
            }

            return localForward.normalized;
        }
    }
}
