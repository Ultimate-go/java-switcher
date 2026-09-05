# Java 环境监测与切换器

> **一键扫描并切换本机 Java 环境的 Windows 小工具**
> Windows GUI tool to scan and switch all local Java environments with one click.
>
> 单文件 EXE · C# WinForms · **无需本机安装 Java** 即可运行
> Single-file EXE · C# WinForms · runs **without** a local Java runtime.

一款 Windows 图形化小工具：自动扫描电脑上安装的所有 JDK/JRE，用「版本卡片」展示，
点一下就把系统 / 用户的 `JAVA_HOME` 和 `Path` 切换过去，**新开的终端 / IDE 立即生效**。
整个程序是**一个独立的 exe**——即使电脑上一个 Java 都没装，也能打开它来扫描、切换或修复你的 Java 环境。

---

## 为什么做成单文件 EXE？

- 用 Java 写的工具，启动本身就依赖 JVM——如果电脑没装 Java，工具根本跑不起来，也就起不到「切换 Java」的作用。
- 本工具改用 **C# WinForms（.NET Framework 4.x，Windows 10/11 系统自带）** 编译成单文件 `JavaSwitcher.exe`，
  **不依赖任何 Java 运行时**，可脱离环境直接运行、一键切换。

## 功能特性

- **自动扫描**本机全部 Java 环境：注册表 `JavaSoft`、系统/用户 `PATH`、常见安装目录递归（`Program Files`、`Eclipse Adoptium`、`.jdks` 等）
  - JDK 优先，按版本从高到低排序
  - 显示版本 / 厂商 / JDK 或 JRE / 安装路径 / 来源
  - 自动标记当前生效版本（★ 当前使用）
- **实时监测**：顶部面板展示系统级 / 用户级 / 生效的 `JAVA_HOME`、`java.exe` 实际位置、当前 java 版本
- **一键切换**：点击版本卡片
  - 写入 `JAVA_HOME`（用户级；若检测到 Java 配置在系统级则同步系统级）
  - 重建 `Path`：把 `%JAVA_HOME%\bin` 置顶，清理旧 JDK/JRE bin、Oracle `javapath` / `java8path`、失效残留条目
  - 广播系统消息（`WM_SETTINGCHANGE`），新开的终端 / IDE 立即生效
- **权限自适应**：Java 配置在系统级时自动请求管理员授权（UAC）；也可「以管理员身份重启」整程序，免除每次授权

## 使用方法

1. 双击 `JavaSwitcher.exe`（Windows 10/11 直接运行，无需安装任何东西）
2. 程序自动扫描，中间区域列出所有 Java 版本卡片
3. 想用哪个版本就点哪张卡片 → 确认 → （如需）允许 UAC 授权
4. **新开**一个终端验证：

   ```powershell
   java -version
   ```

   顶部「当前 java 版本」也会同步刷新

> 说明：切换只影响**之后新开**的终端 / IDE 进程，正在运行的程序不受影响。

## 界面示意

```
┌─ 当前状态（读取自注册表）────────────────────────────┐
│ JAVA_HOME(系统) / JAVA_HOME(用户) / 当前生效值       │
│ java.exe 实际位置 / 当前 java 版本                    │
│ [重新扫描]  [刷新状态]  [以管理员身份重启]  权限提示   │
├─ 已扫描到的 Java 环境（点击任意版本卡片即可切换）───────┤
│ ┌─────────────────────────────────────────────────┐ │
│ │ JDK 17.0.18 / Eclipse Adoptium / JDK       ★ 当前使用 │
│ │ E:\...\jdk-17.0.18.8-hotspot        来源: PATH  │ │
│ └─────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────┐ │
│ │ JDK 1.8.0_201 / JDK                             │ │
│ │ D:\java21                           来源: 注册表 │ │
│ └─────────────────────────────────────────────────┘ │
├─ 日志 ───────────────────────────────────────────────┤
└──────────────────────────────────────────────────────┘
```

## 工作原理

| 环节 | 做法 |
| --- | --- |
| 扫描 | 读注册表 `JavaSoft` 下的 `JavaHome`；解析系统 / 用户 `Path`；递归常见目录并校验存在 `bin\java.exe` |
| 识别 | 解析各 JDK 的 `release` 文件（`JAVA_VERSION` / `IMPLEMENTOR`），失败时回退运行 `java -version` |
| 切换 | 写 `JAVA_HOME`（用户级必写；系统级含旧 Java 配置且已提权时同步写入系统级） |
| 清理 | 重建 `Path`：移除旧 JDK/JRE `bin`、Oracle `javapath` / `java8path`、`%JAVA_HOME%\bin` 旧引用及失效残留目录，并将 `%JAVA_HOME%\bin` 置顶 |
| 生效 | `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, "Environment")` 通知系统刷新环境变量 |

## 编译构建（无需 Visual Studio）

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

使用系统自带编译器 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，产物输出到项目根目录 `JavaSwitcher.exe`。

## 目录结构

```
java/
├─ JavaSwitcher.exe         # 最终产物：单文件，双击即用
├─ build.ps1                # 一键编译脚本
├─ README.md
└─ src/                     # 全部 C# 源码
   ├─ Program.cs            # 入口；含 --switch 提权子进程模式
   ├─ MainForm.cs           # 图形界面 + 版本卡片 + 布局
   ├─ JavaScanner.cs        # 扫描本机 Java 环境
   ├─ EnvironmentService.cs # 读写 JAVA_HOME / Path、广播刷新
   ├─ JavaInstallation.cs   # Java 安装信息模型
   └─ app.manifest          # DPI / UAC 声明
```

## 运行要求

- Windows 10 / 11（自带 .NET Framework 4.8）
- 无需安装任何 Java 或额外运行时
- 若要切换「系统级」Java 配置，需要管理员授权（程序会自动请求）

## 常见问题（FAQ）

**Q：点了切换，新终端里 `java -version` 没变？**
A：请确认是**新开**的终端（旧窗口的环境变量不会自动刷新）；若你的 Java 配置在系统级，还需确认已通过 UAC 授权管理员。

**Q：为什么每次切换都弹 UAC？**
A：说明你的 Java 配置在**系统级**。可在程序里点「以管理员身份重启」，整程序以管理员运行后，切换不再逐个弹窗。

**Q：某个 JDK 没被扫到？**
A：点「重新扫描」。若它装在非标准目录，可先把该 JDK 的 `bin` 目录加入 `PATH` 再扫描。


