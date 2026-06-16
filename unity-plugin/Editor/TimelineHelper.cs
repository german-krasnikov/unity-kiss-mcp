using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEditor;

namespace UnityMCP.Editor
{
    public static class TimelineHelper
    {
        private static readonly Dictionary<string, Type> TrackTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            {"Animation", typeof(AnimationTrack)},
            {"Audio", typeof(AudioTrack)},
            {"Activation", typeof(ActivationTrack)},
            {"Control", typeof(ControlTrack)},
            {"Signal", typeof(SignalTrack)},
            {"Group", typeof(GroupTrack)}
        };

        private static TrackAsset RequireTrack(TimelineAsset tl, string name)
        {
            var t = TimelineSerializer.FindTrack(tl, name);
            if (t == null) throw new InvalidOperationException($"Track not found: {name}");
            return t;
        }

        public static string CreateTimeline(string assetPath, string directorPath, string tracksStr)
        {
            if (string.IsNullOrEmpty(assetPath))
                throw new ArgumentException("asset_path is required");

            AssetHelper.EnsureDirectory(assetPath);

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(timeline, assetPath);

            var sb = new StringBuilder();
            sb.Append("created: ").Append(assetPath);

            // Add tracks
            int trackCount = 0;
            if (!string.IsNullOrEmpty(tracksStr))
            {
                var trackDefs = tracksStr.Split(';');
                foreach (var def in trackDefs)
                {
                    var d = def.Trim();
                    if (string.IsNullOrEmpty(d)) continue;
                    var parts = d.Split(':');
                    var typeName = parts[0].Trim();
                    var trackName = parts.Length > 1 ? parts[1].Trim() : typeName;
                    AddTrack(timeline, typeName, trackName);
                    trackCount++;
                }
            }
            sb.Append(" | ").Append(trackCount).AppendLine(" tracks");

            // List created tracks
            foreach (var track in timeline.GetRootTracks())
                sb.Append("  [").Append(TimelineSerializer.TrackTypeName(track)).Append("] ").AppendLine(track.name);

            // Attach to PlayableDirector if specified
            if (!string.IsNullOrEmpty(directorPath))
            {
                var go = ComponentSerializer.FindObject(directorPath);
                if (go == null)
                    throw new InvalidOperationException(ErrorHelper.ObjectNotFound(directorPath));

                var director = go.GetComponent<PlayableDirector>();
                if (director == null)
                    director = Undo.AddComponent<PlayableDirector>(go);

                director.playableAsset = timeline;
                sb.Append("director: ").AppendLine(directorPath);
            }

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            return sb.ToString().TrimEnd();
        }

        public static string Edit(string path, string action, string trackName, string trackType,
            string clipName, string binding, float? start, float? duration, float? blendIn, float? blendOut)
        {
            var (director, timeline) = TimelineSerializer.Resolve(path);
            Undo.RecordObject(timeline, "Edit Timeline");

            string result;
            switch (action)
            {
                case "add_track":
                    if (string.IsNullOrEmpty(trackType))
                        throw new ArgumentException("track_type is required for add_track");
                    var newTrack = AddTrack(timeline, trackType, trackName ?? trackType);
                    result = $"edited: add_track [{TimelineSerializer.TrackTypeName(newTrack)}] {newTrack.name}";
                    break;

                case "remove_track":
                    var trackToRemove = RequireTrack(timeline, trackName);
                    timeline.DeleteTrack(trackToRemove);
                    result = $"edited: remove_track {trackName}";
                    break;

                case "add_clip":
                    var trackForClip = RequireTrack(timeline, trackName);
                    var newClip = AddClipToTrack(trackForClip, clipName, start, duration);
                    var clipStart = newClip.start.ToString("F1", CultureInfo.InvariantCulture);
                    var clipEnd = (newClip.start + newClip.duration).ToString("F1", CultureInfo.InvariantCulture);
                    result = $"edited: add_clip [{TimelineSerializer.TrackTypeName(trackForClip)}] {trackName} | {newClip.displayName} {clipStart}-{clipEnd}s";
                    break;

                case "remove_clip":
                    var trackForRemove = RequireTrack(timeline, trackName);
                    RemoveClip(timeline, trackForRemove, clipName);
                    result = $"edited: remove_clip {clipName} from {trackName}";
                    break;

                case "set_binding":
                    if (director == null)
                        throw new InvalidOperationException("set_binding requires a PlayableDirector (use GO path, not asset path)");
                    var trackForBind = RequireTrack(timeline, trackName);
                    var bindGo = ComponentSerializer.FindObject(binding);
                    if (bindGo == null)
                        throw new InvalidOperationException(ErrorHelper.ObjectNotFound(binding));
                    director.SetGenericBinding(trackForBind, bindGo);
                    // Director change — no asset save needed
                    return $"edited: set_binding [{TimelineSerializer.TrackTypeName(trackForBind)}] {trackName} -> {binding}";

                case "set_timing":
                    var trackForTiming = RequireTrack(timeline, trackName);
                    SetClipTiming(trackForTiming, clipName, start, duration, blendIn, blendOut);
                    result = $"edited: set_timing {clipName} on {trackName}";
                    break;

                case "mute":
                case "unmute":
                    var trackForMute = RequireTrack(timeline, trackName);
                    trackForMute.muted = (action == "mute");
                    result = $"edited: {action} [{TimelineSerializer.TrackTypeName(trackForMute)}] {trackName}";
                    break;

                case "lock":
                case "unlock":
                    var trackForLock = RequireTrack(timeline, trackName);
                    trackForLock.locked = (action == "lock");
                    result = $"edited: {action} [{TimelineSerializer.TrackTypeName(trackForLock)}] {trackName}";
                    break;

                default:
                    throw new ArgumentException($"Unknown action: {action}");
            }

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            return result;
        }

        public static string Preview(string path, string action, float time)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null)
                throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));

            var director = go.GetComponent<PlayableDirector>();
            if (director == null)
                throw new InvalidOperationException($"No PlayableDirector on: {path}");

            var timelineName = director.playableAsset != null ? director.playableAsset.name : "unknown";

            switch (action)
            {
                case "play":
                    director.time = time;
                    director.Play();
                    return $"preview: {timelineName} @ {time.ToString("F1", CultureInfo.InvariantCulture)}s | playing";
                case "stop":
                    director.Stop();
                    director.time = 0;
                    return $"preview: {timelineName} @ 0.0s | stopped";
                case "pause":
                    director.Pause();
                    return $"preview: {timelineName} @ {director.time.ToString("F1", CultureInfo.InvariantCulture)}s | paused";
                case "sample":
                default:
                    director.time = time;
                    director.Evaluate();
                    return $"preview: {timelineName} @ {time.ToString("F1", CultureInfo.InvariantCulture)}s | sampled";
            }
        }

        private static TrackAsset AddTrack(TimelineAsset timeline, string typeName, string trackName)
        {
            if (!TrackTypes.TryGetValue(typeName, out var trackType))
                throw new ArgumentException($"Unknown track type: {typeName}. Valid: Animation, Audio, Activation, Control, Signal, Group");

            var track = timeline.CreateTrack(trackType, null, trackName);
            return track;
        }

        private static TimelineClip AddClipToTrack(TrackAsset track, string clipRef, float? start, float? duration)
        {
            TimelineClip clip;

            if (track is AnimationTrack animTrack && !string.IsNullOrEmpty(clipRef))
            {
                // Try loading as animation clip asset
                var animClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipRef);
                if (animClip != null)
                {
                    clip = animTrack.CreateClip(animClip);
                }
                else
                {
                    clip = animTrack.CreateClip<AnimationPlayableAsset>();
                    clip.displayName = clipRef;
                }
            }
            else
            {
                clip = track.CreateDefaultClip();
                if (!string.IsNullOrEmpty(clipRef))
                    clip.displayName = clipRef;
            }

            if (start.HasValue) clip.start = start.Value;
            if (duration.HasValue) clip.duration = duration.Value;

            return clip;
        }

        private static void RemoveClip(TimelineAsset timeline, TrackAsset track, string clipName)
        {
            foreach (var clip in track.GetClips())
            {
                if (string.Equals(clip.displayName, clipName, StringComparison.OrdinalIgnoreCase))
                {
                    timeline.DeleteClip(clip);
                    return;
                }
            }
            throw new InvalidOperationException($"Clip not found: {clipName} on track {track.name}");
        }

        private static void SetClipTiming(TrackAsset track, string clipName, float? start, float? duration, float? blendIn, float? blendOut)
        {
            foreach (var clip in track.GetClips())
            {
                if (string.Equals(clip.displayName, clipName, StringComparison.OrdinalIgnoreCase))
                {
                    if (start.HasValue) clip.start = start.Value;
                    if (duration.HasValue) clip.duration = duration.Value;
                    if (blendIn.HasValue) clip.blendInDuration = blendIn.Value;
                    if (blendOut.HasValue) clip.blendOutDuration = blendOut.Value;
                    return;
                }
            }
            throw new InvalidOperationException($"Clip not found: {clipName} on track {track.name}");
        }

    }
}
