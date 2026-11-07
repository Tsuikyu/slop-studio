# Slop Studio 0.13

### The Digital Audio Slopstation

**An music production application born from the finest AI-generated slop.**

**Created by Tsuikyu | Windows 11 x64 | MIT License**

> **WARNING: THIS IS AI SLOP. AND IT'S NOT EVEN FINISHED.**
>
> USE AT YOUR OWN RISK. No guarantees of functionality. No guarantees of quality. Not even your precious production time is guaranteed.
>
> There are bugs. There are missing features. Some features might look like they work while doing absolutely nothing.
>
> **MOD IT. FORK IT. MUTATE IT. BREAK IT.**
>
> Do whatever you want with your own copy. Rewrite it, tear it apart, rebuild it from scratch, or turn it into something completely unrecognizable. The developer will be thoroughly satisfied.
>
> Just remember to back up your important projects. If your greatest masterpiece turns into a steaming pile of slop, that's between you and your hard drive.

---

## What the Hell Is This?

Slop Studio is a digital audio workstation (DAW) for Windows.

It's being developed to handle music composition, arrangement, recording, MIDI editing, mixing, and audio exporting in a single application.

The interface features a black-and-blue-purple design called **Minimal CyberSquare**.

The standard project format is `.slopdaw`.

Slop Studio packs audio tracks, MIDI tracks, a piano roll, mixer, plugins, automation, a sampler, and various other music production features into one glorious pile of software.

Current version: **0.13**.

What can it do?

Quite a few things.

What can't it do?

Quite a few other things.

It's supposed to help you make music, but occasionally, you might find yourself fixing the software instead.

**That's the Slop Studio experience.**

## How to Launch This Thing

Run `SlopStudio.exe` from the distribution folder.

System requirements:

- Windows 11 x64
- .NET Framework 4.8
- A reasonable tolerance for unfinished software

A Quick Start guide appears on the first launch.

The splash screen displays a dedicated image and a progress bar that updates according to the actual startup stages. If the image fails to load, a simpler fallback screen is displayed.

If the application launches successfully, make some music.

If it doesn't, start debugging.

## Projects: `.slopdaw`

Slop Studio saves projects using its proprietary `.slopdaw` format.

Project data includes MIDI, tracks, supported plugin states, insert order, automation, and other information.

This is the standard format for creating, saving, loading, and recovering projects.

When overwriting an existing project, the application creates a backup under a different filename.

Audio files are generally referenced rather than embedded. If you move a project to another computer, keep its audio files accessible.

Automatic collection of all referenced media is still unfinished.

If you don't want yesterday's perfectly functioning kick drum to suddenly become `[Missing]`, keep track of your sample folders.

For details, see the [`.slopdaw` Format Specification](docs/slopdaw-format.md).

## Features: The Cutting Edge of Slop Technology

**Basic Music Production Stuff**

- Audio and MIDI tracks
- Region editing and piano roll
- Playback, recording, Undo / Redo
- Sampler and built-in effects
- VST3 / CLAP plugin hosting
- Mixer, auxiliary channels, and track stacks
- Automation and comping
- Global tracks and chords
- Pitch editing in the waveform editor
- Video preview

**Additional Slop Technology**

- Slop Launcher: grid-based clip launching
- MIDI file import and export
- Limited FLP / ALS / DAWproject import
- WASAPI and an ASIO companion process
- Lua Mods
- OSC
- Game controller support
- AI integration via MCP
- UI resources and manuals in Japanese, English, German, and Korean

Implementation and verification status vary by feature. Some interface text also remains untranslated.

**JUST BECAUSE SOMETHING EXISTS IN A MENU DOESN'T MEAN IT ACTUALLY WORKS.**

Check the [Feature and Verification Register](docs/status-v0.13-ja.md) for the current status.

## Mixer and Effects

Built-in effects and external plugins are processed through an ordered insert chain.

Track and master inserts are supported, including drag-and-drop reordering.

Master effects can be added from the mixer's "Stereo Output" strip or the inspector's output strip.

Visual Equalizer displays a five-band frequency response graph. Dragging points on the graph edits the filters.

The Sampler's Attack, Decay, Sustain, and Release parameters can be adjusted using knobs or numerical controls.

Parameter changes in built-in effects, including Compressor, are reflected in audio processing during playback.

Some audio signal paths and plugin combinations still require further testing.

**If you hear sound, congratulations. Start making music.**

**If you don't, congratulations. You're now a software debugger.**

## Exporting: Turning Slop into Audio Files

Navigate to:

**File → Master → Export Project or Selection as Master**

Keyboard shortcut: `Ctrl+B`

This allows you to export WAV or MP3 files with master processing applied.

The same menu also provides controls to enable or disable master processing globally, display reference audio waveforms, and preview reference audio.

MP3 exporting requires separately installed FFmpeg and libmp3lame.

Project information can also be written to WAV / MP3 metadata tags.

The mastering screen supports exporting stems to a new folder.

Buses and their child tracks may overlap in stem exports. Nonlinear master effects may also cause the sum of exported stems to differ from the original mix.

The following features are currently unfinished:

- Rendering regions or tracks to audio at their original positions
- Replacing all tracks with rendered audio

Their buttons are disabled.

You can't click them.

We haven't built those features yet.

## Some Actually Useful Features

Right-click a region, track header, or mixer strip to change its background and text colors.

The sample browser displays waveforms and lets you audition samples through the default Windows audio output.

Inspector values can be adjusted by dragging vertically or using the mouse wheel.

When ASIO is selected, the settings button opens the driver's configuration panel.

The Lua Mod Manager allows multiple `.lua` files to be registered and executed when selected.

Game controller button assignments can be customized through the Key Commands section of Preferences.

**Press F11 to toggle fullscreen mode.**

Fullscreen makes everything look slightly more professional.

It does absolutely nothing to increase the percentage of completed features.

## Feeding Other DAW Projects to the Slop Machine

Slop Studio provides limited import support for FLP, ALS, and DAWproject files.

FLP importing can recover MIDI notes, their arrangement, and audio files that can be located.

Missing audio files are represented by silent `[Missing]` regions.

Right-click an affected region and select "Relink Audio File..." to specify a WAV file.

Other regions referencing the same missing audio source will also be updated.

The following data cannot currently be fully reconstructed:

- Original instruments and effects
- Automation
- Routing
- Trimming and time stretching
- Other application-specific data

Whatever the importer can recover, it recovers.

Whatever it can't recover is lost somewhere in the vast wilderness of slop.

Keep your original project files somewhere safe.

## What's New in Slop Studio 0.13?

### Master Volume and MIDI

The rectangular slider in the upper-right corner and the Stereo Output fader control the same master volume.

Use "File → Export MIDI" to save a Standard MIDI File.

Velocity bars at the bottom of the piano roll can be adjusted by dragging, and changes can be reverted with Undo.

### Sample Browser

Bookmarks and favorites store references to audio files.

Search operates on the currently displayed folder or the favorites collection.

Your audio files still need to be managed by an actual human being.

### Project Templates

Navigate to:

**File → Save Project as Template**

This creates a `.sloptemplate` file.

Templates and bundled example projects open as unsaved copies.

Save them as new `.slopdaw` projects.

Referenced audio files must remain accessible.

### Themes and Keyboard Shortcuts

Preferences allow you to configure:

- Undo history length
- Themes: `.sloptheme`
- Keyboard shortcuts: `.slopkeys`

Theme changes are applied to shared interface colors after restarting the application.

Some custom interface components still use fixed colors.

If you want to redesign the entire interface, go ahead.

Add more slop. Make it yours.

### Launchpad Support

Slop Studio includes functionality intended for Launchpad X and Launchpad Mini MK3 in Programmer Mode.

With explicitly selected MIDI input and output ports, it can control the first 8 tracks and 8 scenes.

**ACTUAL HARDWARE TESTING HAS NOT BEEN COMPLETED.**

Other Launchpad models are also untested.

According to the code, the lights are supposed to work.

Whether they light up in the physical world is something we'll discover through testing.

### Included Example Projects

Two sample projects are included:

- **EDM Sample - Melodic House** — 128 BPM
- **J-Pop - Shibuya** — 150 BPM

Automatic sample BPM following and track freeze / unfreeze are not implemented.

If your samples don't automatically match the project tempo, human intervention is currently required.

Yes, actual human labor. In 2026.

## Plugins and Presets

The upper sections of plugin, built-in effect, and Sampler windows provide:

- Save and load settings
- Copy and paste
- Comparison with the state captured when the window was opened

Presets use the `.sloppreset` format.

Copied settings remain available until the application closes and can be pasted into the same type of plugin.

Importing or exporting native preset formats belonging to external plugins is currently unfinished.

External samples used by the Sampler are not embedded into preset files.

Their original file paths must remain accessible.

Saving a preset will not magically resurrect missing samples.

## Letting AI Control the Slop: MCP

Slop Studio provides AI integration through stdio-based MCP.

MCP is disabled during normal application startup.

To use it with an MCP client, launch:

```powershell
SlopStudio.exe --mcp
```

The initial connection is read-only.

Editing operations must be authorized for each session through:

**Window → MCP AI Integration Manager**

File operations and record-arm actions require additional confirmation each time.

MCP interoperability and security testing remain in progress.

**AI CONTROLLING A DAW.**

How wonderfully modern.

Unfortunately, letting AI control the slop does not magically transform the slop into a finished product.

Back up important data before experimenting.

## Lua Mods: For People Who Want to Make Things Worse (or Better)

The Lua Mod Manager allows `.lua` files to be registered and explicitly executed.

You are welcome to create Mods, experiment, and extend the application.

Review scripts obtained from other people before running them.

If your own Mod goes completely berserk, congratulations.

That's software development.

**MOD IT. BREAK IT. FIX IT. BREAK IT AGAIN.**

Welcome to Slop Studio.

## Known Issues: The Hall of Unfinished Business

**SLOP STUDIO 0.13 IS NOT FINISHED.**

Features that are not implemented:

- Automatic sample BPM following
- Track freeze / unfreeze
- Rendering regions or tracks to audio at their original positions
- Replacing all tracks with rendered audio

Features and environments requiring additional testing:

- Synchronization between playback position and audible output
- Physical Launchpad devices
- Various combinations of physical MIDI keyboards, microphones, and ASIO devices
- ASIO driver compatibility
- VST3 / CLAP plugin compatibility
- DPI behavior across multiple monitors
- Builds on operating systems other than Windows
- Importing projects from other DAWs
- MCP interoperability and security
- Other unfinished features

**OSC LOCAL UDP RECEPTION FAILED IN THE TEST ENVIRONMENT.**

For the current development status, see the [Feature and Verification Register](docs/status-v0.13-ja.md).

Bug reports are welcome.

Reproduction steps are even more welcome.

"I clicked this button and the application crashes every time" is considerably more useful than "IT DOESN'T WORK."

**A reproducible disaster is the first step toward fixing it.**

## Building: Create Your Own Custom Slop

Slop Studio uses the following technologies:

| Component | Technology |
|---|---|
| Main application | C# / Windows Forms |
| .NET runtime | .NET Framework 4.8 |
| Audio and plugin processing | C++ |
| Build system | CMake |
| Primary target OS | Windows 11 x64 |
| ASIO / Ableton Link | Separate GPL companion processes |

**JUCE IS NOT USED.**

Build requirements:

- CMake 3.24 or newer
- Ninja or MSVC
- x64 C/C++ toolchain
- C# compiler compatible with .NET Framework 4.8
- Required SDKs and license files

For build commands and testing procedures, see [docs/build-and-test.md](docs/build-and-test.md).

Protect existing projects, audio assets, application settings, and Git history during development.

**MODIFY THE SOURCE CODE AS MUCH AS YOU WANT.**

Rewrite the entire UI.

Mutate the mixer beyond recognition.

Add 100 unnecessary features.

Delete your own working copy and start from scratch.

If nothing works afterward, congratulations.

It's your Slop Studio now.

Freedom is wonderful.

Backups are even better.

## Developer Documentation

Before aggressively rewriting everything, read these files:

- [AGENTS.md](AGENTS.md) — Development instructions
- [docs/HANDOFF.md](docs/HANDOFF.md) — Development handoff information
- [docs/status-v0.13-ja.md](docs/status-v0.13-ja.md) — Current implementation and verification status
- [docs/plan-v0.13-ja.md](docs/plan-v0.13-ja.md) — Development roadmap
- [docs/build-and-test.md](docs/build-and-test.md) — Build and testing instructions
- [docs/menu-command-map.json](docs/menu-command-map.json) — Menu-to-command mapping

Record unverified features as unverified.

Record broken features as broken.

**THE SOFTWARE MAY BE SLOP. THE DEVELOPMENT RECORDS DON'T HAVE TO BE.**

## License: The Freedom to Modify and Destroy

Slop Studio's original application code is licensed under the [MIT License](LICENSE).

Subject to the license conditions, you are free to use, modify, copy, redistribute, and sell this code.

**MODIFY YOUR OWN COPY AS MUCH AS YOU WANT.**

Rewrite it until it becomes completely unrecognizable.

Build something entirely different.

That's part of the beauty of open-source software.

Separate licensing conditions apply to third-party SDKs, libraries, external plugins, and runtime components.

The ASIO / Ableton Link companion programs run as separate processes and are licensed under GPL-3.0-only.

When redistributing the software, preserve the necessary licensing materials and corresponding source code, including:

- [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)
- `licenses`
- `Companions/Source`

The splash-screen image requires separate rights clearance. Verify redistribution permissions or replace it with an appropriately licensed image before public distribution.

Enjoy the freedom to modify the code.

## Disclaimer: One Final Warning

This software is provided **"AS IS"**.

No warranty is provided regarding completeness, functionality, compatibility, quality, or fitness for a particular purpose. Liability is governed by applicable law and the relevant license conditions.

**BACK UP YOUR MUSIC PROJECTS AND AUDIO FILES.**

The developer may improve the software.

The developer may change the specifications.

The developer may also make an absolute mess of everything.

Bug fixes, feature suggestions, and forks are welcome.

Slop Studio is unfinished software.

It's here for people who can have fun with that.

---

## Final Words

Slop Studio is a digital audio workstation born from enormous ambitions, ridiculous amounts of code, and a deeply unreasonable quantity of AI-generated slop.

It is not perfect software.

Some features don't exist yet.

Some things that should work don't work.

Some things might work for reasons nobody fully understands.

Nevertheless, if you want to make music with this magnificent pile of AI-generated garbage, you're welcome here.

If you want to modify it, you're welcome here.

If you want to fight bugs until 3 AM, you're definitely welcome here.

**BUILD IT. MOD IT. BREAK IT. BUILD IT AGAIN.**

Just don't forget your backups.

**Slop Studio 0.13**

*Made by Tsuikyu.*

*Built with code, questionable decisions, and an unreasonable amount of slop.*

**NO WARRANTY. NO PERFECTION. JUST SLOP.**

# Slop Studio 0.13

### The Digital Audio Slopstation™

**AIスロップから生まれた、音楽制作ソフトウェア。**

**作者：Tsuikyu | Windows 11 x64 | MIT License**

> **警告：これはAIスロップです。しかも未完成品です。**
>
> 使用は完全に自己責任。動作保証なし。品質保証なし。あなたの制作時間も保証しません。
>
> バグがあります。未実装機能があります。動いているように見えて、実は動いていない機能もあるかもしれません。
>
> **改造自由。フォーク自由。魔改造自由。破壊自由。**
>
> 自分のコピーを好きなだけ改造して、ぶっ壊して、作り直してください。原形をとどめなくなっても作者はご納得です。
>
> 重要なプロジェクトのバックアップだけは取ってください。あなたの最高傑作がスロップになる責任までは負えません。

---

## これは何？

Slop Studioは、Windows向けのデジタル・オーディオ・ワークステーション（DAW）です。

作曲、編曲、録音、MIDI編集、ミキシング、音声書き出しを一つのアプリで行うために開発されています。

画面は黒と青紫を基調とした **Minimal CyberSquare** デザイン。

標準プロジェクト形式は `.slopdaw`。

オーディオトラック、MIDIトラック、ピアノロール、ミキサー、プラグイン、オートメーション、サンプラーなど、音楽制作に必要な機能を詰め込んでいます。

現在のバージョンは **0.13**。

何ができるのか？

いろいろできます。

何ができないのか？

それもいろいろあります。

音楽を作るためのソフトですが、ときにはソフト自体を修理することになるかもしれません。

**それがSlop Studioです。**

## 起動方法

配布フォルダーの `SlopStudio.exe` を実行してください。

必要な環境：

- Windows 11 x64
- .NET Framework 4.8
- 未完成ソフトウェアに対する多少の寛容さ

初回起動時にはクイックスタートが表示されます。

起動画面には専用画像と、実際の起動段階に応じて進むプログレスバーがあります。画像を読み込めなければ簡易画面に切り替わります。

起動に成功したら、音楽を作りましょう。

起動に失敗したら、CodexでもClaude Codeでも開いてデバッグしましょう。

## プロジェクト：`.slopdaw`

Slop Studioは独自形式 `.slopdaw` でプロジェクトを保存します。

保存対象には、MIDI、トラック、対応するプラグイン状態、インサート順、オートメーションなどが含まれます。

新規作成、保存、読み込み、復旧に使用する標準形式です。

既存ファイルを上書きする際は別名のバックアップを残します。

音声ファイルは原則として参照方式です。プロジェクトを別のPCに移動する場合は、音声素材も適切に管理してください。

素材を完全に自動収集する機能はまだありません。

昨日まで鳴っていたキックが突然 `[Missing]` にならないように、素材の保存場所は覚えておきましょう。

詳しくは [`.slopdaw` 形式の仕様](docs/slopdaw-format.md) を参照してください。

## 搭載機能：スロップ技術の結晶

**基本的な制作機能**

- オーディオトラック、MIDIトラック
- リージョン編集、ピアノロール
- 再生、録音、Undo / Redo
- Sampler、内蔵エフェクト
- VST3 / CLAP プラグインホスト
- ミキサー、Aux、トラックスタック
- オートメーション、コンピング
- グローバルトラック、コード
- 波形エディターのピッチ編集
- 動画プレビュー

**その他のスロップ技術**

- Slop Launcher：クリップを格子状に並べる機能
- MIDIファイルの入出力
- FLP / ALS / DAWproject の限定的な読み込み
- WASAPI と別プロセスの ASIO 補助プログラム
- Lua Mod
- OSC
- ゲームコントローラー操作
- MCPによるAI連携
- 日本語、英語、ドイツ語、韓国語のUIとマニュアル

実装・動作確認の状況は機能ごとに異なります。未翻訳の項目も残っています。

**メニューに存在しているからといって、完成しているとは限りません。**

現在の状況は [機能・検証台帳](docs/status-v0.13-ja.md) を参照してください。

## ミキサーとエフェクト

内蔵FXと外部プラグインは、順序付きのインサートチェーンで処理します。

トラックとマスターのインサートに対応し、ドラッグによる順序変更もできます。

マスターFXは、ミキサーの「ステレオ出力」またはインスペクターの出力ストリップから追加できます。

Visual Equalizerは5バンドの周波数特性を表示し、グラフ上のポイントをドラッグしてフィルターを編集できます。

SamplerのADSRはつまみと数値で調整できます。Compressorなどの内蔵FXも、パラメーター変更を再生中の処理に反映します。

音声経路やプラグインの組み合わせには、引き続き検証が必要な部分があります。

**音が出たら制作開始。音が出なければデバッグ開始です。**

## 書き出し：スロップを音声ファイルへ

「ファイル → マスター → プロジェクトまたは選択範囲をマスターとして書き出す」

ショートカット：`Ctrl+B`

ここから、マスター処理を含むWAVまたはMP3を書き出せます。

同じメニューでは、マスター処理の一括有効／無効、参照音源の波形表示と試聴もできます。

MP3書き出しには、別途FFmpegとlibmp3lameが必要です。

プロジェクト情報をWAV / MP3のタグに出力する機能もあります。

マスター画面からステムを新しいフォルダーへ書き出せます。

ステム出力ではバスとその子トラックが重複する場合があります。また、マスターの非線形FXによって、ステムの合計が元のミックスと異なる場合があります。

以下は現在未実装です。

- リージョン／トラックを元位置にオーディオ化する機能
- 全トラックの置き換え

これらのボタンは無効表示されています。

押せません。

まだ作っていないからです。

## ちょっとした便利機能

リージョン、トラックヘッダー、ミキサーストリップを右クリックすると、背景色と文字色を変更できます。

サンプルブラウザーでは波形を表示し、Windowsの既定出力で試聴できます。

インスペクターの数値は上下ドラッグやマウスホイールで変更できます。

ASIO選択時には、設定ボタンからドライバーの設定画面を開けます。

Lua Modマネージャーでは複数の `.lua` ファイルを登録し、選択したスクリプトを実行できます。

ゲームコントローラーのボタン割り当ては、環境設定のキーコマンドから変更できます。

**F11で全画面表示を切り替えます。**

大画面にすると、少しだけ完成度が上がったような気分になれます。

実装率は変わりません。

## 他のDAWのプロジェクトを食べさせる

FLP、ALS、DAWprojectなどの限定的な読み込み機能があります。

FLP読み込みでは、MIDIノートとその配置、参照できる音声を取り込みます。

見つからない音声は、無音の `[Missing]` リージョンとして保持します。

該当リージョンを右クリックして「音声ファイルを再リンク…」を選び、WAVファイルを指定すると、同じ参照先を使うリージョンも更新されます。

現在、完全な再現には対応していないもの：

- 元の音源とエフェクト
- オートメーション
- ルーティング
- トリムやタイムストレッチ
- その他の固有データ

読み込めるものを読み込み、読めなかったものは残念ながらスロップの彼方へ。

重要なプロジェクトは元の形式でも保管してください。

## Slop Studio 0.13の追加機能

### マスター音量とMIDI

右上の角形スライダーとステレオ出力フェーダーは、同じマスター音量を制御します。

「ファイル → MIDIを書き出す」で標準MIDIファイルを保存できます。

ピアノロール下部のベロシティ棒はドラッグで調整でき、Undoで戻せます。

### サンプルブラウザー

ブックマークとお気に入りは素材ファイルへの参照を保存します。

検索対象は表示中のフォルダーまたはお気に入りです。

素材の所在は、引き続き人間が管理する必要があります。

### テンプレート

「ファイル → プロジェクトをテンプレートとして保存」

`.sloptemplate` ファイルを作成できます。

テンプレートや同梱サンプルは未保存のコピーとして開きます。新しい `.slopdaw` に保存してください。

音声素材の参照先は維持する必要があります。

### テーマとショートカット

環境設定から以下を変更できます。

- Undo回数
- テーマ：`.sloptheme`
- ショートカット：`.slopkeys`

テーマ変更は再起動後、共通色に反映されます。

一部の独自画面では固定色が使われています。

外見まで改造したい方は、遠慮なくスロップを増築してください。

### Launchpad

Launchpad X / Mini MK3のProgrammerモード向けに、MIDI入出力を明示選択して先頭8トラック×8シーンを操作する機能があります。

**実機での検証は現在進行中です。**

その他の機種も未検証です。

コード上では光る予定です。

現実世界でも光るかは、これからのテストで確かめます。

### 同梱プロジェクト

以下のサンプルがあります。

- **EDM Sample - Melodic House**：128 BPM
- **J-Pop - Shibuya**：150 BPM

サンプルのBPM自動追従とトラックのフリーズ／解除は未実装です。

サンプルのBPMが合わない場合、現在のところ人間の手が必要です。

## プラグインとプリセット

プラグイン、内蔵FX、Samplerの画面上部から、次の操作ができます。

- 設定の保存・読み込み
- コピー・貼り付け
- 画面を開いた時点との比較

プリセット形式は `.sloppreset` です。

コピーした設定はアプリ終了まで保持され、同じ種類のプラグインに貼り付けられます。

外部プラグイン固有のプリセット形式との相互読み込みは現在未実装です。

Samplerの外部サンプルはプリセットに埋め込まれないため、参照先を維持してください。

プリセットを保存しても、失われた素材が蘇るわけではありません。

## AIにスロップを操作させる：MCP

Slop Studioには、stdio MCPを利用するAI連携機能があります。

通常起動時にはMCPは停止しています。

MCPクライアントから利用する場合は、次のように起動します。

```powershell
SlopStudio.exe --mcp
```

最初は読み取り専用です。

編集操作は「ウインドウ → MCP AI連携マネージャー」からセッションごとに許可します。

ファイル操作や録音待機には追加の都度確認があります。

MCPの相互運用性とセキュリティについては、引き続き検証中です。

**AIがDAWを操作する。**

実に現代的です。

しかし、AIがスロップを操作しても、スロップはスロップのままです。

大切なデータはバックアップしておきましょう。

## Lua Mod：魔改造したい人はこちら

Lua Modマネージャーから `.lua` ファイルを登録し、選択したModを実行できます。

Modの作成、実験、機能追加は歓迎します。

他人から入手したスクリプトは内容を確認してから実行してください。

自分で書いたModが暴走した場合、それもまた開発です。

**改造して、壊して、直して、また壊す。**

ようこそSlop Studioへ。

## 現在わかっている問題

**Slop Studio 0.13は未完成です。**

未実装の機能：

- サンプルのBPM自動追従
- トラックのフリーズ／解除
- リージョン／トラックの元位置へのオーディオ化
- 全トラックの置き換え

検証が不足している項目：

- 再生位置と実際に聞こえる音の同期
- Launchpad実機
- 物理MIDIキーボード、マイク、ASIO機器の各種組み合わせ
- ASIOドライバーとの互換性
- VST3 / CLAPプラグインの互換性
- 複数モニター環境でのDPI表示
- Windows以外のビルド
- 外部DAWプロジェクトの読み込み
- MCPの相互運用性とセキュリティ
- その他の未完成機能

**OSCのローカルUDP受信は、テスト環境で失敗した記録があります。**

現在の開発状況は [機能・検証台帳](docs/status-v0.13-ja.md) を参照してください。

不具合報告を歓迎します。

再現手順があればさらに歓迎します。

「動きません」より「このボタンを押すと必ず落ちます」のほうが開発者は喜びます。

**再現可能な惨事は、修正への第一歩です。**

## ビルド：自分だけのスロップを作ろう

Slop Studioの技術構成：

| 項目 | 技術 |
|---|---|
| アプリ本体 | C# / Windows Forms |
| .NET | .NET Framework 4.8 |
| 音声・プラグイン処理 | C++ |
| ビルドシステム | CMake |
| 主な対象OS | Windows 11 x64 |
| ASIO / Ableton Link | 別プロセスのGPL補助プログラム |

**JUCEは使用していません。**

ビルドに必要なもの：

- CMake 3.24以上
- NinjaまたはMSVC
- x64 C/C++ツールチェーン
- .NET Framework 4.8対応C#コンパイラー
- 必要なSDKとライセンスファイル

具体的な手順は [docs/build-and-test.md](docs/build-and-test.md) を参照してください。

既存プロジェクト、音声素材、設定、Git履歴を保護したうえで作業してください。

**ソースコードは好きに改造してください。**

UIを全部作り直しても構いません。

ミキサーを魔改造しても構いません。

新しい機能を100個追加しても構いません。

自分の作業コピーを全削除して一から作り直しても構いません。

何も動かなくなっても、それはあなたのSlop Studioです。

自由にはバックアップという相棒が必要です。

## 開発者向け資料

コードを変更する前に、以下のファイルを確認してください。

- [AGENTS.md](AGENTS.md) — 開発時の指示
- [docs/HANDOFF.md](docs/HANDOFF.md) — 引き継ぎ情報
- [docs/status-v0.13-ja.md](docs/status-v0.13-ja.md) — 現在の機能と検証状況
- [docs/plan-v0.13-ja.md](docs/plan-v0.13-ja.md) — 開発計画
- [docs/build-and-test.md](docs/build-and-test.md) — ビルドとテスト
- [docs/menu-command-map.json](docs/menu-command-map.json) — メニューとコマンドの対応

未検証の機能は未検証として記録します。

動いていないものを完成済みと記録しないでください。

**ソフトウェアはスロップでも、開発記録までスロップにする必要はありません。**

## ライセンス：改造と破壊の自由

Slop Studio本体の独自コードは [MIT License](LICENSE) です。

ライセンス条件に従って、利用、改造、複製、再配布、販売できます。

**自分のコピーを好きなだけ改造してください。**

原形がなくなるまで魔改造しても構いません。

一から作り直しても構いません。

それもまたオープンソースの自由です。

第三者SDK、ライブラリー、外部プラグイン、実行時構成要素には個別のライセンス条件があります。

ASIO / Ableton Linkの補助プログラムは別プロセスで動作し、GPL-3.0-onlyです。

再配布時には以下の必要なライセンス情報とソースを維持してください。

- [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)
- `licenses`
- `Companions/Source`

起動画面の画像には別途権利確認が必要です。公開配布する場合は、利用許諾を確認するか、適切なライセンスの画像に置き換えてください。

コードを改造する自由を、存分に楽しんでください。

## 免責事項：最後の警告

このソフトウェアは現状有姿（AS IS）で提供されます。

完成度、動作、互換性、品質、特定目的への適合性について保証しません。責任の範囲は適用法令とライセンス条件に従います。

**大切な楽曲や音声素材は必ずバックアップしてください。**

作者は、このソフトウェアを改良することも、仕様を変更することも、めちゃくちゃにすることもあります。

不具合修正、機能提案、フォークを歓迎します。

Slop Studioは未完成です。

それを楽しめる人のためのソフトウェアです。

---

## 最後に

Slop Studioは、壮大な野望と大量のコード、そして少なからぬAIスロップによって生まれたDAWです。

完璧なソフトウェアではありません。

まだ実装できていないものもあります。

動くはずなのに動かないものもあります。

なぜ動いているのか、よく分からないものもあるかもしれません。

それでも、このAIのゴミを使って音楽を作りたい人、自分で改造したい人、バグと戦いたい人は歓迎します。

**作れ。改造しろ。壊せ。そして、また作れ。**

バックアップだけは忘れるな。

**Slop Studio 0.13**

*Made by Tsuikyu.*

*Built with code, questionable decisions, and an unreasonable amount of slop.*

**NO WARRANTY. NO PERFECTION. JUST SLOP.**
