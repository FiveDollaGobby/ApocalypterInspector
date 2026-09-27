using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using UnityEngine;

namespace ApocalypterInspector
{
    internal static class Loadouts
    {
        private const int Cap = 3;

        private static readonly List<Slot> slots = new List<Slot>();
        private static bool loaded;
        private static string slotName = "";
        private static string kit = "";
        private static string note = "";
        private static string kitNote = "";

        private struct Slot
        {
            public string Name;
            public float Health;
            public bool HasHealth;
            public float Speed;
            public float Gravity;
            public bool Sprint;
            public bool HasSprint;
            public string GodState;
            public bool InfiniteHealth;
            public bool InfiniteNeeds;
            public float TimeScale;
            public bool TimeLocked;
        }

        public static void Draw()
        {
            EnsureLoaded();
            MenuUi.Section("Loadouts");
            GUILayout.Label("health, speed, gravity, sprint, godmode, time. locks turn on when you apply.", MenuUi.Muted);

            GUILayout.BeginHorizontal();
            slotName = MenuUi.TextField("loadout-name", slotName, 160f);
            if (GUILayout.Button("Save", MenuUi.Button, GUILayout.Width(70f)))
                SaveCurrent();
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(note))
                GUILayout.Label(note, MenuUi.Label);

            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(slot.Name + "   " + MenuUi.Format(slot.Speed) + "x", MenuUi.Label);
                if (GUILayout.Button("Apply", MenuUi.Button, GUILayout.Width(64f)))
                    Apply(slot);
                if (GUILayout.Button("x", MenuUi.Button, GUILayout.Width(32f)))
                {
                    slots.RemoveAt(i);
                    Write();
                    i--;
                }

                GUILayout.EndHorizontal();
            }
        }

        public static void DrawKit()
        {
            MenuUi.Section("Kit");
            GUILayout.Label("item ids, separated by commas. skipped if that item isn't loaded.", MenuUi.Muted);
            GUILayout.BeginHorizontal();
            kit = MenuUi.TextField("spawn-kit", kit, 220f);
            if (GUILayout.Button("Spawn", MenuUi.Button, GUILayout.Width(70f)))
                SpawnKit();
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(kitNote))
                GUILayout.Label(kitNote, MenuUi.Label);
        }

        private static void SaveCurrent()
        {
            string name = string.IsNullOrEmpty(slotName) ? "loadout" : slotName.Trim();
            name = name.Replace("\t", " ");
            if (slots.Count >= Cap && IndexOf(name) < 0)
            {
                note = "3 loadouts already, delete one";
                return;
            }

            float health;
            bool hasHealth;
            float speed;
            float gravity;
            bool sprint;
            bool hasSprint;
            string god;
            bool infHealth;
            bool infNeeds;
            PlayerCheats.Capture(out health, out hasHealth, out speed, out gravity, out sprint, out hasSprint, out god, out infHealth, out infNeeds);

            float timeScale;
            bool timeLocked;
            WorldTools.CaptureTime(out timeScale, out timeLocked);

            Slot slot = new Slot
            {
                Name = name,
                Health = health,
                HasHealth = hasHealth,
                Speed = speed,
                Gravity = gravity,
                Sprint = sprint,
                HasSprint = hasSprint,
                GodState = god ?? "",
                InfiniteHealth = infHealth,
                InfiniteNeeds = infNeeds,
                TimeScale = timeScale,
                TimeLocked = timeLocked
            };

            int existing = IndexOf(name);
            if (existing >= 0)
                slots[existing] = slot;
            else
                slots.Add(slot);

            Write();
            note = "saved " + name;
        }

        private static void Apply(Slot slot)
        {
            PlayerCheats.ApplyLoadout(
                slot.Health,
                slot.HasHealth,
                slot.Speed,
                slot.Gravity,
                slot.Sprint,
                slot.HasSprint,
                slot.GodState,
                slot.InfiniteHealth,
                slot.InfiniteNeeds);
            WorldTools.ApplyTime(slot.TimeScale, slot.TimeLocked);
            note = "applied " + slot.Name;
        }

        private static void SpawnKit()
        {
            string[] bits = (kit ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            int spawned = 0;
            int missing = 0;
            for (int i = 0; i < bits.Length; i++)
            {
                string id = bits[i].Trim();
                if (id.Length == 0)
                    continue;

                if (ItemSpawner.SpawnId(id))
                    spawned++;
                else
                    missing++;
            }

            kitNote = "spawned " + spawned + ", missing " + missing;
        }

        private static int IndexOf(string name)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (string.Equals(slots[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static void EnsureLoaded()
        {
            if (loaded)
                return;

            loaded = true;
            try
            {
                string path = Path.Combine(Paths.BepInExRootPath, "ApocalypterInspector-loadouts.txt");
                if (!File.Exists(path))
                    return;

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length && slots.Count < Cap; i++)
                {
                    string[] bits = lines[i].Split('\t');
                    float health;
                    float speed;
                    float gravity;
                    float timeScale;
                    int flags;
                    if (bits.Length < 10 ||
                        !float.TryParse(bits[1], NumberStyles.Float, CultureInfo.InvariantCulture, out health) ||
                        !float.TryParse(bits[2], NumberStyles.Float, CultureInfo.InvariantCulture, out speed) ||
                        !float.TryParse(bits[3], NumberStyles.Float, CultureInfo.InvariantCulture, out gravity) ||
                        !float.TryParse(bits[7], NumberStyles.Float, CultureInfo.InvariantCulture, out timeScale) ||
                        !int.TryParse(bits[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out flags))
                        continue;

                    slots.Add(new Slot
                    {
                        Name = bits[0],
                        Health = health,
                        Speed = speed,
                        Gravity = gravity,
                        Sprint = bits[4] == "1",
                        GodState = bits[5],
                        InfiniteHealth = bits[6] == "1",
                        TimeScale = timeScale,
                        InfiniteNeeds = bits[8] == "1",
                        HasHealth = (flags & 1) != 0,
                        HasSprint = (flags & 2) != 0,
                        TimeLocked = (flags & 4) != 0
                    });
                }
            }
            catch (Exception)
            {
            }
        }

        private static void Write()
        {
            try
            {
                string path = Path.Combine(Paths.BepInExRootPath, "ApocalypterInspector-loadouts.txt");
                var lines = new List<string>();
                for (int i = 0; i < slots.Count; i++)
                {
                    Slot slot = slots[i];
                    int flags = (slot.HasHealth ? 1 : 0) | (slot.HasSprint ? 2 : 0) | (slot.TimeLocked ? 4 : 0);
                    lines.Add(string.Join("\t", new[]
                    {
                        slot.Name,
                        slot.Health.ToString(CultureInfo.InvariantCulture),
                        slot.Speed.ToString(CultureInfo.InvariantCulture),
                        slot.Gravity.ToString(CultureInfo.InvariantCulture),
                        slot.Sprint ? "1" : "0",
                        slot.GodState ?? "",
                        slot.InfiniteHealth ? "1" : "0",
                        slot.TimeScale.ToString(CultureInfo.InvariantCulture),
                        slot.InfiniteNeeds ? "1" : "0",
                        flags.ToString(CultureInfo.InvariantCulture)
                    }));
                }

                File.WriteAllLines(path, lines.ToArray());
            }
            catch (Exception ex)
            {
                note = ex.Message;
            }
        }
    }
}
