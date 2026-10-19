using System;
using System.Collections.Generic;
using System.Reflection;

namespace StutterFix
{
    // XmlSerializer: public type, public fields, implicit public default constructor.
    public sealed class FxSlot
    {
        public string Name = "";
        public string Code = "";
    }

    internal static class FxSlots
    {
        internal const int Limit = 12, NameLimit = 32;
        private static string T(string ko, string en) => SettingsWindow.T(ko, en);

        internal static bool Save(Settings c, string name, bool overwrite, out string message)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > NameLimit || HasControl(name))
            {
                message = T("이름은 1~32자로 입력하세요. 줄바꿈은 쓸 수 없습니다.", "Use 1–32 characters without line breaks.");
                return false;
            }
            var slots = c.FxSlots ?? new List<FxSlot>();
            var slot = Find(c, name);
            if (slot != null && !overwrite)
            {
                message = T("같은 이름이 있습니다. 덮어쓰기를 눌러 주세요.", "That name exists. Use Overwrite.");
                return false;
            }
            if (slot == null && slots.Count >= Limit)
            {
                message = T("최대 12개입니다. 하나를 삭제한 뒤 저장하세요.", "Up to 12 presets. Delete one before saving.");
                return false;
            }
            string code = FxShare.Export(c);
            if (slot == null) slots.Add(new FxSlot { Name = name, Code = code });
            else slot.Code = code;
            c.FxSlots = slots;
            message = T("저장했습니다: ", "Saved: ") + name;
            return true;
        }

        private static bool HasControl(string name)
        {
            foreach (char ch in name) if (char.IsControl(ch)) return true;
            return false;
        }

        internal static FxSlot Find(Settings c, string name)
        {
            if (c.FxSlots != null)
                foreach (var slot in c.FxSlots)
                    if (slot != null && string.Equals(slot.Name, (name ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) return slot;
            return null;
        }

        internal static bool Delete(Settings c, string name, out string message)
        {
            var slot = Find(c, name);
            if (slot == null) { message = T("프리셋을 찾지 못했습니다.", "Preset not found."); return false; }
            c.FxSlots.Remove(slot);
            message = T("삭제했습니다: ", "Deleted: ") + slot.Name;
            return true;
        }

        internal static bool Apply(Settings c, string name, out string message)
        {
            var slot = Find(c, name);
            if (slot == null) { message = T("프리셋을 찾지 못했습니다.", "Preset not found."); return false; }
            return Import(slot.Code, c, out message);
        }

        private static bool Import(string code, Settings c, out string message)
        {
            // Local slots contain exactly one exported code, never surrounding clipboard text.
            try
            {
                if (code == null || !code.StartsWith("SFX1:", StringComparison.Ordinal) ||
                    Convert.ToBase64String(Convert.FromBase64String(code.Substring(5))) != code.Substring(5))
                    throw new FormatException();
            }
            catch
            {
                message = T("저장된 코드가 깨졌습니다. 다시 저장해 주세요.", "The saved code is damaged. Save it again.");
                return false;
            }
            return FxShare.Import(code, c, out message);
        }

        internal static bool TurnOff(Settings c, out string message)
        {
            if (ActiveCount(c) == 0) { message = T("켜진 쉐이더가 없습니다.", "No shader is active."); return false; }
            c.FxLastOn = FxShare.Export(c);
            foreach (var f in typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (f.Name.StartsWith("Fx", StringComparison.Ordinal) && f.FieldType == typeof(bool)) f.SetValue(c, false);
            c.FxPreset = 0;
            message = T("모두 껐습니다. 다시 켜기로 되돌릴 수 있습니다.", "All off. Use Turn back on to restore.");
            return true;
        }

        internal static bool Restore(Settings c, out string message) => Import(c.FxLastOn, c, out message);

        internal static int ActiveCount(Settings c)
        {
            int count = 0;
            foreach (var f in typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (f.Name.StartsWith("Fx", StringComparison.Ordinal) && f.FieldType == typeof(bool) && f.Name != "FxGlowStack" && (bool)f.GetValue(c)) ++count;
            return count;
        }

#if DEV || AUTOTEST
        internal static void Command(string arg)
        {
            int split = arg.IndexOf(' ');
            string op = (split < 0 ? arg : arg.Substring(0, split)).Trim().ToLowerInvariant();
            string name = split < 0 ? "" : arg.Substring(split + 1).Trim();
            var c = Main.Config;
            string message;
            bool ok;
            switch (op)
            {
                case "selftest": Main.Entry.Logger.Log("[fxslot] " + SelfTest()); return;
                case "list":
                    Main.Entry.Logger.Log("[fxslot] count=" + (c.FxSlots?.Count ?? 0));
                    if (c.FxSlots != null) foreach (var slot in c.FxSlots) if (slot != null) Main.Entry.Logger.Log("[fxslot] " + slot.Name);
                    return;
                case "save": ok = Save(c, name, false, out message); break;
                case "apply": ok = Apply(c, name, out message); if (ok) ScreenEffects.Apply(); break;
                case "delete": ok = Delete(c, name, out message); break;
                default: throw new ArgumentException("fxslot save|apply|list|delete <name> | selftest");
            }
            if (!ok) throw new InvalidOperationException(message);
            c.Save(Main.Entry);
            Main.Entry.Logger.Log("[fxslot] " + message + " active=" + ActiveCount(c));
        }

        // Detached settings only: never changes or saves Main.Config, never calls Unity.
        internal static string SelfTest()
        {
            var c = new Settings();
            var fields = new List<FieldInfo>();
            foreach (var f in typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!f.Name.StartsWith("Fx", StringComparison.Ordinal) || f.Name == "FxSlots" || f.Name == "FxLastOn") continue;
                fields.Add(f);
                if (f.FieldType == typeof(bool)) f.SetValue(c, true);
                else if (f.FieldType == typeof(float)) f.SetValue(c, -0.12345679f);
                else if (f.FieldType == typeof(int)) f.SetValue(c, 3);
                else if (f.FieldType == typeof(string)) f.SetValue(c, "한글 LUT.png");
            }
            var before = fields.ConvertAll(f => f.GetValue(c));
            string message;
            Check(Save(c, "한글 프리셋", false, out message), message);
            Check(TurnOff(c, out message) && ActiveCount(c) == 0, "off");
            Check(Apply(c, "한글 프리셋", out message), message);
            Equal(c, fields, before);
            Check(TurnOff(c, out message) && Restore(c, out message), "restore");
            Equal(c, fields, before);
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(Settings));
            Settings round;
            using (var writer = new System.IO.StringWriter())
            {
                serializer.Serialize(writer, c);
                using (var reader = new System.IO.StringReader(writer.ToString())) round = (Settings)serializer.Deserialize(reader);
            }
            Check(round.FxSlots.Count == 1 && round.FxSlots[0].Name == "한글 프리셋" && round.FxLastOn == c.FxLastOn, "XML storage");
            Check(TurnOff(round, out message) && Apply(round, "한글 프리셋", out message), "XML apply");
            Equal(round, fields, before);
            using (var reader = new System.IO.StringReader("<Settings />"))
            {
                var legacy = (Settings)serializer.Deserialize(reader);
                Check(legacy.FxSlots != null && legacy.FxSlots.Count == 0 && legacy.FxLastOn == "", "legacy XML");
            }
            Check(!Save(c, "  ", false, out message) && !Save(c, new string('a', 33), false, out message) && !Save(c, "a\nb", false, out message), "names");
            Check(!Save(c, " 한글 프리셋 ", false, out message), "duplicate");
            for (int i = 1; i < Limit; ++i) Check(Save(c, "slot" + i, false, out message), message);
            Check(!Save(c, "overflow", false, out message), "limit");
            Check(Save(c, "slot1", true, out message), "overwrite at limit");
            c.FxSlots[0].Code = "SFX1:!!!";
            Check(!Apply(c, "한글 프리셋", out message), "damaged code");
            Equal(c, fields, before);
            c.FxSlots[0].Code = "SFX1:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Color=0;Contrast=NaN"));
            Check(!Apply(c, "한글 프리셋", out message), "atomic invalid value");
            Equal(c, fields, before);
            Check(Delete(c, "SLOT1", out message) && !Delete(c, "SLOT1", out message), "delete");
            var overwrite = new Settings();
            Check(Save(overwrite, "Update", false, out message), message);
            overwrite.FxContrast = 1.2345679f;
            overwrite.FxColor = true;
            Check(Save(overwrite, "UPDATE", true, out message) && overwrite.FxSlots.Count == 1, "case-insensitive overwrite");
            overwrite.FxContrast = 0;
            Check(Apply(overwrite, "Update", out message) && overwrite.FxContrast == 1.2345679f && overwrite.FxColor, "overwritten values");
            var noList = new Settings { FxSlots = null };
            Check(Save(noList, "Null list", false, out message) && noList.FxSlots.Count == 1, "null list");
            return "PASS fields=" + fields.Count + " bitwise/restore/XML/legacy/names/limit/overwrite/atomic/delete";
        }

        private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("fxslot selftest: " + message); }
        private static void Equal(Settings c, List<FieldInfo> fields, List<object> before)
        {
            for (int i = 0; i < fields.Count; ++i)
            {
                object now = fields[i].GetValue(c), old = before[i];
                bool same = now is float ? BitConverter.SingleToInt32Bits((float)now) == BitConverter.SingleToInt32Bits((float)old) : Equals(now, old);
                Check(same, fields[i].Name);
            }
        }
#endif
    }
}
