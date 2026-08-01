using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;

namespace ThanhDV.AudioConductor.FMOD
{
    public static class WrapperGenerator
    {
        public static bool TryBuildFMODBus(List<BusEntry> busEntries, out string source, out string error)
        {
            try
            {
                source = BuildFMODBus(busEntries);
                error = null;
                return true;
            }
            catch (Exception e)
            {
                source = null;
                error = $"Failed to build FMODBus source: {e.Message}";
                return false;
            }
        }

        public static bool TryBuildFMODEventReference(
            List<EventReferenceEntry> eventReferenceEntries,
            out string source,
            out string error)
        {
            try
            {
                source = BuildFMODEventReference(eventReferenceEntries);
                error = null;
                return true;
            }
            catch (Exception e)
            {
                source = null;
                error = $"Failed to build FMODEventReference source: {e.Message}";
                return false;
            }
        }

        public static bool TryWriteFMODBus(string source, out string error)
        {
            return TryWriteGeneratedFiles(Common.FMOD_BUS_SCRIPT_PATH, source, out error);
        }

        public static bool TryWriteFMODEventReference(string source, out string error)
        {
            return TryWriteGeneratedFiles(Common.FMOD_EVENT_REF_SCRIPT_PATH, source, out error);
        }

        private static bool TryWriteGeneratedFiles(string wrapperPath, string wrapperSource, out string error)
        {
            error = null;
            if (wrapperSource == null)
            {
                error = "Generated wrapper source is null.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(Common.FMOD_REF_SCRIPT_FOLDER);

                bool changed = WriteFileAtomicallyIfChanged(Common.GENERATED_ASMDEF_PATH, BuildGeneratedAsmdef());
                changed |= WriteFileAtomicallyIfChanged(wrapperPath, wrapperSource);

                if (changed) AssetDatabase.Refresh();
                return true;
            }
            catch (Exception e)
            {
                error = $"Failed to write generated wrapper files: {e.Message}";
                return false;
            }
        }

        private static bool WriteFileAtomicallyIfChanged(string targetPath, string content)
        {
            if (File.Exists(targetPath) &&
                string.Equals(File.ReadAllText(targetPath), content, StringComparison.Ordinal))
            {
                return false;
            }

            string temporaryPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(temporaryPath, content, new UTF8Encoding(false));

                if (File.Exists(targetPath))
                {
                    File.Replace(temporaryPath, targetPath, null);
                }
                else
                {
                    File.Move(temporaryPath, targetPath);
                }

                return true;
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static string BuildGeneratedAsmdef()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"    \"name\": \"{Common.GENERATED_ASMDEF_NAME}\",");
            sb.AppendLine("    \"rootNamespace\": \"ThanhDV.AudioConductor.FMOD\",");
            sb.AppendLine("    \"references\": [");
            sb.AppendLine("        \"GUID:0c752da273b17c547ae705acf0f2adf2\"");
            sb.AppendLine("    ],");
            sb.AppendLine("    \"includePlatforms\": [],");
            sb.AppendLine("    \"excludePlatforms\": [],");
            sb.AppendLine("    \"allowUnsafeCode\": false,");
            sb.AppendLine("    \"overrideReferences\": false,");
            sb.AppendLine("    \"precompiledReferences\": [],");
            sb.AppendLine("    \"autoReferenced\": true,");
            sb.AppendLine("    \"defineConstraints\": [],");
            sb.AppendLine("    \"versionDefines\": [],");
            sb.AppendLine("    \"noEngineReferences\": false");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string BuildFMODBus(List<BusEntry> busEntries)
        {
            var entries = new List<(string Identifier, string Path)>();
            var usedIdentifiers = new HashSet<string>(StringComparer.Ordinal);

            if (busEntries != null)
            {
                foreach (BusEntry entry in busEntries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.BusPath)) continue;
                    string identifier = MakeUniqueIdentifier(MakeSafeIdentifier(entry.Key), usedIdentifiers);
                    entries.Add((identifier, EscapeForCSharpStringLiteral(entry.BusPath)));
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("using FMOD.Studio;");
            sb.AppendLine("using FMODUnity;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("namespace ThanhDV.AudioConductor.FMOD");
            sb.AppendLine("{");
            sb.AppendLine("    public static class FMODBus");
            sb.AppendLine("    {");

            foreach ((string identifier, string path) in entries)
            {
                sb.AppendLine($"        private static Bus? _cached{identifier};");
                sb.AppendLine($"        public static Bus {identifier} => GetBus(\"{path}\", ref _cached{identifier});");
                sb.AppendLine();
            }

            sb.Append("        public static readonly string[] Keys = {");
            if (entries.Count > 0)
            {
                sb.Append(' ');
                sb.Append(string.Join(", ", entries.ConvertAll(e => $"\"{e.Identifier}\"")));
                sb.Append(' ');
            }
            sb.AppendLine("};");
            sb.AppendLine();

            sb.AppendLine("        public static Bus GetByKey(string key)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (key)");
            sb.AppendLine("            {");
            foreach ((string identifier, string _) in entries)
            {
                sb.AppendLine($"                case \"{identifier}\": return {identifier};");
            }
            sb.AppendLine("                default:");
            sb.AppendLine("                    Debug.LogWarning($\"[AudioConductor - FMOD] Bus key '{key}' not found in FMODBus.\");");
            sb.AppendLine("                    return default;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        private static Bus GetBus(string path, ref Bus? cache)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (cache.HasValue && cache.Value.isValid()) return cache.Value;");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                Bus result = RuntimeManager.GetBus(path);");
            sb.AppendLine("                if (result.isValid()) cache = result;");
            sb.AppendLine("                return result;");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (BusNotFoundException)");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogWarning($\"[AudioConductor - FMOD] Bus not found: '{path}'. Please check your FMOD Studio project!!!\");");
            sb.AppendLine("                return default;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string BuildFMODEventReference(List<EventReferenceEntry> eventRefEntries)
        {
            var usedIdentifiers = new HashSet<string>(StringComparer.Ordinal);

            var sb = new StringBuilder();
            sb.AppendLine("using FMODUnity;");
            sb.AppendLine();
            sb.AppendLine("namespace ThanhDV.AudioConductor.FMOD");
            sb.AppendLine("{");
            sb.AppendLine("    public static class FMODEventReference");
            sb.AppendLine("    {");

            if (eventRefEntries != null)
            {
                foreach (EventReferenceEntry entry in eventRefEntries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.EventReference.IsNull) continue;

                    string identifier = MakeUniqueIdentifier(MakeSafeIdentifier(entry.Key), usedIdentifiers);
                    global::FMOD.GUID guid = entry.EventReference.Guid;
                    string pathLiteral = EscapeForCSharpStringLiteral(entry.EventReference.Path);

                    sb.AppendLine($"        public static readonly EventReference {identifier} = new EventReference");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            Guid = new global::FMOD.GUID {{ Data1 = {guid.Data1}, Data2 = {guid.Data2}, Data3 = {guid.Data3}, Data4 = {guid.Data4} }},");
                    sb.AppendLine("#if UNITY_EDITOR");
                    sb.AppendLine($"            Path = \"{pathLiteral}\",");
                    sb.AppendLine("#endif");
                    sb.AppendLine("        };");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string MakeUniqueIdentifier(string baseIdentifier, HashSet<string> used)
        {
            if (used.Add(baseIdentifier)) return baseIdentifier;

            int suffix = 2;
            while (true)
            {
                string candidate = baseIdentifier + "_" + suffix;
                if (used.Add(candidate)) return candidate;
                suffix++;
            }
        }

        private static string MakeSafeIdentifier(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Unnamed";

            raw = raw.Trim();
            var sb = new StringBuilder(raw.Length);

            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            }

            if (sb.Length == 0) sb.Append("Unnamed");
            if (!char.IsLetter(sb[0]) && sb[0] != '_') sb.Insert(0, '_');

            string identifier = sb.ToString();
            return IsCSharpKeyword(identifier) ? "_" + identifier : identifier;
        }

        private static string EscapeForCSharpStringLiteral(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private static bool IsCSharpKeyword(string identifier)
        {
            return identifier is
                "abstract" or "as" or "base" or "bool" or "break" or "byte" or "case" or "catch" or "char" or "checked" or
                "class" or "const" or "continue" or "decimal" or "default" or "delegate" or "do" or "double" or "else" or "enum" or
                "event" or "explicit" or "extern" or "false" or "finally" or "fixed" or "float" or "for" or "foreach" or "goto" or
                "if" or "implicit" or "in" or "int" or "interface" or "internal" or "is" or "lock" or "long" or "namespace" or "new" or
                "null" or "object" or "operator" or "out" or "override" or "params" or "private" or "protected" or "public" or "readonly" or
                "ref" or "return" or "sbyte" or "sealed" or "short" or "sizeof" or "stackalloc" or "static" or "string" or "struct" or "switch" or
                "this" or "throw" or "true" or "try" or "typeof" or "uint" or "ulong" or "unchecked" or "unsafe" or "ushort" or "using" or
                "virtual" or "void" or "volatile" or "while";
        }
    }
}