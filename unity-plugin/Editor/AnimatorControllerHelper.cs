using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static class AnimatorControllerHelper
    {
        public static string AddParameters(string path, string paramsStr)
        {
            var ctrl = GetOrCreateController(path);
            var added = new List<string>();

            foreach (var part in paramsStr.Split(';'))
            {
                var p = part.Trim();
                if (string.IsNullOrEmpty(p)) continue;

                var tokens = p.Split(':');
                var name = tokens[0].Trim();
                var typeStr = tokens.Length > 1 ? tokens[1].Trim().ToLower() : "float";
                var defaultStr = tokens.Length > 2 ? tokens[2].Trim() : null;

                // Skip if already exists
                if (HasParameter(ctrl, name)) { added.Add($"{name}(exists)"); continue; }

                Undo.RecordObject(ctrl, "Add Parameter");
                var pType = typeStr switch
                {
                    "float" => AnimatorControllerParameterType.Float,
                    "int" => AnimatorControllerParameterType.Int,
                    "bool" => AnimatorControllerParameterType.Bool,
                    "trigger" => AnimatorControllerParameterType.Trigger,
                    _ => throw new ArgumentException($"Unknown param type: {typeStr}. Use float|int|bool|trigger")
                };
                ctrl.AddParameter(name, pType);
                if (defaultStr != null && typeStr != "trigger")
                    SetParamDefault(ctrl, name, typeStr, defaultStr);
                added.Add($"{name}({typeStr})");
            }

            SaveController(ctrl);
            return $"added: {string.Join(", ", added)}";
        }

        public static string AddStates(string path, string statesStr)
        {
            var ctrl = GetOrCreateController(path);
            var sm = GetStateMachine(ctrl);
            var added = new List<string>();

            int index = sm.states.Length;
            foreach (var part in statesStr.Split(';'))
            {
                var p = part.Trim();
                if (string.IsNullOrEmpty(p)) continue;

                var tokens = p.Split(':');
                var stateName = tokens[0].Trim();
                var clipPath = tokens.Length > 1 ? tokens[1].Trim() : null;

                // Skip if already exists
                if (FindState(sm, stateName) != null) { added.Add($"{stateName}(exists)"); continue; }

                Undo.RecordObject(ctrl, "Add State");
                var state = sm.AddState(stateName, new Vector3(300, index * 80, 0));

                if (!string.IsNullOrEmpty(clipPath))
                {
                    var clip = FindClipAsset(clipPath);
                    if (clip != null) state.motion = clip;
                }

                added.Add(stateName);
                index++;
            }

            SaveController(ctrl);
            return $"added: {string.Join(", ", added)}";
        }

        public static string AddTransition(string path, string source, string target,
            string conditions, float? duration, float? exitTime, bool? hasExitTime)
        {
            var ctrl = GetOrCreateController(path);
            var sm = GetStateMachine(ctrl);

            var targetState = FindState(sm, target);
            if (targetState == null) throw new InvalidOperationException($"Target state not found: {target}");

            Undo.RecordObject(ctrl, "Add Transition");
            AnimatorStateTransition transition;

            if (source == "*")
            {
                transition = sm.AddAnyStateTransition(targetState);
                transition.canTransitionToSelf = false;
            }
            else
            {
                var sourceState = FindState(sm, source);
                if (sourceState == null) throw new InvalidOperationException($"Source state not found: {source}");
                transition = sourceState.AddTransition(targetState);
            }

            // Settings
            transition.hasExitTime = hasExitTime ?? (exitTime.HasValue);
            if (exitTime.HasValue) transition.exitTime = exitTime.Value;
            if (duration.HasValue) transition.duration = duration.Value;

            // Parse conditions
            if (!string.IsNullOrEmpty(conditions))
            {
                foreach (var condStr in conditions.Split(';'))
                {
                    var c = condStr.Trim();
                    if (string.IsNullOrEmpty(c)) continue;
                    var cond = ParseCondition(c, ctrl);
                    transition.AddCondition(cond.mode, cond.threshold, cond.parameter);
                }
                // If we have conditions and no explicit hasExitTime, disable it
                if (!hasExitTime.HasValue && !exitTime.HasValue)
                    transition.hasExitTime = false;
            }

            SaveController(ctrl);
            var label = source == "*" ? "[Any]" : source;
            return $"transition: {label} → {target}";
        }

        public static string SetDefault(string path, string stateName)
        {
            var ctrl = GetOrCreateController(path);
            var sm = GetStateMachine(ctrl);
            var state = FindState(sm, stateName);
            if (state == null) throw new InvalidOperationException($"State not found: {stateName}");

            Undo.RecordObject(ctrl, "Set Default State");
            sm.defaultState = state;
            SaveController(ctrl);
            return $"default: {stateName}";
        }

        public static string Remove(string path, string type, string name, string source, string target)
        {
            var ctrl = GetOrCreateController(path);
            var sm = GetStateMachine(ctrl);
            Undo.RecordObject(ctrl, "Remove " + type);

            switch (type)
            {
                case "param":
                    RemoveParameter(ctrl, name);
                    break;
                case "state":
                    RemoveState(sm, name);
                    break;
                case "transition":
                    RemoveTransition(sm, source, target);
                    break;
                default:
                    throw new ArgumentException($"Unknown remove type: {type}. Use param|state|transition");
            }

            SaveController(ctrl);
            return $"removed: {type} {name}{(source != null ? $" ({source} → {target})" : "")}";
        }

        // --- Internal helpers ---

        internal static AnimatorController GetController(string path)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));

            var animator = go.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return null;

            return animator.runtimeAnimatorController as AnimatorController;
        }

        private static AnimatorController GetOrCreateController(string path)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));

            var animator = go.GetComponent<Animator>();
            if (animator == null)
                animator = Undo.AddComponent<Animator>(go);

            var ctrl = animator.runtimeAnimatorController as AnimatorController;
            if (ctrl == null)
            {
                var dir = "Assets/Animations";
                if (!AssetDatabase.IsValidFolder(dir))
                    AssetDatabase.CreateFolder("Assets", "Animations");
                var ctrlPath = $"{dir}/{go.name}.controller";
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                animator.runtimeAnimatorController = ctrl;
            }
            return ctrl;
        }

        internal static AnimatorStateMachine GetStateMachine(AnimatorController ctrl, int layer = 0)
        {
            return ctrl.layers[layer].stateMachine;
        }

        internal static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (var cs in sm.states)
                if (cs.state.name == name) return cs.state;
            return null;
        }

        private static AnimationClip FindClipAsset(string clipPath)
        {
            // Try exact path first
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip != null) return clip;

            // Try Assets/Animations/{name}
            if (!clipPath.Contains("/"))
            {
                var withDir = $"Assets/Animations/{clipPath}";
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(withDir);
                if (clip != null) return clip;
            }

            // Search by name
            var guids = AssetDatabase.FindAssets($"t:AnimationClip {System.IO.Path.GetFileNameWithoutExtension(clipPath)}");
            foreach (var guid in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
                if (c != null && (c.name == System.IO.Path.GetFileNameWithoutExtension(clipPath) || p.EndsWith(clipPath)))
                    return c;
            }
            return null;
        }

        private static bool HasParameter(AnimatorController ctrl, string name)
        {
            foreach (var p in ctrl.parameters)
                if (p.name == name) return true;
            return false;
        }

        private static void SetParamDefault(AnimatorController ctrl, string name, string typeStr, string defaultStr)
        {
            var parms = ctrl.parameters;
            for (int i = 0; i < parms.Length; i++)
            {
                if (parms[i].name != name) continue;
                switch (typeStr)
                {
                    case "float": parms[i].defaultFloat = float.Parse(defaultStr, CultureInfo.InvariantCulture); break;
                    case "int": parms[i].defaultInt = int.Parse(defaultStr); break;
                    case "bool": parms[i].defaultBool = defaultStr == "true"; break;
                }
                ctrl.parameters = parms;
                return;
            }
        }

        internal struct ParsedCondition
        {
            public AnimatorConditionMode mode;
            public float threshold;
            public string parameter;
        }

        internal static ParsedCondition ParseCondition(string condStr, AnimatorController ctrl)
        {
            var c = condStr.Trim();
            var result = new ParsedCondition();

            // "!IsGrounded" → IfNot
            if (c.StartsWith("!"))
            {
                result.parameter = c.Substring(1);
                result.mode = AnimatorConditionMode.IfNot;
                result.threshold = 0;
                return result;
            }

            // "Speed>0.1" "Speed<0.1" "Type=2" "State!=0"
            int opIdx = -1;
            string op = null;
            // Check != before = to avoid false match
            var neqIdx = c.IndexOf("!=");
            if (neqIdx > 0) { opIdx = neqIdx; op = "!="; }
            else
            {
                for (int i = 1; i < c.Length; i++)
                {
                    if (i + 1 < c.Length && c[i] == '=' && c[i+1] == '=')
                    { opIdx = i; op = "=="; break; }
                    if (c[i] == '>' || c[i] == '<' || c[i] == '=')
                    { opIdx = i; op = c[i].ToString(); break; }
                }
            }

            if (op != null && opIdx > 0)
            {
                result.parameter = c.Substring(0, opIdx).Trim();
                var valueStr = c.Substring(opIdx + op.Length).Trim().ToLower();
                // Bool shorthand: Param==true → Greater 0.5, Param==false → Less 0.5
                if (valueStr == "true")  { result.mode = AnimatorConditionMode.Greater; result.threshold = 0.5f; return result; }
                if (valueStr == "false") { result.mode = AnimatorConditionMode.Less;    result.threshold = 0.5f; return result; }
                result.threshold = float.Parse(valueStr, CultureInfo.InvariantCulture);
                result.mode = op switch
                {
                    ">" => AnimatorConditionMode.Greater,
                    "<" => AnimatorConditionMode.Less,
                    "=" => AnimatorConditionMode.Equals,
                    "==" => AnimatorConditionMode.Equals,
                    "!=" => AnimatorConditionMode.NotEqual,
                    _ => AnimatorConditionMode.If
                };
                return result;
            }

            // "IsGrounded" or "Jump" → If (bool/trigger = true)
            result.parameter = c;
            result.mode = AnimatorConditionMode.If;
            result.threshold = 0;
            return result;
        }

        private static void RemoveParameter(AnimatorController ctrl, string name)
        {
            var parms = ctrl.parameters;
            for (int i = 0; i < parms.Length; i++)
            {
                if (parms[i].name == name)
                {
                    ctrl.RemoveParameter(i);
                    return;
                }
            }
            throw new InvalidOperationException($"Parameter not found: {name}");
        }

        private static void RemoveState(AnimatorStateMachine sm, string name)
        {
            var state = FindState(sm, name);
            if (state == null) throw new InvalidOperationException($"State not found: {name}");
            sm.RemoveState(state);
        }

        private static void RemoveTransition(AnimatorStateMachine sm, string source, string target)
        {
            if (source == "*")
            {
                var anyTransitions = sm.anyStateTransitions;
                for (int i = 0; i < anyTransitions.Length; i++)
                {
                    if (anyTransitions[i].destinationState?.name == target)
                    {
                        sm.RemoveAnyStateTransition(anyTransitions[i]);
                        return;
                    }
                }
                throw new InvalidOperationException($"AnyState transition to '{target}' not found");
            }

            var sourceState = FindState(sm, source);
            if (sourceState == null) throw new InvalidOperationException($"Source state not found: {source}");

            foreach (var t in sourceState.transitions)
            {
                if (t.destinationState?.name == target)
                {
                    sourceState.RemoveTransition(t);
                    return;
                }
            }
            throw new InvalidOperationException($"Transition '{source} → {target}' not found");
        }

        private static void SaveController(AnimatorController ctrl)
        {
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
        }
    }
}
