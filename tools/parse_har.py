# -*- coding: utf-8 -*-
import json, re, collections
from pathlib import Path

har_path = Path(r"E:\我的源码目录\微信群控\微信.har")
out_dir = Path(r"E:\我的源码目录\微信聊天\docs")
out_dir.mkdir(parents=True, exist_ok=True)

with open(har_path, "r", encoding="utf-8") as f:
    har = json.load(f)

entries = har.get("log", {}).get("entries", [])
apis = collections.OrderedDict()

for e in entries:
    req = e.get("request", {})
    url = req.get("url", "")
    if "127.0.0.1:19088" not in url and "localhost:19088" not in url:
        continue
    method = req.get("method", "")
    m = re.search(r"19088(/api/[^?\s]*)", url)
    if not m:
        continue
    path = m.group(1).rstrip("/")
    key = f"{method} {path}"
    post = req.get("postData") or {}
    text = post.get("text") or ""
    mime = post.get("mimeType") or ""
    content = (e.get("response") or {}).get("content") or {}
    rtext = content.get("text") or ""
    title = e.get("comment") or ""
    # try apifox title from headers/cookies? skip
    item = apis.setdefault(
        key,
        {"method": method, "path": path, "mime": mime, "req": [], "resp": [], "titles": set()},
    )
    if title:
        item["titles"].add(title)
    if text and len(item["req"]) < 2:
        item["req"].append(text[:8000])
    if rtext and len(item["resp"]) < 2:
        item["resp"].append(rtext[:12000])

lines = [
    "# Weixin Hook Local API (127.0.0.1:19088)",
    "",
    f"Unique business APIs: **{len(apis)}**",
    "",
    "## All endpoints",
]
for k in apis:
    lines.append(f"- `{k}`")
lines.append("")

focus = [
    "/api/check_login",
    "/api/wechat_init",
    "/api/init_rooms",
    "/api/get_contact_list2",
    "/api/get_chatroom_list",
    "/api/get_room_members",
    "/api/get_member_nick",
    "/api/send_text_msg",
    "/api/send_image_msg",
    "/api/send_file_msg",
    "/api/send_at_text",
    "/api/send_quote",
    "/api/download_img",
    "/api/download_file",
]
lines.append("## Focused endpoints")
for p in focus:
    matches = [(k, v) for k, v in apis.items() if v["path"] == p]
    lines.append(f"### `{p}`")
    if not matches:
        lines.append("_Not found in HAR_")
        lines.append("")
        continue
    for k, v in matches:
        lines.append(f"Method: `{v['method']}`")
        lines.append(f"Content-Type: `{v['mime']}`")
        for i, ex in enumerate(v["req"]):
            lines.append(f"Request example {i+1}:")
            lines.append("```json")
            lines.append(ex)
            lines.append("```")
        for i, ex in enumerate(v["resp"]):
            lines.append(f"Response example {i+1}:")
            lines.append("```json")
            lines.append(ex)
            lines.append("```")
        lines.append("")

out = out_dir / "weixin-hook-api.md"
out_dir.joinpath("weixin-hook-api.md").write_text("\n".join(lines), encoding="utf-8")
print("unique", len(apis))
print("wrote", out)
for p in focus:
    for k, v in apis.items():
        if v["path"] == p:
            print("====", k)
            print("REQ", (v["req"] or [""])[0][:400].replace("\n", " "))
            print("RESP", (v["resp"] or [""])[0][:500].replace("\n", " "))
