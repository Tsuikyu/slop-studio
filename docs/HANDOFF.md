# Slop Studio 開発引き継ぎ

調査日: 2026-11-05。最新と判断したソースは `outputs/slop-studio-0.13-source/`（以下、プロジェクトルート）。このワークスペースには古い `source/`、0.11、0.12 と成果物も残るため、作業対象を取り違えないこと。Git 管理状態は未確認。配布版は `outputs/dist/slop-studio-0.13-windows-x64/`、ソースZIPは `outputs/slop-studio-0.13-source.zip`。この文書はZIPには自動反映されない。

## 目的と規則

Slop Studio は Windows 11 x64 を優先する開発中の独立DAW。Logicに慣れた利用者の操作感を参考にしつつ、Minimal CyberSquareの独自UIを採用する。正式なApple製品を装わず、Appleの画像・アイコン・音源を流用しない。ネイティブプロジェクト形式は `.slopdaw`、テンプレートは `.sloptemplate`。旧形式 `.slopx` を復活させないというユーザー指示がある。最新の明示指示が古い仕様より優先する。

まずルートの `AGENTS.md`、`docs/requirements/` の仕様とメニュー、`docs/status-v0.13-ja.md`、`docs/plan-v0.13-ja.md` を読む。4言語 ja-JP/en-US/de-DE/ko-KR、実機と合成テストの区別、無効な機能を完成と偽らないこと、原子的保存・バックアップ・Undo、既存データの保護を維持。JUCEは使わない。Windows優先、Debian対応は未完成。ネットワーク・ファイルI/O・GUI・スクリプトを音声コールバックに持ち込まない。ユーザーは Downloads の参照を禁止している。外部投稿、公開、送信、pushは無許可で行わない。

## 技術と構造

- `CMakeLists.txt`、`cmake/`: Windows x64、CMake、C++17、Ninja/LLVM-MinGWまたはMSVC。マネージド本体は .NET Framework 4.8 / C# WinForms。現行CMakeはWindows以外を拒否。
- `src/`: UI・プロジェクト・エンジン。`Program.cs`/`MainForm.cs` が起動とコマンド、`ArrangementEngine.cs`/`TransportHistory.cs` が音声と再生位置、`ProjectModel.cs`/`ProjectTemplates.cs` が保存保護、`KeyboardShortcuts.cs`/`ShortcutProfiles.cs` がキー登録、`PianoRollEditor.cs`、`SampleBrowser.cs`、`Launchpad.cs`、`AudioExport.cs` が主要機能。
- `native/`: WASAPI、プラグインホスト、Lua等のネイティブ橋渡し。`companions/`: ASIO / Ableton Link の別プロセスGPLコンポーネント。公式VST3 SDKとCLAP、ARA、Lua等は `third_party/`、通知は `licenses/` と `THIRD-PARTY-NOTICES.md`。MIT本体と各依存物の条件を混同しない。
- `locales/` と `docs/manual/`: 4言語リソースとHTMLマニュアル。`resources/`: 起動画面等。提供画像の公開再配布権は未確認。`examples/`: 128 BPM Melodic House、150 BPM J-Popなど。`tests/`: 合成信号、GUI、プラグインfixture、各版の回帰テスト。
- ワークスペースの `work/tools/` に前回使用したCMake、Ninja、LLVM-MinGWがあるが、別環境での存在は保証しない。`work/` は検証成果物でありソースそのものではない。

## 実装と限界（0.13の既存記録に基づく）

再生・録音、MIDI、アレンジ、ピアノロール、サンプルブラウザー、Sampler/内蔵FX、VST3/CLAP、インサート・ミキサー、スタック、オートメーション、Launcher、テンプレート、Undo上限、SMF書き出し、WAV/MP3タグ、ショートカット・テーマプロファイル、F11全画面等がある。対応範囲は個別に異なり、完成度は `status-v0.13-ja.md` を参照。MP3等には外部FFmpegが必要。音声素材は原則パス参照。

再生位置はオーディオ出力フレーム履歴とLauncher独立時計へ変更済みだが、実際の聞こえる音との同期は未検証。ASIO全機種の修正も未証明。Launchpad X/Mini MK3は8×8のProgrammerモード実装と合成マッピング検証のみで実機未検証。サンプルBPMの確認・自動追従、トラックフリーズ、他社DAWの公式キープリセットは未実装。ステムはバスの重複とマスター非線形処理による合算差がある。テーマは一部の旧画面に固定色が残る。FLP/ALS等のインポート、全プラグイン・全DPI・Debianの互換は限定的または未確認。

0.13の前回記録ではCTest 10スイート中9成功、OSCのUDP受信が失敗。素のローカルUDPもタイムアウトしたが、原因は確定していない。FL Studio ASIOの48kHz無音コールバックとWASAPI仮想出力は確認済みとの記録で、物理聴取や現在の環境の確認を意味しない。サンプル曲の4秒測定は全曲の音質評価ではない。

## 優先順位と最初の作業

1. P0: この文書、既存 `AGENTS.md` と最新台帳を読み、実際のファイルとツール、作業ツリーを確認。既存の作品や設定を変更しない隔離プロファイルで現状を再現する。
2. P0: 再生ヘッドと音のずれを、WASAPI/ASIOのデバイス位置、バッファ、PDC、Launcherのループ・シークを含め検証。安全に実測できる環境と許可がなければ物理同期を未確認のまま記録。
3. P1: サンプルBPM推定の確認ダイアログ、素材単位の永続設定、再編集、原音を守るピッチ維持タイムストレッチとテンポ変更追従を設計・実装・検証。
4. P1: フリーズ／解除について元のMIDI、プラグイン状態、ルーティング、Undo、保存後の復元を維持する設計を先に立て、実音声で検証。
5. P2: OSCのUDP失敗を再現・環境切り分け。ステム、テーマ、ピアノロール、Launchpadとショートカットの不足を順に改善。実機がないものは未検証と明示。

## ビルド・検証

`docs/build-and-test.md` にWindows用の再現可能なCMakeコマンドがある。`outputs/slop-studio-0.13-build-verification-ja.md` は前回の正確な環境とコマンド、ZIPのハッシュを記録。Releaseは `-DBUILD_TESTING=OFF`、検証は別ビルド・別出力で `-DBUILD_TESTING=ON` とし、`ctest --test-dir <test-build> -C Release --output-on-failure`。`V13CoreTests.exe <isolated-dir>`、`V13GuiTests.exe <isolated-dir>`、`V12GuiTests.exe`、`V9GuiTests.exe`、`SlopStudio.exe --smoke <report> ja-JP` が主要な再現手段。GUI・音声試験は他の制作アプリやデバイスへの干渉に注意し、物理録音・出力を無断で行わない。`SLOPX_PROFILE` を作業領域内の専用ディレクトリーに指定する。FFmpegや実機・商用プラグインは別途必要で、診断用プラグインは通常配布版に同梱されない。

この引き継ぎ作成時にはビルドやテストを再実行していない。上記の成功・失敗は2026-10-30の既存記録であり、現在も同じ結果かは未確認。ソース・設定・既存データを削除、初期化、上書きせず、実施結果は新しい記録として追加する。
