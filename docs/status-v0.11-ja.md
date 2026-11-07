# Slop Studio 0.11 — 変更と検証

2026-10-29、Windows 11 x86-64。以下の PASS は記載した試験条件に限ります。インストールされた製品のすべてのバージョンや全デバイスでの動作を保証するものではありません。

| 項目 | 状態 | 対応・結果 |
|---|---|---|
| 暗いメニューの文字 | PASS | 有効項目を明るい文字、無効項目を区別できる灰色で描画。日本語の実メニュー画像とピクセルを GUI テストで検証。 |
| メニューの項目数 | PARTIAL | 設定の7カテゴリ、既存の編集・トラック・ミックス・表示コマンドをメニューに追加。指定された全項目と他社 DAW の全機能は未実装。 |
| Ozone 11 Elements / SerumFX | PASS | この PC にある実 VST3 で発見、挿入、純正エディター表示、プリセット保存・復元、合成 PCM 処理とプロジェクト復元を検証。スピーカーでの聴取は未実施。 |
| Serum 2 | PASS | この PC にある実 VST3 音源で挿入、エディター表示、プリセット、合成 MIDI による発音とプロジェクト復元を検証。物理鍵盤は未使用。 |
| 外部プラグイン全般 | PARTIAL | VST3 初期化の戻り値、コントローラーの接続順、状態サイズの変化、処理中のエディター表示の停止を修正し、それぞれフィクスチャーで検証。全製品・全バージョンは未検証。 |
| Compressor | PASS | つまみ操作が現在の DSP インスタンスに反映され、合成 PCM 出力が変化すること、Undo / Redo と画面更新を検証。音響的な主観評価は未実施。 |
| プリセットバー | PARTIAL | 内蔵 FX、Sampler、VST3 / CLAP に `.sloppreset` の保存・読み込み、同一種別へのセッション内コピー・貼り付け、画面を開いた時点との比較を実装。外部プラグインのネイティブ形式は非対応。Sampler の素材は参照であり埋め込まれない。 |
| Sampler ADSR | PASS | 4 つのつまみと数値入力で設定を変更し、プリセットへ保存・復元する GUI テストが成功。物理入力からの録音は今回未検証。 |
| Visual Equalizer | PARTIAL | 実際の 5 バンドフィルター特性をオリジナルの暗色グラフに表示し、ポイントをドラッグして周波数とゲインを変更する GUI テストが成功。リアルタイムスペクトラム表示や参照画像と同一のバンド構成は未実装。 |
| 過去機能の回帰 | PARTIAL | 既存 V9 GUI テスト12項目、4言語の各17項目のアプリ自己診断が成功。既存の全機能・物理機器は網羅していない。 |
| 自動テスト | PARTIAL | CTest 8 スイート中7成功。StudioIntegrationTests は20項目中19成功、OSC のローカル UDP 受信1項目が失敗。V11 コア8項目と GUI6項目が成功。 |
| 物理デバイス / ASIO | NOT RUN | 本リリースでは物理スピーカー、マイク、MIDI 鍵盤、ASIO 機器による演奏・聴取を行っていない。4言語自己診断で WASAPI 仮想ループバック録音は成功。 |
| Debian / 全 DPI | NOT RUN | Windows 以外、およびモニターごとの DPI 切り替えは検証していない。 |
| 公開・ライセンス | PARTIAL | 本体の MIT、第三者通知、別プロセスの GPL 補助プログラムと対応ソースを同梱。提供された起動画面の第三者公開許諾は未確認。 |

## 原因と修正

- システムのメニューレンダラーが暗い背景に暗い文字を描いていたため、メニュー項目、無効状態、矢印の配色を独自レンダラーに統一した。
- 一部 VST3 の合法な `setProcessing` の未実装応答を失敗と判定していた。明示的な拒否は引き続きエラーにする。MIDI 対応の問い合わせ順もコントローラー接続後へ移した。公式仕様: <https://steinbergmedia.github.io/vst3_dev_portal/pages/Technical%2BDocumentation/API%2BDocumentation/Index.html>。
- 音声処理とプラグイン画面の作成が同じロックを占有し、互いを待つ場合があった。処理中の編集画面表示を許し、状態復元・破棄とは排他する。障害隔離や全ベンダー互換の保証ではない。
- 内蔵 FX の編集時に処理グラフを作り直していた。パラメーターだけを現行プロセッサーに反映し、状態を保つよう変更した。
- 参考画像の構造を分析して操作を設計した。Apple の画像やアイコンは組み込んでいない。プラグイン設定の操作参考: <https://support.apple.com/en-am/guide/logicpro/lgcp4dcb0092/12.3/mac/15.6>。

## ローカル Release ビルド

```powershell
$root = (Get-Location).Path.Replace('\','/')
$cmake = "$root/work/tools/cmake/cmake-4.4.4-windows-x86_64/bin/cmake.exe"
$source = "$root/outputs/slop-studio-0.11-source"
$build = "$root/work/slop-studio-011/release-build"
$dist = "$root/outputs/dist/slop-studio-0.11-windows-x64"
$ninja = "$root/work/tools/ninja/ninja.exe"
$llvm = "$root/work/tools/llvm-mingw/llvm-mingw-20261020-ucrt-x86_64"
& $cmake -S $source -B $build -G Ninja -DCMAKE_BUILD_TYPE=Release "-DCMAKE_MAKE_PROGRAM=$ninja" "-DCMAKE_TOOLCHAIN_FILE=$source/cmake/llvm-mingw.cmake" "-DLLVM_MINGW_ROOT=$llvm" "-DSLOPX_PACKAGE_DIR=$dist" -DBUILD_TESTING=OFF
& $cmake --build $build --config Release --parallel 3
```

テスト用ビルドは同じ指定で別フォルダーに `-DBUILD_TESTING=ON` を使う。一般化した依存関係と手順は `build-and-test.md` を参照。
