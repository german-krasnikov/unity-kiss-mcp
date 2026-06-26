using System;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class AnimatorHelper
    {
        public static string GetState(Animator animator)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));

            var sb = new StringBuilder();

            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                var info = animator.GetCurrentAnimatorStateInfo(layer);
                sb.AppendLine($"layer{layer}: hash={info.shortNameHash} time={info.normalizedTime:F2} speed={info.speed:F1}");

                if (animator.IsInTransition(layer))
                {
                    var trans = animator.GetAnimatorTransitionInfo(layer);
                    sb.AppendLine($"  transition: progress={trans.normalizedTime:F2}");
                }
            }

            for (int i = 0; i < animator.parameterCount; i++)
            {
                var p = animator.GetParameter(i);
                string val = p.type switch
                {
                    AnimatorControllerParameterType.Float   => animator.GetFloat(p.name).ToString("F2"),
                    AnimatorControllerParameterType.Int     => animator.GetInteger(p.name).ToString(),
                    AnimatorControllerParameterType.Bool    => animator.GetBool(p.name).ToString(),
                    AnimatorControllerParameterType.Trigger => "trigger",
                    _                                        => "?"
                };
                sb.AppendLine($"  {p.name}({p.type})={val}");
            }

            return sb.ToString();
        }
    }
}
