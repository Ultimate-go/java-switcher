# Java 环境监测与切换器（单文件 exe）
一键扫描并切换本机 Java 环境的 Windows 小工具 | Windows GUI tool to scan and switch all local Java environments with one click. 单文件 EXE · C# WinForms · 无需依赖本机 Java | Single-file EXE · C# WinForms · runs without a local Java runtime.


纯 **C# WinForms**（.NET Framework 4.x，Win10/11 自带）实现的单文件可执行程序，
**不依赖本机是否安装 Java**，可直接扫描并切换本机所有 Java 环境。

## 目录
- `JavaSwitcher.exe` —— 最终产物，单文件，双击即用
- `src\` —— 全部 C# 源码
- `build.ps1` —— 一键编译脚本

## 功能
1. 扫描本机所有 Java 环境（注册表 JavaSoft / PATH / 常见目录递归）
   - 显示 JDK 优先、按版本从高到低排序，附版本 / 厂商 / 是否 JDK / 安装路径 / 来源
   - 自动标记“★ 当前使用”的版本
2. 顶部面板实时监测当前状态：系统/用户/生效的 `JAVA_HOME`、`java.exe` 实际位置、当前 java 版本
3. 点击任意版本卡片即切换：
   - 写入 `JAVA_HOME`（用户级，若系统级含旧 Java 配置则自动请求管理员同步系统级）
   - 重建 `Path`：把 `%JAVA_HOME%\bin` 放到最前，移除旧的 JDK/JRE bin、Oracle javapath 等条目
   - 广播系统消息，**新开的终端 / IDE** 立即生效
4. 自动判断是否需要管理员权限，按需弹 UAC；也可“以管理员身份重启”整程序免去每次授权

## 编译（无需安装 Visual Studio）
```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```
使用系统自带 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` 生成 `JavaSwitcher.exe`。

## 运行要求
- Windows 10 / 11（自带 .NET Framework 4.8）
- 无需安装任何 Java 或运行时

