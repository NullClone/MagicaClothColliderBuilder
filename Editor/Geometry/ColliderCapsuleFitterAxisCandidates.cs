using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public static partial class ColliderCapsuleFitter
    {
        private static List<Vector3> BuildLimbAxisCandidates(Vector3[] vertices, Vector3 childHint, bool hasParentHint, Vector3 parentHint)
        {
            var candidates = new List<Vector3>();

            if (childHint.sqrMagnitude <= 1.0e-8f)
            {
                return BuildAxisCandidates(vertices, false, Vector3.zero, hasParentHint, parentHint);
            }

            var primary = childHint.normalized;

            candidates.Add(primary);

            if (hasParentHint && parentHint.sqrMagnitude > 1.0e-8f)
            {
                Vector3 towardChildFromParent = (-parentHint).normalized;
                Vector3 blended = (primary * 0.8f) + (towardChildFromParent * 0.2f);

                if (blended.sqrMagnitude > 1.0e-8f)
                {
                    candidates.Add(blended.normalized);
                }
            }

            Vector3 principal = GetPrincipalAxis(vertices);

            if (principal.sqrMagnitude > 1.0e-8f)
            {
                if (Vector3.Dot(principal, primary) < 0.0f)
                {
                    principal = -principal;
                }

                Vector3 principalBlend = (primary * 0.7f) + (principal.normalized * 0.3f);

                if (principalBlend.sqrMagnitude > 1.0e-8f)
                {
                    candidates.Add(principalBlend.normalized);
                }
            }

            Vector3 tangent = Vector3.Cross(primary, Vector3.up);

            if (tangent.sqrMagnitude <= 1.0e-8f)
            {
                tangent = Vector3.Cross(primary, Vector3.right);
            }

            tangent.Normalize();

            Vector3 bitangent = Vector3.Cross(tangent, primary).normalized;

            var angles = new float[] { -14.0f, -8.0f, -4.0f, 4.0f, 8.0f, 14.0f };

            for (int i = 0; i < angles.Length; ++i)
            {
                var q1 = Quaternion.AngleAxis(angles[i], tangent);
                var q2 = Quaternion.AngleAxis(angles[i], bitangent);
                candidates.Add((q1 * primary).normalized);
                candidates.Add((q2 * primary).normalized);
            }

            return DeduplicateAxes(candidates, 0.9985f);
        }

        private static List<Vector3> BuildAxisCandidates(Vector3[] vertices, bool hasChildHint, Vector3 childHint, bool hasParentHint, Vector3 parentHint)
        {
            var candidates = new List<Vector3>();

            if (hasChildHint)
            {
                candidates.Add(childHint.normalized);
            }

            if (hasParentHint)
            {
                candidates.Add(parentHint.normalized);
            }

            candidates.Add(GetPrincipalAxis(vertices));
            candidates.Add(Vector3.right);
            candidates.Add(Vector3.up);
            candidates.Add(Vector3.forward);
            candidates.Add(-Vector3.right);
            candidates.Add(-Vector3.up);
            candidates.Add(-Vector3.forward);

            return DeduplicateAxes(candidates, 0.995f);
        }

        private static List<Vector3> DeduplicateAxes(List<Vector3> candidates, float dotThreshold)
        {
            var unique = new List<Vector3>();

            for (int i = 0; i < candidates.Count; ++i)
            {
                Vector3 candidate = candidates[i];

                if (candidate.sqrMagnitude <= 1.0e-8f) continue;

                Vector3 normalized = candidate.normalized;
                bool duplicated = false;

                for (int n = 0; n < unique.Count; ++n)
                {
                    if (Mathf.Abs(Vector3.Dot(unique[n], normalized)) > dotThreshold)
                    {
                        duplicated = true;
                        break;
                    }
                }

                if (!duplicated)
                {
                    unique.Add(normalized);
                }
            }

            return unique;
        }
    }
}
