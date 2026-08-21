# -*- coding: utf-8 -*-
import json, re
from pathlib import Path

har = json.load(open(r"E:\我的源码目录\微信群控\微信.har", "r", encoding="utf-8"))
pat = re.compile(
    r'"([^"]{2,80})","apiDetail\.(\d+)","(http://127\.0\.0\.1:19088/[^"]+)"'
)
apis = {}
for e in har["log"]["entries"]:
    text = ((e.get("response") or {}).get("content") or {}).get("text") or ""
    if "127.0.0.1:19088" not in text:
        continue
    for name, aid, url in pat.findall(text):
        apis[url] = (name, aid)

print("apis", len(apis))
for url, (name, aid) in sorted(apis.items(), key=lambda x: x[0]):
    print(f"{aid}\t{name}\t{url}")

# Also try to find request body examples near send_text_msg detail pages
# Look for JSON with "wxid" and "msg" near send_text
body_pat = re.compile(r'\{[^{}]*"wxid"[^{}]*"msg"[^{}]*\}')
quote_pat = re.compile(r'\{[^{}]*"newmsgid"[^{}]*\}')
at_pat = re.compile(r'\{[^{}]*"roomId"[^{}]*"wxids"[^{}]*\}|\{[^{}]*"wxids"[^{}]*"roomId"[^{}]*\}')
room_pat = re.compile(r'\{[^{}]*"room_id"[^{}]*\}')
member_pat = re.compile(r'\{[^{}]*"roomId"[^{}]*"wxid"[^{}]*\}|\{[^{}]*"wxid"[^{}]*"roomId"[^{}]*\}')

bodies = {"send_text": set(), "send_at": set(), "send_quote": set(), "room": set(), "member": set()}
for e in har["log"]["entries"]:
    text = ((e.get("response") or {}).get("content") or {}).get("text") or ""
    if not text:
        continue
    for m in body_pat.findall(text):
        if "filepath" not in m:
            bodies["send_text"].add(m[:500])
    for m in at_pat.findall(text):
        bodies["send_at"].add(m[:500])
    for m in quote_pat.findall(text):
        bodies["send_quote"].add(m[:800])
    for m in room_pat.findall(text):
        bodies["room"].add(m[:300])
    for m in member_pat.findall(text):
        bodies["member"].add(m[:400])

print("\n=== BODY SAMPLES ===")
for k, vals in bodies.items():
    print("---", k, len(vals))
    for v in list(vals)[:5]:
        print(v)

# Write markdown catalog
out = Path(r"E:\我的源码目录\微信聊天\docs\weixin-hook-api.md")
lines = [
    "# Weixin Hook Local API (127.0.0.1:19088)",
    "",
    f"Unique business APIs extracted from Apifox HAR: **{len(apis)}**",
    "",
    "## Catalog",
]
for url, (name, aid) in sorted(apis.items(), key=lambda x: x[0]):
    path = url.split("19088", 1)[-1]
    lines.append(f"- `{path}` — {name} (apiDetail.{aid})")
lines.append("")
lines.append("## Request body samples found in HAR payloads")
for k, vals in bodies.items():
    lines.append(f"### {k}")
    for v in list(vals)[:8]:
        lines.append("```json")
        lines.append(v)
        lines.append("```")
    lines.append("")

# Document known schemas from prompt + HAR samples
lines += [
    "## Confirmed schemas (from HAR + user prompt)",
    "",
    "### POST /api/send_text_msg",
    "```json",
    '{"wxid":"filehelper","msg":"6666666666"}',
    "```",
    "Success: `code=1`, `msg=success`",
    "",
    "### POST /api/send_image_msg",
    "```json",
    '{"wxid":"...","filepath":"C:/path/to.png"}',
    "```",
    "Success: `code=1`",
    "",
    "### POST /api/send_file_msg",
    "```json",
    '{"wxid":"filehelper","filepath":"C:/..."}',
    "```",
    "Success: `code=1`",
    "",
    "### POST /api/send_at_text",
    "```json",
    '{"wxids":"wxid_xxx","msg":"@好名字 在干嘛呢","roomId":"39259098574@chatroom"}',
    "```",
    "",
    "### POST /api/send_quote",
    "```json",
    '{"reply":"你好","referContent":"你好","fromUsr":"wxid_xxx","newmsgid":"5217518642639526576","msgSource":"...","createTime":0,"sendto":"49767299448@chatroom"}',
    "```",
    "Success often: `errCode=1`",
    "",
    "### POST /api/get_room_members",
    "```json",
    '{"room_id":"xxxx@chatroom"}',
    "```",
    "",
    "### POST /api/get_member_nick",
    "```json",
    '{"wxid":"wxid_xxx","roomId":"xxxx@chatroom"}',
    "```",
    "",
    "### POST /api/check_login / wechat_init / init_rooms / get_contact_list2 / get_chatroom_list",
    "Body: `{}` or empty",
    "",
    "Note: success codes differ by endpoint (`code=1` for send_*, `code=0` for some list APIs, `errCode=1` for quote).",
]
out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out)
