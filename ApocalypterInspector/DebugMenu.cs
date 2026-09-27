using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ApocalypterInspector
{
    public class DebugMenu : MonoBehaviour
    {
        private const int InsertKey = 0x2D;

        private static DebugMenu instance;
        private static bool loggedScanError;
        private static bool visible;
        private static bool componentOwnsGui;
        private static bool insertWasDown;
        private static bool cursorHeld;
        private static CursorLockMode savedLock;
        private static bool savedCursorVisible;
        private static float nextRefresh;
        private static int tab;
        private static Vector2 scroll;
        private static Rect windowRect = new Rect(24f, 24f, 700f, 740f);
        private static Texture2D pixel;

        public static void Pump()
        {
            Ensure();
            PollHotkeys();

            if ((visible || PlayerCheats.NeedsScan()) && Time.unscaledTime >= nextRefresh)
                Refresh();

            PlayerCheats.ApplyLatches(true);
            WorldTools.ApplyLatches();

            if (visible && cursorHeld)
                HoldCursor();
        }

        public static void DrawFromInjected()
        {
            if (componentOwnsGui)
                return;

            Draw();
        }

        private void OnGUI()
        {
            componentOwnsGui = true;
            Draw();
        }

        public static void Draw()
        {
            PollHotkeys();
            MenuUi.EnsureStyles(ref pixel);

            if ((visible || PlayerCheats.NeedsScan()) && Time.unscaledTime >= nextRefresh && ReadyToScan())
                Refresh();

            Matrix4x4 matrix = GUI.matrix;
            Color color = GUI.color;
            int depth = GUI.depth;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
            GUI.depth = -1000;

            try
            {
                if (!visible)
                {
                    GUI.Label(
                        new Rect(14f, 10f, 78f, 24f),
                        "insert",
                        MenuUi.Chip);
                    return;
                }

                windowRect = GUILayout.Window(
                    918273,
                    windowRect,
                    DrawWindow,
                    "inspector",
                    MenuUi.Window);
            }
            finally
            {
                GUI.depth = depth;
                GUI.color = color;
                GUI.matrix = matrix;

                if (visible)
                {
                    HoldCursor();
                    if (Event.current != null && Event.current.type == EventType.Repaint)
                        MenuUi.FocusedName = GUI.GetNameOfFocusedControl();
                }
                else
                {
                    MenuUi.FocusedName = null;
                }

                if (PlayerCheats.NeedsScan())
                    PlayerCheats.ApplyLatches(false);

                WorldTools.ApplyLatches();
            }
        }

        internal static void SetTab(int index)
        {
            if (index >= 0 && index <= 3)
                tab = index;
        }

        internal static void RequestRefresh()
        {
            nextRefresh = 0f;
        }

        internal static void Refresh()
        {
            nextRefresh = Time.unscaledTime + 0.4f;
            PlayMakerFSM[] found = Resources.FindObjectsOfTypeAll<PlayMakerFSM>();
            var labels = new Dictionary<int, string>();

            for (int i = 0; i < found.Length; i++)
            {
                PlayMakerFSM fsm = found[i];
                if (fsm == null || fsm.gameObject == null || fsm.FsmName != "ItemName")
                    continue;

                string itemName = MenuUi.StringValue(fsm, "ItemName");
                if (!string.IsNullOrEmpty(itemName))
                    labels[fsm.gameObject.GetInstanceID()] = itemName;
            }

            PlayerCheats.Begin();
            WorldTools.Begin();
            ItemSpawner.Begin();
            FsmInspector.Begin();

            for (int i = 0; i < found.Length; i++)
            {
                PlayMakerFSM fsm = found[i];
                if (fsm == null || fsm.gameObject == null)
                    continue;

                try
                {
                    PlayerCheats.Consider(fsm);
                    WorldTools.Consider(fsm);
                    ItemSpawner.Consider(fsm, labels);
                    FsmInspector.Consider(fsm);
                }
                catch (Exception ex)
                {
                    if (!loggedScanError)
                    {
                        loggedScanError = true;
                        UnityEngine.Debug.LogWarning("inspector scan failed: " + ex.Message);
                    }
                }
            }

            ItemSpawner.Finish();
            WorldTools.Finish();
        }

        private static void DrawWindow(int id)
        {
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("insert to close. drag the title to move it.", MenuUi.Muted);
            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();
            if (MenuUi.Tab("Player", tab == 0))
                tab = 0;
            if (MenuUi.Tab("World", tab == 1))
                tab = 1;
            if (MenuUi.Tab("Spawn", tab == 2))
                tab = 2;
            if (MenuUi.Tab("Inspect", tab == 3))
                tab = 3;
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            MenuUi.Rule();

            if (tab == 0)
                PlayerCheats.Draw();
            else if (tab == 1)
                WorldTools.Draw();
            else if (tab == 2)
                ItemSpawner.Draw();
            else
                FsmInspector.Draw();

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private static void Ensure()
        {
            if (instance != null)
                return;

            GameObject host = new GameObject("debug");
            UnityEngine.Object.DontDestroyOnLoad(host);
            instance = host.AddComponent<DebugMenu>();
        }

        private static void PollHotkeys()
        {
            if (Pressed(InsertKey, ref insertWasDown))
                SetVisible(!visible);
        }

        private static void SetVisible(bool show)
        {
            if (show && !cursorHeld)
            {
                savedLock = Cursor.lockState;
                savedCursorVisible = Cursor.visible;
                cursorHeld = true;
            }

            visible = show;
            if (show)
            {
                HoldCursor();
                nextRefresh = 0f;
                return;
            }

            if (!cursorHeld)
                return;

            Cursor.lockState = savedLock;
            Cursor.visible = savedCursorVisible;
            cursorHeld = false;
        }

        private static void HoldCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static bool ReadyToScan()
        {
            Event current = Event.current;
            return current == null || current.type == EventType.Layout;
        }

        private static bool Pressed(int virtualKey, ref bool wasDown)
        {
            bool down = MenuUi.KeyDown(virtualKey);
            bool pressed = down && !wasDown;
            wasDown = down;
            return pressed;
        }
    }

    internal static class MenuUi
    {
        private static readonly Dictionary<string, string> fields = new Dictionary<string, string>();

        public static string FocusedName;
        public static GUIStyle Window;
        public static GUIStyle Label;
        public static GUIStyle Muted;
        public static GUIStyle Header;
        public static GUIStyle Button;
        public static GUIStyle Field;
        public static GUIStyle Chip;

        private static GUIStyle tab;
        private static GUIStyle tabOn;
        private static GUIStyle rule;
        private static Texture2D panelTex;
        private static Texture2D buttonTex;
        private static Texture2D buttonHotTex;
        private static Texture2D buttonDownTex;
        private static Texture2D tabOnTex;
        private static Texture2D fieldTex;
        private static Texture2D chipTex;
        private static Texture2D ruleTex;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public static bool KeyDown(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        public static void Section(string title)
        {
            GUILayout.Space(10f);
            GUILayout.Label(title, Header);
        }

        public static bool Tab(string title, bool selected)
        {
            return GUILayout.Button(title, selected ? tabOn : tab, GUILayout.Height(26f));
        }

        public static void Rule()
        {
            GUILayout.Label(GUIContent.none, rule, GUILayout.Height(1f));
        }

        public static void EnsureStyles(ref Texture2D pixel)
        {
            if (Window != null)
                return;

            Color ink = new Color(0.91f, 0.87f, 0.78f);
            Color dim = new Color(0.62f, 0.56f, 0.46f);
            Color accent = new Color(0.93f, 0.72f, 0.38f);

            panelTex = Solid(new Color(0.09f, 0.08f, 0.07f, 0.96f));
            buttonTex = Solid(new Color(0.20f, 0.17f, 0.14f, 1f));
            buttonHotTex = Solid(new Color(0.30f, 0.25f, 0.18f, 1f));
            buttonDownTex = Solid(new Color(0.42f, 0.30f, 0.14f, 1f));
            tabOnTex = Solid(accent);
            fieldTex = Solid(new Color(0.05f, 0.05f, 0.045f, 1f));
            chipTex = Solid(new Color(0.09f, 0.08f, 0.07f, 0.82f));
            ruleTex = Solid(new Color(0.93f, 0.72f, 0.38f, 0.55f));
            pixel = panelTex;

            Window = new GUIStyle(GUI.skin.window);
            Paint(Window, panelTex, accent);
            Window.fontSize = 15;
            Window.padding = new RectOffset(12, 12, 28, 12);
            Window.border = new RectOffset(0, 0, 0, 0);
            Window.overflow = new RectOffset(0, 0, 0, 0);

            Label = new GUIStyle(GUI.skin.label);
            Label.normal.textColor = ink;
            Label.fontSize = 13;
            Label.wordWrap = true;
            Label.margin = new RectOffset(2, 2, 1, 1);

            Muted = new GUIStyle(Label);
            Muted.normal.textColor = dim;
            Muted.fontSize = 12;

            Header = new GUIStyle(Label);
            Header.normal.textColor = accent;
            Header.fontSize = 15;
            Header.fontStyle = FontStyle.Bold;
            Header.margin = new RectOffset(2, 2, 0, 2);

            Button = new GUIStyle(GUI.skin.button);
            Paint(Button, buttonTex, ink);
            Button.hover.background = buttonHotTex;
            Button.active.background = buttonDownTex;
            Button.onNormal.background = buttonDownTex;
            Button.fontSize = 13;
            Button.padding = new RectOffset(8, 8, 5, 5);
            Button.margin = new RectOffset(2, 2, 2, 2);
            Button.border = new RectOffset(0, 0, 0, 0);
            Button.alignment = TextAnchor.MiddleCenter;

            tab = new GUIStyle(Button);
            tabOn = new GUIStyle(Button);
            Paint(tabOn, tabOnTex, new Color(0.14f, 0.10f, 0.05f));
            tabOn.hover.background = tabOnTex;
            tabOn.active.background = tabOnTex;
            tabOn.fontStyle = FontStyle.Bold;

            Field = new GUIStyle(GUI.skin.textField);
            Paint(Field, fieldTex, ink);
            Field.fontSize = 13;
            Field.padding = new RectOffset(6, 6, 4, 4);
            Field.margin = new RectOffset(2, 2, 2, 2);
            Field.border = new RectOffset(0, 0, 0, 0);

            Chip = new GUIStyle(Label);
            Paint(Chip, chipTex, accent);
            Chip.fontSize = 13;
            Chip.fontStyle = FontStyle.Bold;
            Chip.alignment = TextAnchor.MiddleCenter;
            Chip.padding = new RectOffset(8, 8, 3, 3);

            rule = new GUIStyle();
            rule.normal.background = ruleTex;
            rule.fixedHeight = 1f;
            rule.margin = new RectOffset(2, 2, 2, 6);
        }

        private static Texture2D Solid(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Repeat;
            return texture;
        }

        private static void Paint(GUIStyle style, Texture2D texture, Color text)
        {
            style.normal.background = texture;
            style.hover.background = texture;
            style.active.background = texture;
            style.focused.background = texture;
            style.onNormal.background = texture;
            style.onHover.background = texture;
            style.onActive.background = texture;
            style.onFocused.background = texture;
            style.normal.textColor = text;
            style.hover.textColor = text;
            style.active.textColor = text;
            style.focused.textColor = text;
            style.onNormal.textColor = text;
            style.onHover.textColor = text;
            style.onActive.textColor = text;
            style.onFocused.textColor = text;
        }

        public static GameObject Player()
        {
            return GameObject.Find("Player");
        }

        public static bool IsLive(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.gameObject == null)
                return false;

            Scene scene = fsm.gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        public static bool IsPlayer(PlayMakerFSM fsm)
        {
            return IsLive(fsm) && fsm.gameObject.name == "Player";
        }

        public static string ActiveState(PlayMakerFSM fsm)
        {
            if (fsm == null)
                return "(none)";

            if (!string.IsNullOrEmpty(fsm.ActiveStateName))
                return fsm.ActiveStateName;

            if (fsm.Fsm == null || string.IsNullOrEmpty(fsm.Fsm.ActiveStateName))
                return "(none)";

            return fsm.Fsm.ActiveStateName;
        }

        public static bool Contains(string text, string filter)
        {
            return text != null &&
                text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static FsmFloat FindFloat(PlayMakerFSM fsm, string name)
        {
            FsmFloat[] values = Floats(fsm);
            if (values == null)
                return null;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null && values[i].Name == name)
                    return values[i];
            }

            return null;
        }

        public static FsmBool FindBool(PlayMakerFSM fsm, string name)
        {
            if (fsm == null || fsm.Fsm == null || fsm.Fsm.Variables == null || fsm.Fsm.Variables.BoolVariables == null)
                return null;

            FsmBool[] values = fsm.Fsm.Variables.BoolVariables;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null && values[i].Name == name)
                    return values[i];
            }

            return null;
        }

        public static string StringValue(PlayMakerFSM fsm, string name)
        {
            FsmString variable = FindString(fsm, name);
            return variable == null ? null : variable.Value;
        }

        public static GameObject FirstLinkedObject(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.Fsm == null || fsm.Fsm.Variables == null || fsm.Fsm.Variables.GameObjectVariables == null)
                return null;

            FsmGameObject[] values = fsm.Fsm.Variables.GameObjectVariables;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null && values[i].Value != null)
                    return values[i].Value;
            }

            return null;
        }

        public static FsmString FindString(PlayMakerFSM fsm, string name)
        {
            if (fsm == null || fsm.Fsm == null || fsm.Fsm.Variables == null || fsm.Fsm.Variables.StringVariables == null)
                return null;

            FsmString[] values = fsm.Fsm.Variables.StringVariables;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null && values[i].Name == name)
                    return values[i];
            }

            return null;
        }

        public static FsmFloat[] Floats(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.Fsm == null || fsm.Fsm.Variables == null)
                return null;

            return fsm.Fsm.Variables.FloatVariables;
        }

        public static FsmInt[] Ints(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.Fsm == null || fsm.Fsm.Variables == null)
                return null;

            return fsm.Fsm.Variables.IntVariables;
        }

        public static FsmBool[] Bools(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.Fsm == null || fsm.Fsm.Variables == null)
                return null;

            return fsm.Fsm.Variables.BoolVariables;
        }

        public static FsmString[] Strings(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.Fsm == null || fsm.Fsm.Variables == null)
                return null;

            return fsm.Fsm.Variables.StringVariables;
        }

        public static void Set(FsmFloat variable, float value)
        {
            Write(variable, value);
            DebugMenu.RequestRefresh();
        }

        public static void Write(FsmFloat variable, float value)
        {
            if (variable == null)
                return;

            variable.Value = value;
        }

        public static bool TryParse(string text, out float value)
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;

            return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        public static void TrySet(FsmFloat variable, string text)
        {
            float value;
            if (TryParse(text, out value))
                Set(variable, value);
        }

        public static void TrySet(FsmInt variable, string text)
        {
            int value;
            if (variable != null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                variable.Value = value;
                DebugMenu.RequestRefresh();
            }
        }

        public static void TrySet(FsmString variable, string text)
        {
            if (variable == null)
                return;

            variable.Value = text ?? "";
            DebugMenu.RequestRefresh();
        }

        public static string TextField(string key, string live, float width)
        {
            string buffer;
            if (!fields.TryGetValue(key, out buffer))
                buffer = live;

            // Don't stomp what they typed on the click that hits Set. That click
            // already moved focus off the field, and Repaint is the safe time to resync.
            if (GUI.GetNameOfFocusedControl() != key &&
                (Event.current == null || Event.current.type == EventType.Repaint))
                buffer = live ?? "";

            GUI.SetNextControlName(key);
            buffer = GUILayout.TextField(buffer ?? "", Field, GUILayout.Width(width));
            fields[key] = buffer;
            return buffer;
        }

        public static void ClearFields()
        {
            fields.Clear();
        }

        public static string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
