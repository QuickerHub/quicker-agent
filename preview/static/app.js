const $ = (sel) => document.querySelector(sel);

/** UI strings as Unicode escapes to avoid source file encoding issues on Windows. */
const UI = {
  pageTitle: "\u52a8\u4f5c\u7b80\u4ecb\u9884\u89c8",
  loading: "\u52a0\u8f7d\u4e2d\u2026",
  width: "\u9884\u89c8\u5bbd\u5ea6",
  siteChrome: "\u6a21\u62df\u7ad9\u70b9\u7070\u5e95",
  openFile: "\u6253\u5f00 HTML \u6587\u4ef6",
  statusIdle: "\u9009\u62e9\u5de6\u4fa7\u52a8\u4f5c\u6216\u6253\u5f00\u6587\u4ef6",
  reload: "\u91cd\u65b0\u52a0\u8f7d",
  save: "\u4fdd\u5b58\u5230 info.html",
  sourceHeader: "HTML \u6e90\u7801",
  previewHeader: "\u6e32\u67d3\u9884\u89c8",
  emptyActions: "\u672a\u627e\u5230\u52a8\u4f5c\u3002\u5148\u6267\u884c qkagent pull --code <id>",
  dirty: "\u5df2\u4fee\u6539\uff08\u672a\u4fdd\u5b58\uff09",
  localPreview: "\u672c\u5730\u6587\u4ef6\u9884\u89c8",
  loadingAction: "\u52a0\u8f7d\u4e2d\u2026",
  loaded: "\u5df2\u52a0\u8f7d",
  loadFailed: "\u52a0\u8f7d\u5931\u8d25",
  saving: "\u4fdd\u5b58\u4e2d\u2026",
  saved: "\u5df2\u4fdd\u5b58\u5230 info.html",
  saveFailed: "\u4fdd\u5b58\u5931\u8d25",
  openedFile: "\u5df2\u6253\u5f00\u6587\u4ef6",
};

function applyUiLabels() {
  document.title = `qkagent \u00b7 ${UI.pageTitle}`;
  $("#uiTitle").textContent = UI.pageTitle;
  $("#actionsRoot").textContent = UI.loading;
  $("#uiWidthLabel").textContent = UI.width;
  $("#uiSiteChromeLabel").textContent = UI.siteChrome;
  $("#btnOpenFile").textContent = UI.openFile;
  statusEl.textContent = UI.statusIdle;
  $("#btnReload").textContent = UI.reload;
  $("#btnSave").textContent = UI.save;
  $("#uiSourceHeader").textContent = UI.sourceHeader;
  $("#uiPreviewHeader").textContent = UI.previewHeader;
}

const actionList = $("#actionList");
const htmlEditor = $("#htmlEditor");
const previewFrame = $("#previewFrame");
const previewOuter = $("#previewOuter");
const widthSlider = $("#widthSlider");
const widthValue = $("#widthValue");
const siteChrome = $("#siteChrome");
const statusEl = $("#status");
const btnSave = $("#btnSave");
const btnReload = $("#btnReload");
const fileInput = $("#fileInput");
const btnOpenFile = $("#btnOpenFile");

let currentActionId = null;
let saveTimer = null;
let dirty = false;

function setStatus(text, kind = "") {
  statusEl.textContent = text;
  statusEl.className = `status ${kind}`;
}

function debouncePreview() {
  clearTimeout(saveTimer);
  saveTimer = setTimeout(updatePreview, 200);
}

function updatePreview() {
  const html = htmlEditor.value;
  previewFrame.srcdoc = wrapPreviewDocument(html);
  resizePreviewFrame();
  dirty = true;
  setStatus(currentActionId ? UI.dirty : UI.localPreview);
}

function wrapPreviewDocument(html) {
  return `<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<base target="_blank">
<style>
  html, body { margin: 0; padding: 0; }
  body { font-family: "Microsoft YaHei", system-ui, sans-serif; }
</style>
</head>
<body>${html}</body>
</html>`;
}

function resizePreviewFrame() {
  const doc = previewFrame.contentDocument;
  if (!doc) return;
  const height = Math.max(doc.documentElement.scrollHeight, doc.body?.scrollHeight ?? 0, 400);
  previewFrame.style.height = `${height + 8}px`;
}

function applyPreviewWidth() {
  const w = Number(widthSlider.value);
  widthValue.textContent = `${w}px`;
  previewOuter.style.maxWidth = `${w}px`;
}

function applySiteChrome() {
  previewOuter.classList.toggle("site-chrome", siteChrome.checked);
}

async function loadConfig() {
  const res = await fetch("/api/config");
  const data = await res.json();
  $("#actionsRoot").textContent = data.actionsRoot;
}

async function loadActions() {
  const res = await fetch("/api/actions");
  const actions = await res.json();
  actionList.innerHTML = "";
  if (!actions.length) {
    actionList.innerHTML =
      `<li class="empty-hint">${UI.emptyActions}</li>`;
    return;
  }
  for (const a of actions) {
    const li = document.createElement("li");
    const btn = document.createElement("button");
    btn.type = "button";
    btn.dataset.id = a.id;
    const date = a.updatedAt ? new Date(a.updatedAt).toLocaleString() : "";
    btn.innerHTML = `<span>${a.id.slice(0, 8)}…</span><span class="meta">${date} · ${a.sizeBytes ?? 0} B</span>`;
    btn.addEventListener("click", () => selectAction(a.id));
    li.appendChild(btn);
    actionList.appendChild(li);
  }
}

async function selectAction(id) {
  currentActionId = id;
  dirty = false;
  actionList.querySelectorAll("button").forEach((b) => {
    b.classList.toggle("active", b.dataset.id === id);
  });
  setStatus(UI.loadingAction);
  try {
    const res = await fetch(`/api/actions/${encodeURIComponent(id)}/html`);
    if (!res.ok) throw new Error(await res.text());
    const data = await res.json();
    htmlEditor.value = data.html;
    updatePreview();
    btnSave.disabled = false;
    setStatus(`${UI.loaded} ${id}`, "ok");
  } catch (e) {
    setStatus(`${UI.loadFailed}: ${e.message}`, "error");
  }
}

async function saveCurrent() {
  if (!currentActionId) return;
  setStatus(UI.saving);
  btnSave.disabled = true;
  try {
    const res = await fetch(`/api/actions/${encodeURIComponent(currentActionId)}/html`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ html: htmlEditor.value }),
    });
    if (!res.ok) throw new Error(await res.text());
    dirty = false;
    setStatus(UI.saved, "ok");
    await loadActions();
  } catch (e) {
    setStatus(`${UI.saveFailed}: ${e.message}`, "error");
  } finally {
    btnSave.disabled = false;
  }
}

function openLocalFile(file) {
  const reader = new FileReader();
  reader.onload = () => {
    currentActionId = null;
    actionList.querySelectorAll("button").forEach((b) => b.classList.remove("active"));
    htmlEditor.value = reader.result;
    updatePreview();
    btnSave.disabled = true;
    setStatus(`${UI.openedFile}: ${file.name}`);
  };
  reader.readAsText(file, "UTF-8");
}

htmlEditor.addEventListener("input", debouncePreview);
widthSlider.addEventListener("input", applyPreviewWidth);
siteChrome.addEventListener("change", applySiteChrome);
btnSave.addEventListener("click", saveCurrent);
btnReload.addEventListener("click", () => {
  if (currentActionId) selectAction(currentActionId);
  else updatePreview();
});
fileInput.addEventListener("change", (e) => {
  const file = e.target.files?.[0];
  if (file) openLocalFile(file);
  fileInput.value = "";
});
if (btnOpenFile) {
  btnOpenFile.addEventListener("click", () => fileInput.click());
}

previewFrame.addEventListener("load", () => {
  resizePreviewFrame();
  applyPreviewWidth();
});

document.addEventListener("keydown", (e) => {
  if ((e.ctrlKey || e.metaKey) && e.key === "s") {
    e.preventDefault();
    if (currentActionId) saveCurrent();
  }
});

applyUiLabels();
applyPreviewWidth();
applySiteChrome();
loadConfig();
loadActions();
