using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace ApocalypterInspector
{
    internal static class WorldTools
    {
        private static bool timeLocked;
        private static float timeScale = 1f;
        private static PlayMakerFSM inCar;
        private static GameObject player;
        private static readonly List<LiquidRow> liquids = new List<LiquidRow>();
        private static readonly List<PartRow> parts = new List<PartRow>();
        private static readonly List<WeatherBlock> weather = new List<WeatherBlock>();

        private struct LiquidRow
        {
            public string Name;
            public GameObject Body;
            public FsmFloat Liquid;
            public FsmFloat Capacity;
        }

        private struct PartRow
        {
            public string Name;
            public GameObject Body;
            public FsmFloat Value;
            public float Full;
            public float Distance;
        }

        private class WeatherBlock
        {
            public string Title;
            public readonly List<FsmFloat> Floats = new List<FsmFloat>();
            public readonly List<FsmBool> Bools = new List<FsmBool>();
        }

        public static void Begin()
        {
            inCar = null;
            player = MenuUi.Player();
            liquids.Clear();
            parts.Clear();
            weather.Clear();
        }

        public static void Consider(PlayMakerFSM fsm)
        {
            if (!MenuUi.IsLive(fsm))
                return;

            if (MenuUi.IsPlayer(fsm) && fsm.FsmName == "InCar")
                inCar = fsm;

            if (fsm.FsmName == "LiquidAmount")
            {
                liquids.Add(new LiquidRow
                {
                    Name = fsm.gameObject.name,
                    Body = fsm.gameObject,
                    Liquid = MenuUi.FindFloat(fsm, "Liquid"),
                    Capacity = MenuUi.FindFloat(fsm, "LiquidCapacity")
                });
            }
            else if (fsm.FsmName == "Condition")
            {
                CollectPart(fsm);
            }

            if (weather.Count < 8 && IsWeather(fsm.FsmName))
                CollectWeather(fsm);
        }

        public static void Finish()
        {
            parts.Sort(delegate (PartRow a, PartRow b)
            {
                return a.Distance.CompareTo(b.Distance);
            });
        }

        public static void Draw()
        {
            DrawTime();
            DrawVehicle();
            DrawLiquids();
            DrawParts();
            DrawWeather();
        }

        public static void ApplyLatches()
        {
            if (timeLocked)
                Time.timeScale = timeScale;
        }

        public static void CaptureTime(out float scale, out bool locked)
        {
            scale = timeLocked ? timeScale : Time.timeScale;
            locked = timeLocked;
        }

        public static void ApplyTime(float scale, bool locked)
        {
            if (!locked || Mathf.Approximately(scale, 1f))
            {
                timeLocked = false;
                return;
            }

            timeLocked = true;
            timeScale = scale;
            Time.timeScale = scale;
        }

        private static void DrawTime()
        {
            MenuUi.Section("Time");
            GUILayout.Label(
                "scale " + MenuUi.Format(Time.timeScale) + (timeLocked ? " locked" : ""),
                MenuUi.Label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("0", MenuUi.Button))
                ChooseTime(0f);
            if (GUILayout.Button("0.5", MenuUi.Button))
                ChooseTime(0.5f);
            if (GUILayout.Button("1", MenuUi.Button))
                ChooseTime(1f);
            if (GUILayout.Button("2", MenuUi.Button))
                ChooseTime(2f);
            if (GUILayout.Button("4", MenuUi.Button))
                ChooseTime(4f);
            GUILayout.EndHorizontal();
        }

        private static void DrawVehicle()
        {
            MenuUi.Section("Car");
            if (inCar == null)
                GUILayout.Label("no incar fsm", MenuUi.Label);
            else
                GUILayout.Label("incar " + MenuUi.ActiveState(inCar), MenuUi.Label);

            GameObject focus = MenuUi.FirstLinkedObject(inCar);
            string how = "incar target";
            if (focus == null && parts.Count > 0)
            {
                focus = parts[0].Body;
                how = "nearest part";
            }

            if (focus == null)
            {
                GUILayout.Label("no vehicle part in range", MenuUi.Label);
                return;
            }

            float condition = 0f;
            bool foundCondition = false;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Body != focus || parts[i].Value == null)
                    continue;

                condition = parts[i].Value.Value;
                foundCondition = true;
                break;
            }

            GUILayout.Label(
                how + "  " + focus.name + (foundCondition ? "   " + MenuUi.Format(condition) : ""),
                MenuUi.Label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Repair", MenuUi.Button))
                Repair(focus);
            if (GUILayout.Button("Fill", MenuUi.Button))
                Fill(focus);
            if (GUILayout.Button("Go", MenuUi.Button))
                GoTo(focus);
            if (GUILayout.Button("Pull", MenuUi.Button))
                Pull(focus);
            GUILayout.EndHorizontal();
        }

        private static void Repair(GameObject focus)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (!SameRig(focus, parts[i].Body))
                    continue;

                MenuUi.Set(parts[i].Value, parts[i].Full);
            }
        }

        private static void Fill(GameObject focus)
        {
            for (int i = 0; i < liquids.Count; i++)
            {
                if (!SameRig(focus, liquids[i].Body) || liquids[i].Liquid == null)
                    continue;

                float capacity = liquids[i].Capacity != null ? liquids[i].Capacity.Value : 0f;
                MenuUi.Set(liquids[i].Liquid, capacity > 0f ? capacity : 20f);
            }
        }

        private static void GoTo(GameObject focus)
        {
            GameObject body = MenuUi.Player();
            if (body == null || focus == null)
                return;

            body.transform.position = focus.transform.position + Vector3.up * 1.5f + focus.transform.right * 2f;
        }

        private static void Pull(GameObject focus)
        {
            GameObject body = MenuUi.Player();
            if (body == null || focus == null)
                return;

            focus.transform.SetParent(null);
            focus.transform.position = body.transform.position + body.transform.forward * 4f + Vector3.up;
            focus.transform.rotation = body.transform.rotation;
        }

        private static bool SameRig(GameObject focus, GameObject other)
        {
            if (focus == null || other == null)
                return false;

            if (other == focus)
                return true;

            if (other.transform.IsChildOf(focus.transform) || focus.transform.IsChildOf(other.transform))
                return true;

            return Vector3.Distance(focus.transform.position, other.transform.position) < 6f;
        }

        private static void DrawLiquids()
        {
            MenuUi.Section("Liquids");
            if (liquids.Count == 0)
            {
                GUILayout.Label("no liquid fsms", MenuUi.Label);
                return;
            }

            int show = liquids.Count < 12 ? liquids.Count : 12;
            for (int i = 0; i < show; i++)
            {
                LiquidRow row = liquids[i];
                float capacity = row.Capacity != null ? row.Capacity.Value : 0f;
                float liquid = row.Liquid != null ? row.Liquid.Value : 0f;
                GUILayout.Label(
                    row.Name + "    " + MenuUi.Format(liquid) + " / " + MenuUi.Format(capacity),
                    MenuUi.Label);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Empty to 1", MenuUi.Button) && row.Liquid != null)
                    MenuUi.Set(row.Liquid, 1f);
                if (GUILayout.Button("Fill", MenuUi.Button) && row.Liquid != null)
                    MenuUi.Set(row.Liquid, capacity > 0f ? capacity : 20f);
                GUILayout.EndHorizontal();
            }

            if (liquids.Count > show)
                GUILayout.Label((liquids.Count - show) + " more not shown", MenuUi.Label);
        }

        private static void DrawParts()
        {
            MenuUi.Section("Condition");
            if (parts.Count == 0)
            {
                GUILayout.Label("no condition fsms", MenuUi.Label);
                return;
            }

            int show = parts.Count < 8 ? parts.Count : 8;
            for (int i = 0; i < show; i++)
            {
                PartRow row = parts[i];
                float current = row.Value != null ? row.Value.Value : 0f;
                GUILayout.Label(row.Name + "    " + MenuUi.Format(current), MenuUi.Label);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Empty", MenuUi.Button) && row.Value != null)
                    MenuUi.Set(row.Value, 0f);
                if (GUILayout.Button("Full", MenuUi.Button) && row.Value != null)
                    MenuUi.Set(row.Value, row.Full);
                GUILayout.EndHorizontal();
            }

            if (parts.Count > show)
                GUILayout.Label((parts.Count - show) + " more, further off", MenuUi.Label);
        }

        private static void DrawWeather()
        {
            MenuUi.Section("Weather");
            GUILayout.BeginHorizontal();
            GUILayout.Label("fog " + RenderSettings.fog, MenuUi.Label);
            if (GUILayout.Button(RenderSettings.fog ? "fog off" : "fog on", MenuUi.Button, GUILayout.Width(90f)))
                RenderSettings.fog = !RenderSettings.fog;
            GUILayout.EndHorizontal();

            if (weather.Count == 0)
            {
                GUILayout.Label("no weather fsm. fog button still works.", MenuUi.Label);
                return;
            }

            for (int i = 0; i < weather.Count; i++)
            {
                WeatherBlock block = weather[i];
                GUILayout.Label(block.Title, MenuUi.Label);
                for (int f = 0; f < block.Floats.Count; f++)
                {
                    FsmFloat variable = block.Floats[f];
                    if (variable == null)
                        continue;

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(variable.Name + ": " + MenuUi.Format(variable.Value), MenuUi.Label);
                    string text = MenuUi.TextField(
                        "weather:" + block.Title + ":f:" + variable.Name,
                        MenuUi.Format(variable.Value),
                        90f);
                    if (GUILayout.Button("Set", MenuUi.Button, GUILayout.Width(52f)))
                        MenuUi.TrySet(variable, text);
                    GUILayout.EndHorizontal();
                }

                for (int b = 0; b < block.Bools.Count; b++)
                {
                    FsmBool variable = block.Bools[b];
                    if (variable == null)
                        continue;

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(variable.Name + ": " + variable.Value, MenuUi.Label);
                    if (GUILayout.Button(variable.Value ? "true" : "false", MenuUi.Button, GUILayout.Width(70f)))
                        variable.Value = !variable.Value;
                    GUILayout.EndHorizontal();
                }
            }
        }

        private static void ChooseTime(float scale)
        {
            if (Mathf.Approximately(scale, 1f))
            {
                timeLocked = false;
                Time.timeScale = 1f;
                return;
            }

            timeLocked = true;
            timeScale = scale;
            Time.timeScale = scale;
        }

        private static void CollectPart(PlayMakerFSM fsm)
        {
            FsmFloat[] values = MenuUi.Floats(fsm);
            if (values == null)
                return;

            FsmFloat condition = null;
            float full = 100f;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null)
                    continue;

                if (values[i].Name == "Condition")
                    condition = values[i];
                else if (MenuUi.Contains(values[i].Name, "Max") && values[i].Value > 1f)
                    full = values[i].Value;
            }

            if (condition == null)
                return;

            float distance = 99999f;
            if (player != null)
                distance = Vector3.Distance(player.transform.position, fsm.transform.position);

            parts.Add(new PartRow
            {
                Name = fsm.gameObject.name,
                Body = fsm.gameObject,
                Value = condition,
                Full = full,
                Distance = distance
            });
        }

        private static void CollectWeather(PlayMakerFSM fsm)
        {
            WeatherBlock block = new WeatherBlock();
            block.Title = fsm.gameObject.name + " / " + fsm.FsmName;

            FsmFloat[] floats = MenuUi.Floats(fsm);
            if (floats != null)
            {
                for (int i = 0; i < floats.Length && block.Floats.Count < 8; i++)
                {
                    if (floats[i] != null)
                        block.Floats.Add(floats[i]);
                }
            }

            FsmBool[] bools = MenuUi.Bools(fsm);
            if (bools != null)
            {
                for (int i = 0; i < bools.Length && block.Bools.Count < 8; i++)
                {
                    if (bools[i] != null)
                        block.Bools.Add(bools[i]);
                }
            }

            if (block.Floats.Count == 0 && block.Bools.Count == 0)
                return;

            weather.Add(block);
        }

        private static bool IsWeather(string name)
        {
            return MenuUi.Contains(name, "Weather") ||
                MenuUi.Contains(name, "Rain") ||
                MenuUi.Contains(name, "Fog") ||
                MenuUi.Contains(name, "Snow");
        }
    }
}
