# action-feedback — 动作讨论区反馈 → GitHub Issue

按 **action-feedback-pipeline** skill 执行完整 triage。子 skill：**action-topics-triage**、**cea-action-issues**、**quicker-agent-exe**。

## User context

使用用户输入的 sharedId、Topics URL、或动作名（如 OCR 工作台）。未给出时默认 OCR 工作台：

`2d523add-5d30-4778-7740-08decd685f7e`

## Bootstrap

```powershell
Get-Command qkagent, gh -ErrorAction SilentlyContinue
qkagent help --json
gh auth status
```

`qkagent` 缺失时：`cd tools/qkagent; pwsh -NoProfile -File ./publish/publish-agent.ps1`

## 执行步骤

1. **列出话题**

   ```powershell
   cd tools/qkagent
   qkagent action-topics list --code <sharedId> --json
   ```

2. **逐条评估** — `get --json`；对需跟进的条目 `get --login --json` 确认可归档。

3. **GitHub** — `gh issue list` 去重 → `gh issue create`（**先向用户确认**标题与是否创建）。

4. **回帖 + 归档**（**先确认**）

   ```powershell
   qkagent action-topics reply --id <topicId> --content-file .\draft-reply.txt --json
   qkagent action-topics archive --id <topicId> --json
   ```

5. 输出 triage 摘要表：topicId、标题、决策、issue #、是否已回复/归档。

## 约束

- 所有 `qkagent` / `gh` 写操作：**Agent 自己跑命令**，不要只写步骤给用户。
- 优先 `--json`；失败读 `error`/`message`。
- 勿在 issue 中贴密码、私有路径。
