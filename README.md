# SNAKE · 终端贪吃蛇

一个用 **C# / .NET** 编写的终端贪吃蛇小游戏。无第三方 NuGet 依赖，包含三档难度、实时加速、最高分持久化、暂停/重开界面，以及为终端优化的全屏渲染。

![SNAKE 菜单预览](preview.png)

## 运行

需要 [.NET SDK 10.0](https://dotnet.microsoft.com/download) 或更高版本，以及一个支持 UTF-8 / 24 位色的交互式终端。

```bash
cd /home/yukiko/Project/Lab/snake
dotnet run
```

终端至少需要 **76 × 22** 个字符，推荐使用 80 × 24 或更大的窗口。窗口过小时游戏会自动冻结并提示调整大小。

### 启动参数

```bash
dotnet run -- --no-color     # 关闭颜色（布局与操作不受影响）
dotnet run -- --self-test    # 运行内置逻辑 / 布局自检
dotnet run -- --help
```

注意参数前的 `--` 是 `dotnet run` 转发参数所必需的。

## 操作

| 场景 | 按键 | 作用 |
| --- | --- | --- |
| 主菜单 | `←` / `→` 或 `A` / `D` | 切换难度 |
| 主菜单 | `1` / `2` / `3` | 直接选择 CHILL / CLASSIC / TURBO |
| 主菜单 | `Space` 或 `Enter` | 开始游戏 |
| 游戏中 | `WASD` / 方向键 | 转向 |
| 游戏中 | `P` 或 `Esc` | 暂停 |
| 游戏中 | `Ctrl+C` | 退出 |
| 暂停 | `Space` / `P` / `Esc` | 继续 |
| 暂停 | `R` / `M` / `Q` | 重新开始 / 回主菜单 / 退出 |
| 结束画面 | `Space` / `Enter` / `R` | 再来一局 |
| 结束画面 | `M` / `Q` / `Esc` | 回主菜单 / 退出 |

## 规则

- 吃到一颗食物得 **10 分**，蛇身增长 **1 节**。
- 每吃 **4 颗食物**提升一级，移动速度加快，最高速度有上限。
- 撞墙或撞到自己的身体都会结束游戏；尾巴正在移开的格子可以安全进入。
- 三档难度：

  | 难度 | 初始速度 | 特点 |
  | --- | --- | --- |
  | CHILL | 190 ms / 帧 | 慢节奏，适合热身 |
  | CLASSIC | 145 ms / 帧 | 经典手感 |
  | TURBO | 100 ms / 帧 | 起步就很快，上限也更高 |

- 每个难度的最高分独立保存。存档位置：
  - Linux：`${XDG_DATA_HOME:-~/.local/share}/snake-terminal/highscores.txt`
  - Windows：`%LOCALAPPDATA%\snake-terminal\highscores.txt`

  删除该文件即可重置最高分。游戏退出或中途重开时，当前分数也会被记录。

## 自检

```bash
dotnet run -- --self-test
```

自检覆盖蛇的移动、转向队列、撞墙/撞身判定、进食计分、等级加速、最高分读写，以及菜单与所有覆盖层的画布尺寸。当前 9 项全部通过。

## 项目结构

| 文件 | 职责 |
| --- | --- |
| `Program.cs` | 入口、参数解析、终端环境检测 |
| `GameApp.cs` | 主循环、输入分发、暂停 / 结束状态、分数提交 |
| `SnakeGame.cs` | 与渲染无关的游戏逻辑（移动、碰撞、食物、等级） |
| `GameTypes.cs` | 方向、坐标、难度配置 |
| `HighScoreStore.cs` | 分难度最高分持久化 |
| `Ui/GameRenderer.cs` | 菜单、棋盘、HUD、暂停与结束面板的布局 |
| `Ui/Canvas.cs` | 无依赖的字符画布与 ANSI 真彩色渲染 |
| `Ui/TerminalSession.cs` | 备用屏幕、居中输出、窗口尺寸处理与终端恢复 |
| `SelfTest.cs` | 内置无依赖测试 |

实现上没有使用 `System.Console` 的清屏刷新循环，而是把每一帧合成为字符串后一次性写出，并进入终端备用屏幕，退出时会恢复原来的终端内容和光标。游戏逻辑与 UI 完全分离，因此修改配色、棋盘布局或增加玩法时不需要动核心判定。

## 构建与发布

```bash
dotnet build
dotnet publish -c Release -o publish
./publish/SnakeGame
```

在 Windows 上发布后对应运行 `publish\SnakeGame.exe`。
