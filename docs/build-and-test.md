# Slop Studio — Windows x64 ビルドと検証

Windows 11 x64、.NET Framework 4.8 の `Framework64` C# コンパイラー、CMake 3.24 以上、Ninja と LLVM-MinGW UCRT x64（または Visual Studio の x64 C++ ツールチェーン）が必要です。JUCE は使用しません。固定した SDK のリビジョンは `sdk-revisions.txt` を参照してください。

## 配布用 Release ビルド

PowerShell でソースのルートを開き、環境に合わせてツールのパスを変更します。

```powershell
$cmake = 'C:/Tools/cmake/bin/cmake.exe'
& $cmake -S . -B build/release -G Ninja `
  -DCMAKE_BUILD_TYPE=Release `
  -DCMAKE_MAKE_PROGRAM=C:/Tools/ninja/ninja.exe `
  -DCMAKE_TOOLCHAIN_FILE=cmake/llvm-mingw.cmake `
  -DLLVM_MINGW_ROOT=C:/Tools/llvm-mingw `
  -DSLOPX_PACKAGE_DIR=dist/windows-x64 `
  -DBUILD_TESTING=OFF
& $cmake --build build/release --config Release --parallel 3
```

出力の `SlopStudio.exe`、DLL、`Resources/Startup.png`、`locales`、`docs`、`Examples`、`licenses`、`Companions` と通知を一緒に配布します。OS に付属する .NET と UCRT は同梱しません。FFmpeg は任意の別途依存です。

## テスト用ビルド

別のビルド・出力フォルダーを指定し `-DBUILD_TESTING=ON` にしてから、対応する `ctest.exe --test-dir build/test -C Release --output-on-failure` を実行します。GUI とフィクスチャーテストを実行する際は隔離したテスト用プロファイルを使用し、ユーザーの制作中プロジェクトは使わないでください。

```powershell
dist/test/V9GuiTests.exe verification/gui
dist/test/V11GuiTests.exe verification/gui-v11
dist/test/V11CoreTests.exe verification/core-v11
dist/test/V12CoreTests.exe verification/core-v12
dist/test/V12GuiTests.exe verification/gui-v12
dist/test/V13CoreTests.exe verification/core-v13
dist/test/V13GuiTests.exe verification/gui-v13
dist/test/SlopStudio.exe --smoke verification/app-ja.txt ja-JP
python tests/McpProcessTests.py dist/test/SlopStudio.exe verification/mcp
```

GUI スモークの言語は `ja-JP`, `en-US`, `de-DE`, `ko-KR` を指定できます。仮想ループバックや合成 MIDI は物理デバイスの演奏・録音を証明しません。自動テストの結果と手動未確認項目を分けて記録してください。

今回の結果は `status-v0.13-ja.md` に記録しています。`build.ps1` も用意していますが `-SkipTests` はテスト実行を省く指定であり、テストの成功を意味しません。
