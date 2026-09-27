using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace ApocalypterInspectorPatcher
{
    public static class Patcher
    {
        private const int VkF8 = 0x77;
        private const int VkF9 = 0x78;

        private static readonly string[] Targets =
        {
            "PlayMaker.dll",
            "Assembly-CSharp.dll"
        };

        private static int ticks;
        private static int firstTickMs = int.MinValue;
        private static int fsmWarnings;
        private static bool announced;
        private static bool dumpedStartup;
        private static bool busy;
        private static bool f8WasDown;
        private static bool f9WasDown;
        private static int nextPlayerCheckMs = int.MinValue;
        private static bool loggedPlayerWait;

        public static IEnumerable<string> TargetDLLs
        {
            get { return Targets; }
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public static void Initialize()
        {
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            Note("patcher 2.1.0 starting");
        }

        public static void Patch(AssemblyDefinition assembly)
        {
            int count = 0;
            MethodReference tick = assembly.MainModule.ImportReference(
                typeof(Patcher).GetMethod("OnTick"));
            MethodReference warning = assembly.MainModule.ImportReference(
                typeof(Patcher).GetMethod("OnLogWarning"));
            MethodReference gui = assembly.MainModule.ImportReference(
                typeof(Patcher).GetMethod("OnGuiTick"));

            foreach (TypeDefinition type in AllTypes(assembly.MainModule.Types))
            {
                foreach (MethodDefinition method in type.Methods)
                {
                    if (!method.HasBody)
                        continue;

                    if (method.Name == "Update" &&
                        !method.IsStatic &&
                        method.Parameters.Count == 0)
                    {
                        if (InsertCall(method, tick, false))
                            count++;
                        continue;
                    }

                    if (method.Name == "OnGUI" &&
                        !method.IsStatic &&
                        method.Parameters.Count == 0 &&
                        (type.FullName == "PlayMakerGUI" ||
                         type.FullName == "CameraMovementPro.CameraMovementController"))
                    {
                        if (InsertCall(method, gui, false))
                            count++;
                    }

                    if (type.FullName == "HutongGames.PlayMaker.FsmStateAction" &&
                        method.Name == "LogWarning" &&
                        method.Parameters.Count == 1)
                    {
                        if (InsertCall(method, warning, true))
                            count++;
                    }
                }
            }

            Note("patched " + count + " methods in " + assembly.Name.Name);
        }

        private static int lastPumpMs = int.MinValue;
        private static bool guiResolved;
        private static MethodInfo drawInjected;
        private static MethodInfo pumpMethod;

        public static void OnTick()
        {
            MaybePump();

            if (busy)
                return;

            busy = true;
            try
            {
                ticks++;
                int now = Environment.TickCount;

                if (!announced)
                {
                    announced = true;
                    firstTickMs = now;
                    Note("tick is live");
                }

                if (!dumpedStartup &&
                    firstTickMs != int.MinValue &&
                    unchecked(now - firstTickMs) >= 3000 &&
                    (nextPlayerCheckMs == int.MinValue || unchecked(now - nextPlayerCheckMs) >= 1000))
                {
                    nextPlayerCheckMs = now;
                    if (LivePlayerExists())
                    {
                        Dump("startup");
                        dumpedStartup = true;
                    }
                    else if (!loggedPlayerWait)
                    {
                        loggedPlayerWait = true;
                        Note("no player yet, holding the startup dump");
                    }
                }

                if (KeyPressed(VkF8, ref f8WasDown))
                    ApplyWriteTest();

                if (KeyPressed(VkF9, ref f9WasDown))
                    Dump("F9");
            }
            catch (Exception ex)
            {
                Note(ex.ToString());
            }
            finally
            {
                busy = false;
            }
        }

        public static void OnGuiTick()
        {
            try
            {
                if (!guiResolved)
                {
                    guiResolved = true;
                    Type menu = FindType("ApocalypterInspector.DebugMenu");
                    if (menu != null)
                    {
                        drawInjected = menu.GetMethod(
                            "DrawFromInjected",
                            BindingFlags.Public | BindingFlags.Static);
                    }
                }

                if (drawInjected != null)
                    drawInjected.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Note("Debug menu draw failed: " + ex.Message);
                drawInjected = null;
                guiResolved = true;
            }
        }

        private static void MaybePump()
        {
            int now = Environment.TickCount;
            if (lastPumpMs != int.MinValue && unchecked(now - lastPumpMs) < 16)
                return;

            lastPumpMs = now;

            try
            {
                if (pumpMethod == null)
                {
                    Type plugin = FindType("ApocalypterInspector.Plugin");
                    if (plugin == null)
                        return;

                    pumpMethod = plugin.GetMethod(
                        "Pump",
                        BindingFlags.Public | BindingFlags.Static);
                }

                if (pumpMethod != null)
                    pumpMethod.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Note("Debug menu pump failed: " + ex.Message);
                pumpMethod = null;
            }
        }

        public static void OnLogWarning(string message)
        {
            if (busy || string.IsNullOrEmpty(message))
                return;

            if (message.IndexOf("Could not find FSM", StringComparison.Ordinal) < 0)
                return;

            busy = true;
            try
            {
                fsmWarnings++;
                if (fsmWarnings == 10 || fsmWarnings % 20 == 0)
                    Dump("warning " + fsmWarnings);
            }
            catch (Exception ex)
            {
                Note(ex.ToString());
            }
            finally
            {
                busy = false;
            }
        }

        private static bool InsertCall(
            MethodDefinition method,
            MethodReference call,
            bool passStringArgument)
        {
            try
            {
                if (method.Body.Instructions.Count == 0)
                    return false;

                method.Body.SimplifyMacros();
                ILProcessor il = method.Body.GetILProcessor();
                Instruction first = method.Body.Instructions[0];

                if (passStringArgument)
                    il.InsertBefore(first, il.Create(OpCodes.Ldarg_1));

                il.InsertBefore(first, il.Create(OpCodes.Call, call));
                method.Body.OptimizeMacros();
                return true;
            }
            catch (Exception ex)
            {
                Note("Skip " + method.FullName + ": " + ex.Message);
                return false;
            }
        }

        private static IEnumerable<TypeDefinition> AllTypes(
            IEnumerable<TypeDefinition> types)
        {
            foreach (TypeDefinition type in types)
            {
                yield return type;
                foreach (TypeDefinition nested in AllTypes(type.NestedTypes))
                    yield return nested;
            }
        }

        private static bool KeyPressed(int virtualKey, ref bool wasDown)
        {
            bool down = (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
            bool pressed = down && !wasDown;
            wasDown = down;
            return pressed;
        }

        private static void ApplyWriteTest()
        {
            Type fsmType = FindType("PlayMakerFSM");
            if (fsmType == null)
            {
                Note("F8: no PlayMakerFSM type loaded yet");
                return;
            }

            Array found = FindAll(fsmType);
            int healthWrites = 0;
            int liquidWrites = 0;

            for (int i = 0; i < found.Length; i++)
            {
                object fsm = found.GetValue(i);
                if (fsm == null)
                    continue;

                object gameObject = GetMember(fsm, "gameObject");
                if (gameObject == null)
                    continue;

                string objectName = Convert.ToString(GetMember(gameObject, "name"));
                string fsmName = Convert.ToString(GetMember(fsm, "FsmName"));

                if (objectName == "Player" && fsmName == "Health")
                {
                    if (SetFloat(fsm, objectName, fsmName, "Health", 25f))
                        healthWrites++;
                }

                if (objectName != null &&
                    objectName.StartsWith("Gasoline_Can", StringComparison.Ordinal) &&
                    fsmName == "LiquidAmount")
                {
                    if (SetFloat(fsm, objectName, fsmName, "Liquid", 1f))
                        liquidWrites++;
                }
            }

            Note("F8 done, " + healthWrites + " health / " + liquidWrites + " gas can writes");
        }

        private static bool SetFloat(
            object fsm,
            string objectName,
            string fsmName,
            string variableName,
            float newValue)
        {
            object data = GetMember(fsm, "Fsm");
            object variables = data == null ? null : GetMember(data, "Variables");
            Array floats = variables == null
                ? null
                : GetMember(variables, "FloatVariables") as Array;

            if (floats == null)
            {
                Note(objectName + " " + fsmName + " has no float variables.");
                return false;
            }

            for (int i = 0; i < floats.Length; i++)
            {
                object variable = floats.GetValue(i);
                if (variable == null)
                    continue;

                if (Convert.ToString(GetMember(variable, "Name")) != variableName)
                    continue;

                object previous = GetMember(variable, "Value");
                if (!SetMember(variable, "Value", newValue))
                {
                    Note("Could not set " + objectName + " " + fsmName + "." + variableName);
                    return false;
                }

                object current = GetMember(variable, "Value");
                Note(
                    "Set " + objectName + " " + fsmName + "." + variableName +
                    " " + previous + " -> " + current);
                return true;
            }

            Note(objectName + " " + fsmName + " has no float named " + variableName + ".");
            return false;
        }

        private static bool LivePlayerExists()
        {
            Type fsmType = FindType("PlayMakerFSM");
            if (fsmType == null)
                return false;

            Array found = FindAll(fsmType);
            for (int i = 0; i < found.Length; i++)
            {
                object fsm = found.GetValue(i);
                if (fsm == null)
                    continue;

                object gameObject = GetMember(fsm, "gameObject");
                if (gameObject == null)
                    continue;

                if (Convert.ToString(GetMember(gameObject, "name")) != "Player")
                    continue;

                object scene = GetMember(gameObject, "scene");
                if (scene != null && SceneIsLive(scene))
                    return true;
            }

            return false;
        }

        private static void Dump(string reason)
        {
            Note("dumping fsms (" + reason + ")");

            Type fsmType = FindType("PlayMakerFSM");
            if (fsmType == null)
            {
                Note("no PlayMakerFSM type loaded yet");
                return;
            }

            Array found = FindAll(fsmType);
            var lines = new List<string>();
            string sceneName = null;

            for (int i = 0; i < found.Length; i++)
            {
                object fsm = found.GetValue(i);
                if (fsm == null)
                    continue;

                try
                {
                    object gameObject = GetMember(fsm, "gameObject");
                    if (gameObject == null)
                        continue;

                    object scene = GetMember(gameObject, "scene");
                    if (scene == null || !SceneIsLive(scene))
                        continue;

                    string objectName = Convert.ToString(GetMember(gameObject, "name"));
                    string thisScene = Convert.ToString(GetMember(scene, "name"));
                    if (objectName == "Player")
                        sceneName = thisScene;
                    else if (sceneName == null)
                        sceneName = thisScene;

                    lines.Add(Describe(fsm, gameObject, scene));
                }
                catch (Exception ex)
                {
                    lines.Add("Could not inspect FSM: " + ex.Message);
                }
            }

            lines.Sort(StringComparer.OrdinalIgnoreCase);
            string fileName = "ApocalypterInspector-fsm-" + SafeFilePart(sceneName) + ".txt";
            string path = Path.Combine(BepInExRoot(), fileName);

            var writer = new StringBuilder();
            writer.AppendLine("fsm dump, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            writer.AppendLine("why: " + reason);
            writer.AppendLine("scene: " + (string.IsNullOrEmpty(sceneName) ? "unknown" : sceneName) +
                " (other loaded scenes are in here too)");
            writer.AppendLine(lines.Count + " live fsms");
            for (int i = 0; i < lines.Count; i++)
            {
                writer.AppendLine();
                writer.AppendLine(lines[i]);
            }

            File.WriteAllText(path, writer.ToString());
            Note("wrote " + path);
        }

        private static string SafeFilePart(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "unknown";

            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == ' ')
                {
                    builder.Append('_');
                    continue;
                }

                bool bad = false;
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (c == invalid[j])
                    {
                        bad = true;
                        break;
                    }
                }

                builder.Append(bad ? '_' : c);
            }

            return builder.Length == 0 ? "unknown" : builder.ToString();
        }

        private static string Describe(object fsm, object gameObject, object scene)
        {
            var block = new StringBuilder();
            block.AppendLine("OBJECT: " + GetPath(gameObject));
            block.AppendLine("SCENE: " + GetMember(scene, "name"));

            object fsmName = GetMember(fsm, "FsmName");
            object data = GetMember(fsm, "Fsm");
            if (fsmName == null && data != null)
                fsmName = GetMember(data, "Name");

            block.AppendLine("FSM: " + (fsmName ?? "[UNNAMED]"));

            if (data == null)
            {
                block.Append("STATE: [FSM data is null]");
                return block.ToString();
            }

            block.AppendLine("STATE: " + GetMember(data, "ActiveStateName"));
            AppendVariables(block, GetMember(data, "Variables"));
            return block.ToString().TrimEnd();
        }

        private static void AppendVariables(StringBuilder block, object variables)
        {
            if (variables == null)
            {
                block.Append("VARIABLES: none");
                return;
            }

            int count = 0;
            count += AppendValues(block, variables, "FloatVariables", "FLOAT");
            count += AppendValues(block, variables, "IntVariables", "INT");
            count += AppendValues(block, variables, "BoolVariables", "BOOL");
            count += AppendValues(block, variables, "StringVariables", "STRING");
            count += AppendGameObjects(block, variables);

            if (count == 0)
                block.Append("VARIABLES: none");
        }

        private static int AppendValues(
            StringBuilder block,
            object variables,
            string propertyName,
            string label)
        {
            Array values = GetMember(variables, propertyName) as Array;
            if (values == null)
                return 0;

            for (int i = 0; i < values.Length; i++)
            {
                object variable = values.GetValue(i);
                if (variable == null)
                    continue;

                block.AppendLine(
                    "  [" + label + "] " +
                    GetMember(variable, "Name") +
                    " = " +
                    GetMember(variable, "Value"));
            }

            return values.Length;
        }

        private static int AppendGameObjects(StringBuilder block, object variables)
        {
            Array values = GetMember(variables, "GameObjectVariables") as Array;
            if (values == null)
                return 0;

            for (int i = 0; i < values.Length; i++)
            {
                object variable = values.GetValue(i);
                if (variable == null)
                    continue;

                object value = GetMember(variable, "Value");
                string shown = value == null ? "NULL" : Convert.ToString(GetMember(value, "name"));
                block.AppendLine(
                    "  [GAMEOBJECT] " +
                    GetMember(variable, "Name") +
                    " = " +
                    shown);
            }

            return values.Length;
        }

        private static string GetPath(object gameObject)
        {
            object transform = GetMember(gameObject, "transform");
            string path = Convert.ToString(GetMember(transform, "name"));

            object parent = GetMember(transform, "parent");
            while (parent != null)
            {
                path = GetMember(parent, "name") + "/" + path;
                parent = GetMember(parent, "parent");
            }

            return path;
        }

        private static bool SceneIsLive(object scene)
        {
            object valid = scene.GetType().GetMethod("IsValid").Invoke(scene, null);
            if (!(valid is bool) || !(bool)valid)
                return false;

            object loaded = GetMember(scene, "isLoaded");
            return loaded is bool && (bool)loaded;
        }

        private static Array FindAll(Type componentType)
        {
            Type resources = FindType("UnityEngine.Resources");
            MethodInfo[] methods = resources.GetMethods(
                BindingFlags.Public | BindingFlags.Static);

            foreach (MethodInfo method in methods)
            {
                if (method.Name != "FindObjectsOfTypeAll" ||
                    !method.IsGenericMethodDefinition ||
                    method.GetParameters().Length != 0)
                    continue;

                return (Array)method.MakeGenericMethod(componentType).Invoke(null, null);
            }

            throw new MissingMethodException("UnityEngine.Resources.FindObjectsOfTypeAll");
        }

        private static object GetMember(object target, string name)
        {
            if (target == null)
                return null;

            Type type = target.GetType();
            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            while (type != null)
            {
                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null)
                    return property.GetValue(target, null);

                FieldInfo field = type.GetField(name, flags);
                if (field != null)
                    return field.GetValue(target);

                type = type.BaseType;
            }

            return null;
        }

        private static bool SetMember(object target, string name, object value)
        {
            if (target == null)
                return false;

            Type type = target.GetType();
            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            while (type != null)
            {
                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(target, value, null);
                    return true;
                }

                FieldInfo field = type.GetField(name, flags);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("ApocalypterInspectorPatcher,", StringComparison.Ordinal))
                return typeof(Patcher).Assembly;

            return null;
        }

        private static void Note(string message)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + " " + message + Environment.NewLine;
            try
            {
                File.AppendAllText(
                    Path.Combine(BepInExRoot(), "ApocalypterInspector-patcher.txt"),
                    line);
            }
            catch (Exception)
            {
            }

            try
            {
                string logLine = "[Info   :Apocalypter Inspector] " + message + Environment.NewLine;
                string path = Path.Combine(BepInExRoot(), "LogOutput.log");
                using (var stream = new FileStream(
                    path,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite))
                using (var writer = new StreamWriter(stream))
                    writer.Write(logLine);
            }
            catch (Exception)
            {
            }
        }

        private static string BepInExRoot()
        {
            string location = typeof(Patcher).Assembly.Location;
            return Path.GetDirectoryName(Path.GetDirectoryName(location));
        }
    }
}
