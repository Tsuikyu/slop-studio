using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SlopProX
{
    public sealed class SampleBrowser : UserControl
    {
        private readonly TextBox _path, _search;
        private readonly ComboBox _places;
        private readonly ListView _files;
        private readonly Label _status;
        private readonly SampleWaveformPreview _waveform;
        private string _root;
        private SampleLibraryData _library;
        private bool _favorites, _updatingPlaces;
        private int _previewGeneration;
        public event Action<string> ImportRequested;
        public SampleBrowser()
        {
            Dock = DockStyle.Fill; BackColor = CyberTheme.Surface; ForeColor = CyberTheme.Text; Font = new Font("Segoe UI", 8.5f);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true };
            foreach (var pair in new[] { new[] { "Folder…", "folder" }, new[] { "Up", "up" }, new[] { "Preview", "preview" }, new[] { "Stop", "stop" }, new[] { "Import", "import" }, new[] { "Bookmark Folder +/−", "bookmark" }, new[] { "Favorite +/−", "favorite" } })
            {
                string action = pair[1];
                var button = new Button { Text = UiText.Translate(pair[0]), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(50, 27), Padding = new Padding(3, 0, 3, 0), FlatStyle = FlatStyle.Flat, ForeColor = CyberTheme.Text, BackColor = CyberTheme.Control };
                button.Click += async delegate
                {
                    try
                    {
                        if (action == "folder") { using (var dialog = new FolderBrowserDialog { SelectedPath = _root }) if (dialog.ShowDialog(this) == DialogResult.OK) LoadFolder(dialog.SelectedPath); }
                        else if (action == "up" && !String.IsNullOrEmpty(_root) && Directory.GetParent(_root) != null) LoadFolder(Directory.GetParent(_root).FullName);
                        else if (action == "stop") { _previewGeneration++; WaveAudio.Stop(); }
                        else if (action == "bookmark") ToggleBookmark();
                        else if (action == "favorite") ToggleFavorite();
                        else if (_files.SelectedItems.Count > 0)
                        {
                            string path = Convert.ToString(_files.SelectedItems[0].Tag);
                            if (action == "preview") await PreviewAsync(path);
                            else if (action == "import" && File.Exists(path) && ImportRequested != null) ImportRequested(path);
                        }
                    }
                    catch (Exception ex) { _status.Text = UiText.Translate(ex.Message); }
                };
                toolbar.Controls.Add(button);
            }
            _places=new ComboBox{Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=CyberTheme.Elevated,ForeColor=CyberTheme.Text};
            _places.SelectedIndexChanged+=delegate{if(_updatingPlaces)return;if(_places.SelectedIndex==1)ShowFavorites();else if(_places.SelectedIndex>=2){string location=Convert.ToString(_places.SelectedItem);LoadFolder(location);}else if(_favorites&&!String.IsNullOrEmpty(_root))LoadFolder(_root);};
            _path = new TextBox { Dock = DockStyle.Top, BackColor = CyberTheme.Elevated, ForeColor = CyberTheme.Text };
            _path.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { LoadFolder(_path.Text); e.SuppressKeyPress = true; } };
            _search = new TextBox { Dock = DockStyle.Top, BackColor = CyberTheme.Elevated, ForeColor = CyberTheme.Text };
            _search.TextChanged += delegate { Populate(); };
            _files = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, BackColor = CyberTheme.Elevated, ForeColor = CyberTheme.Text, BorderStyle = BorderStyle.None };
            _files.Columns.Add("Sample / Folder", 210); _files.Columns.Add("Type", 60); _files.Columns.Add("Size", 75);
            _files.SelectedIndexChanged += async delegate { if (_files.SelectedItems.Count > 0) await PreviewAsync(Convert.ToString(_files.SelectedItems[0].Tag)); };
            _files.DoubleClick += delegate { if (_files.SelectedItems.Count == 0) return; string path = Convert.ToString(_files.SelectedItems[0].Tag); if (Directory.Exists(path)) LoadFolder(path); else if (ImportRequested != null) ImportRequested(path); };
            _files.ItemDrag += delegate { if (_files.SelectedItems.Count > 0) DoDragDrop(new DataObject(DataFormats.FileDrop, new[] { Convert.ToString(_files.SelectedItems[0].Tag) }), DragDropEffects.Copy); };
            _status = new Label { Dock = DockStyle.Bottom, Height = 28, AutoEllipsis = true };
            _waveform = new SampleWaveformPreview { Dock = DockStyle.Bottom, Height = 118 };
            Controls.Add(_files); Controls.Add(_waveform); Controls.Add(_search); Controls.Add(_path); Controls.Add(_places); Controls.Add(toolbar); Controls.Add(_status);
            string libraryError=null;try{_library=SampleLibraryStore.Load(SampleLibraryStore.DefaultPath);}catch(Exception ex){libraryError=ex.Message;}
            LoadFolder(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Examples", "First Light", "Media"));
            if(libraryError!=null)_status.Text=UiText.Translate("Sample library could not be loaded")+": "+libraryError;
        }
        public void LoadFolder(string root)
        {
            if (!Directory.Exists(root)) { _status.Text = UiText.Translate("Folder unavailable") + ": " + root; RefreshPlaces(); return; }
            _root = Path.GetFullPath(root); _favorites=false;_path.ReadOnly=false;_path.Text = _root; RefreshPlaces();Populate();
        }
        private void ShowFavorites(){_favorites=true;_path.ReadOnly=true;_path.Text=UiText.Translate("Favorites");RefreshPlaces();Populate();}
        private void RefreshPlaces(){_updatingPlaces=true;try{_places.Items.Clear();_places.Items.Add(UiText.Translate("Current Folder"));_places.Items.Add(UiText.Translate("Favorites"));if(_library!=null)foreach(string path in _library.Bookmarks)_places.Items.Add(path);int selected=_favorites?1:0;if(!_favorites&&_library!=null){int i=_library.Bookmarks.FindIndex(x=>String.Equals(x,_root,StringComparison.OrdinalIgnoreCase));if(i>=0)selected=i+2;}_places.SelectedIndex=selected;}finally{_updatingPlaces=false;}}
        private void SaveLibrary(){if(_library==null)throw new InvalidDataException("Sample library is unavailable; the original settings file has not been changed.");SampleLibraryStore.Save(_library,SampleLibraryStore.DefaultPath);}
        private void ToggleBookmark(){if(_library==null)throw new InvalidDataException("Sample library is unavailable.");if(String.IsNullOrEmpty(_root)||_favorites)return;bool added=SampleLibraryStore.Toggle(_library.Bookmarks,_root);try{SaveLibrary();}catch{SampleLibraryStore.Toggle(_library.Bookmarks,_root);throw;}RefreshPlaces();_status.Text=UiText.Translate(added?"Folder bookmarked":"Folder bookmark removed");}
        private void ToggleFavorite(){if(_library==null)throw new InvalidDataException("Sample library is unavailable.");if(_files.SelectedItems.Count==0)return;string file=Convert.ToString(_files.SelectedItems[0].Tag);bool already=_library.Favorites.Any(x=>String.Equals(x,file,StringComparison.OrdinalIgnoreCase));if(!already&&(!File.Exists(file)||!MediaImport.IsAudio(file)))throw new InvalidOperationException("Select an available audio sample.");bool added=SampleLibraryStore.Toggle(_library.Favorites,file);try{SaveLibrary();}catch{SampleLibraryStore.Toggle(_library.Favorites,file);throw;}if(_favorites)Populate();_status.Text=UiText.Translate(added?"Added to Favorites":"Removed from Favorites");}
        private void Populate()
        {
            if (String.IsNullOrEmpty(_root)&&!_favorites) return; _files.Items.Clear();
            try
            {
                if(_favorites){if(_library!=null)foreach(string file in _library.Favorites){string name=Path.GetFileName(file);if(name.IndexOf(_search.Text,StringComparison.OrdinalIgnoreCase)<0)continue;bool exists=File.Exists(file);_files.Items.Add(new ListViewItem(new[]{name,exists?Path.GetExtension(file).TrimStart('.').ToUpperInvariant():UiText.Translate("Missing"),exists?(new FileInfo(file).Length/1024)+" KiB":"—"}){Tag=file});}_status.Text=_files.Items.Count+" · "+UiText.Translate("Favorites (original files are not moved)");return;}
                foreach (string folder in Directory.GetDirectories(_root)) { string name = Path.GetFileName(folder); if (name.IndexOf(_search.Text, StringComparison.OrdinalIgnoreCase) >= 0) _files.Items.Add(new ListViewItem(new[] { name, "Folder", "" }) { Tag = folder }); }
                foreach (string file in Directory.GetFiles(_root).Where(f => MediaImport.IsAudio(f) || Path.GetExtension(f).Equals(".mid", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(f).Equals(".midi", StringComparison.OrdinalIgnoreCase)))
                { string name = Path.GetFileName(file); if (name.IndexOf(_search.Text, StringComparison.OrdinalIgnoreCase) >= 0) _files.Items.Add(new ListViewItem(new[] { name, Path.GetExtension(file).TrimStart('.').ToUpperInvariant(), (new FileInfo(file).Length / 1024) + " KiB" }) { Tag = file }); }
                _status.Text = _files.Items.Count + " · " + UiText.Translate("Supported audio and MIDI files · double-click to import");
            }
            catch (Exception ex) { _status.Text = ex.Message; }
        }
        private async Task PreviewAsync(string path)
        {
            int generation = ++_previewGeneration; WaveAudio.Stop();
            if (!File.Exists(path) || !MediaImport.IsAudio(path)) { _waveform.SourcePath = null; _status.Text = UiText.Translate("Preview is available for audio files. Import MIDI to audition with an instrument."); return; }
            _status.Text = UiText.Translate("Preparing preview…");
            try
            {
                string folder = Path.Combine(Path.GetDirectoryName(SettingsStore.DefaultPath), "Preview Cache");
                string decoded = await Task.Factory.StartNew(() => MediaImport.Decode(path, folder));
                if (IsDisposed || generation != _previewGeneration) return;
                _waveform.SourcePath = decoded; WaveAudio.Play(decoded); _status.Text = Path.GetFileName(path);
            }
            catch (Exception ex) { if (!IsDisposed && generation == _previewGeneration) _status.Text = ex.Message; }
        }
        protected override void Dispose(bool disposing) { if (disposing) { _previewGeneration++; WaveAudio.Stop(); } base.Dispose(disposing); }
    }
}
