using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace ApocalypterInspector
{
    internal static class ItemSpawner
    {
        private const int ShowCap = 40;
        private static readonly string[] WeaponWords = { "weapon", "gun", "rifle", "ammo", "melee" };
        private static readonly Dictionary<string, ItemRow> known = new Dictionary<string, ItemRow>();
        private static readonly List<ItemRow> items = new List<ItemRow>();
        private static string spawnFilter = "";
        private static bool weaponsOnly;

        private class ItemRow
        {
            public string Id;
            public string Label;
            public GameObject Source;
            public bool IsAsset;
            public bool Seen;
        }

        public static void Begin()
        {
            foreach (KeyValuePair<string, ItemRow> pair in known)
                pair.Value.Seen = false;
        }

        public static void Consider(PlayMakerFSM fsm, Dictionary<int, string> labels)
        {
            if (fsm.FsmName != "ID")
                return;

            string objectName = fsm.gameObject.name;
            if (objectName == "Player" || objectName.StartsWith("Player(", StringComparison.Ordinal))
                return;

            string id = MenuUi.StringValue(fsm, "ID");
            if (string.IsNullOrEmpty(id))
                return;

            string label;
            if (!labels.TryGetValue(fsm.gameObject.GetInstanceID(), out label) || string.IsNullOrEmpty(label))
                label = objectName;

            bool asset = !MenuUi.IsLive(fsm);
            ItemRow row;
            if (!known.TryGetValue(id, out row))
            {
                row = new ItemRow();
                row.Id = id;
                known[id] = row;
            }

            row.Seen = true;
            row.Label = label;
            if (row.Source == null || (asset && !row.IsAsset))
            {
                row.Source = fsm.gameObject;
                row.IsAsset = asset;
            }
        }

        public static void Finish()
        {
            items.Clear();
            foreach (KeyValuePair<string, ItemRow> pair in known)
            {
                ItemRow row = pair.Value;
                if (!row.Seen)
                    row.Source = null;
                items.Add(row);
            }

            items.Sort(delegate (ItemRow a, ItemRow b)
            {
                int group = string.Compare(GroupOf(a.Id), GroupOf(b.Id), StringComparison.OrdinalIgnoreCase);
                if (group != 0)
                    return group;

                return string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
            });
        }

        public static bool SpawnId(string id)
        {
            ItemRow row;
            if (string.IsNullOrEmpty(id) || !known.TryGetValue(id, out row) || row.Source == null)
                return false;

            Spawn(row.Source);
            return true;
        }

        public static void Draw()
        {
            MenuUi.Section("Loaded items");
            GUILayout.Label("remembers ids seen this session. gone means it isn't loaded right now.", MenuUi.Muted);
            GUILayout.Label(items.Count + " remembered", MenuUi.Label);
            spawnFilter = GUILayout.TextField(spawnFilter == null ? "" : spawnFilter, MenuUi.Field);
            GUILayout.Space(4f);

            if (GUILayout.Button(weaponsOnly ? "everything" : "weapons only", MenuUi.Button))
                weaponsOnly = !weaponsOnly;

            GUILayout.Space(4f);
            string filter = spawnFilter.Trim();
            int matched = 0;
            int shown = 0;
            string lastGroup = null;

            for (int i = 0; i < items.Count; i++)
            {
                ItemRow item = items[i];
                if (!Passes(item, filter))
                    continue;

                matched++;
                if (shown >= ShowCap)
                    continue;

                shown++;
                string group = GroupOf(item.Id);
                if (!string.Equals(group, lastGroup, StringComparison.OrdinalIgnoreCase))
                {
                    lastGroup = group;
                    GUILayout.Label(group, MenuUi.Muted);
                }

                bool alive = item.Source != null;
                GUILayout.BeginHorizontal();
                GUILayout.Label(
                    item.Label + "  [" + item.Id + "]  " + (alive ? (item.IsAsset ? "asset" : "scene") : "gone"),
                    MenuUi.Label);
                if (alive && GUILayout.Button("Spawn", MenuUi.Button, GUILayout.Width(72f)))
                    Spawn(item.Source);
                if (alive && GUILayout.Button("Inspect", MenuUi.Button, GUILayout.Width(72f)))
                {
                    FsmInspector.SelectObject(item.Source);
                    DebugMenu.SetTab(3);
                }

                GUILayout.EndHorizontal();
            }

            if (shown == 0)
                GUILayout.Label(weaponsOnly ? "no weapons in what's loaded" : "nothing matches", MenuUi.Label);
            else if (matched > shown)
                GUILayout.Label((matched - shown) + " more, type more of the name", MenuUi.Label);

            Loadouts.DrawKit();
        }

        private static string GroupOf(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "other";

            int split = id.IndexOf('_');
            if (split <= 0)
                return "other";

            return id.Substring(0, split);
        }

        private static bool Passes(ItemRow item, string filter)
        {
            if (weaponsOnly && !IsWeapon(item.Id, item.Label))
                return false;

            if (filter.Length == 0)
                return true;

            return MenuUi.Contains(item.Label, filter) || MenuUi.Contains(item.Id, filter);
        }

        private static bool IsWeapon(string id, string label)
        {
            for (int i = 0; i < WeaponWords.Length; i++)
            {
                if (MenuUi.Contains(id, WeaponWords[i]) || MenuUi.Contains(label, WeaponWords[i]))
                    return true;
            }

            return false;
        }

        private static void Spawn(GameObject source)
        {
            if (source == null || source.name == "Player")
                return;

            GameObject player = MenuUi.Player();
            Vector3 position = new Vector3(0f, 1f, 0f);
            Quaternion rotation = Quaternion.identity;
            if (player != null)
            {
                position = player.transform.position + player.transform.forward * 2.2f + Vector3.up;
                rotation = player.transform.rotation;
            }

            GameObject clone = UnityEngine.Object.Instantiate(source) as GameObject;
            if (clone == null)
                return;

            clone.transform.SetParent(null);
            clone.transform.position = position;
            clone.transform.rotation = rotation;
            clone.SetActive(true);
        }
    }
}
