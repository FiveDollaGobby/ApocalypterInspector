using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace ApocalypterInspector
{
    internal static class FsmInspector
    {
        private const int MatchCap = 16;
        private static readonly string[] PlayerFsms =
        {
            "Health",
            "Movement",
            "GODMODE",
            "GODMODEMovement",
            "Jump",
            "FallDamage",
            "SanityAdd",
            "SanityRemove",
            "InCar",
            "Sleep",
            "Bodypart"
        };

        private static readonly List<Match> matches = new List<Match>();
        private static string search = "";
        private static int matchCount;
        private static PlayMakerFSM selected;
        private static string note = "";

        private struct Match
        {
            public PlayMakerFSM Fsm;
            public string Title;
        }

        public static void Begin()
        {
            matches.Clear();
            matchCount = 0;
        }

        public static void Consider(PlayMakerFSM fsm)
        {
            if (!MenuUi.IsLive(fsm))
                return;

            string filter = search == null ? "" : search.Trim();
            if (filter.Length == 0)
                return;

            if (!MenuUi.Contains(fsm.gameObject.name, filter) && !MenuUi.Contains(fsm.FsmName, filter))
                return;

            matchCount++;
            if (matches.Count >= MatchCap)
                return;

            matches.Add(new Match
            {
                Fsm = fsm,
                Title = fsm.gameObject.name + "  /  " + fsm.FsmName
            });
        }

        public static void Draw()
        {
            MenuUi.Section("Search");
            GUILayout.BeginHorizontal();
            search = GUILayout.TextField(search ?? "", MenuUi.Field);
            if (GUILayout.Button("Find", MenuUi.Button, GUILayout.Width(64f)))
                DebugMenu.RequestRefresh();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            MenuUi.Section("On the player");
            DrawQuickPicks();
            GUILayout.Space(6f);

            if (!string.IsNullOrEmpty(note))
                GUILayout.Label(note, MenuUi.Label);

            if (search != null && search.Trim().Length == 0)
                GUILayout.Label("type a name, or hit one of those", MenuUi.Muted);
            else if (matches.Count == 0)
                GUILayout.Label("nothing", MenuUi.Label);
            else
                DrawMatches();

            GUILayout.Space(8f);
            DrawSelected();
        }

        public static void SelectObject(GameObject source)
        {
            if (source == null)
                return;

            PlayMakerFSM[] fsms = source.GetComponents<PlayMakerFSM>();
            PlayMakerFSM chosen = null;
            for (int i = 0; i < fsms.Length; i++)
            {
                if (fsms[i] == null)
                    continue;

                if (chosen == null)
                    chosen = fsms[i];

                if (fsms[i].FsmName == "ID")
                {
                    chosen = fsms[i];
                    break;
                }
            }

            if (chosen != null)
                Select(chosen);
        }

        private static void DrawQuickPicks()
        {
            int shown = 0;
            for (int i = 0; i < PlayerFsms.Length; i++)
            {
                if (shown % 3 == 0)
                {
                    if (shown > 0)
                        GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }

                string fsmName = PlayerFsms[i];
                if (GUILayout.Button(fsmName, MenuUi.Button))
                    FocusPlayer(fsmName);
                shown++;
            }

            if (shown > 0)
                GUILayout.EndHorizontal();
        }

        private static void DrawMatches()
        {
            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                if (match.Fsm == null)
                    continue;

                string title = match.Title;
                if (selected != null && match.Fsm.GetInstanceID() == selected.GetInstanceID())
                    title = "> " + title;

                GUILayout.BeginHorizontal();
                GUILayout.Label(title + "    " + MenuUi.ActiveState(match.Fsm), MenuUi.Label);
                if (GUILayout.Button("Select", MenuUi.Button, GUILayout.Width(72f)))
                    Select(match.Fsm);
                GUILayout.EndHorizontal();
            }

            if (matchCount > matches.Count)
                GUILayout.Label((matchCount - matches.Count) + " more, narrow it", MenuUi.Label);
        }

        private static void DrawSelected()
        {
            if (selected == null)
            {
                GUILayout.Label("nothing picked", MenuUi.Label);
                return;
            }

            GUILayout.Label(
                selected.gameObject.name + " / " + selected.FsmName +
                "   " + MenuUi.ActiveState(selected),
                MenuUi.Label);
            DrawStates();
            GUILayout.Space(6f);
            DrawEvents();
            GUILayout.Space(6f);
            DrawVariables();
            GUILayout.Space(6f);
            DrawComponents();
        }

        private static void DrawStates()
        {
            MenuUi.Section("States");
            FsmState[] states = selected.FsmStates;
            if ((states == null || states.Length == 0) && selected.Fsm != null)
                states = selected.Fsm.States;

            if (states == null || states.Length == 0)
            {
                GUILayout.Label("no states", MenuUi.Label);
                return;
            }

            int shown = 0;
            for (int i = 0; i < states.Length && shown < 30; i++)
            {
                if (states[i] == null || string.IsNullOrEmpty(states[i].Name))
                    continue;

                string stateName = states[i].Name;
                if (GUILayout.Button(stateName, MenuUi.Button))
                    TryState(stateName);
                shown++;
            }

            if (shown == 0)
                GUILayout.Label("no states", MenuUi.Label);
        }

        private static void DrawEvents()
        {
            MenuUi.Section("Events");
            FsmEvent[] events = selected.FsmEvents;
            if ((events == null || events.Length == 0) && selected.Fsm != null)
                events = selected.Fsm.Events;

            if (events == null || events.Length == 0)
            {
                GUILayout.Label("no events", MenuUi.Label);
                return;
            }

            int shown = 0;
            int available = 0;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i] == null || string.IsNullOrEmpty(events[i].Name))
                    continue;

                available++;
                if (shown >= 30)
                    continue;

                string eventName = events[i].Name;
                if (GUILayout.Button(eventName, MenuUi.Button))
                    TryEvent(eventName);
                shown++;
            }

            if (shown == 0)
                GUILayout.Label("no events", MenuUi.Label);
            else if (available > shown)
                GUILayout.Label((available - shown) + " more", MenuUi.Label);
        }

        private static void DrawVariables()
        {
            MenuUi.Section("Variables");
            int id = selected.GetInstanceID();
            DrawFloats(id);
            DrawInts(id);
            DrawBools();
            DrawStrings(id);
        }

        private static void DrawFloats(int id)
        {
            FsmFloat[] values = MenuUi.Floats(selected);
            if (values == null || values.Length == 0)
                return;

            int shown = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null)
                    continue;

                if (shown >= 30)
                {
                    GUILayout.Label((values.Length - shown) + " more floats", MenuUi.Label);
                    return;
                }

                FsmFloat variable = values[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(variable.Name, MenuUi.Label, GUILayout.Width(180f));
                string text = MenuUi.TextField(id + ":f:" + variable.Name, MenuUi.Format(variable.Value), 90f);
                if (GUILayout.Button("Set", MenuUi.Button, GUILayout.Width(52f)))
                    MenuUi.TrySet(variable, text);
                GUILayout.EndHorizontal();
                shown++;
            }
        }

        private static void DrawInts(int id)
        {
            FsmInt[] values = MenuUi.Ints(selected);
            if (values == null)
                return;

            int shown = 0;
            for (int i = 0; i < values.Length && shown < 20; i++)
            {
                if (values[i] == null)
                    continue;

                FsmInt variable = values[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(variable.Name, MenuUi.Label, GUILayout.Width(180f));
                string text = MenuUi.TextField(
                    id + ":i:" + variable.Name,
                    variable.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    90f);
                if (GUILayout.Button("Set", MenuUi.Button, GUILayout.Width(52f)))
                    MenuUi.TrySet(variable, text);
                GUILayout.EndHorizontal();
                shown++;
            }
        }

        private static void DrawBools()
        {
            FsmBool[] values = MenuUi.Bools(selected);
            if (values == null)
                return;

            int shown = 0;
            for (int i = 0; i < values.Length && shown < 20; i++)
            {
                if (values[i] == null)
                    continue;

                FsmBool variable = values[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(variable.Name + ": " + variable.Value, MenuUi.Label);
                if (GUILayout.Button(variable.Value ? "true" : "false", MenuUi.Button, GUILayout.Width(70f)))
                    variable.Value = !variable.Value;
                GUILayout.EndHorizontal();
                shown++;
            }
        }

        private static void DrawStrings(int id)
        {
            FsmString[] values = MenuUi.Strings(selected);
            if (values == null)
                return;

            int shown = 0;
            for (int i = 0; i < values.Length && shown < 12; i++)
            {
                if (values[i] == null)
                    continue;

                FsmString variable = values[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(variable.Name, MenuUi.Label, GUILayout.Width(140f));
                string text = MenuUi.TextField(id + ":s:" + variable.Name, variable.Value, 180f);
                if (GUILayout.Button("Set", MenuUi.Button, GUILayout.Width(52f)))
                    MenuUi.TrySet(variable, text);
                GUILayout.EndHorizontal();
                shown++;
            }
        }

        private static void DrawComponents()
        {
            MenuUi.Section("Components");
            if (selected.gameObject == null)
                return;

            Component[] components = selected.gameObject.GetComponents<Component>();
            int shown = 0;
            for (int i = 0; i < components.Length && shown < 40; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;

                string line = component.GetType().Name;
                PlayMakerFSM fsm = component as PlayMakerFSM;
                if (fsm != null)
                    line += "  (" + fsm.FsmName + ")";

                GUILayout.Label(line, MenuUi.Label);
                shown++;
            }
        }

        private static void FocusPlayer(string fsmName)
        {
            PlayMakerFSM[] found = Resources.FindObjectsOfTypeAll<PlayMakerFSM>();
            for (int i = 0; i < found.Length; i++)
            {
                PlayMakerFSM fsm = found[i];
                if (MenuUi.IsPlayer(fsm) && fsm.FsmName == fsmName)
                {
                    Select(fsm);
                    note = "";
                    return;
                }
            }

            note = fsmName + " isn't on the player";
        }

        private static void Select(PlayMakerFSM fsm)
        {
            selected = fsm;
            note = "";
            MenuUi.ClearFields();
        }

        private static void TryState(string stateName)
        {
            try
            {
                selected.SetState(stateName);
                note = "state " + stateName;
            }
            catch (Exception ex)
            {
                note = ex.Message;
            }
        }

        private static void TryEvent(string eventName)
        {
            try
            {
                selected.SendEvent(eventName);
                note = "sent " + eventName;
            }
            catch (Exception ex)
            {
                note = ex.Message;
            }
        }
    }
}
