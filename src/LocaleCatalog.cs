using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace SlopStudio
{
    public sealed class LocaleCatalog
    {
        private static readonly string[] RequiredKeys = new string[]
        {
            "app.title", "app.arrangerNotice", "timeline.help", "tempo.label",
            "menu.file", "menu.edit", "menu.view", "menu.help",
            "file.new", "file.open", "file.save", "file.saveAs", "file.exit",
            "edit.undo", "edit.redo", "edit.addAudioTrack", "edit.addMidiTrack",
            "edit.addRegion", "edit.renameSelection", "edit.deleteRegion", "edit.deleteTrack",
            "view.language", "help.about",
            "toolbar.addTrack", "toolbar.addRegion", "toolbar.undo", "toolbar.redo",
            "toolbar.zoomIn", "toolbar.zoomOut",
            "timeline.trackHeader", "timeline.bars", "timeline.empty",
            "project.untitled", "track.audio", "track.midi", "track.audioDefault",
            "track.midiDefault", "region.audioDefault", "region.midiDefault",
            "region.selectionStatus", "track.selectionStatus",
            "status.ready", "status.saved", "status.unsaved",
            "dialog.renameTrack", "dialog.renameRegion", "dialog.unsavedTitle",
            "dialog.unsavedMessage", "dialog.deleteTrackMessage", "dialog.about", "dialog.errorTitle",
            "error.invalidProject", "error.operation", "error.noTrackSelected", "error.noRegionSelected",
            "error.saveFailed", "error.openFailed", "error.settingsFailed"
        };

        private readonly Dictionary<string, string> _strings;
        public string Language { get; private set; }

        private LocaleCatalog(string language, Dictionary<string, string> strings)
        {
            Language = language;
            _strings = strings;
        }

        public string T(string key)
        {
            string value;
            if (!_strings.TryGetValue(key, out value))
                return key;
            return value;
        }

        public static LocaleCatalog Load(string path, string expectedLanguage)
        {
            FileInfo info = new FileInfo(path);
            if (!info.Exists)
                throw new FileNotFoundException("Language pack was not found.", path);
            if (info.Length > 128 * 1024)
                throw new InvalidDataException("Language pack exceeds 128 KiB.");
            string json;
            using (StreamReader reader = new StreamReader(path, new UTF8Encoding(false, true), true))
                json = reader.ReadToEnd();
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = 128 * 1024;
            IDictionary<string, object> root = AsObject(serializer.DeserializeObject(json), "Language pack");
            if (ReadString(root, "schema") != "SLOPSTUDIO-locale")
                throw new InvalidDataException("Language pack schema is unsupported.");
            if (ReadInteger(root, "version") != 1)
                throw new InvalidDataException("Language pack version is unsupported.");
            string language = ReadString(root, "language");
            if (!String.Equals(language, expectedLanguage, StringComparison.Ordinal))
                throw new InvalidDataException("Language pack identifier does not match its file.");
            IDictionary<string, object> values = AsObject(ReadValue(root, "strings"), "Language strings");
            Dictionary<string, string> strings = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> pair in values)
            {
                string value = pair.Value as string;
                if (value == null || value.Length == 0 || value.Length > 400)
                    throw new InvalidDataException("Language string '" + pair.Key + "' must be a non-empty string of at most 400 characters.");
                strings[pair.Key] = value;
            }
            foreach (string key in RequiredKeys)
                if (!strings.ContainsKey(key))
                    throw new InvalidDataException("Language pack is missing required string '" + key + "'.");
            return new LocaleCatalog(language, strings);
        }

        public static string[] SupportedLanguages
        {
            get { return new string[] { "en-US", "de-DE", "ja-JP", "ko-KR" }; }
        }

        private static object ReadValue(IDictionary<string, object> root, string key)
        {
            object value;
            if (!root.TryGetValue(key, out value) || value == null)
                throw new InvalidDataException("Required field '" + key + "' is missing.");
            return value;
        }

        private static string ReadString(IDictionary<string, object> root, string key)
        {
            string value = ReadValue(root, key) as string;
            if (value == null)
                throw new InvalidDataException("Field '" + key + "' must be a string.");
            return value;
        }

        private static int ReadInteger(IDictionary<string, object> root, string key)
        {
            object value = ReadValue(root, key);
            if (value is bool || !(value is IConvertible))
                throw new InvalidDataException("Field '" + key + "' must be an integer.");
            double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (number != Math.Floor(number) || number < Int32.MinValue || number > Int32.MaxValue)
                throw new InvalidDataException("Field '" + key + "' must be an integer.");
            return (int)number;
        }

        private static IDictionary<string, object> AsObject(object value, string label)
        {
            IDictionary<string, object> result = value as IDictionary<string, object>;
            if (result == null)
                throw new InvalidDataException(label + " must be a JSON object.");
            return result;
        }
    }
}
