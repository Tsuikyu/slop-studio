# Slop Studio 0.10 — 機能・検証台帳

2026-10-29、Windows 11 x64。PASS は記載した試験範囲での結果です。仕様書の全項目が完成したという意味ではありません。

| 項目 | 状態 | 検証・残課題 |
|---|---|---|
| Windows x64 Release | PASS | CMake / Ninja / Clang 23.1.4 で独立した配布フォルダーをビルドし、実行ファイルから First Light を開いて正常終了。 |
| 起動画面 | PASS | 提供画像を実行時に読み込み、起動段階に応じてバーと文言を更新。画面を実描画して変化を確認。画像の公開ライセンスは未確認。 |
| .slopdaw | PASS | 新規・編集・保存・再読込、スキーマと未知バージョンの拒否、原子的置換とバックアップをテスト。素材の完全収集・相対化は未実装。 |
| Minimal CyberSquare | PARTIAL | 暗い画面、紫の強調色、四角いコントロール、トラック・ミキサーを実画面で確認。全ダイアログと全 DPI・画面サイズの仕上げは未完了。 |
| マスター FX | PASS | 実 CLAP テストプラグインと内蔵 FX の並べ替え、処理順による PCM 差、バイパス、保存・再読込を検証。全製品互換は未検証。 |
| 再生・録音・MIDI | PARTIAL | WASAPI 仮想ループバックの PCM 録音と合成 MIDI イベント、再生・停止、保存を GUI スモークで確認。物理マイク・鍵盤の演奏や音の聴取は未実施。 |
| ASIO | PARTIAL | FL Studio ASIO で16秒の連続再生、パン変更・シーク、44.1 / 48 kHz 切替と自プロセス障害後の再接続が成功。全ドライバー、MiniFuse 物理接続、聴取は未検証。 |
| VST3 / CLAP | PARTIAL | 公式仕様のフィクスチャープラグインで発見・生成・パラメーター・処理・保存状態を検証。全ベンダー互換、全機能、実音源製品は今回未検証。 |
| OSC | FAIL | ローカル UDP 往復テストの受信が失敗。同じ環境で単純な UDP 自己送受信も失敗し、原因は未確定。実 VRChat 往復は未実施。 |
| MCP | PARTIAL | 標準入出力の起動と11ツール、初期読み取り専用による編集・保存・再生拒否、許可後の編集と Undo を検証。永続監査、細分化権限、正式な相互運用性・安全性は未完了。 |
| メニューカタログ | PARTIAL | 指定された424項目を識別子付きで記録し、54項目を最も近い既存コマンド / ハンドラーへ対応付けた。完全な意味・表示・翻訳の一致を主張しない。 |
| 4言語 | PARTIAL | ja-JP / en-US / de-DE / ko-KR の各17項目の GUI スモークが成功。全文翻訳・全画面の言語品質は未確認。 |
| テスト | PARTIAL | CTest 7スイート中6成功。StudioIntegrationTests は19項目成功、OSC 1項目失敗。GUI 12項目、MCP 実プロセステストは成功。 |
| 公開・ライセンス | PARTIAL | 本体 MIT と第三者通知、独立した GPL 補助プログラムのソースを収録。提供された起動画面の著作権者と公開許諾は未確認。GitHub への公開や法的保証は行っていない。 |
| Debian | NOT RUN | 現在のスタンドアロン構成は Windows 専用。 |

## 今回のビルド

```powershell
$root = (Get-Location).Path.Replace('\','/')
$cmake = "$root/work/tools/cmake/cmake-4.4.4-windows-x86_64/bin/cmake.exe"
$source = "$root/outputs/slop-studio-source"
$build = "$root/work/slop-studio/release-build"
$dist = "$root/outputs/dist/slop-studio-windows-x64-release"
$ninja = "$root/work/tools/ninja/ninja.exe"
$llvm = "$root/work/tools/llvm-mingw/llvm-mingw-20261020-ucrt-x86_64"
& $cmake -S $source -B $build -G Ninja -DCMAKE_BUILD_TYPE=Release "-DCMAKE_MAKE_PROGRAM=$ninja" "-DCMAKE_TOOLCHAIN_FILE=$source/cmake/llvm-mingw.cmake" "-DLLVM_MINGW_ROOT=$llvm" "-DSLOPX_PACKAGE_DIR=$dist" -DBUILD_TESTING=OFF
& $cmake --build $build --config Release --parallel 3
```

テスト用ビルドは `work/slop-studio/build`、出力は `outputs/dist/slop-studio-windows-x64`。`ctest --test-dir work/slop-studio/build -C Release --output-on-failure` を実行した。GUI と ASIO は専用のテストプロファイル・テストプロジェクトを使用した。

## 次の優先事項

OSC 受信環境の切り分け、実機 MIDI / オーディオ / ASIO の検証、メニュー・翻訳の欠落修正、素材収集と形式仕様の深化、プラグイン・MCP の権限と安全性、画面全域の調整を継続する。商用 DAW との完全互換やプロ用途の信頼性はまだ達成していない。
