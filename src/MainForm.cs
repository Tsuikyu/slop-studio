using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace SlopStudio
{
    public sealed partial class MainForm : Form
    {
        private readonly bool _smokeMode;
        private readonly string _smokeReportPath;
        private AppSettings _settings;
        private StudioControlBar _controlBar; private ArrangementToolbar _arrangementToolbar; private ArrangementEngine _engine;
        private volatile bool _recording; private WaveCaptureStream _capture; private MidiRecordingSession _midiRecording; private double _recordStartBeat; private Guid? _recordAudioTrack; private List<Guid> _recordMidiTracks=new List<Guid>();
        private List<PluginEditorForm> _pluginWindows=new List<PluginEditorForm>(); private Region _clipboardRegion; private TrackType _clipboardType;
        private LocaleCatalog _locale;
        private ProjectDocument _project;
        private ProjectHistory _history;
        private string _projectPath;private string _audioDeviceError;
        private Guid? _selectedTrackId;
        private Guid? _selectedRegionId;
        private bool _refreshing;
        private bool _closing;

        private MenuStrip _menu;
        private ToolStripMenuItem _fileMenu;
        private ToolStripMenuItem _editMenu;
        private ToolStripMenuItem _viewMenu;
        private ToolStripMenuItem _helpMenu;
        private ToolStripMenuItem _newMenuItem;
        private ToolStripMenuItem _openMenuItem;
        private ToolStripMenuItem _saveMenuItem;
        private ToolStripMenuItem _saveAsMenuItem;
        private ToolStripMenuItem _exitMenuItem;
        private ToolStripMenuItem _undoMenuItem;
        private ToolStripMenuItem _redoMenuItem;
        private ToolStripMenuItem _addAudioMenuItem;
        private ToolStripMenuItem _addMidiMenuItem;
        private ToolStripMenuItem _addRegionMenuItem;
        private ToolStripMenuItem _importAudioMenuItem;
        private ToolStripMenuItem _playMenuItem;
        private ToolStripMenuItem _stopMenuItem;
        private ToolStripMenuItem _renameMenuItem;
        private ToolStripMenuItem _deleteRegionMenuItem;
        private ToolStripMenuItem _deleteTrackMenuItem;
        private ToolStripMenuItem _languageMenuItem;
        private ToolStripMenuItem _aboutMenuItem;
        private readonly Dictionary<string, ToolStripMenuItem> _languageMenuItems = new Dictionary<string, ToolStripMenuItem>();
        private ToolStrip _toolbar;
        private ToolStripButton _newButton;
        private ToolStripButton _openButton;
        private ToolStripButton _saveButton;
        private ToolStripButton _playButton;
        private ToolStripButton _stopButton;
        private ToolStripButton _importAudioButton;
        private ToolStripButton _undoButton;
        private ToolStripButton _redoButton;
        private ToolStripButton _addTrackButton;
        private ToolStripButton _addRegionButton;
        private ToolStripButton _zoomOutButton;
        private ToolStripButton _zoomInButton;
        private NumericUpDown _tempo;
        private ToolStripControlHost _tempoHost;
        private ToolStripLabel _tempoLabel;
        private ToolStripLabel _noticeLabel;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;
        private TimelineEditor _timeline;
        private ProjectBrowserPanel _browser;private ProjectMediaBrowser _mediaBrowser;private SplitContainer _mediaSplit;
        private TrackInspectorPanel _inspector;
        private MixerPanel _mixer;
        private PianoRollEditor _pianoRoll; private AudioRegionEditor _audioEditor;
        private LowerWorkspacePanel _lowerDock;
        private SplitContainer _workspaceSplit;
        private SplitContainer _columnsSplit;
        private ToolStripMenuItem _showBrowserMenuItem;
        private ToolStripMenuItem _showInspectorMenuItem;
        private ToolStripMenuItem _showMixerMenuItem;
        private ToolStripLabel _positionLabel;
        private double _cursorBeat;
        private Timer _transportTimer;

        public MainForm(AppSettings settings, LocaleCatalog locale, bool smokeMode, string smokeReportPath)
        {
            _settings = settings ?? new AppSettings();CyberTheme.ApplyPalette(_settings.ThemeOverrides);ShortcutRegistry.ApplyOverrides(_settings.ShortcutOverrides);
            _locale = locale;
            _smokeMode = smokeMode;
            _smokeReportPath = smokeReportPath;
            _history = new ProjectHistory();_history.Limit=_settings.UndoLimit;
            _project = ProjectFactory.Create(_locale.T("project.untitled"), TrackDefaultName(TrackType.Audio, 1));
            _project.Tracks[0].Type=TrackType.Midi;_project.Tracks[0].Name="Classic Electric Piano";_project.Tracks[0].Armed=true;_project.Tempo=_settings.DefaultTempo;_project.TimeSignatureNumerator=_settings.DefaultNumerator;_project.TimeSignatureDenominator=_settings.DefaultDenominator;
            _selectedTrackId = _project.Tracks[0].Id;
            _engine=new ArrangementEngine(_settings);_engine.Failed+=OnEngineFailure;_engine.MidiReceived+=OnMidiInput;PluginCatalog.Load();

            try{Icon=Icon.ExtractAssociatedIcon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SlopStudio.exe"));}catch{}
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle=FormBorderStyle.Sizable;
            MaximizedBounds=Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(1120, 720);
            Rectangle initialArea=Screen.FromControl(this).WorkingArea;Size=new Size(Math.Max(1120,Math.Min(1440,initialArea.Width)),Math.Max(720,Math.Min(900,initialArea.Height)));
            WindowState = FormWindowState.Maximized;
            KeyPreview = true;
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = CyberTheme.Background;
            ForeColor = CyberTheme.Text;
            UiText.Load(_settings.Language);BuildInterface();InitializeV7Features();InitializeStudioMenus();InitializeFullScreen();
            ApplyLocalization();
            RefreshState();
            FormClosing += OnFormClosing;
            Shown += OnShown;
        }

        private string T(string key) { return _locale.T(key); }

        private void BuildInterface()
        {
            _menu = new MenuStrip();
            _menu.Dock = DockStyle.Fill;
            _menu.Margin = Padding.Empty;
            _menu.AutoSize = false;
            _menu.Height = 28;
            _menu.BackColor = CyberTheme.Background;
            _menu.ForeColor = CyberTheme.Text;
            _menu.Renderer = CreateDarkRenderer();
            ToolStripManager.Renderer = CreateDarkRenderer();
            _menu.Padding = new Padding(8, 3, 8, 3);
            _fileMenu = new ToolStripMenuItem();
            _editMenu = new ToolStripMenuItem();
            _viewMenu = new ToolStripMenuItem();
            _helpMenu = new ToolStripMenuItem();
            _newMenuItem = AddMenuItem(_fileMenu, delegate { ExecuteCommand(AppCommand.NewProject); });
            _openMenuItem = AddMenuItem(_fileMenu, delegate { ExecuteCommand(AppCommand.OpenProject); });
            _saveMenuItem = AddMenuItem(_fileMenu, delegate { ExecuteCommand(AppCommand.SaveProject); });
            _saveAsMenuItem = AddMenuItem(_fileMenu, delegate { SaveProjectAs(); });
            _fileMenu.DropDownItems.Add(new ToolStripSeparator());
            _exitMenuItem = AddMenuItem(_fileMenu, delegate { Close(); });
            _undoMenuItem = AddMenuItem(_editMenu, delegate { ExecuteCommand(AppCommand.Undo); });
            _redoMenuItem = AddMenuItem(_editMenu, delegate { ExecuteCommand(AppCommand.Redo); });
            _editMenu.DropDownItems.Add(new ToolStripSeparator());
            _addAudioMenuItem = AddMenuItem(_editMenu, delegate { ExecuteCommand(AppCommand.AddAudioTrack); });
            _addMidiMenuItem = AddMenuItem(_editMenu, delegate { ExecuteCommand(AppCommand.AddMidiTrack); });
            _addRegionMenuItem = AddMenuItem(_editMenu, delegate { ExecuteCommand(AppCommand.AddRegion); });
            _importAudioMenuItem = AddMenuItem(_editMenu, delegate { ExecuteCommand(AppCommand.ImportAudio); });
            _editMenu.DropDownItems.Add(new ToolStripSeparator());
            _playMenuItem = AddMenuItem(_editMenu, delegate { ExecuteCommand(AppCommand.PlayOrStop); });
            _stopMenuItem = AddMenuItem(_editMenu, delegate { ExecuteCommand(AppCommand.Stop); });
            _renameMenuItem = AddMenuItem(_editMenu, delegate { ExecuteCommand(_selectedRegionId.HasValue ? AppCommand.RenameRegion : AppCommand.RenameTrack); });
            _deleteRegionMenuItem = AddMenuItem(_editMenu, delegate { ExecuteCommand(AppCommand.DeleteRegion); });
            _deleteTrackMenuItem = AddMenuItem(_editMenu, delegate { ExecuteCommand(AppCommand.DeleteTrack); });
            _languageMenuItem = new ToolStripMenuItem();
            BuildLanguageMenu();
            _showBrowserMenuItem = AddMenuItem(_viewMenu, delegate { ExecuteCommand(AppCommand.ToggleBrowser); });
            _showInspectorMenuItem = AddMenuItem(_viewMenu, delegate { ExecuteCommand(AppCommand.ToggleInspector); });
            _showMixerMenuItem = AddMenuItem(_viewMenu, delegate { ExecuteCommand(AppCommand.ToggleMixer); });
            _viewMenu.DropDownItems.Add(new ToolStripSeparator());
            _viewMenu.DropDownItems.Add(_languageMenuItem);
            _aboutMenuItem = AddMenuItem(_helpMenu, delegate { ShowAbout(); });
            _menu.Items.AddRange(new ToolStripItem[] { _fileMenu, _editMenu, _viewMenu, _helpMenu });
            MainMenuStrip = _menu;

            _toolbar = new ToolStrip();
            _toolbar.Dock = DockStyle.Fill;
            _toolbar.AutoSize = false;
            _toolbar.Height = 48;
            _toolbar.BackColor = CyberTheme.Surface;
            _toolbar.ForeColor = CyberTheme.Text;
            _toolbar.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
            _toolbar.Padding = new Padding(6, 2, 6, 2);
            _toolbar.GripStyle = ToolStripGripStyle.Hidden;
            _toolbar.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
            _toolbar.ImageScalingSize = new Size(20, 20);
            _newButton = AddToolbarButton(delegate { ExecuteCommand(AppCommand.NewProject); });
            _openButton = AddToolbarButton(delegate { ExecuteCommand(AppCommand.OpenProject); });
            _saveButton = AddToolbarButton(delegate { ExecuteCommand(AppCommand.SaveProject); });
            _toolbar.Items.Add(new ToolStripSeparator());
            _playButton = AddToolbarButton(delegate { ExecuteCommand(AppCommand.PlayOrStop); });
            _stopButton = AddToolbarButton(delegate { ExecuteCommand(AppCommand.Stop); });
            _playButton.Text = "▶"; _playButton.ToolTipText = "Play / Stop (Space)";
            _stopButton.Text = "■"; _stopButton.ToolTipText = "Stop";
            _playButton.Font = new Font("Segoe UI Symbol", 12.0f, FontStyle.Bold);
            _stopButton.Font = new Font("Segoe UI Symbol", 12.0f, FontStyle.Bold);
            _playButton.ForeColor = Color.FromArgb(44, 146, 74);
            _stopButton.ForeColor = Color.FromArgb(120, 45, 44);
            _stopButton.Enabled = false;
            _toolbar.Items.Add(new ToolStripSeparator());
            _positionLabel = new ToolStripLabel("1 1 1 1");
            _positionLabel.Font = new Font("Consolas", 11.0f, FontStyle.Bold, GraphicsUnit.Point);
            _positionLabel.ForeColor = Color.FromArgb(228, 231, 235);
            _positionLabel.BackColor = Color.FromArgb(34, 36, 40);
            _positionLabel.Padding = new Padding(10, 4, 10, 4);
            _toolbar.Items.Add(_positionLabel);
            _toolbar.Items.Add(new ToolStripSeparator());
            _importAudioButton = AddToolbarButton(delegate { ExecuteCommand(AppCommand.ImportAudio); });
            _toolbar.Items.Add(new ToolStripSeparator());
            _undoButton = AddToolbarButton(delegate { ExecuteCommand(AppCommand.Undo); });
            _redoButton = AddToolbarButton(delegate { ExecuteCommand(AppCommand.Redo); });
            _toolbar.Items.Add(new ToolStripSeparator());
            _addTrackButton = AddToolbarButton(delegate { ExecuteCommand(AppCommand.AddAudioTrack); });
            _addRegionButton = AddToolbarButton(delegate { ExecuteCommand(AppCommand.AddRegion); });
            _toolbar.Items.Add(new ToolStripSeparator());
            _zoomOutButton = AddToolbarButton(delegate { ExecuteCommand(AppCommand.ZoomOut); });
            _zoomInButton = AddToolbarButton(delegate { ExecuteCommand(AppCommand.ZoomIn); });
            _tempoLabel = new ToolStripLabel();
            _toolbar.Items.Add(_tempoLabel);
            _tempo = new NumericUpDown();
            _tempo.Minimum = 20;
            _tempo.Maximum = 400;
            _tempo.DecimalPlaces = 1;
            _tempo.Increment = 1;
            _tempo.Value = 120;
            _tempo.Width = 88;
            _tempo.Font = new Font("Segoe UI", 10.0f, FontStyle.Regular, GraphicsUnit.Point);
            _tempo.BackColor = Color.FromArgb(20, 22, 26);
            _tempo.ForeColor = Color.FromArgb(240, 242, 245);
            _tempoHost = new ToolStripControlHost(_tempo);
            _tempoHost.AutoSize = false;
            _tempoHost.Width = 94;
            _tempoHost.Height = 30;
            _toolbar.Items.Add(_tempoHost);
            _toolbar.Items.Add(new ToolStripLabel("BPM"));
            _noticeLabel = new ToolStripLabel();
            _noticeLabel.Alignment = ToolStripItemAlignment.Right;
            _noticeLabel.ForeColor = Color.FromArgb(46, 47, 49);
            _toolbar.Items.Add(_noticeLabel);
            _tempo.ValueChanged += OnTempoChanged;
            _controlBar=new StudioControlBar();_controlBar.Margin=Padding.Empty;_controlBar.Command+=ExecuteCommand;_controlBar.TempoChanged+=SetTempo;_controlBar.MeterChanged+=SetMeter;_controlBar.MasterChanged+=SetMasterGain;
            _controlBar.DragWindow+=delegate{if(!_fullScreen){ReleaseCapture();SendMessage(Handle,0x00A1,(IntPtr)2,IntPtr.Zero);}};
            _arrangementToolbar=new ArrangementToolbar();_arrangementToolbar.Command+=ExecuteCommand;_arrangementToolbar.SnapChanged+=delegate(double value){_timeline.SetSnap(value);};_arrangementToolbar.ToolChanged+=delegate(string tool){_timeline.SetTool(tool);};_arrangementToolbar.LaneHeightChanged+=delegate(int height){_timeline.SetLaneHeight(height);};

            _timeline = new TimelineEditor(_settings.TimelineZoom);
            _timeline.Dock = DockStyle.Fill;
            _timeline.SetLocale(_locale);
            _timeline.SelectionChanged += OnTimelineSelectionChanged;
            _timeline.RegionMoveRequested += OnRegionMoveRequested;
            _timeline.RenameSelectionRequested += delegate { ExecuteCommand(_selectedRegionId.HasValue ? AppCommand.RenameRegion : AppCommand.RenameTrack); };
            _timeline.PlayheadRequested += OnPlayheadRequested;
            _timeline.TrackMixRequested += OnTrackMixRequested;
            _timeline.TrackArmRequested+=OnTrackArmRequested;_timeline.TrackReorderRequested+=OnTrackReorderRequested;_timeline.RegionTrimRequested+=OnRegionTrimRequested;_timeline.RegionCreateRequested+=OnRegionCreateRequested;_timeline.RegionSplitRequested+=OnRegionSplitRequested;
            _timeline.CycleRangeRequested+=delegate(object sender,CycleRangeEventArgs e){_history.Execute(_project,new SetCycleRangeCommand(_project,e.Start,e.End));RefreshState();};_timeline.JoinRequested+=delegate{ExecuteCommand(AppCommand.JoinRegions);};_timeline.StackRequested+=CreateTrackStack;_timeline.ToggleStackRequested+=ToggleTrackStack;_timeline.RegionEditorRequested+=delegate{OpenEditor();};_timeline.AddTrackRequested+=delegate{ShowNewTrackMenu();};_timeline.DuplicateTrackRequested+=delegate{DuplicateTrack();};
            _browser = new ProjectBrowserPanel();
            _inspector = new TrackInspectorPanel();
            _mixer = new MixerPanel();
            _pianoRoll = new PianoRollEditor();
            _lowerDock = new LowerWorkspacePanel(_mixer, _pianoRoll);
            _audioEditor=new AudioRegionEditor();_lowerDock.SetAudioEditor(_audioEditor);_audioEditor.TrimRequested+=OnRegionTrimRequested;_audioEditor.PlayheadRequested+=OnPlayheadRequested;
            _pianoRoll.NotesChanged += OnMidiNotesChanged;_pianoRoll.PlayheadRequested+=delegate(object sender,PlayheadRequestEventArgs e){SetCursorBeat(e.Beat);};
            _browser.SelectionRequested += delegate(object sender, StudioSelectionEventArgs args) { Select(args.TrackId, args.RegionId); };
            _browser.AddMidiRequested += delegate { ExecuteCommand(AppCommand.AddMidiTrack); };
            _browser.ImportRequested += delegate { ExecuteCommand(AppCommand.ImportAudio); };
            _inspector.TrackEditRequested += OnInspectorTrackEdit;_inspector.RegionOptionsRequested+=delegate(object sender,InspectorRegionOptionsEventArgs e){Track t=FindTrack(e.TrackId);Region r=FindRegion(t,e.RegionId);if(r==null)return;_history.Execute(_project,new SetRegionOptionsCommand(t,r,e.Muted,e.Transpose,e.VelocityOffset));RefreshState();};_inspector.QuantizeRequested+=QuantizeNotes;
            _inspector.RegionEditRequested += OnInspectorRegionEdit;
            _mixer.MixChanged += OnMixerMixChanged;_mixer.SelectionRequested+=delegate(Guid id){Select(id,null);};
            _inspector.ArmChanged+=OnTrackArmRequested;_mixer.ArmChanged+=OnTrackArmRequested;_inspector.TogglePluginRequested+=TogglePlugin;_mixer.TogglePluginRequested+=TogglePlugin;_mixer.InstrumentRequested+=delegate(Track t){Select(t.Id,null);ShowInstrumentMenu(t);};_inspector.MixChanged+=OnMixerMixChanged;_inspector.AddPluginRequested+=BrowsePlugin;_mixer.AddPluginRequested+=BrowsePlugin;_inspector.EditPluginRequested+=EditPlugin;_mixer.EditPluginRequested+=EditPlugin;_inspector.RemovePluginRequested+=RemovePlugin;_mixer.RemovePluginRequested+=RemovePlugin;_mixer.ReorderInsertRequested+=ReorderInsert;_inspector.ReorderInsertRequested+=ReorderInsert;_mixer.RoutingRequested+=ShowAuxRouting;_inspector.RoutingRequested+=ShowAuxRouting;_inspector.MasterGainChanged+=SetMasterGain;_mixer.MasterGainChanged+=SetMasterGain;_inspector.InstrumentRequested+=delegate{ShowInstrumentMenu(SelectedTrack);};_inspector.InstrumentEditRequested+=delegate{OpenSampler(SelectedTrack);};_mixer.InstrumentEditRequested+=OpenSampler;_browser.PresetRequested+=SelectPreset;_browser.PluginRequested+=delegate(PluginDescriptor d){Track t=_selectedTrackId.HasValue?FindTrack(_selectedTrackId.Value):null;if(t!=null)LoadPlugin(t,d);};

            _statusStrip = new StatusStrip();
            _statusStrip.Dock = DockStyle.Fill;
            _statusStrip.BackColor = CyberTheme.Surface;
            _statusStrip.ForeColor = Color.FromArgb(198, 204, 212);
            _statusStrip.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
            _statusLabel = new ToolStripStatusLabel();
            _statusLabel.Spring = true;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _statusStrip.Items.Add(_statusLabel);

            TableLayoutPanel shell = new TableLayoutPanel();
            shell.Dock = DockStyle.Fill;
            shell.Margin = Padding.Empty;
            shell.Padding = Padding.Empty;
            shell.ColumnCount = 1;
            shell.RowCount = 4;
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            shell.Controls.Add(_menu, 0, 0);
            shell.Controls.Add(_controlBar, 0, 1);
            _columnsSplit = new SplitContainer { Dock=DockStyle.Fill, Orientation=Orientation.Vertical, SplitterWidth=8, BackColor=Color.FromArgb(28,28,28), Size=new Size(1600,850),FixedPanel=FixedPanel.Panel1 };
            _columnsSplit.SplitterDistance=212;_columnsSplit.Panel1MinSize=150;_columnsSplit.Panel2MinSize=530;_columnsSplit.Panel1.Controls.Add(_browser);
            SplitContainer inspectorSplit=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Vertical,SplitterWidth=8,BackColor=Color.FromArgb(28,28,28),Size=new Size(1380,850),FixedPanel=FixedPanel.Panel1};
            inspectorSplit.SplitterDistance=152;inspectorSplit.Panel1MinSize=138;inspectorSplit.Panel2MinSize=350;inspectorSplit.Panel1.Controls.Add(_inspector);
            _workspaceSplit=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterWidth=8,BackColor=Color.FromArgb(30,30,30),Size=new Size(1200,850)};
            _workspaceSplit.SplitterDistance=510;_workspaceSplit.Panel1MinSize=160;_workspaceSplit.Panel2MinSize=190;
            Panel arrangement=new Panel{Dock=DockStyle.Fill};arrangement.Controls.Add(_timeline);arrangement.Controls.Add(_arrangementToolbar);_workspaceSplit.Panel1.Controls.Add(arrangement);_workspaceSplit.Panel2.Controls.Add(_lowerDock);
            inspectorSplit.Panel2.Controls.Add(_workspaceSplit);_columnsSplit.Panel2.Controls.Add(inspectorSplit);_workspaceSplit.Panel2Collapsed=true;
            _mediaBrowser=new ProjectMediaBrowser();_mediaBrowser.SelectionRequested+=delegate(object sender,StudioSelectionEventArgs e){Select(e.TrackId,e.RegionId);};_mediaBrowser.ImportRequested+=delegate{ExecuteCommand(AppCommand.ImportAudio);};_mediaSplit=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Vertical,Size=new Size(1600,850),SplitterWidth=8,FixedPanel=FixedPanel.Panel2,BackColor=Color.FromArgb(29,29,29)};_mediaSplit.SplitterDistance=1350;_mediaSplit.Panel1MinSize=820;_mediaSplit.Panel2MinSize=175;_mediaSplit.Panel1.Controls.Add(_columnsSplit);_mediaSplit.Panel2.Controls.Add(_mediaBrowser);_mediaSplit.Panel2Collapsed=true;shell.Controls.Add(_mediaSplit,0,2);
            shell.Controls.Add(_statusStrip, 0, 3);
            shell.BackColor = BackColor;
            Controls.Add(shell);

            _transportTimer = new Timer();
            _transportTimer.Interval = 33;
            _transportTimer.Tick += OnTransportTimerTick;
            ToolStripMenuItem trackMenu=new ToolStripMenuItem("Track");_menu.Items.Insert(2,trackMenu);AddCommandMenu(trackMenu,AppCommand.AddAudioTrack,"New Audio Track");AddCommandMenu(trackMenu,AppCommand.AddMidiTrack,"New Software Instrument Track");AddCommandMenu(trackMenu,AppCommand.AddExternalMidiTrack,"New External MIDI Track");AddCommandMenu(trackMenu,AppCommand.DuplicateTrack,"New Track with Duplicate Settings");AddCommandMenu(trackMenu,AppCommand.DuplicateTrackWithContent,"Duplicate Settings and Content");AddCommandMenu(trackMenu,AppCommand.ToggleArm,"Record Enable");ToolStripMenuItem transportMenu=new ToolStripMenuItem("Transport");_menu.Items.Insert(3,transportMenu);foreach(AppCommand command in new[]{AppCommand.PlayOrStop,AppCommand.Stop,AppCommand.Pause,AppCommand.Record,AppCommand.GoToBeginning,AppCommand.RewindBar,AppCommand.ForwardBar,AppCommand.ToggleCycle})AddCommandMenu(transportMenu,command,command.ToString());
            AddMenuItem(_fileMenu,delegate{OpenProjectPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Examples","First Light","First Light.slopdaw"));}).Text="Open Example — First Light";AddMenuItem(_fileMenu,delegate{OpenProjectPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Examples","J-Pop - Shibuya.slopdaw"));}).Text="Open Example — J-Pop - Shibuya";InitializeAdvancedFeatures();
            AddCommandMenu(_fileMenu,AppCommand.OpenPreferences,"Settings…");AddCommandMenu(_editMenu,AppCommand.Record,"Record");AddMenuItem(_editMenu,delegate{BrowsePlugin(_selectedTrackId.HasValue?FindTrack(_selectedTrackId.Value):null);}).Text="Audio Plug-ins…";
        }

        private ToolStripMenuItem AddCommandMenu(ToolStripMenuItem parent,AppCommand command,string title){ToolStripMenuItem item=AddMenuItem(parent,delegate{ExecuteCommand(command);});item.Text=title;SetShortcut(item,command);return item;}

        private ToolStripMenuItem AddMenuItem(ToolStripMenuItem parent, Action action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem();
            item.Click += delegate { action(); };
            parent.DropDownItems.Add(item);
            return item;
        }

        private ToolStripButton AddToolbarButton(Action action)
        {
            ToolStripButton button = new ToolStripButton();
            button.DisplayStyle = ToolStripItemDisplayStyle.Text;
            button.AutoSize = true;
            button.Font = new Font("Segoe UI", 10.0f, FontStyle.Regular, GraphicsUnit.Point);
            button.Margin = new Padding(2, 3, 2, 3);
            button.Padding = new Padding(6, 3, 6, 3);
            button.Click += delegate { action(); };
            _toolbar.Items.Add(button);
            return button;
        }

        private static ToolStripRenderer CreateDarkRenderer()
        {
            ToolStripProfessionalRenderer renderer = new StudioMenuRenderer(new DarkColorTable());
            renderer.RoundedEdges = false;
            return renderer;
        }

        private sealed class DarkColorTable : ProfessionalColorTable
        {
            public DarkColorTable() { UseSystemColors = false; }
            public override Color ToolStripGradientBegin { get { return CyberTheme.Surface; } }
            public override Color ToolStripGradientMiddle { get { return CyberTheme.Surface; } }
            public override Color ToolStripGradientEnd { get { return CyberTheme.Surface; } }
            public override Color MenuStripGradientBegin { get { return CyberTheme.Background; } }
            public override Color MenuStripGradientEnd { get { return CyberTheme.Background; } }
            public override Color ToolStripDropDownBackground { get { return CyberTheme.Elevated; } }
            public override Color ImageMarginGradientBegin { get { return CyberTheme.Elevated; } }
            public override Color ImageMarginGradientMiddle { get { return CyberTheme.Elevated; } }
            public override Color ImageMarginGradientEnd { get { return CyberTheme.Elevated; } }
            public override Color MenuItemSelected { get { return CyberTheme.Selection; } }
            public override Color MenuItemSelectedGradientBegin { get { return CyberTheme.Selection; } }
            public override Color MenuItemSelectedGradientEnd { get { return CyberTheme.Selection; } }
            public override Color MenuItemPressedGradientBegin { get { return CyberTheme.Selection; } }
            public override Color MenuItemPressedGradientEnd { get { return CyberTheme.Selection; } }
            public override Color MenuItemBorder { get { return CyberTheme.Accent; } }
            public override Color MenuBorder { get { return CyberTheme.Border; } }
            public override Color ToolStripBorder { get { return CyberTheme.Border; } }
            public override Color SeparatorDark { get { return CyberTheme.Border; } }
            public override Color SeparatorLight { get { return CyberTheme.Border; } }
            public override Color ButtonSelectedGradientBegin { get { return CyberTheme.Selection; } }
            public override Color ButtonSelectedGradientMiddle { get { return CyberTheme.Selection; } }
            public override Color ButtonSelectedGradientEnd { get { return CyberTheme.Selection; } }
            public override Color ButtonPressedGradientBegin { get { return CyberTheme.AccentDark; } }
            public override Color ButtonPressedGradientMiddle { get { return CyberTheme.AccentDark; } }
            public override Color ButtonPressedGradientEnd { get { return CyberTheme.AccentDark; } }
        }
        private void BuildLanguageMenu()
        {
            foreach (string language in LocaleCatalog.SupportedLanguages)
            {
                string code = language;
                ToolStripMenuItem item = new ToolStripMenuItem(LanguageLabel(code));
                item.Click += delegate { ChangeLanguage(code); };
                _languageMenuItems.Add(code, item);
                _languageMenuItem.DropDownItems.Add(item);
            }
        }

        private static string LanguageLabel(string language)
        {
            if (language == "ja-JP") return "日本語";
            if (language == "ko-KR") return "한국어";
            if(language=="de-DE")return "Deutsch";return "English";
        }

        private void ApplyLocalization()
        {
            Text = T("app.title");
            _fileMenu.Text = T("menu.file");
            _editMenu.Text = T("menu.edit");
            _viewMenu.Text = T("menu.view");
            _helpMenu.Text = T("menu.help");
            _newMenuItem.Text = T("file.new");
            SetShortcut(_newMenuItem, AppCommand.NewProject);
            _openMenuItem.Text = T("file.open");
            SetShortcut(_openMenuItem, AppCommand.OpenProject);
            _saveMenuItem.Text = T("file.save");
            SetShortcut(_saveMenuItem, AppCommand.SaveProject);
            _saveAsMenuItem.Text = T("file.saveAs");
            _exitMenuItem.Text = T("file.exit");
            _undoMenuItem.Text = T("edit.undo");
            SetShortcut(_undoMenuItem, AppCommand.Undo);
            _redoMenuItem.Text = T("edit.redo");
            SetShortcut(_redoMenuItem, AppCommand.Redo);
            _addAudioMenuItem.Text = T("edit.addAudioTrack");
            SetShortcut(_addAudioMenuItem, AppCommand.AddAudioTrack);
            _addMidiMenuItem.Text = T("edit.addMidiTrack");
            SetShortcut(_addMidiMenuItem, AppCommand.AddMidiTrack);
            _addRegionMenuItem.Text = T("edit.addRegion");
            _importAudioMenuItem.Text = T("audio.importWav");
            SetShortcut(_importAudioMenuItem, AppCommand.ImportAudio);
            _playMenuItem.Text = T("transport.play");
            SetShortcut(_playMenuItem, AppCommand.PlayOrStop);
            _stopMenuItem.Text = T("transport.stop");
            SetShortcut(_stopMenuItem, AppCommand.Stop);
            SetShortcut(_addRegionMenuItem, AppCommand.AddRegion);
            _renameMenuItem.Text = T("edit.renameSelection");
            _renameMenuItem.ShortcutKeys = Keys.None;
            _deleteRegionMenuItem.Text = T("edit.deleteRegion");
            SetShortcut(_deleteRegionMenuItem, AppCommand.DeleteRegion);
            _deleteTrackMenuItem.Text = T("edit.deleteTrack");
            SetShortcut(_deleteTrackMenuItem, AppCommand.DeleteTrack);
            _languageMenuItem.Text = T("view.language");
            _showBrowserMenuItem.Text = "Show/Hide Project Browser"; SetShortcut(_showBrowserMenuItem, AppCommand.ToggleBrowser);
            _showInspectorMenuItem.Text = "Show/Hide Inspector"; SetShortcut(_showInspectorMenuItem, AppCommand.ToggleInspector);
            _showMixerMenuItem.Text = "Show/Hide Mixer"; SetShortcut(_showMixerMenuItem, AppCommand.ToggleMixer);
            _aboutMenuItem.Text = T("help.about");
            _newButton.Text = "▣"; _newButton.ToolTipText = T("toolbar.new");
            _openButton.Text = "▱"; _openButton.ToolTipText = T("toolbar.open");
            _saveButton.Text = "▤"; _saveButton.ToolTipText = T("toolbar.save");
            _playButton.Text = "▶"; _playButton.ToolTipText = T("toolbar.play");
            _stopButton.Text = "■"; _stopButton.ToolTipText = T("toolbar.stop");
            _importAudioButton.Text = "⇧ WAV"; _importAudioButton.ToolTipText = T("toolbar.importAudio");
            _undoButton.Text = "↶"; _undoButton.ToolTipText = T("toolbar.undo");
            _redoButton.Text = "↷"; _redoButton.ToolTipText = T("toolbar.redo");
            _addTrackButton.Text = "+ Audio"; _addTrackButton.ToolTipText = T("toolbar.addTrack");
            _addRegionButton.Text = "+ Region"; _addRegionButton.ToolTipText = T("toolbar.addRegion");
            _zoomOutButton.Text = T("toolbar.zoomOut");
            _zoomInButton.Text = T("toolbar.zoomIn");
            _tempoLabel.Text = T("tempo.label");
            _noticeLabel.Text = T("app.arrangerNotice");
            _noticeLabel.ToolTipText = T("timeline.help");
            foreach (KeyValuePair<string, ToolStripMenuItem> item in _languageMenuItems)
                item.Value.Checked = item.Key == _locale.Language;
            _timeline.SetLocale(_locale);
            RefreshState();
        }

        private void RefreshState()
        {
            if (_project == null)
                return;
            RepairSelection();
            _refreshing = true;
            try
            {
                decimal tempo = (decimal)_project.Tempo;
                if (tempo < _tempo.Minimum) tempo = _tempo.Minimum;
                if (tempo > _tempo.Maximum) tempo = _tempo.Maximum;
                _tempo.Value = tempo;
            }
            finally { _refreshing = false; }
            _timeline.SetContent(_project, _selectedTrackId, _selectedRegionId);
            _browser.SetContent(_project,_selectedTrackId,_selectedRegionId);_mediaBrowser.SetProject(_project);
            Track selectedTrack=_selectedTrackId.HasValue?FindTrack(_selectedTrackId.Value):null;
            Region selectedRegion=selectedTrack!=null&&_selectedRegionId.HasValue?FindRegion(selectedTrack,_selectedRegionId.Value):null;
            _inspector.SetContent(selectedTrack,selectedRegion);
            _engine.SetProject(_project);_engine.SetMidiThruTrack(selectedTrack!=null&&selectedTrack.Type==TrackType.Midi?(Guid?)selectedTrack.Id:null);
            foreach(var pair in _stockWindows.ToArray())if(!pair.Value.IsDisposed){var effect=_project.Studio.Effects.Find(f=>f.Id==pair.Key);if(effect!=null)pair.Value.RefreshFrom(effect);}
            _mixer.SetProject(_project,_engine);_inspector.SetMixer(_project,_engine);
            _pianoRoll.SetMeter(_project.TimeSignatureNumerator,_project.TimeSignatureDenominator);_arrangementToolbar.SetMeter(_project.TimeSignatureNumerator,_project.TimeSignatureDenominator);_pianoRoll.SetContent(selectedTrack, selectedRegion);
            _audioEditor.SetContent(selectedTrack,selectedRegion,_project.Tempo);
            _controlBar.SetState(_project,_cursorBeat,_engine.Playing,_recording,_engine.CpuLoad);
            UpdatePositionDisplay();
            UpdateViewMenuChecks();
            _undoButton.Enabled = _history.CanUndo;
            _redoButton.Enabled = _history.CanRedo;
            _undoMenuItem.Enabled = _history.CanUndo;
            _redoMenuItem.Enabled = _history.CanRedo;
            _addRegionButton.Enabled = _selectedTrackId.HasValue;
            _addRegionMenuItem.Enabled = _selectedTrackId.HasValue;
            _deleteRegionMenuItem.Enabled = _selectedRegionId.HasValue;
            _renameMenuItem.Enabled = _selectedTrackId.HasValue || _selectedRegionId.HasValue;
            SetShortcut(_renameMenuItem,_selectedRegionId.HasValue?AppCommand.RenameRegion:AppCommand.RenameTrack);
            _deleteTrackMenuItem.Enabled = _selectedTrackId.HasValue;
            _statusLabel.Text = BuildStatus();
            string titleName = String.IsNullOrEmpty(_projectPath) ? _project.Name : Path.GetFileName(_projectPath);
            Text = T("app.title") + " — " + titleName + (_history.IsDirty ? " *" : "");RefreshAdvancedFeatures();UiText.Apply(this);
        }

        private string BuildStatus()
        {
            if(!String.IsNullOrEmpty(_audioDeviceError))return _audioDeviceError;
            if(_engine!=null&&_engine.Warnings.Count>0)return _engine.Warnings[0];
            if(_recording)return UiText.Translate("Recording")+"  ·  "+_cursorBeat.ToString("0.00")+" "+UiText.Translate("beats");
            if(_project!=null&&!String.IsNullOrEmpty(_project.Studio.ImportNotes)&&!_engine.Playing)return UiText.Translate("Imported project — see Import Report")+" · "+_project.Name;
            if(_engine!=null&&_engine.Playing)return UiText.Translate("Playing")+"  ·  "+_project.Tempo.ToString("0.0")+" BPM";
            string state = _history.IsDirty ? T("status.unsaved") : T("status.saved");
            if (_selectedRegionId.HasValue && _selectedTrackId.HasValue)
            {
                Track track = FindTrack(_selectedTrackId.Value);
                Region region = FindRegion(track, _selectedRegionId.Value);
                if (track != null && region != null)
                    return state+"    "+track.Name+"  ·  "+region.Name+"  ·  "+region.StartBeat.ToString("0.00")+" "+UiText.Translate("beats");
            }
            if (_selectedTrackId.HasValue)
            {
                Track track = FindTrack(_selectedTrackId.Value);
                if (track != null)
                    return state + "    " + track.Name;
            }
            return state + "    " + T("status.ready");
        }

        private string TrackDefaultName(TrackType type, int number)
        {
            string key = type == TrackType.Audio ? "track.audioDefault" : "track.midiDefault";
            return String.Format(CultureInfo.CurrentCulture, T(key), number);
        }

        private void Select(Guid? trackId, Guid? regionId)
        {
            _selectedTrackId = trackId;
            _selectedRegionId = regionId;
            _timeline.SetSelection(trackId, regionId);
            RefreshState();
        }

        private void RepairSelection()
        {
            if (_selectedTrackId.HasValue && FindTrack(_selectedTrackId.Value) == null)
                _selectedTrackId = null;
            if (!_selectedTrackId.HasValue && _project.Tracks.Count > 0)
                _selectedTrackId = _project.Tracks[0].Id;
            if (_selectedRegionId.HasValue)
            {
                Track track = _selectedTrackId.HasValue ? FindTrack(_selectedTrackId.Value) : null;
                if (track == null || FindRegion(track, _selectedRegionId.Value) == null)
                    _selectedRegionId = null;
            }
        }

        private Track FindTrack(Guid id)
        {
            foreach (Track track in _project.Tracks)
                if (track.Id == id) return track;
            return null;
        }

        private Region FindRegion(Track track, Guid id)
        {
            if (track == null) return null;
            foreach (Region region in track.Regions)
                if (region.Id == id) return region;
            return null;
        }

        private void NewProject()
        {
            if (!ConfirmDiscardChanges()) return;
            ClosePluginEditors();StopAudio();
            _engine.ResetProject();
            _project = ProjectFactory.Create(T("project.untitled"), "Classic Electric Piano");
            _project.Tracks[0].Type=TrackType.Midi;_project.Tracks[0].Armed=true;_project.Tempo=_settings.DefaultTempo;_project.TimeSignatureNumerator=_settings.DefaultNumerator;_project.TimeSignatureDenominator=_settings.DefaultDenominator;
            _projectPath = null;
            _history.Reset(true);
            _cursorBeat=0;
            _selectedTrackId = _project.Tracks[0].Id;
            _selectedRegionId = null;
            RefreshState();
        }

        private void OpenProject()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = T("file.open");
                dialog.Filter = "Projects / MIDI|*.slopdaw;*.sloptemplate;*.flp;*.als;*.dawproject;*.mid;*.midi|Slop Studio project|*.slopdaw|Slop Studio template|*.sloptemplate|All files|*.*";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                OpenProjectPath(dialog.FileName);
            }
        }

        private void OpenProjectPath(string path)
        {
            if(!Path.GetExtension(path).Equals(".slopdaw",StringComparison.OrdinalIgnoreCase)&&!Path.GetExtension(path).Equals(".sloptemplate",StringComparison.OrdinalIgnoreCase)){OpenExchangePath(path);return;}
            ProjectDocument loaded;
            try
            {
                loaded = ProjectFile.Load(path);
            }
            catch (Exception exception)
            {
                ShowError(T("error.invalidProject"), exception);
                return;
            }
            if (!ConfirmDiscardChanges()) return;
            ClosePluginEditors();StopAudio();
            _engine.ResetProject();
            _project = loaded;
            bool isTemplate=ProjectProtection.IsProtected(path,AppDomain.CurrentDomain.BaseDirectory);
            if(isTemplate)_project.Id=Guid.NewGuid();
            _projectPath = isTemplate?null:Path.GetFullPath(path);
            _history.Reset(true);
            _cursorBeat=0;
            _selectedTrackId = _project.Tracks.Count == 0 ? (Guid?)null : _project.Tracks[0].Id;
            _selectedRegionId = null;
            RefreshState();
        }

        private void SaveProject()
        {
            if (String.IsNullOrEmpty(_projectPath))
            {
                SaveProjectAs();
                return;
            }
            SaveToPath(_projectPath);
        }

        private void SaveProjectAs()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = T("file.saveAs");
                dialog.Filter = "Slop Studio project (*.slopdaw)|*.slopdaw";
                dialog.DefaultExt = "slopdaw";
                dialog.AddExtension = true;
                dialog.OverwritePrompt = true;
                dialog.FileName = SafeFileName(_project.Name) + ".slopdaw";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                SaveToPath(dialog.FileName);
            }
        }

        private static string SafeFileName(string name)
        {
            string result = name;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                result = result.Replace(invalid, '_');
            if (String.IsNullOrWhiteSpace(result)) result = "Untitled Project";
            return result;
        }

        private bool SaveToPath(string path)
        {
            try
            {
                ProjectProtection.ValidateProjectDestination(path,AppDomain.CurrentDomain.BaseDirectory);
                ClosePluginEditors();_engine.CapturePluginStates();
                ProjectFile.SaveAtomic(_project, path);
                _projectPath = Path.GetFullPath(path);
                _history.MarkSaved();
                RefreshState();
                return true;
            }
            catch (Exception exception)
            {
                RefreshState();
                ShowError(T("error.saveFailed"), exception);
                return false;
            }
        }

        private bool ConfirmDiscardChanges()
        {
            ClosePluginEditors();
            if (!_history.IsDirty) return true;
            DialogResult result = MessageBox.Show(this, T("dialog.unsavedMessage"), T("dialog.unsavedTitle"),
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (result == DialogResult.Cancel) return false;
            if (result == DialogResult.Yes) return SaveToPathIfNeeded();
            return true;
        }

        private bool SaveToPathIfNeeded()
        {
            if (String.IsNullOrEmpty(_projectPath))
            {
                SaveProjectAs();
                return !_history.IsDirty;
            }
            return SaveToPath(_projectPath);
        }

        private void AddTrack(TrackType type)
        {
            if (_project.Tracks.Count >= ProjectValidator.MaxTracks)
            {
                ShowError(T("error.operation"), new InvalidOperationException("Track limit reached."));
                return;
            }
            int number = 1;
            foreach (Track track in _project.Tracks)
                if (track.Type == type) number++;
            Track added = ProjectFactory.CreateTrack(TrackDefaultName(type, number), type);
            added.ColorIndex=_project.Tracks.Count;added.Armed=true;
            try
            {
                List<IProjectCommand> commands=new List<IProjectCommand>();foreach(Track existing in _project.Tracks)if(existing.Armed)commands.Add(new SetTrackArmCommand(existing,false));commands.Add(new AddTrackCommand(added,_project.Tracks.Count));
                _history.Execute(_project,new CompositeCommand("Create track",commands));
                Select(added.Id, null);
            }
            catch (Exception exception) { ShowError(T("error.operation"), exception); }
        }

        private void AddRegion()
        {
            if (!_selectedTrackId.HasValue)
            {
                ShowError(T("error.noTrackSelected"), null);
                return;
            }
            Track track = FindTrack(_selectedTrackId.Value);
            if (track == null)
            {
                ShowError(T("error.noTrackSelected"), null);
                return;
            }
            int number = track.Regions.Count + 1;
            string key = track.Type == TrackType.Audio ? "region.audioDefault" : "region.midiDefault";
            string name = String.Format(CultureInfo.CurrentCulture, T(key), number);
            double start = 0.0;
            foreach (Region current in track.Regions)
                start = Math.Max(start, current.StartBeat + current.LengthBeats);
            double barLength = _project.TimeSignatureNumerator * 4.0 / _project.TimeSignatureDenominator;
            if (start > 0.0) start = Math.Ceiling(start / barLength) * barLength;
            Region region = ProjectFactory.CreateRegion(name, start, 4.0);
            try
            {
                _history.Execute(_project, new AddRegionCommand(track.Id, region, track.Regions.Count));
                Select(track.Id, region.Id);
            }
            catch (Exception exception) { ShowError(T("error.operation"), exception); }
        }

        private void ImportWav()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = T("audio.importWav");
                dialog.Filter = "PCM WAV audio (*.wav)|*.wav|All files (*.*)|*.*";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                ImportWavPath(dialog.FileName);
            }
        }

        private void ImportWavPath(string path)
        {
            try
            {
                PcmWaveInfo info = WaveAudio.Inspect(path);
                Track target = _selectedTrackId.HasValue ? FindTrack(_selectedTrackId.Value) : null;
                if (target != null && target.Type != TrackType.Audio) target = null;
                bool addTrack = target == null;
                if (addTrack && _project.Tracks.Count >= ProjectValidator.MaxTracks)
                    throw new InvalidOperationException("Track limit reached.");
                if (addTrack)
                {
                    int number = 1;
                    foreach (Track existing in _project.Tracks)
                        if (existing.Type == TrackType.Audio) number++;
                    target = ProjectFactory.CreateTrack(TrackDefaultName(TrackType.Audio, number), TrackType.Audio);
                }

                double start = 0.0;
                foreach (Region current in target.Regions)
                    start = Math.Max(start, current.StartBeat + current.LengthBeats);
                double barLength = _project.TimeSignatureNumerator * 4.0 / _project.TimeSignatureDenominator;
                if (start > 0.0) start = Math.Ceiling(start / barLength) * barLength;
                double length = Math.Max(0.25, info.DurationSeconds * _project.Tempo / 60.0);
                if (length > ProjectValidator.MaxBeat || start + length > ProjectValidator.MaxBeat)
                    throw new InvalidOperationException("The audio file is too long to place on this timeline.");
                string name = Path.GetFileNameWithoutExtension(info.Path);
                if (String.IsNullOrWhiteSpace(name)) name = T("track.audio");
                if (name.Length > ProjectValidator.MaxNameLength) name = name.Substring(0, ProjectValidator.MaxNameLength);
                Region region = ProjectFactory.CreateRegion(name, start, length);
                region.MediaPath = info.Path;

                if (addTrack)
                    _history.Execute(_project, new AddTrackCommand(target, _project.Tracks.Count));
                _history.Execute(_project, new AddRegionCommand(target.Id, region, target.Regions.Count));
                Select(target.Id, region.Id);
            }
            catch (Exception exception) { ShowError(T("error.audioImportFailed"), exception); }
        }

        private void PlaySelectedAudio(){StartTransport();}
        private void PlaySelectedMidi(Track track,Region region){StartTransport();}
        private void StartTransport()
        {
            try{_engine.SetProject(_project);_engine.Start(_cursorBeat);_audioDeviceError=null;_playButton.Enabled=false;_stopButton.Enabled=true;_controlBar.SetState(_project,_cursorBeat,true,_recording,_engine.CpuLoad);_statusLabel.Text=BuildStatus();}
            catch(Exception ex){ShowError("Playback could not start",ex);}
        }
        private void OnMidiNotesChanged(object sender,MidiNotesChangedEventArgs e)
        {if(!_selectedTrackId.HasValue||!_selectedRegionId.HasValue)return;try{_history.Execute(_project,new SetMidiNotesCommand(_project,_selectedTrackId.Value,_selectedRegionId.Value,e.Notes));RefreshState();}catch(Exception ex){ShowError(T("error.operation"),ex);}}
        private void StopAudio()
        {
            if(_recording)StopRecording();if(_engine!=null){_cursorBeat=_engine.PositionBeat;_engine.Stop();}
            if(_playButton!=null)_playButton.Enabled=true;if(_stopButton!=null)_stopButton.Enabled=false;if(_timeline!=null)_timeline.SetPlayheadBeat(_cursorBeat);UpdatePositionDisplay();if(_controlBar!=null)_controlBar.SetState(_project,_cursorBeat,false,false,_engine==null?0:_engine.CpuLoad);if(_statusLabel!=null)_statusLabel.Text=BuildStatus();
        }
        private void OnTransportTimerTick(object sender,EventArgs e)
        {
            if(_engine==null)return;if(_engine.Playing)_cursorBeat=_engine.PositionBeat;
            List<Guid> recordingTracks=new List<Guid>(_recordMidiTracks);if(_multiCapture!=null)foreach(Track t in _project.Tracks)if(t.Armed&&t.Type==TrackType.Audio)recordingTracks.Add(t.Id);if(_recordAudioTrack.HasValue)recordingTracks.Add(_recordAudioTrack.Value);_timeline.SetRecordingState(_recording?recordingTracks:null,_recordStartBeat,_cursorBeat);_timeline.SetPlayheadBeat(_cursorBeat);_pianoRoll.SetPlayheadBeat(_cursorBeat);_controlBar.SetState(_project,_cursorBeat,_engine.Playing,_recording,_engine.CpuLoad);_mixer.RefreshMeters();_inspector.RefreshMeters();UpdatePositionDisplay();_statusLabel.Text=BuildStatus();
        }
        private void StartRecording()
        {
            if(_recording){StopAudio();return;}try{
                if(_project.CycleEnabled)throw new InvalidOperationException("Cycle recording is not supported in this build. Turn off Cycle (C) before starting a take.");
                List<Track> audio=new List<Track>();_recordMidiTracks.Clear();foreach(Track t in _project.Tracks)if(t.Armed){if(t.Type==TrackType.Audio)audio.Add(t);else _recordMidiTracks.Add(t.Id);}if(audio.Count==0&&_recordMidiTracks.Count==0){ToggleArm();foreach(Track t in _project.Tracks)if(t.Armed){if(t.Type==TrackType.Audio)audio.Add(t);else _recordMidiTracks.Add(t.Id);}}
                
                if(_recordMidiTracks.Count>0&&!_engine.MidiInputConnected&&(_softwareKeyboard==null||!_softwareKeyboard.Visible))throw new InvalidOperationException("Select an available MIDI input in Settings before recording MIDI. Record-enable the destination instrument track.");
                _recordStartBeat=_cursorBeat;
                if(audio.Count>0){if(!_engine.DeviceRunning)_engine.OpenDevices();if(_engine.InputChannels<1)throw new InvalidOperationException("No audio input is available. Select an input in Audio Settings.");string folder=String.IsNullOrEmpty(_projectPath)?Path.Combine(Path.GetDirectoryName(SettingsStore.DefaultPath),"Recordings"):Path.Combine(Path.GetDirectoryName(_projectPath),"Media");_multiCapture=new AudioRecordingSession(audio,_project.Studio,folder,_settings.SampleRate,_engine.InputChannels);_engine.AudioInputReceived+=_multiCapture.Receive;}
                _midiRecording=_recordMidiTracks.Count>0?new MidiRecordingSession(_project.Tempo):null;_recording=true;StartTransport();if(!_engine.Playing){StopRecording();return;}RefreshState();
            }catch(Exception ex){if(_multiCapture!=null){_engine.AudioInputReceived-=_multiCapture.Receive;_multiCapture.Dispose();_multiCapture=null;}if(_capture!=null){_capture.Dispose();_capture=null;}_midiRecording=null;_recording=false;ShowError("Recording could not start",ex);}
        }
        private void StopRecording()
        {
            _recording=false;List<IProjectCommand> edits=new List<IProjectCommand>();Guid? selectedTrack=null,selectedRegion=null;double duration=Math.Max(0.25,_engine.PositionBeat-_recordStartBeat);
            try{if(_multiCapture!=null){AudioRecordingSession session=_multiCapture;_multiCapture=null;_engine.AudioInputReceived-=session.Receive;foreach(var take in session.Finish()){Track t=FindTrack(take.TrackId);Region r=ProjectFactory.CreateRegion("Audio Take "+DateTime.Now.ToString("HH:mm:ss"),_recordStartBeat,Math.Max(0.25,take.Frames/(double)_settings.SampleRate*_project.Tempo/60));r.MediaPath=take.Path;edits.Add(new AddRegionCommand(t.Id,r,t.Regions.Count));selectedTrack=t.Id;selectedRegion=r.Id;}}if(_capture!=null){WaveCaptureStream capture=_capture;_capture=null;capture.Dispose();if(capture.Error!=null)throw capture.Error;PcmWaveInfo info=WaveAudio.Inspect(capture.Path);if(info.DataBytes>0&&_recordAudioTrack.HasValue){Track t=FindTrack(_recordAudioTrack.Value);Region r=ProjectFactory.CreateRegion("Audio Take "+DateTime.Now.ToString("HH:mm:ss"),_recordStartBeat,Math.Max(0.25,info.DurationSeconds*_project.Tempo/60));r.MediaPath=info.Path;edits.Add(new AddRegionCommand(t.Id,r,t.Regions.Count));selectedTrack=t.Id;selectedRegion=r.Id;}}
                if(_midiRecording!=null){List<MidiNote> notes=_midiRecording.Stop();_midiRecording=null;foreach(MidiNote note in notes)duration=Math.Max(duration,note.StartBeat+note.LengthBeats);foreach(Guid id in _recordMidiTracks){Track t=FindTrack(id);if(t==null)continue;Region r=ProjectFactory.CreateRegion("MIDI Take "+DateTime.Now.ToString("HH:mm:ss"),_recordStartBeat,Math.Ceiling(duration*4)/4);r.Notes=ProjectFactory.CloneNotes(notes);foreach(MidiNote note in r.Notes)note.Id=Guid.NewGuid();edits.Add(new AddRegionCommand(t.Id,r,t.Regions.Count));selectedTrack=t.Id;selectedRegion=r.Id;}}
                if(edits.Count>0){_history.Execute(_project,new CompositeCommand("Record take",edits));if(selectedTrack.HasValue){_selectedTrackId=selectedTrack;_selectedRegionId=selectedRegion;}RefreshState();}
            }catch(Exception ex){ShowError("The recording stopped with an error. Captured audio remains in its take file.",ex);}finally{_recordMidiTracks.Clear();_recordAudioTrack=null;_midiRecording=null;}
        }
        private void OnMidiInput(object sender,MidiInputMessage e){MidiRecordingSession session=_midiRecording;if(_recording&&session!=null)session.Receive(e);}
        private void OnEngineFailure(Exception ex){_audioDeviceError=ex.Message;try{if(IsHandleCreated&&!IsDisposed)BeginInvoke((Action)delegate{if(_engine.DeviceFailed&&_engine.Playing)StopAudio();_statusLabel.Text=UiText.Translate(ex.Message);});}catch{}}
        private void SetTempo(double value){if(_recording){MessageBox.Show(this,"Stop recording before changing tempo.","Tempo");return;}if(Math.Abs(value-_project.Tempo)<0.0001)return;bool resume=_engine.Playing;double beat=_engine.PositionBeat;StopAudio();_history.Execute(_project,new SetTempoCommand(_project.Tempo,value));RefreshState();SetCursorBeat(beat);if(resume)StartTransport();}
        private void SetMeter(int numerator,int denominator){_history.Execute(_project,new SetMeterCommand(_project,numerator,denominator));RefreshState();}
        private void SetMasterGain(double value){if(Math.Abs(_project.MasterGainDb-value)<0.02)return;_history.Execute(_project,new SetMasterGainCommand(_project.MasterGainDb,value));_engine.UpdateMasterGain(value);_controlBar.SetState(_project,_cursorBeat,_engine.Playing,_recording,_engine.CpuLoad);_inspector.RefreshMeters();_mixer.RefreshMeters();}

        private void RenameSelection()
        {
            if (_selectedRegionId.HasValue && _selectedTrackId.HasValue)
            {
                Track track = FindTrack(_selectedTrackId.Value);
                Region region = FindRegion(track, _selectedRegionId.Value);
                if (region == null) return;
                string value = NamePrompt.Show(this, T("dialog.renameRegion"), T("dialog.namePrompt"), region.Name);
                if (value == null) return;
                try
                {
                    _history.Execute(_project, new RenameRegionCommand(track.Id, region.Id, region.Name,
                        ProjectValidator.NormalizeName(value, "Region name")));
                    RefreshState();
                }
                catch (Exception exception) { ShowError(T("error.operation"), exception); }
                return;
            }
            if (_selectedTrackId.HasValue)
            {
                Track track = FindTrack(_selectedTrackId.Value);
                if (track == null) return;
                string value = NamePrompt.Show(this, T("dialog.renameTrack"), T("dialog.namePrompt"), track.Name);
                if (value == null) return;
                try
                {
                    _history.Execute(_project, new RenameTrackCommand(track.Id, track.Name,
                        ProjectValidator.NormalizeName(value, "Track name")));
                    RefreshState();
                }
                catch (Exception exception) { ShowError(T("error.operation"), exception); }
            }
        }

        private void DeleteRegion()
        {
            if (!_selectedTrackId.HasValue || !_selectedRegionId.HasValue)
            {
                ShowError(T("error.noRegionSelected"), null);
                return;
            }
            try
            {
                Guid trackId = _selectedTrackId.Value;
                _history.Execute(_project, new DeleteRegionCommand(_project, trackId, _selectedRegionId.Value));
                _selectedRegionId = null;
                RefreshState();
            }
            catch (Exception exception) { ShowError(T("error.operation"), exception); }
        }

        private void DeleteTrack()
        {
            if(_recording)StopAudio();ClosePluginEditors();
            if (!_selectedTrackId.HasValue)
            {
                ShowError(T("error.noTrackSelected"), null);
                return;
            }
            Track track = FindTrack(_selectedTrackId.Value);
            if (track == null) return;
            if (track.Regions.Count > 0)
            {
                DialogResult result = MessageBox.Show(this, String.Format(CultureInfo.CurrentCulture,
                    T("dialog.deleteTrackMessage"), track.Name, track.Regions.Count), T("edit.deleteTrack"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;
            }
            try
            {
                _history.Execute(_project, new DeleteTrackCommand(_project, track.Id));
                _selectedTrackId = _project.Tracks.Count == 0 ? (Guid?)null : _project.Tracks[Math.Min(_project.Tracks.Count - 1, 0)].Id;
                _selectedRegionId = null;
                RefreshState();
            }
            catch (Exception exception) { ShowError(T("error.operation"), exception); }
        }

        private void SetShortcut(ToolStripMenuItem item, AppCommand command)
        {
            item.Tag=command;item.ShortcutKeys=Keys.None;
            item.ShowShortcutKeys=true;
            item.ShortcutKeyDisplayString=ShortcutRegistry.Display(command);
        }

        private void RefreshShortcutLabels(ToolStripItemCollection items){foreach(ToolStripItem raw in items){var item=raw as ToolStripMenuItem;if(item==null)continue;if(item.Tag is AppCommand)item.ShortcutKeyDisplayString=ShortcutRegistry.Display((AppCommand)item.Tag);RefreshShortcutLabels(item.DropDownItems);}}

        private void ExecuteCommand(AppCommand command)
        {
            if(ExecuteV8(command)||ExecuteV7(command)||ExecuteAdvanced(command))return;
            switch(command)
            {
                case AppCommand.NewProject: NewProject(); break;
                case AppCommand.ToggleFullScreen: ToggleFullScreen(); break;
                case AppCommand.OpenProject: OpenProject(); break;
                case AppCommand.SaveProject: SaveProject(); break;
                case AppCommand.Undo: Undo(); break;
                case AppCommand.Redo: Redo(); break;
                case AppCommand.AddAudioTrack: AddTrack(TrackType.Audio); break;
                case AppCommand.AddMidiTrack: AddTrack(TrackType.Midi); break;
                case AppCommand.AddExternalMidiTrack: AddTrack(TrackType.Midi);FindTrack(_selectedTrackId.Value).ExternalMidi=true;FindTrack(_selectedTrackId.Value).Name="External MIDI";RefreshState();break;
                case AppCommand.AddRegion: AddRegion(); break;
                case AppCommand.ImportAudio: ImportMediaDialog(false); break;
                case AppCommand.PlayOrStop: if(_engine.Playing) StopAudio(); else StartTransport(); break;
                case AppCommand.Stop: StopAudio(); break;
                case AppCommand.GoToBeginning: StopAudio(); SetCursorBeat(0); break;
                case AppCommand.RewindBar: MoveCursorByBars(-1); break;
                case AppCommand.ForwardBar: MoveCursorByBars(1); break;
                case AppCommand.DeleteRegion: if(_selectedRegionId.HasValue)DeleteRegion(); break;
                case AppCommand.DeleteTrack: DeleteTrack(); break;
                case AppCommand.RenameTrack: case AppCommand.RenameRegion: RenameSelection(); break;
                case AppCommand.ZoomOut: ZoomBy(-8); break;
                case AppCommand.ZoomIn: ZoomBy(8); break;
                case AppCommand.SelectPreviousTrack: _timeline.MoveSelection(-1); break;
                case AppCommand.SelectNextTrack: _timeline.MoveSelection(1); break;
                case AppCommand.SelectPreviousRegion: SelectAdjacentRegion(-1); break;
                case AppCommand.SelectNextRegion: SelectAdjacentRegion(1); break;
                case AppCommand.ToggleMixer: _lowerDock.ShowMixer(); _workspaceSplit.Panel2Collapsed=!_workspaceSplit.Panel2Collapsed; break;
                case AppCommand.ToggleLibrary: _columnsSplit.Panel1Collapsed=!_columnsSplit.Panel1Collapsed; break;
                case AppCommand.ToggleBrowser: _mediaSplit.Panel2.Controls.Clear();_mediaSplit.Panel2.Controls.Add(_mediaBrowser); _mediaSplit.Panel2Collapsed=!_mediaSplit.Panel2Collapsed;break;
                case AppCommand.ToggleInspector: FindInspectorSplit().Panel1Collapsed=!FindInspectorSplit().Panel1Collapsed; break;
                case AppCommand.ToggleMute: ToggleSelectedMix(true); break;
                case AppCommand.ToggleSolo: ToggleSelectedMix(false); break;
                case AppCommand.ClearSolo: SetMixForAll(false,true); break;
                case AppCommand.UnmuteAll: SetMixForAll(true,false); break;
                case AppCommand.Record: StartRecording(); break;
                case AppCommand.Pause: if(_engine.Playing)StopAudio();else StartTransport(); break;
                case AppCommand.ShowPianoRoll: if(!_workspaceSplit.Panel2Collapsed&&_lowerDock.IsPianoRollVisible)_workspaceSplit.Panel2Collapsed=true;else OpenEditor();break;
                case AppCommand.ShowEditor: if(!_workspaceSplit.Panel2Collapsed)_workspaceSplit.Panel2Collapsed=true;else OpenEditor();break;
                case AppCommand.OpenPreferences: OpenPreferences(); break;
                case AppCommand.OpenMcpManager: ShowMcpManager(); break;
                case AppCommand.OpenOscSettings: OpenPreferencesPage(5); break;
                case AppCommand.OpenControllerSettings: OpenPreferencesPage(3); break;
                case AppCommand.Panic: StopAudio();_engine.ReturnToArrangement();_statusLabel.Text=UiText.Translate("All notes stopped");break;
                case AppCommand.ToggleCycle: _history.Execute(_project,new SetCycleCommand(_project.CycleEnabled,!_project.CycleEnabled));RefreshState();break;
                case AppCommand.ToggleArm: ToggleArm();break;
                case AppCommand.DuplicateTrack: DuplicateTrack();break;
                case AppCommand.DuplicateTrackWithContent: DuplicateTrackWithContent();break;
                case AppCommand.DuplicateRegion: DuplicateRegion();break;
                case AppCommand.SplitRegion: SplitAtCursor();break;
                case AppCommand.Quantize: QuantizeNotes();break;
                case AppCommand.Copy: CopyRegion(false);break;
                case AppCommand.Cut: CopyRegion(true);break;
                case AppCommand.Paste: PasteRegion();break;
                case AppCommand.DeselectAll: _selectedRegionId=null;RefreshState();break;
            }
            UpdateViewMenuChecks();
        }

        private void ShowNewTrackMenu(){ContextMenuStrip menu=new ContextMenuStrip();menu.Items.Add("Audio Track",null,delegate{AddTrack(TrackType.Audio);});menu.Items.Add("Software Instrument Track",null,delegate{AddTrack(TrackType.Midi);});menu.Show(Cursor.Position);}
        private void ToggleArm(){Track t=_selectedTrackId.HasValue?FindTrack(_selectedTrackId.Value):null;if(t!=null)OnTrackArmRequested(this,new TrackArmEventArgs{TrackId=t.Id,Armed=!t.Armed});}
        private void OnTrackArmRequested(object sender,TrackArmEventArgs e){Track t=FindTrack(e.TrackId);if(t==null)return;_history.Execute(_project,new SetTrackArmCommand(t,e.Armed));RefreshState();}
        private void OnTrackReorderRequested(object sender,TrackReorderEventArgs e){_history.Execute(_project,new ReorderTrackCommand(_project,e.TrackId,e.Index));RefreshState();}
        private void OnRegionTrimRequested(object sender,RegionTrimEventArgs e){Track t=FindTrack(e.TrackId);Region r=FindRegion(t,e.RegionId);if(r==null)return;try{_history.Execute(_project,new TrimRegionCommand(t,r,e.StartBeat,e.LengthBeats,t.Type==TrackType.Audio?e.SourceOffset:0));RefreshState();}catch(Exception ex){ShowError("Region trim failed",ex);}}
        private void OnRegionCreateRequested(object sender,RegionCreateEventArgs e){Track t=FindTrack(e.TrackId);if(t==null)return;Region r=ProjectFactory.CreateRegion(t.Type==TrackType.Midi?"MIDI Region":"Audio Region",e.Beat,_project.TimeSignatureNumerator*4.0/_project.TimeSignatureDenominator);_history.Execute(_project,new AddRegionCommand(t.Id,r,t.Regions.Count));Select(t.Id,r.Id);}
        private void OnRegionSplitRequested(object sender,RegionMoveEventArgs e){SplitRegion(e.TrackId,e.RegionId,e.NewStartBeat);}
        private static Region CopyRegionData(Region source,bool newIds){Region r=new Region{Id=newIds?Guid.NewGuid():source.Id,Name=source.Name,StartBeat=source.StartBeat,LengthBeats=source.LengthBeats,MediaPath=source.MediaPath,AudioOffsetSeconds=source.AudioOffsetSeconds,Muted=source.Muted,Transpose=source.Transpose,VelocityOffset=source.VelocityOffset,Notes=ProjectFactory.CloneNotes(source.Notes)};if(newIds)foreach(MidiNote n in r.Notes)n.Id=Guid.NewGuid();return r;}
        private void DuplicateTrack(){DuplicateTrackCore(false);}
        private void DuplicateTrackWithContent(){DuplicateTrackCore(true);}
        private void DuplicateTrackCore(bool includeContent){if(!_selectedTrackId.HasValue)return;Track original=FindTrack(_selectedTrackId.Value);if(original==null)return;_engine.CapturePluginStates();ProjectDocument temp=new ProjectDocument();temp.Tracks.Add(original);Track copy=ProjectFactory.Clone(temp).Tracks[0];copy.Id=Guid.NewGuid();copy.Name=original.Name+" Copy";copy.Armed=false;if(!includeContent)copy.Regions.Clear();foreach(Region r in copy.Regions){r.Id=Guid.NewGuid();foreach(MidiNote n in r.Notes)n.Id=Guid.NewGuid();}foreach(PluginSlot slot in copy.Plugins)slot.Id=Guid.NewGuid();_history.Execute(_project,new AddTrackCommand(copy,_project.Tracks.IndexOf(original)+1));Select(copy.Id,null);}
        private void DuplicateRegion(){Track t=_selectedTrackId.HasValue?FindTrack(_selectedTrackId.Value):null;Region r=t!=null&&_selectedRegionId.HasValue?FindRegion(t,_selectedRegionId.Value):null;if(r==null)return;Region copy=CopyRegionData(r,true);copy.StartBeat=r.StartBeat+r.LengthBeats;_history.Execute(_project,new AddRegionCommand(t.Id,copy,t.Regions.Count));Select(t.Id,copy.Id);}
        private void SplitAtCursor(){if(_selectedTrackId.HasValue&&_selectedRegionId.HasValue)SplitRegion(_selectedTrackId.Value,_selectedRegionId.Value,_cursorBeat);}
        private void SplitRegion(Guid trackId,Guid regionId,double beat){Track t=FindTrack(trackId);Region r=FindRegion(t,regionId);if(r==null||beat-r.StartBeat<0.25||r.StartBeat+r.LengthBeats-beat<0.25)return;double delta=beat-r.StartBeat;Region right=CopyRegionData(r,true);right.StartBeat=beat;right.LengthBeats=r.LengthBeats-delta;right.AudioOffsetSeconds=r.AudioOffsetSeconds+(t.Type==TrackType.Audio?delta*60/_project.Tempo:0);right.Notes.Clear();foreach(MidiNote original in r.Notes){double a=Math.Max(0,original.StartBeat-delta),b=Math.Min(right.LengthBeats,original.StartBeat+original.LengthBeats-delta);if(b-a<0.0625)continue;MidiNote n=new MidiNote{StartBeat=a,LengthBeats=b-a,Pitch=original.Pitch,Velocity=original.Velocity};right.Notes.Add(n);}_history.Execute(_project,new CompositeCommand("Split region",new IProjectCommand[]{new TrimRegionCommand(t,r,r.StartBeat,delta,r.AudioOffsetSeconds),new AddRegionCommand(t.Id,right,t.Regions.Count)}));Select(t.Id,right.Id);}
        private void QuantizeNotes(){QuantizeNotes(0.25);}
        private void QuantizeNotes(double grid){Track t=_selectedTrackId.HasValue?FindTrack(_selectedTrackId.Value):null;Region r=t!=null&&_selectedRegionId.HasValue?FindRegion(t,_selectedRegionId.Value):null;if(r==null||t.Type!=TrackType.Midi)return;List<MidiNote> notes=ProjectFactory.CloneNotes(r.Notes);foreach(MidiNote n in notes){n.StartBeat=Math.Max(0,Math.Min(r.LengthBeats-0.0625,Math.Round(n.StartBeat/grid)*grid));n.LengthBeats=Math.Min(n.LengthBeats,r.LengthBeats-n.StartBeat);}_history.Execute(_project,new SetMidiNotesCommand(_project,t.Id,r.Id,notes));RefreshState();}
        private void CopyRegion(bool cut){Track t=_selectedTrackId.HasValue?FindTrack(_selectedTrackId.Value):null;Region r=t!=null&&_selectedRegionId.HasValue?FindRegion(t,_selectedRegionId.Value):null;if(r==null)return;_clipboardRegion=CopyRegionData(r,false);_clipboardType=t.Type;if(cut)DeleteRegion();}
        private void PasteRegion(){if(_clipboardRegion==null||!_selectedTrackId.HasValue)return;Track t=FindTrack(_selectedTrackId.Value);if(t.Type!=_clipboardType){MessageBox.Show(this,"Paste into a track with the same audio/MIDI type.","Paste");return;}Region r=CopyRegionData(_clipboardRegion,true);r.StartBeat=_cursorBeat;_history.Execute(_project,new AddRegionCommand(t.Id,r,t.Regions.Count));Select(t.Id,r.Id);}
        private void OpenEditor(){Track t=_selectedTrackId.HasValue?FindTrack(_selectedTrackId.Value):null;Region r=t!=null&&_selectedRegionId.HasValue?FindRegion(t,_selectedRegionId.Value):null;_workspaceSplit.Panel2Collapsed=false;if(t==null||t.Type==TrackType.Midi){_pianoRoll.SetContent(t,r);_lowerDock.ShowPianoRoll();if(r!=null)_pianoRoll.CenterNotes();}else{_audioEditor.SetContent(t,r,_project.Tempo);_lowerDock.ShowAudioEditor();}UpdateViewMenuChecks();}
        private void ShowLibraryForSelection(){Track t=_selectedTrackId.HasValue?FindTrack(_selectedTrackId.Value):null;if(t!=null&&t.Type==TrackType.Audio){OpenPreferences();return;}_columnsSplit.Panel1Collapsed=false;_browser.ShowSounds();UpdateViewMenuChecks();}
        private void SelectPreset(string name){Track t=_selectedTrackId.HasValue?FindTrack(_selectedTrackId.Value):null;if(t==null||t.Type!=TrackType.Midi){AddTrack(TrackType.Midi);t=FindTrack(_selectedTrackId.Value);}bool hasPlugin=t.Plugins.Exists(delegate(PluginSlot slot){return slot.IsInstrument;});if(t.Instrument==name&&!t.ExternalMidi&&!hasPlugin)return;ClosePluginEditors();List<IProjectCommand> changes=new List<IProjectCommand>();changes.Add(new SetInstrumentCommand(t,name));if(hasPlugin){List<PluginSlot> effects=t.Plugins.FindAll(delegate(PluginSlot slot){return !slot.IsInstrument;});changes.Add(new SetPluginSlotsCommand(t,effects));}_history.Execute(_project,new CompositeCommand("Select instrument",changes));RefreshState();}
        private void BrowsePlugin(Track track)
        {
            using(PluginBrowserDialog dialog=new PluginBrowserDialog(_settings.PluginPaths))
                if(dialog.ShowDialog(this)==DialogResult.OK&&dialog.SelectedPlugin!=null)
                    LoadPlugin(track,dialog.SelectedPlugin);
        }
        private void LoadPlugin(Track track,PluginDescriptor descriptor)
        {
            if(descriptor.Format=="Built-in"){AddStockEffect(track,descriptor.ClassId);return;}
            try
            {
                PluginSlot slot=descriptor.CreateSlot();
                if(track==null)
                {
                    if(slot.IsInstrument)throw new InvalidOperationException("Instrument plug-ins cannot be inserted on the master output.");
                    using(NativePlugin verify=new NativePlugin(slot,_settings.SampleRate,_settings.BufferFrames))slot.State=verify.SaveState();
                    var masterState=_project.Studio.Copy();masterState.MasterPlugins.Add(slot);
                    var order=InsertChain.Entries(_project,Guid.Empty).Select(e=>e.Id).ToList();order.Add(slot.Id);InsertChain.Set(masterState,Guid.Empty,order);
                    CommitStudio("Add master plug-in",masterState);EditPlugin(slot);return;
                }
                if(slot.UseAra)slot.AraDocument=AraDocumentInfo.FromTrack(_project,track);
                using(NativePlugin verify=new NativePlugin(slot,_settings.SampleRate,_settings.BufferFrames)){slot.State=verify.SaveState();slot.AraState=verify.Slot.AraState;}
                if(slot.IsInstrument&&track.Type==TrackType.Audio)
                {
                    Track instrument=ProjectFactory.CreateTrack(slot.Name,TrackType.Midi);instrument.Armed=true;instrument.Plugins.Add(slot);_history.Execute(_project,new AddTrackCommand(instrument,_project.Tracks.Count));Select(instrument.Id,null);
                }
                else
                {
                    List<PluginSlot> slots=new List<PluginSlot>();foreach(PluginSlot current in track.Plugins)if(!slot.IsInstrument||!current.IsInstrument)slots.Add(current);if(slot.IsInstrument)slots.Insert(0,slot);else slots.Add(slot);
                    var state=_project.Studio.Copy();if(!slot.IsInstrument){var order=InsertChain.Entries(_project,track).Select(e=>e.Id).ToList();order.Add(slot.Id);InsertChain.Set(state,track.Id,order);}
                    _history.Execute(_project,new CompositeCommand("Add plug-in",new IProjectCommand[]{new SetPluginSlotsCommand(track,slots),new SetStudioCommand("Update insert order",_project.Studio,state)}));RefreshState();
                }
                EditPlugin(slot);
            }
            catch(Exception ex){ShowError("Plugin could not be loaded",ex);}
        }
        private Track TrackForSlot(Guid id){foreach(Track t in _project.Tracks)foreach(PluginSlot s in t.Plugins)if(s.Id==id)return t;return null;}
        private void EditPlugin(PluginSlot slot)
        {
            if(slot.Format=="Built-in"){OpenStockEffect(slot.Id);return;}
            NativePlugin plugin=_engine.GetPlugin(slot.Id);if(plugin==null){RefreshState();plugin=_engine.GetPlugin(slot.Id);}if(plugin==null){ShowError("Plugin is unavailable",new InvalidOperationException(slot.Name));return;}
            foreach(PluginEditorForm existing in _pluginWindows)if(existing.Tag is Guid&&(Guid)existing.Tag==slot.Id){existing.Activate();return;}
            PluginEditorForm form=new PluginEditorForm(plugin);form.Tag=slot.Id;_pluginWindows.Add(form);
            form.StateChanged+=delegate
            {
                _pluginWindows.Remove(form);
                try
                {
                    string state=plugin.SaveState();PluginSlot master=_project.Studio.MasterPlugins.Find(s=>s.Id==slot.Id);
                    if(master!=null){var studio=_project.Studio.Copy();PluginSlot copy=studio.MasterPlugins.Find(s=>s.Id==slot.Id);if(copy!=null&&copy.State!=state){copy.State=state;CommitStudio("Save master plug-in state",studio);}return;}
                    Track t=TrackForSlot(slot.Id);if(t==null)return;List<PluginSlot> after=new List<PluginSlot>();bool changed=false;foreach(PluginSlot current in t.Plugins){PluginSlot copy=current.Copy();if(current.Id==slot.Id&&(copy.State!=state||copy.AraState!=plugin.Slot.AraState)){copy.State=state;copy.AraState=plugin.Slot.AraState;changed=true;}after.Add(copy);}if(changed){_history.Execute(_project,new SetPluginSlotsCommand(t,after));RefreshState();}
                }
                catch(Exception ex){ShowError("Plugin state save failed",ex);}
            };
            form.Show(this);
        }
        private void TogglePlugin(PluginSlot slot)
        {
            if(slot.Format=="Built-in"){var state=_project.Studio.Copy();var effect=state.Effects.Find(f=>f.Id==slot.Id);if(effect!=null){effect.Enabled=!effect.Enabled;CommitStudio("Toggle effect",state);}return;}
            PluginSlot master=_project.Studio.MasterPlugins.Find(s=>s.Id==slot.Id);if(master!=null){var studio=_project.Studio.Copy();studio.MasterPlugins.Find(s=>s.Id==slot.Id).Bypassed=!master.Bypassed;CommitStudio("Toggle master plug-in",studio);return;}
            Track t=TrackForSlot(slot.Id);if(t==null)return;List<PluginSlot> slots=new List<PluginSlot>();foreach(PluginSlot p in t.Plugins){PluginSlot copy=p.Copy();if(copy.Id==slot.Id)copy.Bypassed=!copy.Bypassed;slots.Add(copy);}_history.Execute(_project,new SetPluginSlotsCommand(t,slots));RefreshState();
        }
        private void RemovePlugin(PluginSlot slot)
        {
            if(slot.Format=="Built-in"){StockEffectEditor editor;if(_stockWindows.TryGetValue(slot.Id,out editor))editor.Close();var state=_project.Studio.Copy();state.Effects.RemoveAll(f=>f.Id==slot.Id);foreach(var order in state.InsertOrders)order.Items.Remove(slot.Id);CommitStudio("Remove effect",state);return;}
            ClosePluginEditors();PluginSlot masterSlot=_project.Studio.MasterPlugins.Find(s=>s.Id==slot.Id);if(masterSlot!=null){var studio=_project.Studio.Copy();studio.MasterPlugins.RemoveAll(s=>s.Id==slot.Id);foreach(var order in studio.InsertOrders)order.Items.Remove(slot.Id);CommitStudio("Remove master plug-in",studio);return;}
            Track t=TrackForSlot(slot.Id);if(t==null)return;List<PluginSlot> slots=new List<PluginSlot>();foreach(PluginSlot s in t.Plugins)if(s.Id!=slot.Id)slots.Add(s);_history.Execute(_project,new SetPluginSlotsCommand(t,slots));RefreshState();
        }        private void ClosePluginEditors(){foreach(PluginEditorForm window in new List<PluginEditorForm>(_pluginWindows))if(!window.IsDisposed)window.Close();_pluginWindows.Clear();}
        private void OpenPreferences(){OpenPreferencesPage(0);}
        private static bool AudioSettingsChanged(AppSettings a,AppSettings b){return a.AudioBackend!=b.AudioBackend||a.OutputEndpoint!=b.OutputEndpoint||a.InputEndpoint!=b.InputEndpoint||a.AsioDriver!=b.AsioDriver||a.AudioOutput!=b.AudioOutput||a.AudioInput!=b.AudioInput||a.MidiInput!=b.MidiInput||a.MidiOutput!=b.MidiOutput||a.SampleRate!=b.SampleRate||a.BufferFrames!=b.BufferFrames||a.PdcEnabled!=b.PdcEnabled;}
        private void OpenPreferencesPage(int page){
            if(_recording)StopAudio();using(var dialog=new PreferencesForm(_settings,page,delegate(string id){return _engine.OpenActiveAsioPanel(id);})){
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;var old=_settings;var candidate=dialog.Result;
                if(AudioSettingsChanged(old,candidate)){
                    ClosePluginEditors();StopAudio();try{_engine.CapturePluginStates();_engine.Configure(candidate);_settings=candidate;_audioDeviceError=null;}
                    catch(Exception ex){try{_engine.Configure(old);}catch(Exception restore){ErrorLog.Write("Restore audio configuration",restore);}_settings=old;ShowError("Audio device configuration could not be applied",ex);}
                }else _settings=candidate;
                if(!_smokeMode)try{SettingsStore.Save(_settings,SettingsStore.DefaultPath);}catch(Exception ex){ShowError("Settings could not be saved",ex);}
                _history.Limit=_settings.UndoLimit;ShortcutRegistry.ApplyOverrides(_settings.ShortcutOverrides);RefreshShortcutLabels(_menu.Items);ApplyUiScale();RefreshState();RestartIntegrations();
            }
        }
        private void ApplyUiScale(){if(_scaling||_timeline==null)return;_scaling=true;var controls=new List<Control>(Descendants(this));SuspendLayout();foreach(var c in controls)c.SuspendLayout();if(IsHandleCreated)SendMessage(Handle,11,IntPtr.Zero,IntPtr.Zero);try{DisplayScale.User=_settings.UiScale/100f;float ratio=DisplayScale.For(this);TableLayoutPanel shell=_controlBar.Parent as TableLayoutPanel;if(shell!=null){int menuHeight=Math.Max(24,_menu.Font.Height+_menu.Padding.Vertical+4);shell.RowStyles[0].Height=menuHeight;shell.RowStyles[1].Height=(float)Math.Ceiling(56*ratio);}if(!_columnsSplit.Panel1Collapsed)_columnsSplit.SplitterDistance=Math.Max(_columnsSplit.Panel1MinSize,Math.Min(_columnsSplit.Width-_columnsSplit.Panel2MinSize-_columnsSplit.SplitterWidth,(int)(212*ratio)));SplitContainer inspector=FindInspectorSplit();if(!inspector.Panel1Collapsed)inspector.SplitterDistance=Math.Max(inspector.Panel1MinSize,Math.Min(inspector.Width-inspector.Panel2MinSize-inspector.SplitterWidth,(int)(180*ratio)));if(_launcher!=null)ApplyAdvancedScale();_transportTimer.Interval=_settings.SmoothAnimation?16:50;DisplayScale.TouchMode=_settings.InputMode=="Touchscreen";_settings.Integration.ControllerEnabled=_settings.InputMode=="XInput Controller"||_settings.InputMode=="DirectInput Controller"||_settings.Integration.ControllerEnabled;}finally{foreach(var c in controls)c.ResumeLayout(false);ResumeLayout(true);if(_controlBar.Parent!=null)_controlBar.Parent.PerformLayout();if(IsHandleCreated)SendMessage(Handle,11,(IntPtr)1,IntPtr.Zero);Invalidate(true);_scaling=false;}}
        protected override void WndProc(ref Message m){if(m.Msg==0x02e0){base.WndProc(ref m);if(_timeline!=null)BeginInvoke((Action)delegate{ApplyUiScale();});return;}if(!_fullScreen&&FormBorderStyle==FormBorderStyle.None&&m.Msg==0x84&&WindowState!=FormWindowState.Maximized){base.WndProc(ref m);long position=m.LParam.ToInt64();Point point=PointToClient(new Point((short)(position&0xffff),(short)((position>>16)&0xffff)));bool left=point.X<6,right=point.X>Width-6,top=point.Y<6,bottom=point.Y>Height-6;int hit=left?(top?13:bottom?16:10):right?(top?14:bottom?17:11):top?12:bottom?15:1;m.Result=(IntPtr)hit;return;}base.WndProc(ref m);}

        private SplitContainer FindInspectorSplit()
        {
            foreach(Control c in _columnsSplit.Panel2.Controls) if(c is SplitContainer) return (SplitContainer)c;
            throw new InvalidOperationException("The inspector divider is unavailable.");
        }

        private void UpdateViewMenuChecks()
        {
            if(_showBrowserMenuItem!=null)_showBrowserMenuItem.Checked=!_mediaSplit.Panel2Collapsed;
            if(_showInspectorMenuItem!=null)_showInspectorMenuItem.Checked=!FindInspectorSplit().Panel1Collapsed;
            if(_showMixerMenuItem!=null)_showMixerMenuItem.Checked=!_workspaceSplit.Panel2Collapsed;
        }

        private void ToggleSelectedMix(bool mute)
        {
            if(!_selectedTrackId.HasValue)return;Track t=FindTrack(_selectedTrackId.Value);if(t==null)return;
            ApplyTrackMix(t.Id,t.GainDb,t.Pan,mute?!t.Muted:t.Muted,mute?t.Solo:!t.Solo);
        }

        private void SetMixForAll(bool clearMute,bool clearSolo)
        {
            List<IProjectCommand> commands=new List<IProjectCommand>();foreach(Track t in _project.Tracks){double gain=t.GainDb,pan=t.Pan;bool muted=clearMute?false:t.Muted;bool solo=clearSolo?false:t.Solo;if(muted!=t.Muted||solo!=t.Solo)commands.Add(new SetTrackMixCommand(t.Id,CopyMix(t),CopyMix(t,gain,pan,muted,solo)));}
            if(commands.Count==0)return;try{_history.Execute(_project,new CompositeCommand(clearSolo?"Clear solo":"Unmute all",commands));RefreshState();}catch(Exception ex){ShowError(T("error.operation"),ex);}
        }

        private static Track CopyMix(Track track){return CopyMix(track,track.GainDb,track.Pan,track.Muted,track.Solo);}
        private static Track CopyMix(Track track,double gain,double pan,bool muted,bool solo){return new Track{Id=track.Id,Name=track.Name,Type=track.Type,GainDb=gain,Pan=pan,Muted=muted,Solo=solo};}

        private void ApplyTrackMix(Guid id,double gain,double pan,bool muted,bool solo)
        {
            Track before=FindTrack(id);if(before==null)return;if(Math.Abs(before.GainDb-gain)<0.0001&&Math.Abs(before.Pan-pan)<0.0001&&before.Muted==muted&&before.Solo==solo)return;
            try{_history.Execute(_project,new SetTrackMixCommand(id,CopyMix(before),CopyMix(before,gain,pan,muted,solo)));RefreshState();}catch(Exception ex){ShowError(T("error.operation"),ex);}
        }
        private void OnMixerMixChanged(object sender,MixerTrackEventArgs e){ApplyTrackMix(e.TrackId,e.GainDb,e.Pan,e.Muted,e.Solo);}
        private void OnTrackMixRequested(object sender,MixerTrackEventArgs e){ApplyTrackMix(e.TrackId,e.GainDb,e.Pan,e.Muted,e.Solo);}

        private void OnInspectorTrackEdit(object sender,InspectorTrackEventArgs e)
        {
            Track track=FindTrack(e.TrackId);if(track==null)return;List<IProjectCommand> commands=new List<IProjectCommand>();string name=ProjectValidator.NormalizeName(e.Name,"Track name");if(name!=track.Name)commands.Add(new RenameTrackCommand(track.Id,track.Name,name));if(Math.Abs(track.GainDb-e.GainDb)>0.0001||Math.Abs(track.Pan-e.Pan)>0.0001||track.Muted!=e.Muted||track.Solo!=e.Solo)commands.Add(new SetTrackMixCommand(track.Id,CopyMix(track),CopyMix(track,e.GainDb,e.Pan,e.Muted,e.Solo)));if(commands.Count==0)return;try{_history.Execute(_project,new CompositeCommand("Edit track",commands));RefreshState();}catch(Exception ex){ShowError(T("error.operation"),ex);}
        }
        private void OnInspectorRegionEdit(object sender,InspectorRegionEventArgs e)
        {
            Track track=FindTrack(e.TrackId);Region region=track==null?null:FindRegion(track,e.RegionId);if(region==null)return;List<IProjectCommand> commands=new List<IProjectCommand>();string name=ProjectValidator.NormalizeName(e.Name,"Region name");if(name!=region.Name)commands.Add(new RenameRegionCommand(track.Id,region.Id,region.Name,name));if(Math.Abs(region.StartBeat-e.StartBeat)>0.0001)commands.Add(new MoveRegionCommand(track.Id,region.Id,region.StartBeat,e.StartBeat));if(Math.Abs(region.LengthBeats-e.LengthBeats)>0.0001)commands.Add(new ResizeRegionCommand(track.Id,region.Id,region.LengthBeats,e.LengthBeats));if(commands.Count==0)return;try{_history.Execute(_project,new CompositeCommand("Edit region",commands));RefreshState();}catch(Exception ex){ShowError(T("error.operation"),ex);}
        }

        private void OnPlayheadRequested(object sender,PlayheadRequestEventArgs e){if(_recording)StopAudio();SetCursorBeat(e.Beat);}
        private void SetCursorBeat(double beat){_cursorBeat=Math.Max(0.0,beat);if(_engine!=null)_engine.Seek(_cursorBeat);_timeline.SetPlayheadBeat(_cursorBeat);UpdatePositionDisplay();if(_controlBar!=null)_controlBar.SetState(_project,_cursorBeat,_engine!=null&&_engine.Playing,_recording,_engine==null?0:_engine.CpuLoad);}
        private void MoveCursorByBars(int count){if(_recording)StopAudio();double bar=_project.TimeSignatureNumerator*4.0/_project.TimeSignatureDenominator;SetCursorBeat(_cursorBeat+bar*count);}
        private void UpdatePositionDisplay(){if(_positionLabel==null)return;double barLength=_project.TimeSignatureNumerator*4.0/_project.TimeSignatureDenominator;if(barLength<=0)barLength=4;int bar=(int)Math.Floor(_cursorBeat/barLength)+1;double within=_cursorBeat%barLength;int beat=(int)Math.Floor(within)+1;int tick=(int)Math.Round((within-Math.Floor(within))*960);_positionLabel.Text=bar.ToString("000")+"  "+beat.ToString("0")+"  "+tick.ToString("000");}
        private void SelectAdjacentRegion(int delta){if(!_selectedTrackId.HasValue)return;Track t=FindTrack(_selectedTrackId.Value);if(t==null||t.Regions.Count==0)return;t.Regions.Sort(delegate(Region a,Region b){return a.StartBeat.CompareTo(b.StartBeat);});int index=-1;if(_selectedRegionId.HasValue)for(int i=0;i<t.Regions.Count;i++)if(t.Regions[i].Id==_selectedRegionId.Value){index=i;break;}index=Math.Max(0,Math.Min(t.Regions.Count-1,index+delta));Select(t.Id,t.Regions[index].Id);}

        private void Undo()
        {
            if(_recording)StopAudio();ClosePluginEditors();
            if (!_history.CanUndo) return;
            try { _history.Undo(_project); RefreshState(); }
            catch (Exception exception) { ShowError(T("error.operation"), exception); }
        }

        private void Redo()
        {
            if(_recording)StopAudio();ClosePluginEditors();
            if (!_history.CanRedo) return;
            try { _history.Redo(_project); RefreshState(); }
            catch (Exception exception) { ShowError(T("error.operation"), exception); }
        }

        private void ZoomBy(int delta)
        {
            _settings.TimelineZoom = Math.Max(4, Math.Min(144, _settings.TimelineZoom + delta));
            _timeline.SetZoom(_settings.TimelineZoom);
        }

        private void ChangeLanguage(string language)
        {
            try
            {
                string path = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "locales"), language + ".json");
                _locale = LocaleCatalog.Load(path, language);
                _settings.Language = language;UiText.Load(language);
                ApplyLocalization();
            }
            catch (Exception exception)
            {
                ShowError(T("error.operation"), exception);
            }
        }

        private void ShowAbout(){
         using(var about=new Form{Text=UiText.Translate("About Slop Studio"),ClientSize=new Size(730,550),MinimumSize=new Size(640,480),BackColor=Color.FromArgb(29,33,41),ForeColor=Color.Gainsboro,StartPosition=FormStartPosition.CenterParent,Font=new Font("Segoe UI",9)}){
          var art=new Panel{Dock=DockStyle.Left,Width=185};art.Paint+=delegate(object s,PaintEventArgs e){BrandMark.Draw(e.Graphics,new Rectangle(22,32,145,145));};
          var text=new Label{Dock=DockStyle.Top,AutoSize=true,MaximumSize=new Size(480,0),Padding=new Padding(8,34,22,12),Text="Slop Studio 0.13\r\nWindows x86-64\r\n"+UiText.Translate("Author")+": Tsuikyu\r\n\r\n"+UiText.Translate("Digital audio workstation.")+"\r\n\r\n"+UiText.Translate("Original application code: MIT.")+"\r\nASIO / Ableton Link companion: GPL v3.\r\nLua: MIT · ARA SDK: Apache 2.0\r\nVST3 / CLAP: official SDKs.\r\n\r\nASIO is a registered trademark of Steinberg Media Technologies GmbH.\r\n\r\n"+UiText.Translate("See THIRD-PARTY-NOTICES.md and licenses for full notices.")};var content=new Panel{Dock=DockStyle.Fill,AutoScroll=true};content.Controls.Add(text);
          var notice=new Label{Dock=DockStyle.Bottom,Height=104,Padding=new Padding(23,10,23,5),ForeColor=Color.FromArgb(163,182,201),Text=UiText.Translate(UiText.Disclaimer)+"\r\n\r\n"+UiText.Translate(UiText.Attribution)};var close=new Button{Text=UiText.Translate("Close"),Dock=DockStyle.Bottom,Height=38,FlatStyle=FlatStyle.Flat};close.Click+=delegate{about.Close();};about.Controls.Add(content);about.Controls.Add(art);about.Controls.Add(notice);about.Controls.Add(close);about.ShowDialog(this);
         }
        }

        private void ShowError(string message, Exception exception)
        {
            ErrorLog.Write(message,exception);string text = UiText.Translate(message);
            if (exception != null && !String.IsNullOrEmpty(exception.Message))
                text += Environment.NewLine + Environment.NewLine + UiText.Translate(exception.Message);
            MessageBox.Show(this, text, T("dialog.errorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void OnTempoChanged(object sender, EventArgs e)
        {
            if (_refreshing || _project == null) return;
            double value = (double)_tempo.Value;
            if (Math.Abs(value - _project.Tempo) < 0.0001) return;
            try
            {
                _history.Execute(_project, new SetTempoCommand(_project.Tempo, value));
                RefreshState();
            }
            catch (Exception exception) { ShowError(T("error.operation"), exception); }
        }

        private void OnTimelineSelectionChanged(object sender, TimelineSelectionEventArgs e)
        {
            _selectedTrackId = e.TrackId;
            _selectedRegionId = e.RegionId;
            RefreshState();
        }

        private void OnRegionMoveRequested(object sender, RegionMoveEventArgs e)
        {
            try
            {
                Region region = FindRegion(FindTrack(e.TrackId), e.RegionId);
                if (region == null) return;
                double delta=e.NewStartBeat-region.StartBeat;var moves=new List<IProjectCommand>();var selected=new HashSet<Guid>(_timeline.SelectedRegions);selected.Add(region.Id);double earliest=Double.MaxValue;foreach(var t in _project.Tracks)foreach(var r in t.Regions)if(selected.Contains(r.Id))earliest=Math.Min(earliest,r.StartBeat);delta=Math.Max(-earliest,delta);foreach(var t in _project.Tracks)foreach(var r in t.Regions)if(selected.Contains(r.Id))moves.Add(new MoveRegionCommand(t.Id,r.Id,r.StartBeat,r.StartBeat+delta));_history.Execute(_project,new CompositeCommand("Move regions",moves));
                _selectedTrackId = e.TrackId;
                _selectedRegionId = e.RegionId;
                RefreshState();
            }
            catch (Exception exception) { ShowError(T("error.operation"), exception); }
        }

        private void OnShown(object sender, EventArgs e)
        {
            BeginInvoke((Action)delegate{Rectangle area=Screen.FromHandle(Handle).WorkingArea;MaximizedBounds=area;WindowState=FormWindowState.Normal;Bounds=area;WindowState=FormWindowState.Maximized;ApplyUiScale();});_timeline.Focus();_transportTimer.Start();
            if(!_smokeMode){try{_engine.OpenDevices();}catch(Exception ex){_audioDeviceError=ex.Message;_statusLabel.Text=ex.Message;}StartAdvancedFeatures();return;}
            Timer timer = new Timer();
            timer.Interval = 180;
            timer.Tick += delegate
            {
                timer.Stop();
                timer.Dispose();
                RunSmokeTest();
            };
            timer.Start();
        }

        private static void WriteSmokeWave(string path)
        {
            const int rate = 22050;
            const int samples = 2205;
            const int dataBytes = samples * 2;
            using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(new byte[] { 82, 73, 70, 70 });
                writer.Write(36 + dataBytes);
                writer.Write(new byte[] { 87, 65, 86, 69 });
                writer.Write(new byte[] { 102, 109, 116, 32 });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(rate);
                writer.Write(rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new byte[] { 100, 97, 116, 97 });
                writer.Write(dataBytes);
                for (int i = 0; i < samples; i++)
                    writer.Write((short)(Math.Sin(2.0 * Math.PI * 440.0 * i / rate) * 8000.0));
            }
        }

        [DllImport("user32.dll")]private static extern bool ReleaseCapture();

        [DllImport("user32.dll", CharSet=CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

        private static MidiNote FindMidiNote(Region region,Guid id)
        {
            if(region==null||region.Notes==null)return null;foreach(MidiNote note in region.Notes)if(note.Id==id)return note;return null;
        }

        private static Control FindControlByTypeName(Control root,string typeName)
        {
            if(root==null)return null; foreach(Control child in root.Controls){if(child.GetType().Name==typeName)return child;Control found=FindControlByTypeName(child,typeName);if(found!=null)return found;} return null;
        }

        private static T FindDescendant<T>(Control parent,int index) where T:Control
        {
            foreach(Control child in parent.Controls)
            {
                if(child is T){if(index==0)return (T)child;index--;}
                T nested=FindDescendant<T>(child,index);
                if(nested!=null)return nested;
                int found=CountDescendants<T>(child);
                if(found>0)index-=found;
            }
            return null;
        }

        private static int CountDescendants<T>(Control parent) where T:Control
        {
            int count=0;foreach(Control child in parent.Controls){if(child is T)count++;count+=CountDescendants<T>(child);}return count;
        }

        private static bool MenuShortcutMatches(ToolStripMenuItem item,AppCommand command){return item!=null&&item.ShortcutKeyDisplayString==ShortcutRegistry.Display(command);}

        private void WaitGui(int milliseconds){DateTime end=DateTime.UtcNow.AddMilliseconds(milliseconds);while(DateTime.UtcNow<end){Application.DoEvents();System.Threading.Thread.Sleep(8);}}
        [DllImport("user32.dll")]private static extern bool PrintWindow(IntPtr window,IntPtr device,uint flags);
        private void CaptureGui(string suffix){using(Bitmap bitmap=new Bitmap(Width,Height)){using(var graphics=Graphics.FromImage(bitmap)){IntPtr dc=graphics.GetHdc();try{if(!PrintWindow(Handle,dc,2))throw new InvalidOperationException("Own-window capture failed.");}finally{graphics.ReleaseHdc(dc);}}string directory=Path.GetDirectoryName(_smokeReportPath),name=Path.GetFileNameWithoutExtension(_smokeReportPath)+suffix+".png";bitmap.Save(Path.Combine(directory,name),System.Drawing.Imaging.ImageFormat.Png);}}
        private bool SendTestShortcut(Keys keys){Message message=Message.Create(Handle,0x100,(IntPtr)(int)(keys&Keys.KeyCode),IntPtr.Zero);return ProcessCmdKey(ref message,keys);}
        private void RunSmokeTest()
        {
            List<string> results=new List<string>();string directory=Path.GetDirectoryName(_smokeReportPath);string projectPath=Path.Combine(directory,"workflow.slopdaw"),wavePath=Path.Combine(directory,"workflow.wav");List<string> capturedFiles=new List<string>();
            try{
                if(!Visible||!IsHandleCreated)throw new InvalidOperationException("Window did not become visible.");WaitGui(50);Rectangle workArea=Screen.FromHandle(Handle).WorkingArea;Rectangle visibleClient=RectangleToScreen(ClientRectangle);if(visibleClient.Right>workArea.Right+1||visibleClient.Bottom>workArea.Bottom+1||visibleClient.Left<workArea.Left-1||visibleClient.Top<workArea.Top-1)throw new InvalidOperationException("Initial maximized window exceeded the working area: actual="+Bounds+" work="+workArea+" state="+WindowState);CaptureGui(String.Empty);
                if(_browser.Height<ClientSize.Height*.70||_inspector.Height<ClientSize.Height*.70||_timeline.Width<400)throw new InvalidOperationException("Reference panel hierarchy did not fill the window.");results.Add("PASS Windows window shown; library and inspector extend to the bottom; arrangement has usable width");
                ShortcutRegistry.ValidateUniqueActiveAssignments();results.Add("PASS unique central shortcut registry");
                AddRegion();Track midi=FindTrack(_selectedTrackId.Value);Region midiRegion=FindRegion(midi,_selectedRegionId.Value);List<MidiNote> notes=new List<MidiNote>();for(int i=0;i<4;i++)notes.Add(new MidiNote{StartBeat=i,LengthBeats=0.5,Pitch=60+i*4,Velocity=92});_history.Execute(_project,new SetMidiNotesCommand(_project,midi.Id,midiRegion.Id,notes));RefreshState();OpenEditor();WaitGui(30);
                if(!_lowerDock.IsPianoRollVisible||_pianoRoll.NoteCount!=4)throw new InvalidOperationException("Piano roll did not show MIDI events.");Control canvas=FindControlByTypeName(_pianoRoll,"PianoRollCanvas");MethodInfo down=canvas.GetType().GetMethod("OnMouseDown",BindingFlags.Instance|BindingFlags.NonPublic),up=canvas.GetType().GetMethod("OnMouseUp",BindingFlags.Instance|BindingFlags.NonPublic);MethodInfo doubleClick=canvas.GetType().GetMethod("OnMouseDoubleClick",BindingFlags.Instance|BindingFlags.NonPublic);down.Invoke(canvas,new object[]{new MouseEventArgs(MouseButtons.Left,1,DisplayScale.Pixels(canvas,78),DisplayScale.Pixels(canvas,76),0)});doubleClick.Invoke(canvas,new object[]{new MouseEventArgs(MouseButtons.Left,2,DisplayScale.Pixels(canvas,78),DisplayScale.Pixels(canvas,76),0)});up.Invoke(canvas,new object[]{new MouseEventArgs(MouseButtons.Left,1,DisplayScale.Pixels(canvas,78),DisplayScale.Pixels(canvas,76),0)});if(_pianoRoll.NoteCount!=5)throw new InvalidOperationException("Piano roll double-click did not persist a note.");Undo();if(_pianoRoll.NoteCount!=4)throw new InvalidOperationException("Note undo failed.");Redo();if(_pianoRoll.NoteCount!=5)throw new InvalidOperationException("Note redo failed.");CaptureGui("-midi");results.Add("PASS MIDI note editing gesture, undo/redo, selection and lower editor dock");
                AddTrack(TrackType.Audio);WriteSmokeWave(wavePath);ImportWavPath(wavePath);Track audio=FindTrack(_selectedTrackId.Value);if(audio.Type!=TrackType.Audio||audio.Regions.Count<1)throw new InvalidOperationException("Audio import did not produce a real region.");results.Add("PASS WAV import into an audio track");
                int fixtureCount=0;string fixtureFolder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TestFixtures");foreach(string extension in new[]{"vst3","clap"}){string fixture=Path.Combine(fixtureFolder,"FixtureGain."+extension);if(!File.Exists(fixture)){results.Add("NOT RUN "+extension+" fixture: diagnostic plug-in is not included in the release package");continue;}PluginDescriptor descriptor=NativePlugin.Scan(fixture)[0];PluginSlot slot=descriptor.CreateSlot();List<PluginSlot> slots=new List<PluginSlot>(audio.Plugins);slots.Add(slot);_history.Execute(_project,new SetPluginSlotsCommand(audio,slots));RefreshState();NativePlugin plugin=_engine.GetPlugin(slot.Id);if(plugin==null||plugin.Parameters().Count!=1)throw new InvalidOperationException("Plugin was not routed to the app track: "+extension);plugin.SetParameter(1000,0.4);fixtureCount++;results.Add("PASS "+extension+" discovery, app track instantiation and real parameter access");}
                SetCursorBeat(0);StartTransport();WaitGui(180);if(!_engine.Playing||_engine.PositionBeat<=0||_engine.MasterPeak<=0)throw new InvalidOperationException("Continuous arrangement audio did not run.");StopAudio();if(_engine.Playing)throw new InvalidOperationException("Transport Stop failed.");results.Add("PASS continuous multitrack audio output, internal MIDI synthesis, real meters and transport stop (not audibly auditioned)"+(fixtureCount==2?"; VST3/CLAP fixture chain included":"; fixture chain not tested"));
                MidiRecordingSession recording=new MidiRecordingSession(120);long now=System.Diagnostics.Stopwatch.GetTimestamp();recording.Receive(new MidiInputMessage{Status=0x90,Data1=64,Data2=96,Timestamp=now});recording.Receive(new MidiInputMessage{Status=0x80,Data1=64,Timestamp=now+System.Diagnostics.Stopwatch.Frequency/4});List<MidiNote> captured=recording.Stop();if(captured.Count!=1||captured[0].Pitch!=64||captured[0].LengthBeats<0.49)throw new InvalidOperationException("Captured MIDI message timing was not preserved.");results.Add("PASS MIDI capture event/timestamp fixture; physical MIDI input not injected");
                DeviceChoice loopInput=null,loopOutput=null;foreach(DeviceChoice d in WinMmDevices.AudioInputs())if(d.Name.Contains("Loopback Main"))loopInput=d;foreach(DeviceChoice d in WinMmDevices.AudioOutputs())if(d.Name.Contains("Virtual 7/8"))loopOutput=d;
                if(loopInput!=null&&loopOutput!=null){_settings.AudioBackend="WASAPI";foreach(var d in WasapiStream.Devices(false))if(d.Name.Contains("Virtual 7/8"))_settings.OutputEndpoint=d.Id;foreach(var d in WasapiStream.Devices(true))if(d.Name.Contains("Loopback Main"))_settings.InputEndpoint=d.Id;_settings.AudioInput=loopInput.Id;_settings.AudioOutput=loopOutput.Id;_engine.Configure(_settings);RefreshState();Select(audio.Id,audio.Regions[0].Id);SetCursorBeat(0);int before=audio.Regions.Count;ExecuteCommand(AppCommand.Record);WaitGui(270);if(!_recording)throw new InvalidOperationException("Record command did not start the input capture workflow.");CaptureGui("-recording");StopAudio();if(audio.Regions.Count!=before+1)throw new InvalidOperationException("Stopping recording did not preserve an audio take.");Region take=audio.Regions[audio.Regions.Count-1];PcmWaveInfo takeInfo=WaveAudio.Inspect(take.MediaPath);if(takeInfo.DataBytes<1000)throw new InvalidOperationException("Recorded input PCM was not preserved.");capturedFiles.Add(take.MediaPath);results.Add("PASS Record/Stop GUI workflow captured and preserved actual WASAPI Minifuse virtual loopback PCM input");}
                else results.Add("NOT RUN GUI audio recording hardware test: matching virtual loopback devices unavailable");
                List<DeviceChoice> midiInputs=WinMmDevices.MidiInputs();if(midiInputs.Count>1){_settings.MidiInput=midiInputs[1].Id;_engine.Configure(_settings);foreach(Track t in _project.Tracks)t.Armed=t.Id==midi.Id;RefreshState();Select(midi.Id,midiRegion.Id);SetCursorBeat(4);int before=midi.Regions.Count;ExecuteCommand(AppCommand.Record);_engine.InjectMidi(0x90,67,100);WaitGui(90);_engine.InjectMidi(0x80,67,0);StopAudio();if(midi.Regions.Count!=before+1||midi.Regions[midi.Regions.Count-1].Notes.Count!=1)throw new InvalidOperationException("MIDI Record/Stop did not preserve the injected short messages.");results.Add("PASS MIDI Record/Stop workflow with actual MIDI input open and injected timestamped note messages; physical key performance remains unverified");}
                _engine.CapturePluginStates();ProjectDocument expected=ProjectFactory.Clone(_project);if(!SaveToPath(projectPath))throw new InvalidOperationException("Project save failed.");OpenProjectPath(projectPath);if(!ProjectFile.Equivalent(expected,_project)){File.WriteAllText(Path.Combine(directory,"expected.json"),ProjectFile.Serialize(expected));File.WriteAllText(Path.Combine(directory,"actual.json"),ProjectFile.Serialize(_project));throw new InvalidOperationException("Project reopen changed notes, plugin state or media.");}results.Add("PASS application Save/Open with MIDI events and audio media"+(fixtureCount==2?" and VST3/CLAP fixture state":"; plug-in state not tested"));
                AppSettings pref=new AppSettings{AudioOutput=-1,AudioInput=-1,MidiInput=-1,MidiOutput=-1,SampleRate=48000,BufferFrames=256,UiScale=115,DefaultTempo=128,DefaultNumerator=3,DefaultDenominator=4};string prefPath=Path.Combine(directory,"preferences.json");SettingsStore.Save(pref,prefPath);AppSettings restored=SettingsStore.Load(prefPath);if(restored.BufferFrames!=256||restored.UiScale!=115||restored.DefaultNumerator!=3)throw new InvalidOperationException("Device/display/project preferences did not persist.");using(PreferencesForm form=new PreferencesForm(_settings)){form.Show(this);WaitGui(35);using(Bitmap shot=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(shot,new Rectangle(Point.Empty,form.Size));shot.Save(Path.Combine(directory,Path.GetFileNameWithoutExtension(_smokeReportPath)+"-preferences.png"),System.Drawing.Imaging.ImageFormat.Png);}if(!form.Visible)throw new InvalidOperationException("Preferences window did not show.");form.Close();}results.Add("PASS persistent device/buffer/MIDI/plugin-path/display/project settings and preferences window");
                WindowState=FormWindowState.Normal;Size=new Size(1280,800);WaitGui(50);if(_timeline.Width<350||_browser.Height<600)throw new InvalidOperationException("1280x800 layout failed.");CaptureGui("-1280");_settings.UiScale=150;ApplyUiScale();WaitGui(35);if(_timeline.Width<350)throw new InvalidOperationException("150% workspace scale clipped the arrangement.");if(((Control)_controlBar).ClientSize.Height<Math.Ceiling(51*DisplayScale.For(_controlBar)))throw new InvalidOperationException("Scaled transport was clipped vertically.");if(_menu.Items.Cast<ToolStripItem>().Any(i=>i.Available && i.Bounds.Bottom>_menu.ClientSize.Height-_menu.Padding.Bottom))throw new InvalidOperationException("Main menu items were clipped vertically.");results.Add("PASS main menu and scaled transport bounds at 150% UI scale");CaptureGui("-scale150");_settings.UiScale=100;ApplyUiScale();results.Add("PASS panel and track scale at 100% / 150%; this is not a per-monitor DPI test");WindowState=FormWindowState.Maximized;WaitGui(50);_workspaceSplit.Panel2Collapsed=false;_lowerDock.ShowMixer();WaitGui(50);CaptureGui("-project");results.Add("PASS restored/maximized layout and actual GUI captures at 1280x800 and host size");
                Control arrangementCanvas=FindControlByTypeName(_timeline,"TimelineCanvas");arrangementCanvas.Focus();bool originalMute=_project.Tracks[0].Muted;SendTestShortcut(Keys.M);if(_project.Tracks[0].Muted==originalMute)throw new InvalidOperationException("M did not execute the shared mute command.");SendTestShortcut(Keys.M);SendTestShortcut(Keys.Space);WaitGui(80);if(!_engine.Playing)throw new InvalidOperationException("Space did not start playback.");SendTestShortcut(Keys.NumPad0);if(_engine.Playing)throw new InvalidOperationException("Numeric keypad Stop did not stop playback.");TextBox entry=FindDescendant<TextBox>(_inspector,0);entry.Focus();bool wasRecording=_recording;SendTestShortcut(Keys.R);SendTestShortcut(Keys.Space);if(_recording!=wasRecording||_engine.Playing)throw new InvalidOperationException("Ordinary text entry triggered a DAW command.");results.Add("PASS dispatched Space / Num 0 / M shortcuts and text-field focus guard");_timeline.Focus();
                string example=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Examples","First Light","First Light.slopdaw");if(File.Exists(example)){_history.Reset(true);OpenProjectPath(example);if(_project.Tracks.Count!=12||!File.Exists(_project.Tracks[8].Regions[0].MediaPath))throw new InvalidOperationException("Portable example media paths failed.");_timeline.SetZoom(12);_timeline.SetLaneHeight(30);_workspaceSplit.Panel2Collapsed=false;_workspaceSplit.SplitterDistance=Math.Max(210,(int)(_workspaceSplit.Height*.58));_lowerDock.ShowMixer();SetCursorBeat(0);StartTransport();WaitGui(250);if(_engine.MasterPeak<=0)throw new InvalidOperationException("Example did not render audio.");CaptureGui("-example-mixer");StopAudio();Select(_project.Tracks[0].Id,_project.Tracks[0].Regions[0].Id);OpenEditor();WaitGui(100);CaptureGui("-example-piano");ExecuteCommand(AppCommand.ToggleBrowser);WaitGui(50);if(!_mediaBrowser.Visible)throw new InvalidOperationException("Right project browser did not open.");CaptureGui("-example-browser");ExecuteCommand(AppCommand.ToggleBrowser);results.Add("PASS original playable example: 12 tracks / 46 real regions; MIDI synthesis + linked PCM; mixer, piano roll, separate right media browser");}
                results.Add("NOT VERIFIED physical speaker/headphone output; physical MIDI note injection; microphone capture in this GUI smoke");
            }catch(Exception ex){Environment.ExitCode=1;results.Add("FAIL "+ex.ToString());}
            finally{foreach(string capturedFile in capturedFiles)try{if(File.Exists(capturedFile))File.Delete(capturedFile);}catch{}try{StopAudio();DisposeAdvancedFeatures();_engine.Dispose();_transportTimer.Stop();}catch(Exception ex){results.Add("FAIL cleanup "+ex.Message);Environment.ExitCode=1;}try{File.WriteAllText(_smokeReportPath,String.Join(Environment.NewLine,results.ToArray())+Environment.NewLine,new UTF8Encoding(false));}catch{}Close();}
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_smokeMode || _closing)
                return;
            if (!ConfirmDiscardChanges())
            {
                e.Cancel = true;
                return;
            }
            try
            {
                SettingsStore.Save(_settings, SettingsStore.DefaultPath);
                StopAudio();_engine.Dispose();_transportTimer.Stop();
                _closing = true;
            }
            catch (Exception exception)
            {
                DialogResult result = MessageBox.Show(this, T("error.settingsFailed") + Environment.NewLine + Environment.NewLine +
                    exception.Message, T("dialog.errorTitle"), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (result == DialogResult.Cancel)
                    e.Cancel = true;
                else
                    _closing = true;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if(_mayhem!=null&&(keyData&Keys.KeyCode)==Keys.Space){StopMayhem();return true;}
            if(IsTextEntryFocused()){Keys code=keyData&Keys.KeyCode,modifiers=keyData&Keys.Modifiers;if((modifiers&Keys.Alt)!=0)return base.ProcessCmdKey(ref msg,keyData);bool typing=(modifiers&(Keys.Control|Keys.Alt))==Keys.None&&!(code>=Keys.F1&&code<=Keys.F24);bool editing=modifiers==Keys.Control&&(code==Keys.A||code==Keys.C||code==Keys.X||code==Keys.V||code==Keys.Z||code==Keys.Y||code==Keys.Left||code==Keys.Right||code==Keys.Home||code==Keys.End||code==Keys.Back);if(typing||editing)return base.ProcessCmdKey(ref msg,keyData);}
            ShortcutBinding binding;
            bool arrangementFocused=(_timeline!=null&&_timeline.ContainsFocus)||(_launcher!=null&&_launcher.ContainsFocus)||PianoContext()||(_timeline!=null&&_timeline.Visible&&_timeline.RectangleToScreen(_timeline.ClientRectangle).Contains(_editorPointer()));
            if(PianoContext()&&_pianoRoll.HandleNoteKey(keyData))return true;
            if(ShortcutRegistry.TryFind(keyData,arrangementFocused,out binding)){bool repeat=(msg.LParam.ToInt64()&(1L<<30))!=0;if(repeat&&(binding.Command==AppCommand.ToggleFullScreen||binding.Command==AppCommand.ToggleSoftwareKeyboard||binding.Command==AppCommand.UserManual||binding.Command==AppCommand.Record||binding.Command==AppCommand.PlayOrStop||binding.Command==AppCommand.ToggleCycle||binding.Command==AppCommand.ShowPianoRoll||binding.Command==AppCommand.ShowEditor||binding.Command==AppCommand.ToggleMixer||binding.Command==AppCommand.ToggleMute||binding.Command==AppCommand.ToggleSolo||binding.Command==AppCommand.ToggleArm||binding.Command==AppCommand.ToggleLibrary||binding.Command==AppCommand.ToggleInspector||binding.Command==AppCommand.ShowScore||binding.Command==AppCommand.ShowAutomation||binding.Command==AppCommand.ShowPitch||binding.Command==AppCommand.ToggleLauncher||binding.Command==AppCommand.ShowSamples||binding.Command==AppCommand.ToggleBrowser||binding.Command==AppCommand.Pause))return true;ExecuteCommand(binding.Command);return true;}
            return base.ProcessCmdKey(ref msg,keyData);
        }

        private bool IsTextEntryFocused()
        {
            Control current=this;
            while(current is ContainerControl)
            {
                Control child=((ContainerControl)current).ActiveControl;
                if(child==null)break;
                current=child;
            }
            return current is TextBoxBase || current is NumericUpDown || current is ComboBox || current is StudioChoice || (_tempo!=null&&_tempo.Focused);
        }
    }

    internal sealed class NamePrompt : Form
    {
        private readonly TextBox _textBox;

        private NamePrompt(string title, string prompt, string initialValue)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(380, 116);
            Label label = new Label();
            label.Text = prompt;
            label.AutoSize = false;
            label.SetBounds(12, 12, 350, 20);
            _textBox = new TextBox();
            _textBox.Text = initialValue;
            _textBox.SetBounds(12, 38, 354, 24);
            Button ok = new Button();
            ok.Text = "OK";
            ok.DialogResult = DialogResult.OK;
            ok.SetBounds(200, 76, 78, 26);
            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(288, 76, 78, 26);
            Controls.AddRange(new Control[] { label, _textBox, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
        }

        public static string Show(IWin32Window owner, string title, string prompt, string initialValue)
        {
            using (NamePrompt dialog = new NamePrompt(title, prompt, initialValue))
                return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._textBox.Text : null;
        }
    }
}
