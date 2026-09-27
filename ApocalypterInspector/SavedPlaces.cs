using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using UnityEngine;

namespace ApocalypterInspector
{
    internal static class SavedPlaces
    {
        private const int Cap = 40;

        private static readonly List<Place> places = new List<Place>();
        private static bool loaded;
        private static string placeName = "";
        private static string coords = "";
        private static string note = "";

        private struct Place
        {
            public string Name;
            public Vector3 Position;
        }

        public static void Draw(GameObject player)
        {
            EnsureLoaded();
            MenuUi.Section("Places");
            GUILayout.Label("saved in BepInEx/ApocalypterInspector-places.txt", MenuUi.Muted);

            GUILayout.BeginHorizontal();
            placeName = MenuUi.TextField("place-name", placeName, 160f);
            if (GUILayout.Button("Save here", MenuUi.Button, GUILayout.Width(90f)))
                SaveHere(player);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            coords = MenuUi.TextField("place-coords", coords, 160f);
            if (GUILayout.Button("Go", MenuUi.Button, GUILayout.Width(90f)))
                GoCoords(player);
            GUILayout.EndHorizontal();
            GUILayout.Label("coords are x y z", MenuUi.Muted);

            if (!string.IsNullOrEmpty(note))
                GUILayout.Label(note, MenuUi.Label);

            for (int i = 0; i < places.Count; i++)
            {
                Place place = places[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(
                    place.Name + "   " +
                    place.Position.x.ToString("0.#") + " " +
                    place.Position.y.ToString("0.#") + " " +
                    place.Position.z.ToString("0.#"),
                    MenuUi.Label);
                if (GUILayout.Button("Go", MenuUi.Button, GUILayout.Width(48f)))
                    Move(player, place.Position);
                if (GUILayout.Button("x", MenuUi.Button, GUILayout.Width(32f)))
                {
                    places.RemoveAt(i);
                    Write();
                    i--;
                }

                GUILayout.EndHorizontal();
            }
        }

        private static void SaveHere(GameObject player)
        {
            if (player == null)
            {
                note = "no player";
                return;
            }

            string name = string.IsNullOrEmpty(placeName) ? "place" : placeName.Trim();
            name = name.Replace("\t", " ");
            if (places.Count >= Cap && IndexOf(name) < 0)
            {
                note = "40 places already, delete one";
                return;
            }

            Place place = new Place
            {
                Name = name,
                Position = player.transform.position
            };
            int existing = IndexOf(name);
            if (existing >= 0)
                places[existing] = place;
            else
                places.Add(place);

            Write();
            note = "saved " + name;
        }

        private static void GoCoords(GameObject player)
        {
            string[] bits = (coords ?? "").Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            float x;
            float y;
            float z;
            if (bits.Length < 3 ||
                !float.TryParse(bits[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                !float.TryParse(bits[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
                !float.TryParse(bits[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
            {
                note = "need three numbers";
                return;
            }

            Move(player, new Vector3(x, y, z));
        }

        private static void Move(GameObject player, Vector3 position)
        {
            if (player == null)
            {
                note = "no player";
                return;
            }

            player.transform.position = position;
            note = "";
        }

        private static int IndexOf(string name)
        {
            for (int i = 0; i < places.Count; i++)
            {
                if (string.Equals(places[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static void EnsureLoaded()
        {
            if (loaded)
                return;

            loaded = true;
            places.Clear();
            try
            {
                string path = Path.Combine(Paths.BepInExRootPath, "ApocalypterInspector-places.txt");
                if (!File.Exists(path))
                    return;

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length && places.Count < Cap; i++)
                {
                    string[] bits = lines[i].Split('\t');
                    float x;
                    float y;
                    float z;
                    if (bits.Length < 4 ||
                        string.IsNullOrEmpty(bits[0]) ||
                        !float.TryParse(bits[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                        !float.TryParse(bits[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
                        !float.TryParse(bits[3], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                        continue;

                    places.Add(new Place
                    {
                        Name = bits[0],
                        Position = new Vector3(x, y, z)
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
                string path = Path.Combine(Paths.BepInExRootPath, "ApocalypterInspector-places.txt");
                var lines = new List<string>();
                for (int i = 0; i < places.Count; i++)
                {
                    Place place = places[i];
                    lines.Add(
                        place.Name + "\t" +
                        place.Position.x.ToString(CultureInfo.InvariantCulture) + "\t" +
                        place.Position.y.ToString(CultureInfo.InvariantCulture) + "\t" +
                        place.Position.z.ToString(CultureInfo.InvariantCulture));
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
