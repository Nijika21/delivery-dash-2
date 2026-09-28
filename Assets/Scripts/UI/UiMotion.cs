using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeliveryDash.UI
{
    // Pemutar gerak lapisan "ui" dari Design/d5/gerak.json (HANDOFF bagian gerak): keyframe opacity / scale / x / y,
    // easing per keyframe (kolom "e" = easing ruas yang mulai di keyframe itu, seperti CSS), jeda, durasi, ulang "terus".
    // Waktu nyata, jadi tetap jalan saat Time.timeScale = 0 (menu, jeda, sorotan).
    public static class UiMotion
    {
        private sealed class Key
        {
            public float T;
            public string Ease;
            public readonly Dictionary<string, float> Values = new Dictionary<string, float>();
        }

        private sealed class Clip
        {
            public float Delay;
            public float Duration;
            public bool Loop;
            public string Ease;
            public List<Key> Keys;
        }

        private static readonly string[] Properties = { "opacity", "scale", "x", "y" };
        private static Dictionary<string, Dictionary<string, object>> motions;
        private static readonly Dictionary<VisualElement, IVisualElementScheduledItem> running =
            new Dictionary<VisualElement, IVisualElementScheduledItem>();

        public static bool Loaded => motions != null;

        public static void Load(TextAsset asset)
        {
            if (asset == null || motions != null) return;
            motions = new Dictionary<string, Dictionary<string, object>>();
            if (!(Json.Parse(asset.text) is Dictionary<string, object> root) || !(root.TryGetValue("gerak", out object list) && list is List<object> items)) return;
            foreach (object item in items)
                if (item is Dictionary<string, object> motion && motion.TryGetValue("id", out object id))
                    motions[(string)id] = motion;
        }

        // Lama gerak (ms → detik), termasuk jeda trek; 0 kalau tidak ada.
        public static float Length(string id, string track = null, string variant = null)
        {
            Clip clip = Find(id, track, variant);
            return clip == null ? 0f : clip.Delay + clip.Duration;
        }

        public static void Play(VisualElement element, string id, string track = null, string variant = null,
            float extraDelay = 0f, Action done = null)
        {
            if (element == null) return;
            PruneDetached();
            Clip clip = Find(id, track, variant);
            if (clip == null) { done?.Invoke(); return; }
            Stop(element);
            float start = Time.realtimeSinceStartup + clip.Delay + extraDelay;
            Apply(element, clip, 0f);
            IVisualElementScheduledItem item = null;
            item = element.schedule.Execute(() =>
            {
                if (element.panel == null) { Stop(element); return; }
                float elapsed = Time.realtimeSinceStartup - start;
                if (elapsed < 0f) return;
                float t = clip.Duration <= 0f ? 1f : elapsed / clip.Duration;
                if (clip.Loop) { Apply(element, clip, t - Mathf.Floor(t)); return; }
                Apply(element, clip, Mathf.Clamp01(t));
                if (t < 1f) return;
                ClearNeutral(element, clip);
                Stop(element);
                done?.Invoke();
            }).Every(16);
            running[element] = item;
        }

        public static void Stop(VisualElement element)
        {
            if (element != null && running.TryGetValue(element, out IVisualElementScheduledItem item))
            {
                item.Pause();
                running.Remove(element);
            }
        }

        public static bool IsPlaying(VisualElement element) => element != null && running.ContainsKey(element);

        // Jadwal milik elemen yang sudah lepas dari panel tidak pernah jalan lagi, jadi entrinya dibuang di sini
        // (gerak berulang pada layar yang sudah diganti tidak menumpuk di kamus statis).
        private static readonly List<VisualElement> detached = new List<VisualElement>();

        private static void PruneDetached()
        {
            foreach (KeyValuePair<VisualElement, IVisualElementScheduledItem> entry in running)
                if (entry.Key.panel == null) detached.Add(entry.Key);
            foreach (VisualElement element in detached) Stop(element);
            detached.Clear();
        }

        private static Clip Find(string id, string track, string variant)
        {
            if (motions == null || !motions.TryGetValue(id, out Dictionary<string, object> motion)) return null;
            float duration = Number(motion, "durasi", 0f);
            var clip = new Clip
            {
                Ease = motion.TryGetValue("easing", out object ease) ? ease as string : "linear",
                Loop = motion.TryGetValue("ulang", out object loop) && loop is string text && text.StartsWith("terus", StringComparison.Ordinal),
                Duration = duration / 1000f
            };
            object keys = motion.TryGetValue("kf", out object direct) ? direct : null;
            if (variant != null && motion.TryGetValue("varian", out object variants) && variants is Dictionary<string, object> map &&
                map.TryGetValue(variant, out object chosen) && chosen is Dictionary<string, object> variantMotion)
                keys = variantMotion.TryGetValue("kf", out object variantKeys) ? variantKeys : keys;
            if (track != null && motion.TryGetValue("trek", out object tracks) && tracks is List<object> trackList)
            {
                foreach (object entry in trackList)
                {
                    if (!(entry is Dictionary<string, object> candidate) || !(candidate["elemen"] is string name) ||
                        !name.StartsWith(track, StringComparison.Ordinal)) continue;
                    clip.Delay = Number(candidate, "jeda", 0f) / 1000f;
                    clip.Duration = Number(candidate, "durasi", duration) / 1000f;
                    keys = candidate.TryGetValue("kf", out object trackKeys) ? trackKeys : null;
                    break;
                }
            }
            if (!(keys is List<object> keyList)) return null;
            clip.Keys = new List<Key>();
            foreach (object entry in keyList)
            {
                if (!(entry is Dictionary<string, object> source)) continue;
                var key = new Key { T = Number(source, "t", 0f), Ease = source.TryGetValue("e", out object e) ? e as string : null };
                foreach (string property in Properties)
                    if (source.TryGetValue(property, out object value) && value is double number) key.Values[property] = (float)number;
                clip.Keys.Add(key);
            }
            return clip;
        }

        private static void Apply(VisualElement element, Clip clip, float t)
        {
            float x = 0f, y = 0f;
            bool translate = false;
            foreach (string property in Properties)
            {
                if (!Sample(clip, property, t, out float value)) continue;
                switch (property)
                {
                    case "opacity": element.style.opacity = value; break;
                    case "scale": element.style.scale = new Scale(new Vector3(value, value, 1f)); break;
                    case "x": x = value; translate = true; break;
                    case "y": y = value; translate = true; break;
                }
            }
            if (translate) element.style.translate = new Translate(x, y);
        }

        // Nilai properti di waktu t: di antara dua keyframe yang memuat properti itu, dengan easing keyframe awal.
        private static bool Sample(Clip clip, string property, float t, out float value)
        {
            Key previous = null;
            value = 0f;
            foreach (Key key in clip.Keys)
            {
                if (!key.Values.TryGetValue(property, out float current)) continue;
                if (key.T >= t)
                {
                    if (previous == null || key.T <= previous.T) { value = current; return true; }
                    float local = (t - previous.T) / (key.T - previous.T);
                    value = Mathf.LerpUnclamped(previous.Values[property], current, Evaluate(previous.Ease ?? clip.Ease, local));
                    return true;
                }
                previous = key;
            }
            if (previous == null) return false;
            value = previous.Values[property];
            return true;
        }

        // Sesudah selesai, nilai netral (opacity 1, skala 1, geser 0) dikembalikan ke USS supaya keadaan tekan tetap jalan.
        private static void ClearNeutral(VisualElement element, Clip clip)
        {
            if (Sample(clip, "opacity", 1f, out float opacity) && Mathf.Approximately(opacity, 1f)) element.style.opacity = StyleKeyword.Null;
            if (Sample(clip, "scale", 1f, out float scale) && Mathf.Approximately(scale, 1f)) element.style.scale = StyleKeyword.Null;
            bool hasX = Sample(clip, "x", 1f, out float x), hasY = Sample(clip, "y", 1f, out float y);
            if ((hasX || hasY) && Mathf.Approximately(x, 0f) && Mathf.Approximately(y, 0f)) element.style.translate = StyleKeyword.Null;
        }

        public static float Evaluate(string ease, float t)
        {
            switch (ease)
            {
                case "out-back": return global::Ease.OutBack(t);
                case "out-cubic": return global::Ease.OutCubic(t);
                case "in-cubic": return global::Ease.InCubic(t);
                case "in-out-sine": return global::Ease.InOutSine(t);
                case "out-sine": return global::Ease.OutSine(t);
                case "in-sine": return global::Ease.InSine(t);
                case "ease":
                case "ease-out": return global::Ease.OutCubic(t);
                default: return Mathf.Clamp01(t);
            }
        }

        private static float Number(Dictionary<string, object> source, string key, float fallback) =>
            source.TryGetValue(key, out object value) && value is double number ? (float)number : fallback;

        // Pengurai JSON kecil (objek → Dictionary, larik → List, angka → double) untuk gerak.json.
        private static class Json
        {
            public static object Parse(string text)
            {
                int index = 0;
                return Value(text, ref index);
            }

            private static object Value(string s, ref int i)
            {
                Skip(s, ref i);
                char c = s[i];
                if (c == '{')
                {
                    var map = new Dictionary<string, object>();
                    i++;
                    Skip(s, ref i);
                    if (s[i] == '}') { i++; return map; }
                    while (true)
                    {
                        Skip(s, ref i);
                        string key = String(s, ref i);
                        Skip(s, ref i);
                        i++; // ':'
                        map[key] = Value(s, ref i);
                        Skip(s, ref i);
                        if (s[i++] == '}') return map;
                    }
                }
                if (c == '[')
                {
                    var list = new List<object>();
                    i++;
                    Skip(s, ref i);
                    if (s[i] == ']') { i++; return list; }
                    while (true)
                    {
                        list.Add(Value(s, ref i));
                        Skip(s, ref i);
                        if (s[i++] == ']') return list;
                    }
                }
                if (c == '"') return String(s, ref i);
                if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
                if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
                if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
                int begin = i;
                while (i < s.Length && "+-.eE0123456789".IndexOf(s[i]) >= 0) i++;
                return double.Parse(s.Substring(begin, i - begin), NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            private static string String(string s, ref int i)
            {
                var builder = new StringBuilder();
                i++; // '"'
                while (s[i] != '"')
                {
                    char c = s[i++];
                    if (c != '\\') { builder.Append(c); continue; }
                    char escape = s[i++];
                    switch (escape)
                    {
                        case 'n': builder.Append('\n'); break;
                        case 't': builder.Append('\t'); break;
                        case 'r': builder.Append('\r'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'u': builder.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                        default: builder.Append(escape); break;
                    }
                }
                i++;
                return builder.ToString();
            }

            private static void Skip(string s, ref int i)
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }
        }
    }
}
