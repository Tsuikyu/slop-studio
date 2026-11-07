using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace SlopProX
{
    public sealed class AppSettings
    {
        public string Language { get; set; }public string InputMode{get;set;}public bool SmoothAnimation{get;set;}
        public string AudioBackend{get;set;}public string OutputEndpoint{get;set;}public string InputEndpoint{get;set;}public string AsioDriver{get;set;}public bool PdcEnabled{get;set;}public IntegrationSettings Integration{get;set;}
        public int TimelineZoom { get; set; }
        public int AudioOutput { get; set; } public int AudioInput { get; set; } public int MidiInput { get; set; } public int MidiOutput { get; set; }
        public int SampleRate { get; set; } public int BufferFrames { get; set; } public int UiScale { get; set; } public int UndoLimit { get; set; }
        public double DefaultTempo { get; set; } public int DefaultNumerator { get; set; } public int DefaultDenominator { get; set; }
        public List<string> PluginPaths { get; set; }
        public Dictionary<string,int> ShortcutOverrides { get; set; }
        public Dictionary<string,int> ThemeOverrides { get; set; }

        public AppSettings()
        {
            Language = "en-US";InputMode="Mouse";SmoothAnimation=true;AudioBackend="WASAPI";OutputEndpoint="";InputEndpoint="";AsioDriver="";PdcEnabled=true;Integration=new IntegrationSettings();
            TimelineZoom = 8; AudioOutput=-1;AudioInput=-1;MidiInput=-1;MidiOutput=-1;SampleRate=48000;BufferFrames=512;UiScale=100;UndoLimit=200;DefaultTempo=120;DefaultNumerator=4;DefaultDenominator=4;
            string common=Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);PluginPaths=new List<string>{Path.Combine(common,"VST3"),Path.Combine(common,"CLAP")};ShortcutOverrides=new Dictionary<string,int>();ThemeOverrides=new Dictionary<string,int>();
        }
    }

    public static class SettingsStore
    {
        public static string DefaultPath
        {
            get
            {
                string root = Environment.GetEnvironmentVariable("SLOPX_PROFILE");
                if(String.IsNullOrWhiteSpace(root))root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SlopProX");
                return Path.Combine(root,"settings.json");
                /*
                */
            }
        }

        public static AppSettings LoadOrDefault(out string warning)
        {
            warning = null;
            string path = DefaultPath;
            if (!File.Exists(path))
                return new AppSettings();
            try
            {
                return Load(path);
            }
            catch (Exception exception)
            {
                warning = exception.Message;
                return new AppSettings();
            }
        }

        public static AppSettings Load(string path)
        {
            FileInfo info = new FileInfo(path);
            if (!info.Exists)
                return new AppSettings();
            if (info.Length > 64 * 1024)
                throw new InvalidDataException("Settings exceed the 64 KiB size limit.");
            string json;
            using (StreamReader reader = new StreamReader(path, new UTF8Encoding(false, true), true))
                json = reader.ReadToEnd();
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = 64 * 1024;
            IDictionary<string, object> root = AsObject(serializer.DeserializeObject(json), "Settings");
            if (ReadString(root, "schema") != "slopx-settings" || (ReadInteger(root, "version") != 1 && ReadInteger(root, "version") != 2))
                throw new InvalidDataException("Settings schema or version is unsupported.");
            AppSettings settings = new AppSettings();
            settings.Language = ReadString(root, "language");settings.InputMode=root.ContainsKey("inputMode")?ReadString(root,"inputMode"):"Mouse";settings.SmoothAnimation=!root.ContainsKey("smoothAnimation")||Convert.ToBoolean(root["smoothAnimation"]);settings.AudioBackend=root.ContainsKey("audioBackend")?ReadString(root,"audioBackend"):"MME";settings.OutputEndpoint=root.ContainsKey("outputEndpoint")?ReadString(root,"outputEndpoint"):"";settings.InputEndpoint=root.ContainsKey("inputEndpoint")?ReadString(root,"inputEndpoint"):"";settings.AsioDriver=root.ContainsKey("asioDriver")?ReadString(root,"asioDriver"):"";settings.PdcEnabled=!root.ContainsKey("pdc")||Convert.ToBoolean(root["pdc"]);if(root.ContainsKey("integration"))settings.Integration=serializer.ConvertToType<IntegrationSettings>(root["integration"]);
            settings.TimelineZoom = ReadInteger(root, "timelineZoom");
            settings.UndoLimit=OptionalInt(root,"undoLimit",200);
            if(root.ContainsKey("shortcutOverrides")){var values=AsObject(root["shortcutOverrides"],"Shortcut assignments");settings.ShortcutOverrides=new Dictionary<string,int>();foreach(var value in values)settings.ShortcutOverrides.Add(value.Key,ReadInteger(values,value.Key));settings.ShortcutOverrides=ShortcutRegistry.NormalizeOverrides(settings.ShortcutOverrides);}
            if(root.ContainsKey("themeOverrides")){var values=AsObject(root["themeOverrides"],"Theme colors");settings.ThemeOverrides=new Dictionary<string,int>();foreach(var value in values)settings.ThemeOverrides.Add(value.Key,ReadInteger(values,value.Key));}
            if (Array.IndexOf(LocaleCatalog.SupportedLanguages, settings.Language) < 0)
                throw new InvalidDataException("Settings language is unsupported.");
            settings.AudioOutput=OptionalInt(root,"audioOutput",-1);settings.AudioInput=OptionalInt(root,"audioInput",-1);settings.MidiInput=OptionalInt(root,"midiInput",-1);settings.MidiOutput=OptionalInt(root,"midiOutput",-1);settings.SampleRate=OptionalInt(root,"sampleRate",48000);settings.BufferFrames=OptionalInt(root,"bufferFrames",512);settings.UiScale=OptionalInt(root,"uiScale",100);settings.DefaultTempo=root.ContainsKey("defaultTempo")?Convert.ToDouble(root["defaultTempo"],CultureInfo.InvariantCulture):120;settings.DefaultNumerator=OptionalInt(root,"defaultNumerator",4);settings.DefaultDenominator=OptionalInt(root,"defaultDenominator",4);
            object paths;if(root.TryGetValue("pluginPaths",out paths)&&paths is System.Collections.IList){settings.PluginPaths=new List<string>();foreach(object pathValue in (System.Collections.IList)paths){string pluginPath=pathValue as string;if(pluginPath==null)throw new InvalidDataException("Plugin search paths must be strings.");settings.PluginPaths.Add(pluginPath);}}
            Validate(settings);
            if (settings.TimelineZoom < 4 || settings.TimelineZoom > 144)
                throw new InvalidDataException("Timeline zoom must be between 4 and 144 pixels per beat.");
            return settings;
        }

        public static void Save(AppSettings settings, string path)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");
            if (Array.IndexOf(LocaleCatalog.SupportedLanguages, settings.Language) < 0)
                throw new InvalidDataException("Settings language is unsupported.");
            Validate(settings);
            if (settings.TimelineZoom < 4 || settings.TimelineZoom > 144)
                throw new InvalidDataException("Timeline zoom must be between 24 and 144 pixels per beat.");
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            Dictionary<string, object> values = new Dictionary<string, object>();
            values["audioBackend"]=settings.AudioBackend;values["outputEndpoint"]=settings.OutputEndpoint;values["inputEndpoint"]=settings.InputEndpoint;values["asioDriver"]=settings.AsioDriver;values["pdc"]=settings.PdcEnabled;values["integration"]=settings.Integration;
            values["inputMode"]=settings.InputMode;values["smoothAnimation"]=settings.SmoothAnimation;values["schema"] = "slopx-settings";
            values["version"] = 2;
            values["language"] = settings.Language;
            values["timelineZoom"] = settings.TimelineZoom;
            values["undoLimit"] = settings.UndoLimit;
            values["shortcutOverrides"] = settings.ShortcutOverrides;
            values["themeOverrides"] = settings.ThemeOverrides;
            values["audioOutput"]=settings.AudioOutput;values["audioInput"]=settings.AudioInput;values["midiInput"]=settings.MidiInput;values["midiOutput"]=settings.MidiOutput;values["sampleRate"]=settings.SampleRate;values["bufferFrames"]=settings.BufferFrames;values["uiScale"]=settings.UiScale;values["defaultTempo"]=settings.DefaultTempo;values["defaultNumerator"]=settings.DefaultNumerator;values["defaultDenominator"]=settings.DefaultDenominator;values["pluginPaths"]=settings.PluginPaths;
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = 64 * 1024;
            byte[] bytes = new UTF8Encoding(false).GetBytes(serializer.Serialize(values));
            string fullPath = Path.GetFullPath(path);
            string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(fullPath))
                    File.Replace(temporaryPath, fullPath, null);
                else
                    File.Move(temporaryPath, fullPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    try { File.Delete(temporaryPath); }
                    catch { }
                }
            }
        }

        public static void Validate(AppSettings s)
        {
            if(Array.IndexOf(new[]{"Mouse","Trackpad","Touchscreen","XInput Controller","DirectInput Controller"},s.InputMode)<0)throw new InvalidDataException("Unsupported UI input mode.");
            if(Array.IndexOf(new string[]{"MME","WASAPI","WASAPI Exclusive","ASIO"},s.AudioBackend)<0)throw new InvalidDataException("Unknown audio backend.");if(s.Integration==null)throw new InvalidDataException("Integration settings missing.");s.Integration.Validate();
            if(s.Integration.LaunchpadEnabled&&(s.Integration.LaunchpadInput==s.MidiInput||s.Integration.LaunchpadOutput==s.MidiOutput))throw new InvalidDataException("Choose separate Launchpad and instrument MIDI ports.");
            if(s.AudioOutput < -1||s.AudioInput < -1||s.MidiInput < -1||s.MidiOutput < -1)throw new InvalidDataException("A device identifier is invalid.");
            if(Array.IndexOf(new int[]{22050,44100,48000,88200,96000},s.SampleRate)<0)throw new InvalidDataException("The sample rate is unsupported.");
            if(Array.IndexOf(new int[]{128,256,512,1024,2048},s.BufferFrames)<0)throw new InvalidDataException("The buffer size is unsupported.");
            if(s.UiScale<60||s.UiScale>200)throw new InvalidDataException("UI scale must be 60–200 percent.");
            if(s.UndoLimit<1||s.UndoLimit>10000)throw new InvalidDataException("Undo history must contain 1–10000 steps.");
            ShortcutRegistry.ValidateOverrides(s.ShortcutOverrides);
            CyberTheme.ValidatePalette(s.ThemeOverrides);
            if(s.DefaultTempo<20||s.DefaultTempo>400||s.DefaultNumerator<1||s.DefaultNumerator>32||Array.IndexOf(new int[]{1,2,4,8,16,32},s.DefaultDenominator)<0)throw new InvalidDataException("Project defaults are invalid.");
            if(s.PluginPaths==null||s.PluginPaths.Count>32)throw new InvalidDataException("Plugin search paths are invalid.");foreach(string path in s.PluginPaths)if(String.IsNullOrWhiteSpace(path)||path.Length>32767)throw new InvalidDataException("A plugin search path is invalid.");
        }
        private static int OptionalInt(IDictionary<string,object> root,string key,int fallback){return root.ContainsKey(key)?ReadInteger(root,key):fallback;}
        private static object ReadValue(IDictionary<string, object> values, string key)
        {
            object value;
            if (!values.TryGetValue(key, out value) || value == null)
                throw new InvalidDataException("Required setting '" + key + "' is missing.");
            return value;
        }

        private static string ReadString(IDictionary<string, object> values, string key)
        {
            string value = ReadValue(values, key) as string;
            if (value == null)
                throw new InvalidDataException("Setting '" + key + "' must be a string.");
            return value;
        }

        private static int ReadInteger(IDictionary<string, object> values, string key)
        {
            object value = ReadValue(values, key);
            if (value is bool || !(value is IConvertible))
                throw new InvalidDataException("Setting '" + key + "' must be an integer.");
            double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (number != Math.Floor(number) || number < Int32.MinValue || number > Int32.MaxValue)
                throw new InvalidDataException("Setting '" + key + "' must be an integer.");
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
