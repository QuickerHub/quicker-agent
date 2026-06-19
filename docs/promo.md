# quicker-agent 宣传文案（本地草稿）

> 命名说明（写文案前先对齐）  
> - **QuickerAgent**：`quicker-rpc` 仓库里的**桌面 AI 副驾**（Electron + 聊天 + 动作设计器），面向 Quicker 终端用户。  
> - **qkagent**：本仓库（`quicker-agent`）的 **CLI**，面向动作作者与开发者——维护 getquicker 动作页 HTML、讨论区发帖等。  
> - **qkrpc**：`quicker-rpc` 的 CLI/MCP，无头读写 Quicker 动作步骤，与 qkagent 互补、不重叠。  
>  
> 三者同属 QuickerHub 生态；下文分别给出可单独使用的宣传段落。

---

## 一、QuickerAgent 桌面版（主产品向）

### 一句话

**QuickerAgent — 懂 Quicker 的 AI 副驾：找得到、跑得动、改得快。**

### 短文案（README / 下载页摘要）

QuickerAgent 是专为 [Quicker](https://getquicker.net/) 打造的桌面 AI 助手，不是通用聊天工具。它通过本机 QuickerRpc 插件直接连上你的 Quicker，帮你**搜索并运行动作、逐步调试、在应用内编辑步骤、整理动作页、发布到动作库**。写步骤前会查模块定义与图标库，不靠猜参数。

按 **Ctrl+Shift+Space** 唤起轻量启动器，眼前这一下办完即走；复杂编排交给主窗口慢慢聊。

**下载**：[GitHub Releases](https://github.com/QuickerHub/quicker-rpc/releases/latest/download/quicker-agent-win-x64-setup.exe)  
**前置**：Windows + Quicker + [插件动作](https://getquicker.net/Sharedaction?code=aa5917ad-1256-4c73-7022-08debe3efcbe)

### 长文案（讨论区 / 博客 / 动作页）

#### 标题候选

- QuickerAgent：长在 Quicker 旁边的 AI 副驾，终于不用翻面板找动作了
- 动作多了之后，我需要一个「懂我家底」的助手——QuickerAgent 使用体验
- 找、跑、改、发一条龙：QuickerAgent 想解决的不是「会不会用 Quicker」

#### 正文

如果你用 Quicker 有一段时间，大概都经历过同一种疲惫：动作越积越多，真正费时间的往往不是「Quicker 好不好用」，而是——

- 记不清动作叫什么名字，搜半天；
- 改几个步骤要反复开设计器、查模块参数；
- 有个新想法想马上试，却在找入口和排错上耗掉半小时；
- 动作页说明、版本介绍要上线改一遍，又得登录网页、进源代码模式粘贴 HTML。

**QuickerAgent** 想缩短的，就是「找—跑—改—发」这条链。

它是专为 Quicker 打造的**桌面 AI 副驾**（不是通用聊天机器人）。安装 QuickerAgent 插件动作后，应用通过本机 **QuickerRpc** 与你的 Quicker 进程通信：你可以用自然语言描述用途，让它搜索动作库、直接运行、逐步调试；也可以在内置编辑器里改步骤、补变量、整理动作页，需要时一键发布到 getquicker。

和通用 AI 的差别在于：**它默认懂 Quicker**。写步骤前会查 step-runner 的模块 schema，而不是凭空编造 `inputParams`；日常回复也尽量说人话，不堆术语。

还有两个使用节奏值得分开看：

| 场景 | 怎么做 |
|------|--------|
| 马上运行某个动作、改个标题、打开某页设置 | **Ctrl+Shift+Space** 启动器浮窗，办完即走 |
| 从零搭流程、多轮改步骤、调试、发布 | 主窗口长对话 + 内置动作设计器 |

数据方面：与 Quicker 的交互在本机完成；发给大模型的聊天内容由所选模型服务商处理，可在设置里接入自己的 API。

**适合谁？**

- Quicker 老用户：动作库大了，需要一个懂上下文的检索与执行入口；
- 动作作者：少点鼠标，把精力放在逻辑和体验上；
- 想快速验证自动化想法的人：从草稿到可运行，周期更短。

**如何开始？**

1. 安装 [Quicker](https://getquicker.net/) 并保持运行  
2. 在 Quicker 中安装 [QuickerAgent 插件动作](https://getquicker.net/Sharedaction?code=aa5917ad-1256-4c73-7022-08debe3efcbe)  
3. 下载并安装 [Windows x64 安装包](https://github.com/QuickerHub/quicker-rpc/releases/latest/download/quicker-agent-win-x64-setup.exe)  
4. 在 Quicker 里运行 QuickerAgent 动作，或从开始菜单打开应用  

更完整的介绍见 [quicker-rpc 文档](https://github.com/QuickerHub/quicker-rpc/blob/main/docs/quicker-agent.md)。

*QuickerAgent — 让 Quicker 更好用，让动作更好做。*

### 卖点速查（表格 / 幻灯片）

| 痛点 | QuickerAgent 怎么做 |
|------|---------------------|
| 动作记不清名字 | 按用途描述即可搜索、点选、执行 |
| 改步骤要反复开设计器 | 内置编辑器 + 步骤知识库，参数按 schema 补全 |
| 新想法想马上试 | 直接运行或逐步调试，侧栏看每步输出 |
| 从零搭流程太累 | 创建、编排、整理、发布可在一套对话里完成 |
| 只想顺手改个设置 | 启动器浮窗，不污染长对话 |
| 和 Cursor 等工具协作 | 同生态的 `qkrpc` 提供 MCP/HTTP，第三方 Agent 可无头改动作 |

### 收尾金句（任选）

- 不替代 Quicker，而是把 Quicker 旁边那层效率补齐。
- 通用 AI 会聊天；QuickerAgent 会找动作、跑动作、改动作。
- 复杂编排慢慢聊，眼前这一下 Ctrl+Shift+Space。

---

## 二、qkagent CLI（本仓库工具向）

### 一句话

**qkagent — 把 getquicker 动作页说明当代码维护：本地改 `page.html`，一条命令推上线。**

### 短文案（GitHub Description / 工具介绍）

`qkagent` 是 Quicker 动作作者的命令行助手。用 **Playwright** 登录 getquicker.net，读写分享动作的网页简介 HTML；配合本仓库 `actions/<sharedId>/page.html` + 共享样式，实现**版本管理、本地预览、一键发布**。

还支持 **`qkagent qa post`**，在讨论区自动发帖（标题、分类、正文）。

与 `qkrpc` 分工明确：**qkrpc 改动作步骤，qkagent 改动作页说明与站点文案。**

### 长文案（开发者 / 作者向）

#### 标题候选

- 还在网页编辑器里粘贴动作说明？试试把 intro 放进 git
- qkagent：Quicker 动作页 HTML 的 pull / push / preview 工作流
- 动作作者工具链补全：qkrpc 管步骤，qkagent 管说明页

#### 正文

分享动作时，getquicker 上的「动作说明」往往是用户第一眼看到的东西。但很多作者仍停留在：

1. 浏览器登录 → 编辑信息 → 点「源代码」→ 粘贴一大段 HTML；  
2. 样式各写各的，过几天自己都认不出；  
3. 改个下载链接或版本号，又要重复手工流程；  
4. 想和 AI 协作，却没有稳定的文件入口和 `--json` 输出。

**qkagent** 把这件事当成**工程问题**来解决。

在本仓库里，每个分享动作对应 `actions/<sharedId>/page.html`（源文件，进 git）和构建产物 `info.html`（本地生成，不上传仓库）。共享样式在 `actions/_shared/intro.css`，全站动作说明外观统一。改完执行：

```powershell
qkagent push --code <sharedId> --json
```

工具会自动：构建 HTML → 用持久化浏览器配置登录（Cookie 复用）→ 打开编辑页 → 写入 Summernote 源代码 → 点击「更新动作信息」。

**和手动浏览器相比：**

| 维度 | qkagent | 手动 |
|------|---------|------|
| 源文件 | `page.html` 可 PR、可 diff | 网页里一次性粘贴 |
| 样式 | 共享 CSS 内联构建 | 易散落 inline style |
| 发布 | 一条 `push` | 多步点击 |
| 自动化 | `--json`、结构化错误码、Cursor skills | 难脚本化 |
| 预览 | `preview/run-dev.ps1` 本地 HMR | 只能上线后看 |

首次可从线上拉取：`qkagent pull --code <sharedId>`（仅 bootstrap，之后以 `page.html` 为准）。

**讨论区发帖**（实验能力）：

```powershell
qkagent qa post --title "标题" --category 功能建议 --content-file .\draft.md --json
```

**安装**

```powershell
# 仓库内构建发布
.\publish\publish-agent.ps1
# 配置 .env
QUICKER_EMAIL=...
QUICKER_PASSWORD=...
```

发现命令：`qkagent help --json` · 工作流指南：`qkagent guide get --topic workflow --json`

仓库：[QuickerHub/quicker-agent](https://github.com/QuickerHub/quicker-agent)

### 与 QuickerAgent / qkrpc 的关系（一段话）

QuickerAgent 负责「在 Quicker 里把动作做好」；`qkrpc` 让 Cursor 等 Agent 无头读写 `data.json` 步骤；**qkagent** 负责「把动作在 getquicker 上的脸（说明页、讨论帖）维护好」。QuickerHub 发布 QuickerAgent 安装包时，动作页里的版本占位符也会走 `qkagent push` / `Sync-QuickerAgentActionDoc.ps1` 这条链。

---

## 三、多场景摘录（复制即用）

### 讨论区「信息发布」短帖（QuickerAgent）

QuickerAgent 公测/更新速报：

专为 Quicker 做的桌面 AI 副驾，支持搜索运行动作、内置步骤编辑、逐步调试、发布到动作库。`Ctrl+Shift+Space` 秒开启动器，复杂任务进主窗口。

下载：https://github.com/QuickerHub/quicker-rpc/releases/latest  
插件动作：https://getquicker.net/Sharedaction?code=aa5917ad-1256-4c73-7022-08debe3efcbe  
详细介绍见动作页说明或 GitHub `docs/quicker-agent.md`。

### 讨论区「动作开发」短帖（qkagent）

分享动作的作者可以看下 **qkagent** CLI：

- `actions/<id>/page.html` 写说明，git 管理  
- `qkagent push` 一键上传 getquicker 动作页 HTML  
- 本地 `preview/run-dev.ps1` 预览  
- `qkagent qa post` 讨论区发帖（新）

仓库：https://github.com/QuickerHub/quicker-agent  
和 `qkrpc` 不冲突：qkrpc 改步骤，qkagent 改说明页。

### GitHub Release 备注（QuickerAgent）

```text
QuickerAgent x.y.z

懂 Quicker 的 AI 副驾：找 / 跑 / 改 / 发动作。
需要本机已安装 Quicker 并加载 QuickerAgent 插件动作。

安装包：quicker-agent-win-x64-setup.exe
文档：https://github.com/QuickerHub/quicker-rpc/blob/main/docs/quicker-agent.md
```

### GitHub Release 备注（qkagent）

```text
qkagent x.y.z

CLI：getquicker 动作页 HTML pull/push + QA 发帖。
Windows x64，.NET 8 自包含。

qkagent help --json
qkagent guide get --topic workflow --json
```

### 三行电梯演讲

1. **QuickerAgent**：Quicker 旁边的 AI，会说话也会改你的动作。  
2. **qkrpc**：给 Cursor 和脚本用的 Quicker 遥控 API。  
3. **qkagent**：动作说明页和讨论帖，别手搓浏览器了。

---

## 四、待发布前核对清单

- [ ] 快捷键以 `docs/quicker-agent.md` 为准：**Ctrl+Shift+Space**（quicker-rpc README 个别处仍写 Alt+Space，对外统一口径）  
- [ ] 下载链接是否指向当前 Release  
- [ ] 插件动作 sharedId `aa5917ad-1256-4c73-7022-08debe3efcbe` 是否仍有效  
- [ ] 讨论区分类：QuickerAgent 用「信息发布」或「经验创意」；qkagent 用「动作开发」或「信息发布」  
- [ ] 测试帖、内部链接等敏感内容勿原样发出  

---

*草稿版本：本地 `docs/promo.md`，未同步到 getquicker 动作页或讨论区。*
