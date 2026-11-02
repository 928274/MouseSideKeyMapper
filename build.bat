@echo off
chcp 65001 >nul
setlocal

:: .NET4.5 csc 路径，主流win10/win11默认位置
set CSC_PATH=%windir%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set OUT_EXE=鼠标侧键映射.exe
set SRC=Form1.cs
set MANIFEST=app.manifest
set ICO=trayicon.ico

if not exist "%CSC_PATH%" (
    echo ❌ 找不到 csc.exe
    pause
    exit /b 1
)

if not exist "%ICO%" (
    echo ⚠️ 警告：同目录未找到 %ICO%，将不设置exe图标
)

echo 🧱 开始编译 ...
"%CSC_PATH%" /win32manifest:"%MANIFEST%" /win32icon:"%ICO%" /target:winexe /reference:"%windir%\Microsoft.NET\Framework64\v4.0.30319\System.dll" /reference:"%windir%\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll" /reference:"%windir%\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll" /reference:"%windir%\Microsoft.NET\Framework64\v4.0.30319\System.Management.dll" /reference:"%windir%\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.Serialization.dll" /out:"%OUT_EXE%" "%SRC%"

if %errorlevel% equ 0 (
    echo ✅ 编译成功
) else (
    echo ❌ 编译失败
)
pause
