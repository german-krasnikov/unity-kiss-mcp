using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class PhysicsHelper
    {
        public static string GetState(GameObject go, float radius = 5f)
        {
            if (go == null) throw new ArgumentNullException(nameof(go));

            var sb = new StringBuilder();

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
                sb.AppendLine($"rb: vel={rb.linearVelocity} angVel={rb.angularVelocity} mass={rb.mass} drag={rb.linearDamping} kinematic={rb.isKinematic}");

            foreach (var col in go.GetComponents<Collider>())
                sb.AppendLine($"col: {col.GetType().Name} trigger={col.isTrigger} enabled={col.enabled}");

            var nearby = Physics.OverlapSphere(go.transform.position, radius);
            if (nearby.Length > 0)
            {
                sb.AppendLine($"nearby({radius:F1}m): {nearby.Length} colliders");
                foreach (var col in nearby.Take(10))
                {
                    float dist = Vector3.Distance(go.transform.position, col.transform.position);
                    sb.AppendLine($"  {col.name} {dist:F1}m");
                }
            }

            int layer = go.layer;
            var collidesWith = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                string ln = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(ln) && !Physics.GetIgnoreLayerCollision(layer, i))
                    collidesWith.Add(ln);
            }
            sb.AppendLine($"layer={LayerMask.LayerToName(layer)} collides=[{string.Join(",", collidesWith)}]");

            return sb.ToString();
        }
    }
}
