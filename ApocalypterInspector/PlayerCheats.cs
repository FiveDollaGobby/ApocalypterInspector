using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace ApocalypterInspector
{
    internal static class PlayerCheats
    {
        private const int KeyW = 0x57;
        private const int KeyA = 0x41;
        private const int KeyS = 0x53;
        private const int KeyD = 0x44;
        private const int KeySpace = 0x20;
        private const int KeyCtrl = 0x11;

        private static PlayMakerFSM playerHealth;
        private static PlayMakerFSM godMode;
        private static FsmFloat playerHealthValue;
        private static FsmFloat moveHorizontal;
        private static FsmFloat moveVertical;
        private static FsmFloat godHorizontal;
        private static FsmFloat godVertical;
        private static FsmFloat gravity;
        private static FsmBool sprint;
        private static string healthInput = "100";
        private static readonly List<CanRow> cans = new List<CanRow>();
        private static readonly List<StatRow> stats = new List<StatRow>();
        private static readonly List<JumpRow> jumps = new List<JumpRow>();
        private static readonly Dictionary<string, float> lockedJumps = new Dictionary<string, float>();
        private static readonly List<Behaviour> pinned = new List<Behaviour>();
        private static readonly List<bool> pinnedWasOn = new List<bool>();

        private static bool speedLocked;
        private static float speedMultiplier = 1f;
        private static bool gravityLocked;
        private static float gravityValue = -9.81f;
        private static bool sprintLocked;
        private static bool sprintLockedValue;
        private static bool godLocked;
        private static string godState = "";
        private static bool infiniteHealth;
        private static bool infiniteNeeds;
        private static bool noclip;
        private static bool noclipApplied;
        private static int lastMoveMs = int.MinValue;
        private static string note = "";

        private struct CanRow
        {
            public string Name;
            public FsmFloat Liquid;
            public FsmFloat Capacity;
        }

        private struct StatRow
        {
            public string Label;
            public FsmFloat Value;
        }

        private struct JumpRow
        {
            public string Label;
            public FsmFloat Value;
            public bool IsInput;
        }

        private struct Mark
        {
            public bool Saved;
            public Vector3 Position;
        }

        private static readonly Mark[] marks = new Mark[3];

        public static bool NeedsScan()
        {
            return speedLocked ||
                gravityLocked ||
                sprintLocked ||
                godLocked ||
                infiniteHealth ||
                infiniteNeeds ||
                noclip ||
                lockedJumps.Count > 0;
        }

        public static void Begin()
        {
            playerHealth = null;
            playerHealthValue = null;
            godMode = null;
            moveHorizontal = null;
            moveVertical = null;
            godHorizontal = null;
            godVertical = null;
            gravity = null;
            sprint = null;
            cans.Clear();
            stats.Clear();
            jumps.Clear();
        }

        public static void Consider(PlayMakerFSM fsm)
        {
            if (!MenuUi.IsPlayer(fsm))
            {
                if (MenuUi.IsLive(fsm) &&
                    fsm.gameObject.name.StartsWith("Gasoline_Can", StringComparison.Ordinal) &&
                    fsm.FsmName == "LiquidAmount")
                {
                    cans.Add(new CanRow
                    {
                        Name = fsm.gameObject.name,
                        Liquid = MenuUi.FindFloat(fsm, "Liquid"),
                        Capacity = MenuUi.FindFloat(fsm, "LiquidCapacity")
                    });
                }

                return;
            }

            string fsmName = fsm.FsmName;
            if (fsmName == "Health")
            {
                playerHealth = fsm;
                playerHealthValue = MenuUi.FindFloat(fsm, "Health");
            }
            else if (fsmName == "Movement")
            {
                moveHorizontal = MenuUi.FindFloat(fsm, "HorizontalMultiply");
                moveVertical = MenuUi.FindFloat(fsm, "VerticalMultiply");
                gravity = MenuUi.FindFloat(fsm, "Gravity");
                sprint = MenuUi.FindBool(fsm, "isRunning");
            }
            else if (fsmName == "GODMODEMovement")
            {
                godHorizontal = MenuUi.FindFloat(fsm, "HorizontalMultiply");
                godVertical = MenuUi.FindFloat(fsm, "VerticalMultiply");
            }
            else if (fsmName == "GODMODE")
            {
                godMode = fsm;
            }

            CollectNeeds(fsm, fsmName);
            CollectJumps(fsm, fsmName, fsmName == "Jump");
        }

        public static void Draw()
        {
            DrawHealth();
            DrawSpeed();
            DrawStats();
            DrawGodMode();
            DrawJumps();
            DrawTeleport();
            DrawNoclip();
            DrawCans();
            Loadouts.Draw();

            if (!string.IsNullOrEmpty(note))
            {
                GUILayout.Space(6f);
                GUILayout.Label(note, MenuUi.Label);
            }
        }

        public static void ApplyLatches(bool movePlayer)
        {
            if (noclip && !noclipApplied)
                BeginNoclip();
            else if (!noclip && noclipApplied)
                EndNoclip();

            if (speedLocked)
                WriteSpeed();

            if (gravityLocked && !noclipApplied)
                MenuUi.Write(gravity, gravityValue);

            if (sprintLocked && sprint != null)
                sprint.Value = sprintLockedValue;

            if (godLocked && godMode != null && !string.IsNullOrEmpty(godState))
            {
                string active = godMode.ActiveStateName;
                if (active != godState)
                {
                    try
                    {
                        godMode.SetState(godState);
                    }
                    catch (Exception ex)
                    {
                        note = ex.Message;
                    }
                }
            }

            if (infiniteHealth)
                MenuUi.Write(playerHealthValue, 100f);

            if (infiniteNeeds)
            {
                for (int i = 0; i < stats.Count; i++)
                    MenuUi.Write(stats[i].Value, 100f);
            }

            for (int i = 0; i < jumps.Count; i++)
            {
                float locked;
                if (jumps[i].Value != null && lockedJumps.TryGetValue(jumps[i].Label, out locked))
                    MenuUi.Write(jumps[i].Value, locked);
            }

            if (!noclipApplied)
                return;

            PinBodies();
            MenuUi.Write(gravity, 0f);
            if (movePlayer && string.IsNullOrEmpty(MenuUi.FocusedName))
                MoveNoclip();
        }

        private static void DrawHealth()
        {
            MenuUi.Section("Health");
            if (playerHealthValue == null)
            {
                GUILayout.Label("no health fsm yet", MenuUi.Label);
                return;
            }

            GUILayout.Label(
                MenuUi.ActiveState(playerHealth) + "   " + MenuUi.Format(playerHealthValue.Value),
                MenuUi.Label);

            GUILayout.BeginHorizontal();
            healthInput = MenuUi.TextField("player-health", MenuUi.Format(playerHealthValue.Value), 120f);
            if (GUILayout.Button("Set", MenuUi.Button, GUILayout.Width(64f)))
                MenuUi.TrySet(playerHealthValue, healthInput);
            if (GUILayout.Button("100", MenuUi.Button, GUILayout.Width(52f)))
                MenuUi.Set(playerHealthValue, 100f);
            if (GUILayout.Button("25", MenuUi.Button, GUILayout.Width(52f)))
                MenuUi.Set(playerHealthValue, 25f);
            if (GUILayout.Button("1", MenuUi.Button, GUILayout.Width(52f)))
                MenuUi.Set(playerHealthValue, 1f);
            if (GUILayout.Button(infiniteHealth ? "inf on" : "inf", MenuUi.Button))
                infiniteHealth = !infiniteHealth;
            GUILayout.EndHorizontal();
        }

        private static void DrawSpeed()
        {
            MenuUi.Section("Move");
            float current = moveHorizontal != null ? moveHorizontal.Value : 0f;
            GUILayout.Label(
                "speed " + MenuUi.Format(current / 2f) + "x" +
                (speedLocked ? " locked" : "") +
                "    grav " + (gravity != null ? MenuUi.Format(gravity.Value) : "?") +
                (gravityLocked ? " locked" : ""),
                MenuUi.Label);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1x", MenuUi.Button))
                ChooseSpeed(1f);
            if (GUILayout.Button("2x", MenuUi.Button))
                ChooseSpeed(2f);
            if (GUILayout.Button("4x", MenuUi.Button))
                ChooseSpeed(4f);
            if (GUILayout.Button("8x", MenuUi.Button))
                ChooseSpeed(8f);
            if (GUILayout.Button(speedLocked ? "unlock speed" : "lock speed", MenuUi.Button))
            {
                if (!speedLocked && moveHorizontal != null && moveHorizontal.Value > 0.1f)
                    speedMultiplier = moveHorizontal.Value / 2f;
                speedLocked = !speedLocked;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("normal", MenuUi.Button))
                ChooseGravity(-9.81f);
            if (GUILayout.Button("low", MenuUi.Button))
                ChooseGravity(-3f);
            if (GUILayout.Button("moon", MenuUi.Button))
                ChooseGravity(-1f);
            if (GUILayout.Button(gravityLocked ? "unlock grav" : "lock grav", MenuUi.Button))
            {
                if (!gravityLocked && gravity != null)
                    gravityValue = gravity.Value;
                gravityLocked = !gravityLocked;
            }
            GUILayout.EndHorizontal();

            if (sprint != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(
                    "sprint " + sprint.Value + (sprintLocked ? " locked" : ""),
                    MenuUi.Label);
                if (GUILayout.Button(sprint.Value ? "walk" : "sprint", MenuUi.Button))
                {
                    sprintLockedValue = !sprint.Value;
                    sprint.Value = sprintLockedValue;
                }
                if (GUILayout.Button(sprintLocked ? "unlock" : "lock", MenuUi.Button))
                {
                    if (!sprintLocked)
                        sprintLockedValue = sprint.Value;
                    sprintLocked = !sprintLocked;
                }
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawStats()
        {
            MenuUi.Section("Needs");
            if (GUILayout.Button(infiniteNeeds ? "needs locked" : "lock needs", MenuUi.Button))
                infiniteNeeds = !infiniteNeeds;

            if (stats.Count == 0)
            {
                GUILayout.Label("no hunger/thirst/fatigue floats on the player. names might be different, check inspect.", MenuUi.Label);
                return;
            }

            for (int i = 0; i < stats.Count; i++)
            {
                StatRow stat = stats[i];
                if (stat.Value == null)
                    continue;

                GUILayout.Label(stat.Label + ": " + MenuUi.Format(stat.Value.Value), MenuUi.Label);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("0", MenuUi.Button))
                    MenuUi.Set(stat.Value, 0f);
                if (GUILayout.Button("50", MenuUi.Button))
                    MenuUi.Set(stat.Value, 50f);
                if (GUILayout.Button("100", MenuUi.Button))
                    MenuUi.Set(stat.Value, 100f);
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawGodMode()
        {
            MenuUi.Section("Godmode");
            if (godMode == null || godMode.Fsm == null)
            {
                GUILayout.Label("no godmode fsm on the player", MenuUi.Label);
                return;
            }

            GUILayout.Label(
                MenuUi.ActiveState(godMode) + (godLocked ? "  (stuck)" : ""),
                MenuUi.Label);

            FsmState[] states = godMode.Fsm.States;
            int shown = 0;
            if (states != null)
            {
                for (int i = 0; i < states.Length; i++)
                {
                    if (states[i] == null || string.IsNullOrEmpty(states[i].Name))
                        continue;

                    if (shown % 3 == 0)
                    {
                        if (shown > 0)
                            GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                    }

                    string stateName = states[i].Name;
                    if (GUILayout.Button(stateName, MenuUi.Button))
                        ChooseGod(stateName);
                    shown++;
                }
            }

            if (shown > 0)
                GUILayout.EndHorizontal();

            if (GUILayout.Button(godLocked ? "let it change" : "keep this state", MenuUi.Button))
            {
                if (!godLocked && string.IsNullOrEmpty(godState))
                    godState = godMode.ActiveStateName ?? "";
                godLocked = !godLocked && !string.IsNullOrEmpty(godState);
            }
        }

        private static void DrawJumps()
        {
            MenuUi.Section("Jump");
            if (jumps.Count == 0)
            {
                GUILayout.Label("no jump floats on the player", MenuUi.Label);
                return;
            }

            GUILayout.Label("the float named Jump is the button, not the height. it sits at 0 on the ground.", MenuUi.Muted);
            for (int i = 0; i < jumps.Count; i++)
            {
                JumpRow row = jumps[i];
                if (row.Value == null)
                    continue;

                bool locked = lockedJumps.ContainsKey(row.Label);
                string caption = row.Label + (row.IsInput ? " (button)" : "") + " " + MenuUi.Format(row.Value.Value);
                GUILayout.Label(caption + (locked ? "  locked" : ""), MenuUi.Label);
                GUILayout.BeginHorizontal();
                string text = MenuUi.TextField("jump:" + row.Label, MenuUi.Format(row.Value.Value), 90f);
                if (GUILayout.Button("Set", MenuUi.Button, GUILayout.Width(52f)))
                {
                    MenuUi.TrySet(row.Value, text);
                    float parsed;
                    if (locked && MenuUi.TryParse(text, out parsed))
                        lockedJumps[row.Label] = parsed;
                }

                if (GUILayout.Button(locked ? "unlock" : "lock", MenuUi.Button, GUILayout.Width(70f)))
                {
                    if (locked)
                        lockedJumps.Remove(row.Label);
                    else
                        lockedJumps[row.Label] = row.Value.Value;
                }

                GUILayout.EndHorizontal();
            }
        }

        private static void DrawTeleport()
        {
            MenuUi.Section("Bookmarks");
            GameObject player = MenuUi.Player();
            if (player == null)
                GUILayout.Label("no player", MenuUi.Label);
            else
                DrawSlots(player);

            SavedPlaces.Draw(player);
        }

        private static void DrawSlots(GameObject player)
        {

            for (int i = 0; i < marks.Length; i++)
            {
                Mark mark = marks[i];
                string where = mark.Saved
                    ? mark.Position.x.ToString("0.#") + ", " + mark.Position.y.ToString("0.#") + ", " + mark.Position.z.ToString("0.#")
                    : "empty";
                GUILayout.BeginHorizontal();
                GUILayout.Label("slot " + (i + 1) + "  " + where, MenuUi.Label);
                if (GUILayout.Button("Save", MenuUi.Button, GUILayout.Width(64f)))
                {
                    mark.Saved = true;
                    mark.Position = player.transform.position;
                    marks[i] = mark;
                }

                if (GUILayout.Button("Load", MenuUi.Button, GUILayout.Width(64f)) && mark.Saved)
                    player.transform.position = mark.Position;
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawNoclip()
        {
            MenuUi.Section("Fly");
            string status = noclip ? "on" : "off";
            if (noclip && !noclipApplied)
                status = "no player yet";
            GUILayout.Label(status + "    wasd, space up, ctrl down", MenuUi.Muted);
            if (GUILayout.Button(noclip ? "stop" : "fly", MenuUi.Button))
                noclip = !noclip;
        }

        private static void DrawCans()
        {
            MenuUi.Section("Gas cans");
            if (cans.Count == 0)
            {
                GUILayout.Label("none loaded", MenuUi.Label);
                return;
            }

            for (int i = 0; i < cans.Count; i++)
            {
                CanRow can = cans[i];
                float capacity = can.Capacity != null ? can.Capacity.Value : 0f;
                float liquid = can.Liquid != null ? can.Liquid.Value : 0f;
                GUILayout.Label(
                    can.Name + "    " + MenuUi.Format(liquid) + " / " + MenuUi.Format(capacity),
                    MenuUi.Label);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Empty to 1", MenuUi.Button) && can.Liquid != null)
                    MenuUi.Set(can.Liquid, 1f);
                if (GUILayout.Button("Fill", MenuUi.Button) && can.Liquid != null)
                    MenuUi.Set(can.Liquid, capacity > 0f ? capacity : 20f);
                GUILayout.EndHorizontal();
            }
        }

        private static void CollectNeeds(PlayMakerFSM fsm, string fsmName)
        {
            FsmFloat[] values = MenuUi.Floats(fsm);
            if (values == null)
                return;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null || !IsNeed(values[i].Name) || stats.Count >= 12)
                    continue;

                if (HasStat(values[i]))
                    continue;

                stats.Add(new StatRow
                {
                    Label = fsmName + " / " + values[i].Name,
                    Value = values[i]
                });
            }
        }

        private static void CollectJumps(PlayMakerFSM fsm, string fsmName, bool allFloats)
        {
            FsmFloat[] values = MenuUi.Floats(fsm);
            if (values == null)
                return;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null || jumps.Count >= 16)
                    continue;

                bool namedJump = MenuUi.Contains(values[i].Name, "Jump");
                if (!allFloats && !namedJump)
                    continue;

                if (HasJump(values[i]))
                    continue;

                jumps.Add(new JumpRow
                {
                    Label = fsmName + " / " + values[i].Name,
                    Value = values[i],
                    IsInput = values[i].Name == "Jump"
                });
            }
        }

        private static bool HasStat(FsmFloat value)
        {
            for (int i = 0; i < stats.Count; i++)
            {
                if (ReferenceEquals(stats[i].Value, value))
                    return true;
            }

            return false;
        }

        private static bool HasJump(FsmFloat value)
        {
            for (int i = 0; i < jumps.Count; i++)
            {
                if (ReferenceEquals(jumps[i].Value, value))
                    return true;
            }

            return false;
        }

        private static bool IsNeed(string name)
        {
            if (string.IsNullOrEmpty(name) || name == "Health")
                return false;

            return MenuUi.Contains(name, "Hunger") ||
                MenuUi.Contains(name, "Thirst") ||
                MenuUi.Contains(name, "Fatigue") ||
                MenuUi.Contains(name, "Sanity") ||
                MenuUi.Contains(name, "Armor") ||
                MenuUi.Contains(name, "Damage") ||
                MenuUi.Contains(name, "Stamina");
        }

        public static void Capture(
            out float health,
            out bool hasHealth,
            out float speed,
            out float gravityOut,
            out bool sprintOn,
            out bool hasSprint,
            out string god,
            out bool infHealth,
            out bool infNeeds)
        {
            hasHealth = playerHealthValue != null;
            health = hasHealth ? playerHealthValue.Value : 100f;
            speed = speedLocked || moveHorizontal == null ? speedMultiplier : moveHorizontal.Value / 2f;
            gravityOut = gravityLocked || gravity == null ? gravityValue : gravity.Value;
            hasSprint = sprint != null;
            sprintOn = hasSprint && sprint.Value;
            god = "";
            if (godLocked && !string.IsNullOrEmpty(godState))
                god = godState;
            else if (godMode != null && !string.IsNullOrEmpty(godMode.ActiveStateName))
                god = godMode.ActiveStateName;
            infHealth = infiniteHealth;
            infNeeds = infiniteNeeds;
        }

        public static void ApplyLoadout(
            float health,
            bool hasHealth,
            float speed,
            float grav,
            bool sprintOn,
            bool hasSprint,
            string god,
            bool infHealth,
            bool infNeeds)
        {
            if (hasHealth)
                MenuUi.Write(playerHealthValue, health);

            infiniteHealth = infHealth;
            infiniteNeeds = infNeeds;
            speedMultiplier = speed;
            speedLocked = true;
            WriteSpeed();
            gravityValue = grav;
            gravityLocked = true;
            MenuUi.Write(gravity, grav);

            if (hasSprint && sprint != null)
            {
                sprintLockedValue = sprintOn;
                sprintLocked = true;
                sprint.Value = sprintOn;
            }

            if (!string.IsNullOrEmpty(god))
            {
                godState = god;
                godLocked = true;
                try
                {
                    if (godMode != null)
                        godMode.SetState(god);
                }
                catch (Exception ex)
                {
                    note = ex.Message;
                }
            }
        }

        private static void ChooseSpeed(float multiplier)
        {
            speedMultiplier = multiplier;
            WriteSpeed();
            DebugMenu.RequestRefresh();
        }

        private static void WriteSpeed()
        {
            float value = 2f * speedMultiplier;
            MenuUi.Write(moveHorizontal, value);
            MenuUi.Write(moveVertical, value);
            MenuUi.Write(godHorizontal, value);
            MenuUi.Write(godVertical, value);
        }

        private static void ChooseGravity(float value)
        {
            gravityValue = value;
            MenuUi.Set(gravity, value);
        }

        private static void ChooseGod(string state)
        {
            godState = state;
            try
            {
                if (godMode != null)
                    godMode.SetState(state);
                note = "";
            }
            catch (Exception ex)
            {
                note = ex.Message;
            }
        }

        private static void BeginNoclip()
        {
            GameObject player = MenuUi.Player();
            if (player == null)
                return;

            pinned.Clear();
            pinnedWasOn.Clear();
            Component[] all = player.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Component component = all[i];
                Behaviour behaviour = component as Behaviour;
                if (behaviour == null)
                    continue;

                bool pin = component.GetType().Name == "CharacterController";
                PlayMakerFSM fsm = component as PlayMakerFSM;
                if (fsm != null && (fsm.FsmName == "Movement" || fsm.FsmName == "GODMODEMovement"))
                    pin = true;

                if (!pin)
                    continue;

                pinned.Add(behaviour);
                pinnedWasOn.Add(behaviour.enabled);
                behaviour.enabled = false;
            }

            noclipApplied = true;
            lastMoveMs = int.MinValue;
        }

        private static void EndNoclip()
        {
            for (int i = 0; i < pinned.Count; i++)
            {
                if (pinned[i] != null)
                    pinned[i].enabled = pinnedWasOn[i];
            }

            pinned.Clear();
            pinnedWasOn.Clear();
            noclipApplied = false;
            if (!gravityLocked)
                MenuUi.Write(gravity, -9.81f);
        }

        private static void PinBodies()
        {
            for (int i = 0; i < pinned.Count; i++)
            {
                if (pinned[i] != null)
                    pinned[i].enabled = false;
            }
        }

        private static void MoveNoclip()
        {
            GameObject player = MenuUi.Player();
            if (player == null)
                return;

            int now = Environment.TickCount;
            float dt = 0.016f;
            if (lastMoveMs != int.MinValue)
            {
                int elapsed = unchecked(now - lastMoveMs);
                if (elapsed > 0 && elapsed < 250)
                    dt = elapsed / 1000f;
            }

            lastMoveMs = now;
            Transform basis = player.transform;
            if (Camera.main != null)
                basis = Camera.main.transform;

            Vector3 flat = Vector3.ProjectOnPlane(basis.forward, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f)
                flat = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);

            Vector3 delta = Vector3.zero;
            if (flat.sqrMagnitude > 0.0001f)
            {
                flat.Normalize();
                Vector3 right = Vector3.ProjectOnPlane(basis.right, Vector3.up);
                if (right.sqrMagnitude < 0.0001f)
                    right = Vector3.Cross(Vector3.up, flat);
                right.Normalize();

                if (MenuUi.KeyDown(KeyW))
                    delta += flat;
                if (MenuUi.KeyDown(KeyS))
                    delta -= flat;
                if (MenuUi.KeyDown(KeyD))
                    delta += right;
                if (MenuUi.KeyDown(KeyA))
                    delta -= right;
            }
            if (MenuUi.KeyDown(KeySpace))
                delta += Vector3.up;
            if (MenuUi.KeyDown(KeyCtrl))
                delta -= Vector3.up;

            if (delta.sqrMagnitude < 0.0001f)
                return;

            player.transform.position += delta.normalized * (12f * dt);
        }
    }
}
