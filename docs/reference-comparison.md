Current 0.8 audit: [defects](defect-report-v8.md), [reference comparison](reference-comparison-v8.md), [shortcut registry](shortcut-spec-v8.json). Earlier results below are historical.

# Visual reference comparison — 0.7

比較元はユーザー提供の2024-12-19、2024-09-13、2024-03-20の画面と、追加のアレンジ／Launcher／プラグイン／起動画面です。画像は分析だけに使い、アプリや配布物に埋め込んでいません。独自GDI描画・SVG・ICOと既存WinForms部品を使用しています。現在のApple公式リリースノートでは12.4が掲示されています。参照画像から未提供の13の画面仕様を推定して一致認定していません。

| 領域 | 参照と旧版の差／影響 | 0.7での実装・担当ファイル | 確認結果／残差 |
|---|---|---|---|
| 全体構成 | 初期版は小さなトラック一覧と白い空白が大部分 | MainForm / StudioPanels。左Library、Inspector、固定track headers、可動Arrangement、下部Mixer/Editors、右Browserを分割配置 | 最大化／1280×800で実画面確認。構造は近づいたが厳密一致はPARTIAL |
| 上部操作 | 文字だけの小さなtoolbar、transport欠落 | StudioChrome。左view group、中央8 transport controlsとLCD、右masterとutility controls | 実再生／停止／録音と状態同期。参照にある全utility buttonは未実装 |
| 配色・文字 | 明るすぎる地面と小さすぎる作業領域 | charcoalの編集面、灰色chrome、緑MIDI／青audio／紫の選択、Segoe UI、一定border | 4言語、選択／hover、文字重なりと目盛りを修正。Appleフォントや固有assetは使用しない |
| Library / Inspector | トラック選択と編集対象の関係が不明瞭 | StudioPanels。sound library、region/track fields、選択channel+master strips | 選択・名前・M/S/R・音量・plugin controlsに接続。参照の全patch/library体系はない |
| track headers | 一覧の密度・サイズ・armが不適切 | TimelineCanvas。番号、色帯、独自楽器／wave icon、名前、M/S/R、選択と幅変更 | 26/46等のlane高さとスクロール、8pixel hit area。アイコンの精細さは参照と異なる |
| Arrangement | 空白／ruler／region geometry／playhead不備 | TimelineEditor。beat ruler、cycle bar、sample由来waveform、MIDI bars、捕捉playhead、edge pan | 12 tracks /46 regionsを実際に発音。任意データの密度で違いが出るため画素一致認定なし |
| Mixer | 短いfader、被る数字、少ない状態表示 | ChannelStripControl。instrument/FX、pan、dB、real peak meters、fader、M/S/R、色strip。高さに応じfaderへ配分 | 操作と実エンジンに接続。aux/send/VCA/full bus graphは未実装で飾りにしない |
| Piano roll / score | 相互関係と拡縮が弱い | PianoRollEditor / CreationEditors。同じMIDIノートの編集、独立カメラ、幅変更、下部tabs | 一音編集・undo・譜面/step entry。高度な複数選択／商用浄書はPARTIAL |
| Pitch / Timing | 参照のFlex系編集と差が大きい | SlopPitch / TimingAnalysis。音声に由来するpitch segments／waveform、editable destination markers | 数値試験とGUI実編集PASS。AI／polyphonic／formant品質の一致は未達 |
| Launcher | 参照のセルとtimeline併置 | LauncherCanvas / MainFeatures。clip cells、scene launch、quantize、tracksとのsplit | ループ先頭音を修正。直接セル録音／performance recordingは未実装 |
| plugin UI | 一般的なリストしかなく使いにくい | PluginBrowserDialog。category、search、native dark headers、details、scan errors。vendor editorは実instanceを埋込み | real fixture/catalogとactual vendor editor確認。Apple付属pluginのGUIやアルゴリズムを模倣するfake画面は作らない |
| ウィンドウ | 最新指示では右上Windows方式が必要 | native Sizable/FixedDialog frames、DWM caption colors、独自icon | 右上ボタンを使用。Macの交通信号は撤去済み |
| 起動とAbout | 参照の製品感と案内が不足 | BrandMark / SplashForm / QuickStartForm / ShowAbout | 独自の猫耳シルエットと4言語指定文。Apple logo、スクショ切り抜き、他社iconは使わない |

実際のアプリ撮影: verification/v7/app-ja-JP-example-mixer.png、同example-piano、gui-finalのTiming、6effect、About、QuickStart等。撮影は対象アプリのPrintWindowだけで行い、デスクトップや他アプリを撮影していません。画像差分による厳密な一致率は算出していません。独自データ、解像度、OS frameが異なる画像に架空の一致率は付けません。

Source: https://support.apple.com/en-us/109503
