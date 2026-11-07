using System;
using System.IO;
using System.Windows.Forms;

namespace SlopProX
{
    internal static class Program
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]private static extern uint SetErrorMode(uint mode);
        [STAThread]
        private static void Main(string[] args)
        {
            if(args!=null&&args.Length==2&&args[0]=="--export-shortcuts"){var rows=new System.Collections.Generic.List<object>();foreach(var b in ShortcutRegistry.Bindings)rows.Add(new {command=b.Command.ToString(),logicCommand=b.LogicCommand,mac=b.MacShortcut,windows=b.WindowsShortcut,origin=b.Origin.ToString(),source=b.SourceUrl,active=b.Implemented,arrangementOnly=b.ArrangementOnly,note=b.Note});File.WriteAllText(args[1],new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(rows));return;}
            if(args!=null&&args.Length==2&&args[0]=="--plugin-worker"){SetErrorMode(0x8003);try{PluginWorker.Run(args[1]);}catch(Exception ex){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"worker-startup-error.log"),ex.ToString());Environment.ExitCode=74;}return;}
            if(args!=null&&args.Length==3&&args[0]=="--scan-plugin")
            {
                try{SetErrorMode(0x8003);Application.OleRequired();var plugins=LocalPlugin.Scan(args[1]);File.WriteAllText(args[2],new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(plugins));Environment.ExitCode=0;}
                catch(Exception ex){File.WriteAllText(args[2],ex.Message);Environment.ExitCode=1;}return;
            }
            if(args.Length==3&&args[0]=="--lua-run"){LuaMods.RunWorker(args[1],args[2]);return;}
            bool smoke = args != null && args.Length > 0 && String.Equals(args[0], "--smoke", StringComparison.Ordinal);
            string reportPath = smoke && args.Length > 1 ? args[1] : null;
            if (smoke && String.IsNullOrWhiteSpace(reportPath))
            {
                Environment.ExitCode = 2;
                return;
            }

            if(smoke)Environment.SetEnvironmentVariable("SLOPX_PROFILE",Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath)),"smoke-profile"));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string localeDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "locales");
            AppSettings settings;
            string settingsWarning = null;
            if (smoke)
            {
                settings = new AppSettings();
                if (args.Length > 2 && Array.IndexOf(LocaleCatalog.SupportedLanguages, args[2]) >= 0)
                    settings.Language = args[2];
            }
            else
                settings = SettingsStore.LoadOrDefault(out settingsWarning);

            LocaleCatalog locale;
            try
            {
                string localePath = Path.Combine(localeDirectory, settings.Language + ".json");
                locale = LocaleCatalog.Load(localePath, settings.Language);
            }
            catch (Exception)
            {
                settings.Language = "en-US";
                string localePath = Path.Combine(localeDirectory, "en-US.json");
                try { locale = LocaleCatalog.Load(localePath, "en-US"); }
                catch
                {
                    Environment.ExitCode = 1;
                    MessageBox.Show("The English language pack could not be loaded.", "Slop Studio",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            UiText.Load(settings.Language);Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);Application.ThreadException+=delegate(object sender,System.Threading.ThreadExceptionEventArgs e){ErrorLog.Write("UI event",e.Exception);if(smoke){try{File.AppendAllText(reportPath,"FAIL UI event "+e.Exception+Environment.NewLine);}catch{}Environment.ExitCode=1;Application.ExitThread();return;}MessageBox.Show(UiText.Translate("The operation failed. Details were saved to errors.log.")+Environment.NewLine+e.Exception.Message,"Slop Studio",MessageBoxButtons.OK,MessageBoxIcon.Error);};
            SplashForm splash=null;if(!smoke&&Array.IndexOf(args,"--mcp")<0){splash=new SplashForm();splash.Show();splash.SetProgress(20,"Settings and language loaded");Application.DoEvents();splash.SetProgress(40,"Preparing workspace…");}
            MainForm form;
            try
            {
                form = new MainForm(settings, locale, smoke, reportPath);if(splash!=null){splash.SetProgress(75,"Opening audio and session…");form.Shown+=delegate{splash.SetProgress(100,"Ready");splash.Close();splash.Dispose();};}form.McpMode=Array.IndexOf(args,"--mcp")>=0;for(int i=0;i<args.Length;i++)if(File.Exists(args[i])&&Array.IndexOf(new[]{".slopdaw",".sloptemplate",".mid",".midi",".flp",".als",".dawproject"},Path.GetExtension(args[i]).ToLowerInvariant())>=0)form.StartupProjectPath=args[i];
            }
            catch (Exception exception)
            {
                if(splash!=null){splash.Close();splash.Dispose();}
                Environment.ExitCode = 1;
                if (smoke)
                {
                    try { File.WriteAllText(reportPath, "FAIL startup " + exception.GetType().Name + ": " + exception.Message); }
                    catch { }
                }
                else
                    MessageBox.Show("Slop Studio could not start: " + exception.Message, "Slop Studio",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!smoke && !String.IsNullOrEmpty(settingsWarning))
            {
                form.Shown += delegate
                {
                    MessageBox.Show(form, locale.T("error.settingsFailed") + Environment.NewLine + Environment.NewLine + settingsWarning,
                        locale.T("dialog.errorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                };
            }
            try
            {
                Application.Run(form);
            }
            catch (Exception exception)
            {
                Environment.ExitCode = 1;
                MessageBox.Show("Slop Studio could not continue: " + exception.Message, "Slop Studio",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
