using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static class AnimationSerializer
    {
        public static string Serialize(string path, string clipName, float? sampleTime)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new System.InvalidOperationException(ErrorHelper.ObjectNotFound(path));

            if (string.IsNullOrEmpty(clipName))
                return SerializeClipList(go);

            var clip = FindClip(go, clipName);
            if (clip == null) throw new System.InvalidOperationException($"Clip not found: {clipName}");

            if (sampleTime.HasValue)
                return SerializeClipAtTime(go, clip, sampleTime.Value);

            return SerializeClipDetail(clip);
        }

        private static string SerializeClipList(GameObject go)
        {
            var clips = GetAllClips(go);
            if (clips == null || clips.Length == 0)
                return "No animation clips";

            var sb = new StringBuilder();
            // First line: component type and clip names
            var animator = go.GetComponent<Animator>();
            sb.Append(animator != null ? "Animator: " : "Animation: ");
            sb.AppendLine(string.Join(", ", System.Array.ConvertAll(clips, c => c.name)));
            sb.AppendLine("---");

            foreach (var clip in clips)
            {
                var bindings = AnimationUtility.GetCurveBindings(clip);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                sb.Append(clip.name);
                sb.Append(" | ").Append(clip.length.ToString("F1", CultureInfo.InvariantCulture)).Append("s");
                sb.Append(" | ").Append(bindings.Length).Append(" curves");
                if (settings.loopTime) sb.Append(" | loop");
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd('\n');
        }

        private static string SerializeClipDetail(AnimationClip clip)
        {
            var sb = new StringBuilder();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            sb.Append("clip: ").Append(clip.name);
            sb.Append(" | ").Append(clip.length.ToString("F1", CultureInfo.InvariantCulture)).Append("s");
            if (settings.loopTime) sb.Append(" | loop");
            sb.AppendLine();
            sb.AppendLine("---");

            var bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in bindings)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                var keys = curve.keys;
                sb.Append(binding.propertyName);
                if (!string.IsNullOrEmpty(binding.path))
                    sb.Append(" (").Append(binding.path).Append(")");
                sb.Append(": ");

                int limit = System.Math.Min(keys.Length, 50);
                for (int i = 0; i < limit; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(keys[i].value.ToString("G4", CultureInfo.InvariantCulture));
                    sb.Append('@');
                    sb.Append(keys[i].time.ToString("G4", CultureInfo.InvariantCulture));
                }
                if (keys.Length > 50)
                    sb.Append($",...+{keys.Length - 50}");
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd('\n');
        }

        private static string SerializeClipAtTime(GameObject go, AnimationClip clip, float time)
        {
            var sb = new StringBuilder();
            sb.Append("sample: ").Append(clip.name).Append(" @ ");
            sb.Append(time.ToString("F2", CultureInfo.InvariantCulture)).AppendLine("s");
            sb.AppendLine("---");

            // Use AnimationMode to sample
            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(go, clip, time);
                AnimationMode.EndSampling();

                var bindings = AnimationUtility.GetCurveBindings(clip);
                foreach (var binding in bindings)
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    float val = curve.Evaluate(time);
                    sb.Append(binding.propertyName).Append(": ");
                    sb.AppendLine(val.ToString("G4", CultureInfo.InvariantCulture));
                }
            }
            finally { AnimationMode.StopAnimationMode(); }
            return sb.ToString().TrimEnd('\n');
        }

        internal static AnimationClip FindClip(GameObject go, string clipName)
        {
            var clips = GetAllClips(go);
            if (clips == null) return null;
            foreach (var c in clips)
                if (c.name == clipName) return c;
            return null;
        }

        internal static AnimationClip[] GetAllClips(GameObject go)
        {
            // Try Animator first
            var animator = go.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
                return animator.runtimeAnimatorController.animationClips;

            // Try legacy Animation
            var animation = go.GetComponent<Animation>();
            if (animation != null)
            {
                var list = new System.Collections.Generic.List<AnimationClip>();
                foreach (AnimationState state in animation)
                    list.Add(state.clip);
                return list.ToArray();
            }
            return null;
        }
    }
}
