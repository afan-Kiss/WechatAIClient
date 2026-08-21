# WechatAIClient 本地 API 开发摘要（127.0.0.1:19088）

**业务 API 唯一路径总数（`/api/*` on 19088）: 115**

来源 HAR: `E:\我的源码目录\微信群控\微信.har`

说明：HAR 中无直接访问 `127.0.0.1:19088` 的网络条目；以下接口来自 Apifox 在线文档（`s.apifox.cn` `*.data`）内嵌的 `http://127.0.0.1:19088/api/...` 定义与 curl/JSON 示例。已排除 Apifox 静态资源、Google Analytics、广告/追踪等噪声。

## 全部唯一 `/api/*` 路径与方法计数

| Path | 中文名 | Method 计数（详情页） |
|------|--------|----------------------|
| `/api/add_friend` | 添加好友 | POST（仅目录） |
| `/api/add_label` | 增加标签 | POST×1 |
| `/api/add_member_to_chat_room` | 添加群成员40人以内 | POST×1 |
| `/api/anti_revoke` | 防撤回 | POST×1 |
| `/api/api/del_room_admin` | 删除群管理 | POST×1 |
| `/api/api/set_room_admin` | 添加群管理 | POST×1 |
| `/api/auto_login` | 自动登陆 | POST×1 |
| `/api/backup_database` | 数据库备份 | POST×1 |
| `/api/batch_get_wxids` | 批量获取wxid信息 | POST×1 |
| `/api/batch_getroom_cache` | 获取所有的群资料(缓存速度极快) | POST×1 |
| `/api/batch_getroom_contact` | 获取所有群的资料(网络获取长耗时接口) | POST×1 |
| `/api/black_user` | 拉黑好友 | POST×1 |
| `/api/cancel_top` | 取消置顶 | POST×1 |
| `/api/cdn_download` | cdn下载 | POST×1 |
| `/api/cdn_video_forward` | cdn转发视频 | POST×1 |
| `/api/check_login` | 获取登陆状态 | POST×1 |
| `/api/creat_chat_room` | 创建群聊 | POST×1 |
| `/api/decrypt_db` | 解密数据库 | POST×1 |
| `/api/del_black_user` | 移出黑名单 | POST×1 |
| `/api/del_contact` | 删除好友 | POST×1 |
| `/api/del_label` | 删除标签 | POST×1 |
| `/api/del_member_from_chat_room` | 踢出群成员 | POST×1 |
| `/api/del_mute_user` | 关闭消息免打扰 | POST×1 |
| `/api/del_start` | 取消星标 | POST×1 |
| `/api/download_file` | 下载文件 | POST×2 |
| `/api/download_img` | 下载图片 | POST×1 |
| `/api/download_sns_media` | 朋友圈图片/视频下载(测试中) | POST×1 |
| `/api/download_video` | 下载视频 | POST×1 |
| `/api/download_voice` | 下载语音 | POST×1 |
| `/api/download_wxwork_file` | 下载企业文件/图片 | POST×1 |
| `/api/enter_room` | 同意群聊邀请 | POST×2 |
| `/api/folding` | 折叠群聊或者个人 | POST×1 |
| `/api/get_a8key` | 群聊获取A8key | POST×2 |
| `/api/get_all_room_detail` | 获取所有群聊的成员列表,群头像,昵称等数据 | POST×1 |
| `/api/get_cdn_info` | 获取cdn信息 | POST×1 |
| `/api/get_chatroom_announcement` | 获取群公告 | POST×1 |
| `/api/get_chatroom_detail_cache` | 获取群详情缓存 | POST×2 |
| `/api/get_chatroom_info` | 获取群详情 | POST×1 |
| `/api/get_chatroom_list` | 获取群聊列表 | POST×1 |
| `/api/get_config_path` | 获取配置文件保存目录 | POST×1 |
| `/api/get_contact` | 获取好友最新资料(网络获取) | POST×1 |
| `/api/get_contact_fast` | 快速查找好友资料(非常快) | POST×1 |
| `/api/get_contact_list2` | 获取好友列表方法2(二叉树 群聊只有保存到通讯录里才显示) | POST×1 |
| `/api/get_db_handle` | 获取数据库句柄 | POST×1 |
| `/api/get_favs` | 获取收藏列表 | POST×1 |
| `/api/get_friend_wxids` | 获取所有好友的wxid(网络长耗时) | POST×1 |
| `/api/get_group_member_contact` | 查询群成员信息 | POST×1 |
| `/api/get_group_memeber_info` | 获取群成员数据 | POST×1 |
| `/api/get_groupmember_bysql` | 获取群成员数据(简要不包含头像) | POST×1 |
| `/api/get_label_lists` | 获取标签列表 | POST×1 |
| `/api/get_lbs_friend` | 获取附近人 | POST×1 |
| `/api/get_member_nick` | 获取群成员简要信息(获取群成员昵称接口) | POST×1 |
| `/api/get_my_qrocde` | 获取好友二维码 | POST×1 |
| `/api/get_profile_cache` | 获取个人资料缓存 | POST×1 |
| `/api/get_profile_new` | 获取个人最新网络 | POST×1 |
| `/api/get_room_members` | 获取群成员列表 | POST×1 |
| `/api/get_room_wxids` | 获取所有群wxids(网络长耗时) | POST×1 |
| `/api/get_rooms_info` | 获取群成员数量,群昵称 | POST×1 |
| `/api/get_voice_trans` | 语音转文本 | POST×1 |
| `/api/getwxbasepath` | 获取微信缓存目录 | POST×1 |
| `/api/init_rooms` | 初始化群聊(事件需要) | POST×1 |
| `/api/invite_member_to_chat_room` | 邀请进入群聊 | POST×1 |
| `/api/js_login` | 获取小程序code | POST×1 |
| `/api/logout` | 退出登陆 | POST×1 |
| `/api/mod_chat_room_self_nick_name` | 修改自己在群里的昵称 | POST×1 |
| `/api/mod_chatroom_topic` | 修改群名称 | POST×1 |
| `/api/mod_self_nick_name` | 修改自己昵称 | POST×1 |
| `/api/mod_self_nick_signature` | 修改个人签名 | POST×1 |
| `/api/modify_contact_label` | 修改好友标签 | POST×1 |
| `/api/net_scene_search_contact` | 搜索微信号/手机号 | POST×1 |
| `/api/qrscan` | 二维码识别 | POST×1 |
| `/api/quit_and_del_chat_room` | 退出群聊 | POST×1 |
| `/api/reflash_qrcode` | 获取登录二维码 | POST×1 |
| `/api/remark_contact` | 修改好友备注 | POST×1 |
| `/api/remov_chatroom_to_contact` | 移除群聊通讯录 | POST×1 |
| `/api/revoke_any` | 撤回任何消息 | POST×1 |
| `/api/save_chatroom_to_contact` | 保存群聊到通讯录 | POST×1 |
| `/api/send_app_msg` | 发送卡片/XML消息 | POST×1 |
| `/api/send_applet_msg` | 发送小程序 | POST×1 |
| `/api/send_at_text` | 发送AT消息 | POST×1 |
| `/api/send_cdn_img_msg` | cdn发送图片(无源可用做转发消息) | POST×1 |
| `/api/send_emotion_msg` | 发送本地GIF信息 | POST×1 |
| `/api/send_fav_emotion` | 发送收藏表情 | POST×1 |
| `/api/send_file_msg` | 发送文件消息 | POST×1 |
| `/api/send_image_msg` | 发送图片消息 | POST×1 |
| `/api/send_location_msg` | 发送位置消息 | POST×1 |
| `/api/send_mp3_voice` | 发送MP3语音 | POST×1 |
| `/api/send_pat` | 发送拍一拍 | POST×1 |
| `/api/send_quote` | 发送引用消息 | POST×1 |
| `/api/send_text_msg` | 发送文本消息 | POST×1 |
| `/api/send_xml` | 发送链接信息 | POST×1 |
| `/api/set_mute_user` | 开启消息免打扰 | POST×1 |
| `/api/set_room_announcement_pb` | 设置群公告 | POST×2 |
| `/api/set_start` | 星标好友 | POST×1 |
| `/api/set_top` | 置顶好友 | POST×1 |
| `/api/sns_comment_reply` | 朋友圈回复 | POST×1 |
| `/api/sns_del` | 删除朋友圈 | POST×1 |
| `/api/sns_del_comment` | 删除朋友圈评论 | POST×1 |
| `/api/sns_get_detail` | 获取朋友圈详情 | POST×1 |
| `/api/sns_get_first_page` | 获取朋友圈首页 | POST×1 |
| `/api/sns_get_next_page` | 获取朋友圈下一页 | POST×1 |
| `/api/sns_post` | 发送朋友圈 | POST×1 |
| `/api/sns_send_img` | 发送图片朋友圈 | POST×1 |
| `/api/sns_upload` | 朋友圈图片上传 | POST×1 |
| `/api/sqlite3_exec` | 获取好友列表数据库查询 | POST×3 |
| `/api/ten_pay_trans_fer_confirm` | 确认收款 | POST×1 |
| `/api/transferchatroomowner` | 转让群主 | POST×1 |
| `/api/un_ten_pay_trans_fer_confirm` | 拒绝收款 | POST×1 |
| `/api/unfolding` | 取消折叠群聊或者个人 | POST×1 |
| `/api/update_all_friend` | 更新好友列表 | POST×1 |
| `/api/update_label_name` | 更新标签名字 | POST×1 |
| `/api/update_single_profile` | 更新单个用户资料 | POST×1 |
| `/api/upload_head_img` | 修改头像 | POST×1 |
| `/api/verify_friend` | 同意好友申请(有变动) | POST×1 |
| `/api/wechat_init` | 微信初始化好友列表,群列表 | POST×1 |

## 回调 / Callback 相关（文档条目）

Apifox 目录中的回调类文档（主动推送事件说明；通常无对应 `/api/*` HTTP 调用路径）：

- curl --location --request PUT 'http://test-cn.your-api-server.com个人发送图片消息回调'
- curl --location --request PUT 'http://test-cn.your-api-server.com个人发送文本消息回调'
- curl --location --request PUT 'http://test-cn.your-api-server.com窗口切换事件回调'
- curl --location --request PUT 'http://test-cn.your-api-server.com群成员进群回调'
- curl --location --request PUT 'http://test-cn.your-api-server.com群成员退群回调'
- 个人发送图片消息回调
- 个人发送文件/卡片/小程序/等等xml回调
- 个人发送文本消息回调
- 朋友圈消息回调
- 私聊消息回调
- 窗口切换事件回调
- 群成员修改昵称回调
- 群成员进群回调
- 群成员退群回调
- 群聊消息回调
- 聊天对象切换回调
- 该接口里的所有数据均来自消息回调

## 接口明细（重点优先）

### 获取登陆状态 — `/api/check_login`

- **Method**: `POST`
- **URL path**: `/api/check_login`
- **完整 URL**: `http://127.0.0.1:19088/api/check_login`
- **Content-Type**: `application/json`

#### 请求示例

```json
{}
```

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 微信初始化好友列表,群列表 — `/api/wechat_init`

- **Method**: `POST`
- **URL path**: `/api/wechat_init`
- **完整 URL**: `http://127.0.0.1:19088/api/wechat_init`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 初始化群聊(事件需要) — `/api/init_rooms`

- **Method**: `POST`
- **URL path**: `/api/init_rooms`
- **完整 URL**: `http://127.0.0.1:19088/api/init_rooms`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 获取好友列表方法2(二叉树 群聊只有保存到通讯录里才显示) — `/api/get_contact_list2`

- **Method**: `POST`
- **URL path**: `/api/get_contact_list2`
- **完整 URL**: `http://127.0.0.1:19088/api/get_contact_list2`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 获取群聊列表 — `/api/get_chatroom_list`

- **Method**: `POST`
- **URL path**: `/api/get_chatroom_list`
- **完整 URL**: `http://127.0.0.1:19088/api/get_chatroom_list`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 获取群成员列表 — `/api/get_room_members`

- **Method**: `POST`
- **URL path**: `/api/get_room_members`
- **完整 URL**: `http://127.0.0.1:19088/api/get_room_members`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "room_id": "39259098574@chatroom"
}
```

#### 响应示例

```json
{
  "baseResponse": {
    "ret": 0,
    "errMsg": {}
  },
  "chatroomUserName": "49767299448@chatroom",
  "serverVersion": 10004,
  "newChatroomData": {
    "memberCount": 2,
    "chatRoomMember": [
      {
        "userName": "wxid1",
        "nickName": "隔壁老陈",
        "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/F3mNcrM9JiccgM56eLOzD4aiaZMibW4efYpAUMf0HuV9ricVBtdc19smEhdO26tBJ0IwqZmsANDHCf3rJVpic0NWrgPHXbiawI6vnZlV4hibibGvqb7hsTkr7fBYfO5Ss7LsksvF/0",
        "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/F3mNcrM9JiccgM56eLOzD4aiaZMibW4efYpAUMf0HuV9ricVBtdc19smEhdO26tBJ0IwqZmsANDHCf3rJVpic0NWrgPHXbiawI6vnZlV4hibibGvqb7hsTkr7fBYfO5Ss7LsksvF/132",
        "chatroomMemberFlag": 1,
        "status": 0
      },
      {
        "userName": "wxid2",
        "nickName": "不必",
        "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/UfAy94vgEmryCeyWxYAa1moicl0Tia1RnDzIDTHxxQZNKC7rjBtdRsezeL0B7sMicEicUILaxxic8QiazNlaDqRZD8vn2GrL4RIjhLuoAlfcPCPJLjQiaYe6ibn28oAdEwpsuh5W/0",
        "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/UfAy94vgEmryCeyWxYAa1moicl0Tia1RnDzIDTHxxQZNKC7rjBtdRsezeL0B7sMicEicUILaxxic8QiazNlaDqRZD8vn2GrL4RIjhLuoAlfcPCPJLjQiaYe6ibn28oAdEwpsuh5W/132",
        "chatroomMemberFlag": 1,
        "inviterUserName": "wxid_ozyqateb85un22",
        "status": 0,
        "addChatRoomSceneNewXml": "<sysmsg type=\"ChatRoomMemberTraceBack\">\n\t<ChatRoomMemberTraceBack>\n\t\t<text><![CDATA[$inviter_username$邀请进群]]></text>\n\t\t<link>\n\t\t\t<username><![CDATA[wxid_ozyqateb85un22]]></username>\n\t\t</link>\n\t</ChatRoomMemberTraceBack>\n</sysmsg>\n"
      }
    ],
    "infoMask": 0,
    "chatRoomUserName": {},
    "watchMemberCount": 0
  },
  "chatRoomOwner": "wxid",
  "allMemberCount": 2,
  "allMemberUserNameList": [
    {
      "String": "wxid1"
    },
    {
      "String": "wxid2"
    }
  ],
  "adminCount": 0
}
```

#### 关键字段

请求:
- `room_id (str): '39259098574@chatroom'`
响应:
- `baseResponse (object)`
- `baseResponse.ret (int): 0`
- `baseResponse.errMsg (object)`
- `chatroomUserName (str): '49767299448@chatroom'`
- `serverVersion (int): 10004`
- `newChatroomData (object)`
- `newChatroomData.memberCount (int): 2`
- `newChatroomData.chatRoomMember (array, len=2)`
- `newChatroomData.chatRoomMember[].userName (str): 'wxid1'`
- `newChatroomData.chatRoomMember[].nickName (str): '隔壁老陈'`
- `newChatroomData.chatRoomMember[].bigHeadImgUrl (str): 'https://wx.qlogo.cn/mmhead/ver_1/F3mNcrM9JiccgM56eLOzD4aiaZMibW4efYpAUMf0HuV9ricVBtdc19smEhdO26tBJ0IwqZmsANDHCf3rJVpic0...`
- `newChatroomData.chatRoomMember[].smallHeadImgUrl (str): 'https://wx.qlogo.cn/mmhead/ver_1/F3mNcrM9JiccgM56eLOzD4aiaZMibW4efYpAUMf0HuV9ricVBtdc19smEhdO26tBJ0IwqZmsANDHCf3rJVpic0...`
- `newChatroomData.chatRoomMember[].chatroomMemberFlag (int): 1`
- `newChatroomData.chatRoomMember[].status (int): 0`
- `newChatroomData.infoMask (int): 0`
- `newChatroomData.chatRoomUserName (object)`
- `newChatroomData.watchMemberCount (int): 0`
- `chatRoomOwner (str): 'wxid'`
- `allMemberCount (int): 2`
- `allMemberUserNameList (array, len=2)`
- `allMemberUserNameList[].String (str): 'wxid1'`
- `adminCount (int): 0`

### 获取群成员简要信息(获取群成员昵称接口) — `/api/get_member_nick`

- **Method**: `POST`
- **URL path**: `/api/get_member_nick`
- **完整 URL**: `http://127.0.0.1:19088/api/get_member_nick`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "wxid_8zggbw1yo5ib22",
  "roomId": "18402658081@chatroom"
}
```

#### 响应示例

```json
{
  "account_wxid": "string",
  "data": {
    "addChatRoomSceneNewXml": "string",
    "bigHeadImgUrl": "string",
    "chatroomMemberFlag": 0,
    "inviterUserName": "string",
    "nickName": "string",
    "smallHeadImgUrl": "string",
    "status": 0,
    "userName": "string"
  },
  "errCode": 0,
  "errMsg": "string"
}
```

#### 关键字段

请求:
- `wxid (str): 'wxid_8zggbw1yo5ib22'`
- `roomId (str): '18402658081@chatroom'`
响应:
- `account_wxid (str): 'string'`
- `data (object)`
- `data.addChatRoomSceneNewXml (str): 'string'`
- `data.bigHeadImgUrl (str): 'string'`
- `data.chatroomMemberFlag (int): 0`
- `data.inviterUserName (str): 'string'`
- `data.nickName (str): 'string'`
- `data.smallHeadImgUrl (str): 'string'`
- `data.status (int): 0`
- `data.userName (str): 'string'`
- `errCode (int): 0`
- `errMsg (str): 'string'`

### 发送文本消息 — `/api/send_text_msg`

- **Method**: `POST`
- **URL path**: `/api/send_text_msg`
- **完整 URL**: `http://127.0.0.1:19088/api/send_text_msg`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "filehelper",
  "msg": "6666666666"
}
```

#### 响应示例

```json
{
  "code": 1,
  "data": null,
  "msg": "success"
}
```

#### 关键字段

请求:
- `wxid (str): 'filehelper'`
- `msg (str): '6666666666'`
响应:
- `code (int): 1`
- `data (NoneType): None`
- `msg (str): 'success'`

### 发送图片消息 — `/api/send_image_msg`

- **Method**: `POST`
- **URL path**: `/api/send_image_msg`
- **完整 URL**: `http://127.0.0.1:19088/api/send_image_msg`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "21419004893@chatroom",
  "filepath": "C:\\Users\\Admin\\Desktop\\山姆\\config\\wxid_k5gs9mcpu2pa22\\cache\\老公的读书会_3747.txt"
}
```

#### 响应示例

```json
{
  "code": 1,
  "data": null,
  "msg": "success"
}
```

#### 关键字段

请求:
- `wxid (str): '21419004893@chatroom'`
- `filepath (str): 'C:\\Users\\Admin\\Desktop\\山姆\\config\\wxid_k5gs9mcpu2pa22\\cache\\老公的读书会_3747.txt'`
响应:
- `code (int): 1`
- `data (NoneType): None`
- `msg (str): 'success'`

### 发送文件消息 — `/api/send_file_msg`

- **Method**: `POST`
- **URL path**: `/api/send_file_msg`
- **完整 URL**: `http://127.0.0.1:19088/api/send_file_msg`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "filehelper",
  "filepath": "C:/Users/Admin/Desktop/山姆/config/wxid_k5gs9mcpu2pa22/cache/老公的读书会_3747.txt"
}
```

#### 响应示例

```json
{
  "code": 1,
  "data": null,
  "info": "请勿使用二手贩子贩卖的成品,售后无人处理 请联系作者购买",
  "msg": "success"
}
```

#### 关键字段

请求:
- `wxid (str): 'filehelper'`
- `filepath (str): 'C:/Users/Admin/Desktop/山姆/config/wxid_k5gs9mcpu2pa22/cache/老公的读书会_3747.txt'`
响应:
- `code (int): 1`
- `data (NoneType): None`
- `info (str): '请勿使用二手贩子贩卖的成品,售后无人处理 请联系作者购买'`
- `msg (str): 'success'`

### 发送AT消息 — `/api/send_at_text`

- **Method**: `POST`
- **URL path**: `/api/send_at_text`
- **完整 URL**: `http://127.0.0.1:19088/api/send_at_text`
- **Content-Type**: `application/json`

#### 请求体（完整，不臆造字段）

```json
{
  "wxids": "wxid_8543785438012",
  "msg": " @好名字 在干嘛呢",
  "roomId": "39259098574@chatroom"
}
```

#### 响应示例

```json
{
  "code": 1,
  "data": null,
  "msg": "success"
}
```

#### 关键字段

请求:
- `wxids (str): 'wxid_8543785438012'`
- `msg (str): '\u2005@好名字\u2005在干嘛呢'`
- `roomId (str): '39259098574@chatroom'`
响应:
- `code (int): 1`
- `data (NoneType): None`
- `msg (str): 'success'`

### 发送引用消息 — `/api/send_quote`

- **Method**: `POST`
- **URL path**: `/api/send_quote`
- **完整 URL**: `http://127.0.0.1:19088/api/send_quote`
- **Content-Type**: `application/json`

#### 请求体（完整，不臆造字段）

```json
{
  "reply": "你好",
  "referContent": "你好",
  "fromUsr": "wxid_ozyqateb85un22",
  "newmsgid": "5217518642639526576",
  "msgSource": "这个参数可要可不要",
  "createTime": 0,
  "sendto": "49767299448@chatroom"
}
```

#### 响应示例

```json
{
  "account_wxid": "wxid_8543785438012",
  "data": {
    "actionFlag": 0,
    "baseResponse": {
      "errMsg": {},
      "ret": 0
    },
    "clientMsgId": "NAQonGUnnCOF5CfFWKLu4cIgyins5GMZ",
    "createTime": 1761396536,
    "fromUserName": "wxid_8543785438012",
    "msgId": 832912929,
    "msgSource": "<msgsource>\n\t<bizflag>0</bizflag>\n\t<sec_msg_node>\n\t\t<uuid>a63d06f8497204d91fe69e0c1486e5e0_</uuid>\n\t\t<risk-file-flag></risk-file-flag>\n\t\t<risk-file-md5-list></risk-file-md5-list>\n\t</sec_msg_node>\n</msgsource>\n",
    "newMsgId": "843118478561634774",
    "toUserName": "49767299448@chatroom",
    "type": 57
  },
  "errCode": 1,
  "errMsg": "请求处理成功"
}
```

#### 关键字段

请求:
- `reply (str): '你好'`
- `referContent (str): '你好'`
- `fromUsr (str): 'wxid_ozyqateb85un22'`
- `newmsgid (str): '5217518642639526576'`
- `msgSource (str): '这个参数可要可不要'`
- `createTime (int): 0`
- `sendto (str): '49767299448@chatroom'`
响应:
- `account_wxid (str): 'wxid_8543785438012'`
- `data (object)`
- `data.actionFlag (int): 0`
- `data.baseResponse (object)`
- `data.baseResponse.errMsg (object)`
- `data.baseResponse.ret (int): 0`
- `data.clientMsgId (str): 'NAQonGUnnCOF5CfFWKLu4cIgyins5GMZ'`
- `data.createTime (int): 1761396536`
- `data.fromUserName (str): 'wxid_8543785438012'`
- `data.msgId (int): 832912929`
- `data.msgSource (str): '<msgsource>\n\t<bizflag>0</bizflag>\n\t<sec_msg_node>\n\t\t<uuid>a63d06f8497204d91fe69e0c1486e5e0_</uuid>\n\t\t<risk-fi...`
- `data.newMsgId (str): '843118478561634774'`
- `data.toUserName (str): '49767299448@chatroom'`
- `data.type (int): 57`
- `errCode (int): 1`
- `errMsg (str): '请求处理成功'`

### 下载图片 — `/api/download_img`

- **Method**: `POST`
- **URL path**: `/api/download_img`
- **完整 URL**: `http://127.0.0.1:19088/api/download_img`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "to_user": "wxid_ozyqateb85un22",
  "from_user": "wxid_8543785438012",
  "start_pos": 0,
  "total_len": 44041,
  "data_len": 44041,
  "compress_type": 0,
  "MsgId": 1213935352,
  "path": "d:\\7878787878.jpg"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `to_user (str): 'wxid_ozyqateb85un22'`
- `from_user (str): 'wxid_8543785438012'`
- `start_pos (int): 0`
- `total_len (int): 44041`
- `data_len (int): 44041`
- `compress_type (int): 0`
- `MsgId (int): 1213935352`
- `path (str): 'd:\\7878787878.jpg'`

### 下载文件 — `/api/download_file`

- **Method**: `POST`
- **URL path**: `/api/download_file`
- **完整 URL**: `http://127.0.0.1:19088/api/download_file`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "from_user": "",
  "total_len": "31538",
  "MsgId": 2754393265637994605,
  "path": "d:\\罗泽南8月考勤表.xlsx",
  "attachid": "@cdn_3057020100044b3049020100020403e0b2d502032df85f020426372f70020468baeb70042439666537653139352d646235392d343035662d613664372d3231393862356533326130350204011800050201000405004c57c300_f2db3329fe5ea2fd06e2ad245da1965e_1",
  "type": "6"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `from_user (str): ''`
- `total_len (str): '31538'`
- `MsgId (int): 2754393265637994605`
- `path (str): 'd:\\罗泽南8月考勤表.xlsx'`
- `attachid (str): '@cdn_3057020100044b3049020100020403e0b2d502032df85f020426372f70020468baeb70042439666537653139352d646235392d343035662d61...`
- `type (str): '6'`

### 添加好友 — `/api/add_friend`

- **Method**: `POST`
- **URL path**: `/api/add_friend`
- **完整 URL**: `http://127.0.0.1:19088/api/add_friend`
- **Content-Type**: _未见_
- **备注**: 仅在目录中出现，未捕获到独立详情页示例

#### 请求示例

_无请求体示例_

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 增加标签 — `/api/add_label`

- **Method**: `POST`
- **URL path**: `/api/add_label`
- **完整 URL**: `http://127.0.0.1:19088/api/add_label`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "label": "标签名字"
}
```

#### 响应示例

```json
{
  "baseResponse": {
    "ret": 0,
    "errMsg": {}
  },
  "labelCount": 1,
  "labelPairList": [
    {
      "labelName": "我的标签",
      "labelId": 12
    }
  ]
}
```

#### 关键字段

请求:
- `label (str): '标签名字'`
响应:
- `baseResponse (object)`
- `baseResponse.ret (int): 0`
- `baseResponse.errMsg (object)`
- `labelCount (int): 1`
- `labelPairList (array, len=1)`
- `labelPairList[].labelName (str): '我的标签'`
- `labelPairList[].labelId (int): 12`

### 添加群成员40人以内 — `/api/add_member_to_chat_room`

- **Method**: `POST`
- **URL path**: `/api/add_member_to_chat_room`
- **完整 URL**: `http://127.0.0.1:19088/api/add_member_to_chat_room`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid_list": "wxid_3e9mll0g0fad21",
  "room_id": "45220347292@chatroom"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid_list (str): 'wxid_3e9mll0g0fad21'`
- `room_id (str): '45220347292@chatroom'`

### 防撤回 — `/api/anti_revoke`

- **Method**: `POST`
- **URL path**: `/api/anti_revoke`
- **完整 URL**: `http://127.0.0.1:19088/api/anti_revoke`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "swtich": "true"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `swtich (str): 'true'`

### 删除群管理 — `/api/api/del_room_admin`

- **Method**: `POST`
- **URL path**: `/api/api/del_room_admin`
- **完整 URL**: `http://127.0.0.1:19088/api/api/del_room_admin`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "roomId": "49767299448@chatroom",
  "admin": "wxid_8543785438012"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `roomId (str): '49767299448@chatroom'`
- `admin (str): 'wxid_8543785438012'`

### 添加群管理 — `/api/api/set_room_admin`

- **Method**: `POST`
- **URL path**: `/api/api/set_room_admin`
- **完整 URL**: `http://127.0.0.1:19088/api/api/set_room_admin`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "roomId": "49767299448@chatroom",
  "admin": "wxid_8543785438012"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `roomId (str): '49767299448@chatroom'`
- `admin (str): 'wxid_8543785438012'`

### 自动登陆 — `/api/auto_login`

- **Method**: `POST`
- **URL path**: `/api/auto_login`
- **完整 URL**: `http://127.0.0.1:19088/api/auto_login`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 数据库备份 — `/api/backup_database`

- **Method**: `POST`
- **URL path**: `/api/backup_database`
- **完整 URL**: `http://127.0.0.1:19088/api/backup_database`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "outputDir": "C:\\Users\\Admin\\AppData\\Roaming\\WechatTools\\wxid_8543785438012_databasebackup",
  "name": "contact.db"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `outputDir (str): 'C:\\Users\\Admin\\AppData\\Roaming\\WechatTools\\wxid_8543785438012_databasebackup'`
- `name (str): 'contact.db'`

### 批量获取wxid信息 — `/api/batch_get_wxids`

- **Method**: `POST`
- **URL path**: `/api/batch_get_wxids`
- **完整 URL**: `http://127.0.0.1:19088/api/batch_get_wxids`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxids": "wxid_60ow7mbi0gpj22,wxid_8543785438012,wxid_tqyh06fntmo722,wxid_rdpo01enuad821,wxid_akegk9w99zg922"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxids (str): 'wxid_60ow7mbi0gpj22,wxid_8543785438012,wxid_tqyh06fntmo722,wxid_rdpo01enuad821,wxid_akegk9w99zg922'`

### 获取所有的群资料(缓存速度极快) — `/api/batch_getroom_cache`

- **Method**: `POST`
- **URL path**: `/api/batch_getroom_cache`
- **完整 URL**: `http://127.0.0.1:19088/api/batch_getroom_cache`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 获取所有群的资料(网络获取长耗时接口) — `/api/batch_getroom_contact`

- **Method**: `POST`
- **URL path**: `/api/batch_getroom_contact`
- **完整 URL**: `http://127.0.0.1:19088/api/batch_getroom_contact`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 拉黑好友 — `/api/black_user`

- **Method**: `POST`
- **URL path**: `/api/black_user`
- **完整 URL**: `http://127.0.0.1:19088/api/black_user`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "群wxid"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): '群wxid'`

### 取消置顶 — `/api/cancel_top`

- **Method**: `POST`
- **URL path**: `/api/cancel_top`
- **完整 URL**: `http://127.0.0.1:19088/api/cancel_top`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "群wxid"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): '群wxid'`

### cdn下载 — `/api/cdn_download`

- **Method**: `POST`
- **URL path**: `/api/cdn_download`
- **完整 URL**: `http://127.0.0.1:19088/api/cdn_download`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "fileid": "3057020100044b3049020100020403e0b2d502032e1e4102046e8ae4730204691587fc042463613538626461322d663963632d346231312d383831652d313038623037653336636431020405152a010201000405004c55cd00",
  "asekey": "04a64148a121f65a94ad23396e565a73",
  "imgType": 2,
  "out": "D:\\test1.jpg"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `fileid (str): '3057020100044b3049020100020403e0b2d502032e1e4102046e8ae4730204691587fc042463613538626461322d663963632d346231312d3838316...`
- `asekey (str): '04a64148a121f65a94ad23396e565a73'`
- `imgType (int): 2`
- `out (str): 'D:\\test1.jpg'`

### cdn转发视频 — `/api/cdn_video_forward`

- **Method**: `POST`
- **URL path**: `/api/cdn_video_forward`
- **完整 URL**: `http://127.0.0.1:19088/api/cdn_video_forward`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "string",
  "cdnVideoUrl": "string",
  "aesKey": "string",
  "videoLength": 0,
  "thumbLength": 0,
  "playLength": 0
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): 'string'`
- `cdnVideoUrl (str): 'string'`
- `aesKey (str): 'string'`
- `videoLength (int): 0`
- `thumbLength (int): 0`
- `playLength (int): 0`

### 创建群聊 — `/api/creat_chat_room`

- **Method**: `POST`
- **URL path**: `/api/creat_chat_room`
- **完整 URL**: `http://127.0.0.1:19088/api/creat_chat_room`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxids": "wxid_3e9mll0g0fad21,wxid_8543785438012"
}
```

#### 响应示例

```json
{
  "baseResponse": {
    "ret": -2,
    "errMsg": {
      "String": "<e>\n<ShowType>1</ShowType>\n<Content><![CDATA[创建群聊失败]]></Content>\n<Url><![CDATA[]]></Url>\n<DispSec>30</DispSec>\n<Title><![CDATA[]]></Title>\n<Action>4</Action>\n<DelayConnSec>0</DelayConnSec>\n<Countdown>0</Countdown>\n<Ok><![CDATA[]]></Ok>\n<Cancel><![CDATA[]]></Cancel>\n<Icon>0</Icon>\n</e>\n"
    }
  },
  "topic": {},
  "pyinitial": {},
  "quanPin": {},
  "memberCount": 0,
  "chatRoomName": {},
  "imgBuf": {
    "iLen": 0
  }
}
```

#### 关键字段

请求:
- `wxids (str): 'wxid_3e9mll0g0fad21,wxid_8543785438012'`
响应:
- `baseResponse (object)`
- `baseResponse.ret (int): -2`
- `baseResponse.errMsg (object)`
- `baseResponse.errMsg.String (str): '<e>\n<ShowType>1</ShowType>\n<Content><![CDATA[创建群聊失败]]></Content>\n<Url><![CDATA[]]></Url>\n<DispSec>30</DispSec>\n<Ti...`
- `topic (object)`
- `pyinitial (object)`
- `quanPin (object)`
- `memberCount (int): 0`
- `chatRoomName (object)`
- `imgBuf (object)`
- `imgBuf.iLen (int): 0`

### 解密数据库 — `/api/decrypt_db`

- **Method**: `POST`
- **URL path**: `/api/decrypt_db`
- **完整 URL**: `http://127.0.0.1:19088/api/decrypt_db`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "dp_path": "E:\\新建文件夹\\xwechat_files\\wxid_ozyqateb85un22_15ff\\db_storage\\contact\\contact.db",
  "out_path": "E:\\新建文件夹\\xwechat_files\\wxid_ozyqateb85un22_15ff\\db_storage\\contact\\contact2.db",
  "key": "910e9301f30a4250aefaf9c51fb8e1646103c228ae1b4cc7899a8456762cdb16"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `dp_path (str): 'E:\\新建文件夹\\xwechat_files\\wxid_ozyqateb85un22_15ff\\db_storage\\contact\\contact.db'`
- `out_path (str): 'E:\\新建文件夹\\xwechat_files\\wxid_ozyqateb85un22_15ff\\db_storage\\contact\\contact2.db'`
- `key (str): '910e9301f30a4250aefaf9c51fb8e1646103c228ae1b4cc7899a8456762cdb16'`

### 移出黑名单 — `/api/del_black_user`

- **Method**: `POST`
- **URL path**: `/api/del_black_user`
- **完整 URL**: `http://127.0.0.1:19088/api/del_black_user`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "群wxid"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): '群wxid'`

### 删除好友 — `/api/del_contact`

- **Method**: `POST`
- **URL path**: `/api/del_contact`
- **完整 URL**: `http://127.0.0.1:19088/api/del_contact`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "wxid_8543785438012"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): 'wxid_8543785438012'`

### 删除标签 — `/api/del_label`

- **Method**: `POST`
- **URL path**: `/api/del_label`
- **完整 URL**: `http://127.0.0.1:19088/api/del_label`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "label_id": "33"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `label_id (str): '33'`

### 踢出群成员 — `/api/del_member_from_chat_room`

- **Method**: `POST`
- **URL path**: `/api/del_member_from_chat_room`
- **完整 URL**: `http://127.0.0.1:19088/api/del_member_from_chat_room`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid_list": "wxid_8543785438012",
  "room_id": "49767299448@chatroom"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid_list (str): 'wxid_8543785438012'`
- `room_id (str): '49767299448@chatroom'`

### 关闭消息免打扰 — `/api/del_mute_user`

- **Method**: `POST`
- **URL path**: `/api/del_mute_user`
- **完整 URL**: `http://127.0.0.1:19088/api/del_mute_user`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "群wxid"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): '群wxid'`

### 取消星标 — `/api/del_start`

- **Method**: `POST`
- **URL path**: `/api/del_start`
- **完整 URL**: `http://127.0.0.1:19088/api/del_start`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "群wxid"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): '群wxid'`

### 朋友圈图片/视频下载(测试中) — `/api/download_sns_media`

- **Method**: `POST`
- **URL path**: `/api/download_sns_media`
- **完整 URL**: `http://127.0.0.1:19088/api/download_sns_media`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "url": "http://szmmsns.qpic.cn/mmsns/kNygNStd3auSwbznAAtVHbEU7BHs4GpxjWPo2pFknCoJu1hTo6ER5f730CSzPHdvvaCGDBcLo0w/0?idx=1&token=WSEN6qDsKwV8A02w3onOGQYfxnkibdqSOkmHhZGNB4DHt3pnjm3WJX7cEMmZ4fmuaZSErly6ktkaTvHH6xQxHjA",
  "taskid": "15237862496303085072",
  "out": "d:\\sns.jpg"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `url (str): 'http://szmmsns.qpic.cn/mmsns/kNygNStd3auSwbznAAtVHbEU7BHs4GpxjWPo2pFknCoJu1hTo6ER5f730CSzPHdvvaCGDBcLo0w/0?idx=1&token=...`
- `taskid (str): '15237862496303085072'`
- `out (str): 'd:\\sns.jpg'`

### 下载视频 — `/api/download_video`

- **Method**: `POST`
- **URL path**: `/api/download_video`
- **完整 URL**: `http://127.0.0.1:19088/api/download_video`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "total_len": 68264760,
  "NewMsgId": 150973212120165915,
  "path": "d:\\121.mp4",
  "MsgId": 425004325
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `total_len (int): 68264760`
- `NewMsgId (int): 150973212120165915`
- `path (str): 'd:\\121.mp4'`
- `MsgId (int): 425004325`

### 下载语音 — `/api/download_voice`

- **Method**: `POST`
- **URL path**: `/api/download_voice`
- **完整 URL**: `http://127.0.0.1:19088/api/download_voice`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "newMsgId": "3823231950088215754",
  "length": "70541",
  "MsgId": "1669230954",
  "path": "d:\\7054.slik"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `newMsgId (str): '3823231950088215754'`
- `length (str): '70541'`
- `MsgId (str): '1669230954'`
- `path (str): 'd:\\7054.slik'`

### 下载企业文件/图片 — `/api/download_wxwork_file`

- **Method**: `POST`
- **URL path**: `/api/download_wxwork_file`
- **完整 URL**: `http://127.0.0.1:19088/api/download_wxwork_file`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "url": "https://wwfile.work.weixin.qq.com/cgi-bin/download?f=306902010204623060020100020484b49bcd02030f42420204d8e3039902046918218c042432666564396435372d653061352d343161362d383834622d3032373266313331363737340201000203025da00410bf9d463b99e815ac1faac255147ac6200201020201000400&t=F2DB20A4ED766AF16ED0136F9EDDBFD0CAF0C173759C8AEEDF158D782C26CA6B5608128A09258696D5BCCCA6E351BF54980F73B8F3A1AA21BB42BEE4537B0D7BEB3EAA49083AB2CF9A507763DBB3D0A135FDF699777AC6BB41232FBEEC8194B0DC3FDA4DBB29574F14907D76209048C9324C5BA25B679427A52CBFD93DDFD97955911622C8C5C714BFC3D82230F26DBEE7BAA2941A197373FD9175E13D6229BEF81C8228B28B8EE3D35EC6A9242018DE6CA71F46E4C97A40F86DBE429A3CF8C70DA8EB79693C78ABE048A47E08B621305D84E7F464445356825C734D37AE137F80764005FA5249F01EEC93BCB0E84849BFC61AD577F75856E6E3950E0DFA6BFC3F9B67C85A6D8BB750041753BA9D8CA0&p=3",
  "key": "0efef41ef3567c6e6be689f5b951bbac",
  "out": "d:\\揽收6.jpg",
  "authkey": "0A2B6F4E2D4D77415141414141415941646C674E577938334B6E34456C55367643703940696D2E7778776F726B10F392F231"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `url (str): 'https://wwfile.work.weixin.qq.com/cgi-bin/download?f=306902010204623060020100020484b49bcd02030f42420204d8e3039902046918...`
- `key (str): '0efef41ef3567c6e6be689f5b951bbac'`
- `out (str): 'd:\\揽收6.jpg'`
- `authkey (str): '0A2B6F4E2D4D77415141414141415941646C674E577938334B6E34456C55367643703940696D2E7778776F726B10F392F231'`

### 同意群聊邀请 — `/api/enter_room`

- **Method**: `POST`
- **URL path**: `/api/enter_room`
- **完整 URL**: `http://127.0.0.1:19088/api/enter_room`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "url": "https://weixin.qq.com/g/AQYAAHTnjQ-tHLCRwz7OEsG40PUGTTWtGIYAaE9-09DtKqsJ-_icjSkR72_N_D2P"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `url (str): 'https://weixin.qq.com/g/AQYAAHTnjQ-tHLCRwz7OEsG40PUGTTWtGIYAaE9-09DtKqsJ-_icjSkR72_N_D2P'`

### 折叠群聊或者个人 — `/api/folding`

- **Method**: `POST`
- **URL path**: `/api/folding`
- **完整 URL**: `http://127.0.0.1:19088/api/folding`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "roomId": "18402658081@chatroom"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `roomId (str): '18402658081@chatroom'`

### 群聊获取A8key — `/api/get_a8key`

- **Method**: `POST`
- **URL path**: `/api/get_a8key`
- **完整 URL**: `http://127.0.0.1:19088/api/get_a8key`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "url": "https://support.weixin.qq.com/cgi-bin/mmsupport-bin/addchatroombyinvite?ticket=AwfZ4kSJ9P2FbmFK6LPrpg%3D%3D",
  "urlType": "0",
  "scene": "0"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `url (str): 'https://support.weixin.qq.com/cgi-bin/mmsupport-bin/addchatroombyinvite?ticket=AwfZ4kSJ9P2FbmFK6LPrpg%3D%3D'`
- `urlType (str): '0'`
- `scene (str): '0'`

### 获取所有群聊的成员列表,群头像,昵称等数据 — `/api/get_all_room_detail`

- **Method**: `POST`
- **URL path**: `/api/get_all_room_detail`
- **完整 URL**: `http://127.0.0.1:19088/api/get_all_room_detail`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 获取cdn信息 — `/api/get_cdn_info`

- **Method**: `POST`
- **URL path**: `/api/get_cdn_info`
- **完整 URL**: `http://127.0.0.1:19088/api/get_cdn_info`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 获取群公告 — `/api/get_chatroom_announcement`

- **Method**: `POST`
- **URL path**: `/api/get_chatroom_announcement`
- **完整 URL**: `http://127.0.0.1:19088/api/get_chatroom_announcement`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "roomId": "38879414299@chatroom"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `roomId (str): '38879414299@chatroom'`

### 获取群详情缓存 — `/api/get_chatroom_detail_cache`

- **Method**: `POST`
- **URL path**: `/api/get_chatroom_detail_cache`
- **完整 URL**: `http://127.0.0.1:19088/api/get_chatroom_detail_cache`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "roomId": "18402658081@chatroom"
}
```

#### 响应示例

```json
{
  "baseResponse": {
    "ret": 0,
    "errMsg": {}
  },
  "chatroomUserName": "49767299448@chatroom",
  "serverVersion": 10004,
  "newChatroomData": {
    "memberCount": 2,
    "chatRoomMember": [
      {
        "userName": "wxid1",
        "nickName": "隔壁老陈",
        "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/F3mNcrM9JiccgM56eLOzD4aiaZMibW4efYpAUMf0HuV9ricVBtdc19smEhdO26tBJ0IwqZmsANDHCf3rJVpic0NWrgPHXbiawI6vnZlV4hibibGvqb7hsTkr7fBYfO5Ss7LsksvF/0",
        "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/F3mNcrM9JiccgM56eLOzD4aiaZMibW4efYpAUMf0HuV9ricVBtdc19smEhdO26tBJ0IwqZmsANDHCf3rJVpic0NWrgPHXbiawI6vnZlV4hibibGvqb7hsTkr7fBYfO5Ss7LsksvF/132",
        "chatroomMemberFlag": 1,
        "status": 0
      },
      {
        "userName": "wxid2",
        "nickName": "不必",
        "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/UfAy94vgEmryCeyWxYAa1moicl0Tia1RnDzIDTHxxQZNKC7rjBtdRsezeL0B7sMicEicUILaxxic8QiazNlaDqRZD8vn2GrL4RIjhLuoAlfcPCPJLjQiaYe6ibn28oAdEwpsuh5W/0",
        "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/UfAy94vgEmryCeyWxYAa1moicl0Tia1RnDzIDTHxxQZNKC7rjBtdRsezeL0B7sMicEicUILaxxic8QiazNlaDqRZD8vn2GrL4RIjhLuoAlfcPCPJLjQiaYe6ibn28oAdEwpsuh5W/132",
        "chatroomMemberFlag": 1,
        "inviterUserName": "wxid_ozyqateb85un22",
        "status": 0,
        "addChatRoomSceneNewXml": "<sysmsg type=\"ChatRoomMemberTraceBack\">\n\t<ChatRoomMemberTraceBack>\n\t\t<text><![CDATA[$inviter_username$邀请进群]]></text>\n\t\t<link>\n\t\t\t<username><![CDATA[wxid_ozyqateb85un22]]></username>\n\t\t</link>\n\t</ChatRoomMemberTraceBack>\n</sysmsg>\n"
      }
    ],
    "infoMask": 0,
    "chatRoomUserName": {},
    "watchMemberCount": 0
  },
  "chatRoomOwner": "wxid",
  "allMemberCount": 2,
  "allMemberUserNameList": [
    {
      "String": "wxid1"
    },
    {
      "String": "wxid2"
    }
  ],
  "adminCount": 0
}
```

#### 关键字段

请求:
- `roomId (str): '18402658081@chatroom'`
响应:
- `baseResponse (object)`
- `baseResponse.ret (int): 0`
- `baseResponse.errMsg (object)`
- `chatroomUserName (str): '49767299448@chatroom'`
- `serverVersion (int): 10004`
- `newChatroomData (object)`
- `newChatroomData.memberCount (int): 2`
- `newChatroomData.chatRoomMember (array, len=2)`
- `newChatroomData.chatRoomMember[].userName (str): 'wxid1'`
- `newChatroomData.chatRoomMember[].nickName (str): '隔壁老陈'`
- `newChatroomData.chatRoomMember[].bigHeadImgUrl (str): 'https://wx.qlogo.cn/mmhead/ver_1/F3mNcrM9JiccgM56eLOzD4aiaZMibW4efYpAUMf0HuV9ricVBtdc19smEhdO26tBJ0IwqZmsANDHCf3rJVpic0...`
- `newChatroomData.chatRoomMember[].smallHeadImgUrl (str): 'https://wx.qlogo.cn/mmhead/ver_1/F3mNcrM9JiccgM56eLOzD4aiaZMibW4efYpAUMf0HuV9ricVBtdc19smEhdO26tBJ0IwqZmsANDHCf3rJVpic0...`
- `newChatroomData.chatRoomMember[].chatroomMemberFlag (int): 1`
- `newChatroomData.chatRoomMember[].status (int): 0`
- `newChatroomData.infoMask (int): 0`
- `newChatroomData.chatRoomUserName (object)`
- `newChatroomData.watchMemberCount (int): 0`
- `chatRoomOwner (str): 'wxid'`
- `allMemberCount (int): 2`
- `allMemberUserNameList (array, len=2)`
- `allMemberUserNameList[].String (str): 'wxid1'`
- `adminCount (int): 0`

### 获取群详情 — `/api/get_chatroom_info`

- **Method**: `POST`
- **URL path**: `/api/get_chatroom_info`
- **完整 URL**: `http://127.0.0.1:19088/api/get_chatroom_info`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "roomId": "45220347292@chatroom"
}
```

#### 响应示例

```json
{
  "baseResponse": {
    "ret": -2,
    "errMsg": {}
  },
  "chatRoomInfoVersion": 0,
  "announcementPublishTime": 0,
  "chatRoomStatus": 0,
  "chatRoomBusinessType": "0",
  "roomTools": {
    "roomToolsWxAppCount": 0
  },
  "roomBindAppList": {
    "roomBindAppListCount": 0
  },
  "spamStatus": 0,
  "finderInfo": {
    "iLen": 0
  },
  "topMsgInfo": {
    "iLen": 0
  }
}
```

#### 关键字段

请求:
- `roomId (str): '45220347292@chatroom'`
响应:
- `baseResponse (object)`
- `baseResponse.ret (int): -2`
- `baseResponse.errMsg (object)`
- `chatRoomInfoVersion (int): 0`
- `announcementPublishTime (int): 0`
- `chatRoomStatus (int): 0`
- `chatRoomBusinessType (str): '0'`
- `roomTools (object)`
- `roomTools.roomToolsWxAppCount (int): 0`
- `roomBindAppList (object)`
- `roomBindAppList.roomBindAppListCount (int): 0`
- `spamStatus (int): 0`
- `finderInfo (object)`
- `finderInfo.iLen (int): 0`
- `topMsgInfo (object)`
- `topMsgInfo.iLen (int): 0`

### 获取配置文件保存目录 — `/api/get_config_path`

- **Method**: `POST`
- **URL path**: `/api/get_config_path`
- **完整 URL**: `http://127.0.0.1:19088/api/get_config_path`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 获取好友最新资料(网络获取) — `/api/get_contact`

- **Method**: `POST`
- **URL path**: `/api/get_contact`
- **完整 URL**: `http://127.0.0.1:19088/api/get_contact`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "wxid_rj8cjqdrg5cl22"
}
```

#### 响应示例

```json
{
  "baseResponse": {
    "ret": 0,
    "errMsg": {}
  },
  "contactCount": 1,
  "contactList": [
    {
      "userName": {
        "String": "filehelper"
      },
      "nickName": {
        "String": "文件传输助手"
      },
      "pyinitial": {
        "String": "WJCSZS"
      },
      "quanPin": {
        "String": "wenjianchuanshuzhushou"
      },
      "sex": 0,
      "imgBuf": {
        "iLen": 0
      },
      "bitMask": 4294967295,
      "bitVal": 3,
      "imgFlag": 3,
      "remark": {},
      "remarkPyinitial": {},
      "remarkQuanPin": {},
      "contactType": 0,
      "roomInfoCount": 0,
      "domainList": {},
      "chatRoomNotify": 0,
      "addContactScene": 0,
      "personalCard": 0,
      "hasWeiXinHdHeadImg": 1,
      "verifyFlag": 0,
      "level": 0,
      "source": 6,
      "weiboFlag": 0,
      "albumStyle": 0,
      "albumFlag": 0,
      "snsUserInfo": {
        "snsFlag": 0,
        "snsBgobjectId": "0",
        "snsFlagEx": 16,
        "snsPrivacyRecent": 0
      },
      "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/fKufuRnT26ianqvqMDmkqSGb1nyezCStqvyHhOL5PLMRqvM8UfxYD4EOXibox1oTsaNLjY8cEk7EJculnbH9cm9KOze8IFWI5Aoibc4umTxPiayibDfXibvfjoA4mjroHJUtVf/0",
      "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/fKufuRnT26ianqvqMDmkqSGb1nyezCStqvyHhOL5PLMRqvM8UfxYD4EOXibox1oTsaNLjY8cEk7EJculnbH9cm9KOze8IFWI5Aoibc4umTxPiayibDfXibvfjoA4mjroHJUtVf/132",
      "customizedInfo": {
        "brandFlag": 0
      },
      "headImgMd5": "860baf36d77682daa9ce1210be61374e",
      "encryptUserName": "v3_020b3826fd03010000000000283d02027bc00e000000501ea9a3dba12f95f6b60a0536a1adb6f580631340234a6fd1c318fdb3566c7e0f6dd453b321e7729cd107b0e7f2e215e554ae6e2e881d8a917e9584@stranger",
      "additionalContactList": {
        "linkedinContactItem": {}
      },
      "chatroomVersion": 0,
      "chatroomMaxCount": 0,
      "chatroomAccessType": 0,
      "newChatroomData": {
        "memberCount": 0,
        "infoMask": 0,
        "chatRoomUserName": {},
        "watchMemberCount": 0
      },
      "deleteFlag": 0,
      "phoneNumListInfo": {
        "count": 0
      },
      "chatroomInfoVersion": 0,
      "deleteContactScene": 0,
      "chatroomStatus": 0,
      "extFlag": 0,
      "chatRoomBusinessType": "0",
      "friendUserName": "filehelper",
      "textStatusFlag": 2,
      "ringBackSetting": {
        "finderObjectId": "0",
        "startTs": 0,
        "endTs": 0
      },
      "bitMask2": "18446744073709551615",
      "bitValue2": "0",
      "contactExtraInfoBuf": {
        "iLen": 0
      },
      "isInChatRoom": 0,
      "eraseChatRoomMemberData": 0
    }
  ],
  "ret": [
    0
  ],
  "verifyUserValidTicketList": {
    "username": "filehelper",
    "antispamticket": "v4_000b708f0b0400000100000000007216f55900af00be97e0d58baf681000000050ded0b020927e3c97896a09d47e6e9e23b2464fed6bdfd91729d2159eef78ffea979d110f34e73a4d6d1247cc360645720f1e8928b6cb80404c08635111878eeafc925805736f6382dc8cc062d71929b3878d61500db77779d534021191ba6b6aaeab78f8357452@stranger"
  }
}
```

#### 关键字段

请求:
- `wxid (str): 'wxid_rj8cjqdrg5cl22'`
响应:
- `baseResponse (object)`
- `baseResponse.ret (int): 0`
- `baseResponse.errMsg (object)`
- `contactCount (int): 1`
- `contactList (array, len=1)`
- `contactList[].userName (object)`
- `contactList[].userName.String (str): 'filehelper'`
- `contactList[].nickName (object)`
- `contactList[].nickName.String (str): '文件传输助手'`
- `contactList[].pyinitial (object)`
- `contactList[].pyinitial.String (str): 'WJCSZS'`
- `contactList[].quanPin (object)`
- `contactList[].quanPin.String (str): 'wenjianchuanshuzhushou'`
- `contactList[].sex (int): 0`
- `contactList[].imgBuf (object)`
- `contactList[].imgBuf.iLen (int): 0`
- `contactList[].bitMask (int): 4294967295`
- `contactList[].bitVal (int): 3`
- `contactList[].imgFlag (int): 3`
- `contactList[].remark (object)`
- `contactList[].remarkPyinitial (object)`
- `contactList[].remarkQuanPin (object)`
- `contactList[].contactType (int): 0`
- `contactList[].roomInfoCount (int): 0`
- `contactList[].domainList (object)`
- `contactList[].chatRoomNotify (int): 0`
- `contactList[].addContactScene (int): 0`
- `contactList[].personalCard (int): 0`
- `contactList[].hasWeiXinHdHeadImg (int): 1`
- `contactList[].verifyFlag (int): 0`
- `contactList[].level (int): 0`
- `contactList[].source (int): 6`
- `contactList[].weiboFlag (int): 0`
- `contactList[].albumStyle (int): 0`
- `contactList[].albumFlag (int): 0`
- `contactList[].snsUserInfo (object)`
- `contactList[].snsUserInfo.snsFlag (int): 0`
- `contactList[].snsUserInfo.snsBgobjectId (str): '0'`
- `contactList[].snsUserInfo.snsFlagEx (int): 16`
- `contactList[].snsUserInfo.snsPrivacyRecent (int): 0`
- `contactList[].bigHeadImgUrl (str): 'https://wx.qlogo.cn/mmhead/ver_1/fKufuRnT26ianqvqMDmkqSGb1nyezCStqvyHhOL5PLMRqvM8UfxYD4EOXibox1oTsaNLjY8cEk7EJculnbH9cm...`
- `contactList[].smallHeadImgUrl (str): 'https://wx.qlogo.cn/mmhead/ver_1/fKufuRnT26ianqvqMDmkqSGb1nyezCStqvyHhOL5PLMRqvM8UfxYD4EOXibox1oTsaNLjY8cEk7EJculnbH9cm...`
- `contactList[].customizedInfo (object)`
- `contactList[].customizedInfo.brandFlag (int): 0`
- `contactList[].headImgMd5 (str): '860baf36d77682daa9ce1210be61374e'`
- `contactList[].encryptUserName (str): 'v3_020b3826fd03010000000000283d02027bc00e000000501ea9a3dba12f95f6b60a0536a1adb6f580631340234a6fd1c318fdb3566c7e0f6dd453...`
- `contactList[].additionalContactList (object)`
- `contactList[].additionalContactList.linkedinContactItem (object)`
- `contactList[].chatroomVersion (int): 0`
- `contactList[].chatroomMaxCount (int): 0`
- `contactList[].chatroomAccessType (int): 0`
- `contactList[].newChatroomData (object)`
- `contactList[].newChatroomData.memberCount (int): 0`
- `contactList[].newChatroomData.infoMask (int): 0`
- `contactList[].newChatroomData.chatRoomUserName (object)`
- `contactList[].newChatroomData.watchMemberCount (int): 0`
- `contactList[].deleteFlag (int): 0`
- `contactList[].phoneNumListInfo (object)`
- `contactList[].phoneNumListInfo.count (int): 0`
- `contactList[].chatroomInfoVersion (int): 0`

### 快速查找好友资料(非常快) — `/api/get_contact_fast`

- **Method**: `POST`
- **URL path**: `/api/get_contact_fast`
- **完整 URL**: `http://127.0.0.1:19088/api/get_contact_fast`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "wxid_rdpo01enuad821"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): 'wxid_rdpo01enuad821'`

### 获取数据库句柄 — `/api/get_db_handle`

- **Method**: `POST`
- **URL path**: `/api/get_db_handle`
- **完整 URL**: `http://127.0.0.1:19088/api/get_db_handle`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 获取收藏列表 — `/api/get_favs`

- **Method**: `POST`
- **URL path**: `/api/get_favs`
- **完整 URL**: `http://127.0.0.1:19088/api/get_favs`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 获取所有好友的wxid(网络长耗时) — `/api/get_friend_wxids`

- **Method**: `POST`
- **URL path**: `/api/get_friend_wxids`
- **完整 URL**: `http://127.0.0.1:19088/api/get_friend_wxids`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 查询群成员信息 — `/api/get_group_member_contact`

- **Method**: `POST`
- **URL path**: `/api/get_group_member_contact`
- **完整 URL**: `http://127.0.0.1:19088/api/get_group_member_contact`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "wxid_8zggbw1yo5ib22",
  "roomId": "18402658081@chatroom"
}
```

#### 响应示例

```json
{
  "baseResponse": {
    "ret": 0,
    "errMsg": {}
  },
  "contactCount": 1,
  "contactList": [
    {
      "userName": {
        "String": "群成员的wxid"
      },
      "nickName": {
        "String": "不必"
      },
      "pyinitial": {
        "String": "BB"
      },
      "quanPin": {
        "String": "bubi"
      },
      "sex": 1,
      "imgBuf": {
        "iLen": 0
      },
      "bitMask": 4294967295,
      "bitVal": 3,
      "imgFlag": 3,
      "remark": {
        "String": "9763"
      },
      "remarkPyinitial": {
        "String": "9763"
      },
      "remarkQuanPin": {
        "String": "9763"
      },
      "contactType": 0,
      "roomInfoCount": 0,
      "domainList": {},
      "chatRoomNotify": 0,
      "addContactScene": 0,
      "province": "Zhejiang",
      "city": "Hangzhou",
      "signature": "特别害怕失去很熟悉的人",
      "personalCard": 1,
      "hasWeiXinHdHeadImg": 1,
      "verifyFlag": 0,
      "level": 0,
      "source": 3,
      "alias": "jryswygq",
      "weiboFlag": 0,
      "albumStyle": 0,
      "albumFlag": 0,
      "snsUserInfo": {
        "snsFlag": 1,
        "snsBgimgId": "http://shmmsns.qpic.cn/mmsns/qcKhiayu3sNlcQLCwMDHfX38h9o7pCHkLtgBam5F6IgeABvibBTTib1bXiaVjCPZzEYTtVsbvian0EIk/0",
        "snsBgobjectId": "14693141287014765172",
        "snsFlagEx": 7297,
        "snsPrivacyRecent": 72
      },
      "country": "CN",
      "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/hic1c1goZbfmqcPfk2UllbdDGA5TC4ZwB7uINxase77pCZX2OU2MicGBw1ia3jBHKLPnbcSoySrCfsul8DjQBwAAAyPTA5Th5yibtZNBuxZhR8KIWvRqTGXNEQgticdEF61SX/0",
      "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/hic1c1goZbfmqcPfk2UllbdDGA5TC4ZwB7uINxase77pCZX2OU2MicGBw1ia3jBHKLPnbcSoySrCfsul8DjQBwAAAyPTA5Th5yibtZNBuxZhR8KIWvRqTGXNEQgticdEF61SX/132",
      "myBrandList": "<brandlist></brandlist>",
      "customizedInfo": {
        "brandFlag": 0
      },
      "headImgMd5": "00a7b20ff61ed9356a1221a6e265134d",
      "encryptUserName": "V3",
      "additionalContactList": {
        "linkedinContactItem": {}
      },
      "chatroomVersion": 0,
      "chatroomMaxCount": 0,
      "chatroomAccessType": 0,
      "newChatroomData": {
        "memberCount": 0,
        "infoMask": 0,
        "chatRoomUserName": {
          "String": "49767299448@chatroom"
        },
        "watchMemberCount": 0
      },
      "deleteFlag": 0,
      "phoneNumListInfo": {
        "count": 0
      },
      "chatroomInfoVersion": 0,
      "deleteContactScene": 0,
      "chatroomStatus": 0,
      "extFlag": 0,
      "chatRoomBusinessType": "0",
      "friendUserName": "群成员的wxid",
      "textStatusFlag": 2,
      "ringBackSetting": {
        "finderObjectId": "0",
        "startTs": 0,
        "endTs": 0
      },
      "bitMask2": "18446744073709551615",
      "bitValue2": "256",
      "contactExtraInfoBuf": {
        "iLen": 0
      },
      "isInChatRoom": 0,
      "eraseChatRoomMemberData": 0
    }
  ],
  "ret": [
    0
  ],
  "verifyUserValidTicketList": {
    "username": "群成员的wxid",
    "antispamticket": "V4"
  }
}
```

#### 关键字段

请求:
- `wxid (str): 'wxid_8zggbw1yo5ib22'`
- `roomId (str): '18402658081@chatroom'`
响应:
- `baseResponse (object)`
- `baseResponse.ret (int): 0`
- `baseResponse.errMsg (object)`
- `contactCount (int): 1`
- `contactList (array, len=1)`
- `contactList[].userName (object)`
- `contactList[].userName.String (str): '群成员的wxid'`
- `contactList[].nickName (object)`
- `contactList[].nickName.String (str): '不必'`
- `contactList[].pyinitial (object)`
- `contactList[].pyinitial.String (str): 'BB'`
- `contactList[].quanPin (object)`
- `contactList[].quanPin.String (str): 'bubi'`
- `contactList[].sex (int): 1`
- `contactList[].imgBuf (object)`
- `contactList[].imgBuf.iLen (int): 0`
- `contactList[].bitMask (int): 4294967295`
- `contactList[].bitVal (int): 3`
- `contactList[].imgFlag (int): 3`
- `contactList[].remark (object)`
- `contactList[].remark.String (str): '9763'`
- `contactList[].remarkPyinitial (object)`
- `contactList[].remarkPyinitial.String (str): '9763'`
- `contactList[].remarkQuanPin (object)`
- `contactList[].remarkQuanPin.String (str): '9763'`
- `contactList[].contactType (int): 0`
- `contactList[].roomInfoCount (int): 0`
- `contactList[].domainList (object)`
- `contactList[].chatRoomNotify (int): 0`
- `contactList[].addContactScene (int): 0`
- `contactList[].province (str): 'Zhejiang'`
- `contactList[].city (str): 'Hangzhou'`
- `contactList[].signature (str): '特别害怕失去很熟悉的人'`
- `contactList[].personalCard (int): 1`
- `contactList[].hasWeiXinHdHeadImg (int): 1`
- `contactList[].verifyFlag (int): 0`
- `contactList[].level (int): 0`
- `contactList[].source (int): 3`
- `contactList[].alias (str): 'jryswygq'`
- `contactList[].weiboFlag (int): 0`
- `contactList[].albumStyle (int): 0`
- `contactList[].albumFlag (int): 0`
- `contactList[].snsUserInfo (object)`
- `contactList[].snsUserInfo.snsFlag (int): 1`
- `contactList[].snsUserInfo.snsBgimgId (str): 'http://shmmsns.qpic.cn/mmsns/qcKhiayu3sNlcQLCwMDHfX38h9o7pCHkLtgBam5F6IgeABvibBTTib1bXiaVjCPZzEYTtVsbvian0EIk/0'`
- `contactList[].snsUserInfo.snsBgobjectId (str): '14693141287014765172'`
- `contactList[].snsUserInfo.snsFlagEx (int): 7297`
- `contactList[].snsUserInfo.snsPrivacyRecent (int): 72`
- `contactList[].country (str): 'CN'`
- `contactList[].bigHeadImgUrl (str): 'https://wx.qlogo.cn/mmhead/ver_1/hic1c1goZbfmqcPfk2UllbdDGA5TC4ZwB7uINxase77pCZX2OU2MicGBw1ia3jBHKLPnbcSoySrCfsul8DjQBw...`
- `contactList[].smallHeadImgUrl (str): 'https://wx.qlogo.cn/mmhead/ver_1/hic1c1goZbfmqcPfk2UllbdDGA5TC4ZwB7uINxase77pCZX2OU2MicGBw1ia3jBHKLPnbcSoySrCfsul8DjQBw...`
- `contactList[].myBrandList (str): '<brandlist></brandlist>'`
- `contactList[].customizedInfo (object)`
- `contactList[].customizedInfo.brandFlag (int): 0`
- `contactList[].headImgMd5 (str): '00a7b20ff61ed9356a1221a6e265134d'`
- `contactList[].encryptUserName (str): 'V3'`
- `contactList[].additionalContactList (object)`
- `contactList[].additionalContactList.linkedinContactItem (object)`
- `contactList[].chatroomVersion (int): 0`
- `contactList[].chatroomMaxCount (int): 0`

### 获取群成员数据 — `/api/get_group_memeber_info`

- **Method**: `POST`
- **URL path**: `/api/get_group_memeber_info`
- **完整 URL**: `http://127.0.0.1:19088/api/get_group_memeber_info`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "roomId": "49767299448@chatroom",
  "memeberId": "wxid_bktzp6cv7wxe12"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `roomId (str): '49767299448@chatroom'`
- `memeberId (str): 'wxid_bktzp6cv7wxe12'`

### 获取群成员数据(简要不包含头像) — `/api/get_groupmember_bysql`

- **Method**: `POST`
- **URL path**: `/api/get_groupmember_bysql`
- **完整 URL**: `http://127.0.0.1:19088/api/get_groupmember_bysql`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "roomId": "18402658081@chatroom"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `roomId (str): '18402658081@chatroom'`

### 获取标签列表 — `/api/get_label_lists`

- **Method**: `POST`
- **URL path**: `/api/get_label_lists`
- **完整 URL**: `http://127.0.0.1:19088/api/get_label_lists`
- **Content-Type**: `application/json`

#### 请求示例

```json
{}
```

#### 响应示例

```json
{
  "baseResponse": {
    "ret": 0,
    "errMsg": {}
  },
  "labelCount": 7,
  "labelPairList": [
    {
      "labelName": "取完钱",
      "labelId": 2
    },
    {
      "labelName": "1",
      "labelId": 7
    },
    {
      "labelName": "2",
      "labelId": 8
    },
    {
      "labelName": "777777777777",
      "labelId": 9
    },
    {
      "labelName": "标签名字6667",
      "labelId": 11
    },
    {
      "labelName": "6666666666666666",
      "labelId": 6
    },
    {
      "labelName": "15454454545",
      "labelId": 10
    }
  ]
}
```

#### 关键字段

响应:
- `baseResponse (object)`
- `baseResponse.ret (int): 0`
- `baseResponse.errMsg (object)`
- `labelCount (int): 7`
- `labelPairList (array, len=7)`
- `labelPairList[].labelName (str): '取完钱'`
- `labelPairList[].labelId (int): 2`

### 获取附近人 — `/api/get_lbs_friend`

- **Method**: `POST`
- **URL path**: `/api/get_lbs_friend`
- **完整 URL**: `http://127.0.0.1:19088/api/get_lbs_friend`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "longitude": "120.24646699999994",
  "latitude": "30.197153999999998"
}
```

#### 响应示例

```json
{
  "baseResponse": {
    "ret": "返回码，0 表示成功",
    "errMsg": "错误信息"
  },
  "contactCount": "联系人数量",
  "contactList": [
    {
      "userName": "用户名（可能是加密后的唯一标识）",
      "nickName": "昵称",
      "province": "省份",
      "city": "城市",
      "signature": "个性签名",
      "distance": "距离（与自己的物理距离）",
      "sex": "性别（1=男，2=女，0=未知）",
      "imgStatus": "头像状态",
      "verifyFlag": "认证标志",
      "weiboFlag": "是否绑定微博",
      "headImgVersion": "头像版本号",
      "snsUserInfo": {
        "snsFlag": "朋友圈标志（是否开启朋友圈）",
        "snsBgimgId": "朋友圈背景图链接",
        "snsBgobjectId": "朋友圈背景图对象ID",
        "snsFlagEx": "朋友圈扩展标志位",
        "snsPrivacyRecent": "朋友圈隐私设置"
      },
      "country": "国家",
      "bigHeadImgUrl": "大头像 URL",
      "smallHeadImgUrl": "小头像 URL",
      "customizedInfo": {
        "brandFlag": "品牌标志（公众号/企业号相关）"
      },
      "antispamTicket": "防骚扰 ticket（陌生人校验用）",
      "flag": "标志位",
      "finderFlag": "视频号标志"
    }
  ],
  "state": "状态码",
  "flushTime": "刷新时间（秒）",
  "isShowRoom": "是否显示聊天室",
  "roomMemberCount": "聊天室成员数量"
}
```

#### 关键字段

请求:
- `longitude (str): '120.24646699999994'`
- `latitude (str): '30.197153999999998'`
响应:
- `baseResponse (object)`
- `baseResponse.ret (str): '返回码，0 表示成功'`
- `baseResponse.errMsg (str): '错误信息'`
- `contactCount (str): '联系人数量'`
- `contactList (array, len=1)`
- `contactList[].userName (str): '用户名（可能是加密后的唯一标识）'`
- `contactList[].nickName (str): '昵称'`
- `contactList[].province (str): '省份'`
- `contactList[].city (str): '城市'`
- `contactList[].signature (str): '个性签名'`
- `contactList[].distance (str): '距离（与自己的物理距离）'`
- `contactList[].sex (str): '性别（1=男，2=女，0=未知）'`
- `contactList[].imgStatus (str): '头像状态'`
- `contactList[].verifyFlag (str): '认证标志'`
- `contactList[].weiboFlag (str): '是否绑定微博'`
- `contactList[].headImgVersion (str): '头像版本号'`
- `contactList[].snsUserInfo (object)`
- `contactList[].snsUserInfo.snsFlag (str): '朋友圈标志（是否开启朋友圈）'`
- `contactList[].snsUserInfo.snsBgimgId (str): '朋友圈背景图链接'`
- `contactList[].snsUserInfo.snsBgobjectId (str): '朋友圈背景图对象ID'`
- `contactList[].snsUserInfo.snsFlagEx (str): '朋友圈扩展标志位'`
- `contactList[].snsUserInfo.snsPrivacyRecent (str): '朋友圈隐私设置'`
- `contactList[].country (str): '国家'`
- `contactList[].bigHeadImgUrl (str): '大头像 URL'`
- `contactList[].smallHeadImgUrl (str): '小头像 URL'`
- `contactList[].customizedInfo (object)`
- `contactList[].customizedInfo.brandFlag (str): '品牌标志（公众号/企业号相关）'`
- `contactList[].antispamTicket (str): '防骚扰 ticket（陌生人校验用）'`
- `contactList[].flag (str): '标志位'`
- `contactList[].finderFlag (str): '视频号标志'`
- `state (str): '状态码'`
- `flushTime (str): '刷新时间（秒）'`
- `isShowRoom (str): '是否显示聊天室'`
- `roomMemberCount (str): '聊天室成员数量'`

### 获取好友二维码 — `/api/get_my_qrocde`

- **Method**: `POST`
- **URL path**: `/api/get_my_qrocde`
- **完整 URL**: `http://127.0.0.1:19088/api/get_my_qrocde`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "45220347292@chatroom",
  "opcode": "0",
  "style": "7",
  "info": "说明1-8 style都是风格 你们可以自己看看"
}
```

#### 响应示例

```json
{
  "baseResponse": {
    "ret": -2,
    "errMsg": {}
  },
  "qrcode": {
    "iLen": 0
  },
  "style": 0,
  "dominatorColorSize": 0
}
```

#### 关键字段

请求:
- `wxid (str): '45220347292@chatroom'`
- `opcode (str): '0'`
- `style (str): '7'`
- `info (str): '说明1-8 style都是风格 你们可以自己看看'`
响应:
- `baseResponse (object)`
- `baseResponse.ret (int): -2`
- `baseResponse.errMsg (object)`
- `qrcode (object)`
- `qrcode.iLen (int): 0`
- `style (int): 0`
- `dominatorColorSize (int): 0`

### 获取个人资料缓存 — `/api/get_profile_cache`

- **Method**: `POST`
- **URL path**: `/api/get_profile_cache`
- **完整 URL**: `http://127.0.0.1:19088/api/get_profile_cache`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

```json
{
  "baseResponse": {
    "ret": 0,
    "errMsg": {}
  },
  "userInfo": {
    "bitFlag": 190,
    "userName": {
      "String": "你的wxid"
    },
    "nickName": {
      "String": "你的昵称"
    },
    "bindUin": 0,
    "bindEmail": {},
    "bindMobile": {
      "String": "你的手机号"
    },
    "status": 234021,
    "imgLen": 0,
    "sex": 2,
    "province": "Guangdong",
    "city": "Zhuhai",
    "signature": "随便记住我 然后把我忘了吧",
    "personalCard": 1,
    "disturbSetting": {
      "nightSetting": 0,
      "nightTime": {
        "beginTime": 0,
        "endTime": 0
      },
      "allDaySetting": 0,
      "allDayTime": {
        "beginTime": 0,
        "endTime": 0
      }
    },
    "pluginFlag": 16939169,
    "verifyFlag": 0,
    "point": 325,
    "experience": 1476,
    "level": 5,
    "levelLowExp": 1401,
    "levelHighExp": 2000,
    "pluginSwitch": 41984,
    "gmailList": {
      "count": 0
    },
    "alias": "hbbhcds",
    "weiboFlag": 0,
    "faceBookFlag": 0,
    "fbuserId": "0",
    "albumStyle": 0,
    "albumFlag": 0,
    "txnewsCategory": 0,
    "country": "CN"
  },
  "userInfoExt": {
    "snsUserInfo": {
      "snsFlag": 1,
      "snsBgimgId": "http://shmmsns.qpic.cn/mmsns/VT6V5OXuTMxYhxJetaAnqELiclpwsucyHFO7656Ds1ztTH25ZhuUvUibwNFLL2LBlha5rVp4picviaY/0",
      "snsBgobjectId": "13647912401971261663",
      "snsFlagEx": 7297,
      "snsPrivacyRecent": 72
    },
    "myBrandList": "",
    "bigChatRoomSize": 0,
    "bigChatRoomQuota": 0,
    "bigChatRoomInvite": 0,
    "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/YbDSeSCFxQTo42ZYic6kLk6OYqKSUDZ0qfwwdbcNrk0uc4jh1gDRibBhHrlS67UKB7ibIickhoNWdQ6lGQfMkVyWXY1LFUEC0eUf9xGBptHhAoh7Yl7CsrTJQnZ8nlM0R58c/0",
    "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/YbDSeSCFxQTo42ZYic6kLk6OYqKSUDZ0qfwwdbcNrk0uc4jh1gDRibBhHrlS67UKB7ibIickhoNWdQ6lGQfMkVyWXY1LFUEC0eUf9xGBptHhAoh7Yl7CsrTJQnZ8nlM0R58c/132",
    "mainAcctType": 0,
    "extXml": {},
    "safeDeviceList": {
      "count": 5,
      "list": [
        {
          "name": "Android设备",
          "uuid": "1111111111",
          "deviceType": "android-33",
          "createTime": 1666420216
        },
        {
          "name": "Xiaomi-2211133C",
          "uuid": "22222222222222",
          "deviceType": "android-33",
          "createTime": 1675040523
        },
        "... (3 more items truncated)"
      ]
    },
    "safeDevice": 0,
    "grayscaleFlag": 359,
    "regCountry": "CN",
    "linkedinContactItem": {},
    "patternLockInfo": {
      "patternVersion": 7,
      "sign": {
        "iLen": 156,
        "buffer": "66666666666666"
      },
      "lockStatus": 0
    },
    "payWalletType": 0,
    "walletRegion": 1,
    "extStatus": "563500112150534",
    "userStatus": 1,
    "paySetting": "1",
    "patSuffix": "的钱包说请你吃饭",
    "patSuffixVersion": 2,
    "teenagerModeFinderSetting": 1,
    "teenagerModeBizAcctSetting": 0,
    "teenagerModeMiniProgramSetting": 0,
    "xagreementInfo": {
      "funcsSwitch": "0",
      "funcsUserChoiceSwitch": "0"
    },
    "salt": "888888888888888888",
    "finderSetting": "0",
    "ringBackSetting": {
      "finderObjectId": "0",
      "startTs": 0,
      "endTs": 0
    },
    "smcryptoFlag": 0,
    "globalRingBackSetting": {
      "type": 0,
      "startTime": 0,
      "endTime": 0,
      "music": {
        "sid": 0
      },
      "finder": {
        "finderObjectId": "0"
      }
    },
    "newcomeMsgDefaultVoiceNumber": 0,
    "discoveryPageCtrlFlag": "1",
    "extStatus2": "128",
    "finderLiveAliasSync": {
      "updateTime": "0",
      "spamFlag": 0,
      "deleteTime": "0"
    },
    "liveAliasRoleType": 1,
    "verifyContentList": {
      "count": 0
    },
    "lqtversion": 0,
    "teenagerModeEmotionSetting": 0,
    "notificationBannerDisplayContentSetting": 0
  }
}
```

_（已截断）_

#### 关键字段

响应:
- `baseResponse (object)`
- `baseResponse.ret (int): 0`
- `baseResponse.errMsg (object)`
- `userInfo (object)`
- `userInfo.bitFlag (int): 190`
- `userInfo.userName (object)`
- `userInfo.userName.String (str): '你的wxid'`
- `userInfo.nickName (object)`
- `userInfo.nickName.String (str): '你的昵称'`
- `userInfo.bindUin (int): 0`
- `userInfo.bindEmail (object)`
- `userInfo.bindMobile (object)`
- `userInfo.bindMobile.String (str): '你的手机号'`
- `userInfo.status (int): 234021`
- `userInfo.imgLen (int): 0`
- `userInfo.sex (int): 2`
- `userInfo.province (str): 'Guangdong'`
- `userInfo.city (str): 'Zhuhai'`
- `userInfo.signature (str): '随便记住我 然后把我忘了吧'`
- `userInfo.personalCard (int): 1`
- `userInfo.disturbSetting (object)`
- `userInfo.disturbSetting.nightSetting (int): 0`
- `userInfo.disturbSetting.nightTime (object)`
- `userInfo.disturbSetting.nightTime.beginTime (int): 0`
- `userInfo.disturbSetting.nightTime.endTime (int): 0`
- `userInfo.disturbSetting.allDaySetting (int): 0`
- `userInfo.disturbSetting.allDayTime (object)`
- `userInfo.disturbSetting.allDayTime.beginTime (int): 0`
- `userInfo.disturbSetting.allDayTime.endTime (int): 0`
- `userInfo.pluginFlag (int): 16939169`
- `userInfo.verifyFlag (int): 0`
- `userInfo.point (int): 325`
- `userInfo.experience (int): 1476`
- `userInfo.level (int): 5`
- `userInfo.levelLowExp (int): 1401`
- `userInfo.levelHighExp (int): 2000`
- `userInfo.pluginSwitch (int): 41984`
- `userInfo.gmailList (object)`
- `userInfo.gmailList.count (int): 0`
- `userInfo.alias (str): 'hbbhcds'`
- `userInfo.weiboFlag (int): 0`
- `userInfo.faceBookFlag (int): 0`
- `userInfo.fbuserId (str): '0'`
- `userInfo.albumStyle (int): 0`
- `userInfo.albumFlag (int): 0`
- `userInfo.txnewsCategory (int): 0`
- `userInfo.country (str): 'CN'`
- `userInfoExt (object)`
- `userInfoExt.snsUserInfo (object)`
- `userInfoExt.snsUserInfo.snsFlag (int): 1`
- `userInfoExt.snsUserInfo.snsBgimgId (str): 'http://shmmsns.qpic.cn/mmsns/VT6V5OXuTMxYhxJetaAnqELiclpwsucyHFO7656Ds1ztTH25ZhuUvUibwNFLL2LBlha5rVp4picviaY/0'`
- `userInfoExt.snsUserInfo.snsBgobjectId (str): '13647912401971261663'`
- `userInfoExt.snsUserInfo.snsFlagEx (int): 7297`
- `userInfoExt.snsUserInfo.snsPrivacyRecent (int): 72`
- `userInfoExt.myBrandList (str): ''`
- `userInfoExt.bigChatRoomSize (int): 0`
- `userInfoExt.bigChatRoomQuota (int): 0`
- `userInfoExt.bigChatRoomInvite (int): 0`
- `userInfoExt.bigHeadImgUrl (str): 'https://wx.qlogo.cn/mmhead/ver_1/YbDSeSCFxQTo42ZYic6kLk6OYqKSUDZ0qfwwdbcNrk0uc4jh1gDRibBhHrlS67UKB7ibIickhoNWdQ6lGQfMkV...`
- `userInfoExt.smallHeadImgUrl (str): 'https://wx.qlogo.cn/mmhead/ver_1/YbDSeSCFxQTo42ZYic6kLk6OYqKSUDZ0qfwwdbcNrk0uc4jh1gDRibBhHrlS67UKB7ibIickhoNWdQ6lGQfMkV...`

### 获取个人最新网络 — `/api/get_profile_new`

- **Method**: `POST`
- **URL path**: `/api/get_profile_new`
- **完整 URL**: `http://127.0.0.1:19088/api/get_profile_new`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

```json
{
  "baseResponse": {
    "ret": 0,
    "errMsg": {}
  },
  "userInfo": {
    "bitFlag": 190,
    "userName": {
      "String": "你的wxid"
    },
    "nickName": {
      "String": "隔壁老陈"
    },
    "bindUin": 0,
    "bindEmail": {},
    "bindMobile": {
      "String": "电话"
    },
    "status": 234021,
    "imgLen": 0,
    "sex": 2,
    "province": "省份",
    "city": "城市",
    "signature": "随便记住我 然后把我忘了吧",
    "personalCard": 1,
    "disturbSetting": {
      "nightSetting": 0,
      "nightTime": {
        "beginTime": 0,
        "endTime": 0
      },
      "allDaySetting": 0,
      "allDayTime": {
        "beginTime": 0,
        "endTime": 0
      }
    },
    "pluginFlag": 16939169,
    "verifyFlag": 0,
    "point": 478,
    "experience": 62,
    "level": 1,
    "levelLowExp": 0,
    "levelHighExp": 200,
    "pluginSwitch": 41984,
    "gmailList": {
      "count": 0
    },
    "alias": "hbbhcds",
    "weiboFlag": 0,
    "faceBookFlag": 0,
    "fbuserId": "0",
    "albumStyle": 0,
    "albumFlag": 0,
    "txnewsCategory": 0,
    "country": "CN"
  },
  "userInfoExt": {
    "snsUserInfo": {
      "snsFlag": 1,
      "snsBgimgId": "http://shmmsns.qpic.cn/mmsns/VT6V5OXuTMxYhxJetaAnqELiclpwsucyHFO7656Ds1ztTH25ZhuUvUibwNFLL2LBlha5rVp4picviaY/0",
      "snsBgobjectId": "13647912401971261663",
      "snsFlagEx": 7297,
      "snsPrivacyRecent": 72
    },
    "myBrandList": "****",
    "bigChatRoomSize": 0,
    "bigChatRoomQuota": 0,
    "bigChatRoomInvite": 0,
    "bigHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/YbDSeSCFxQTo42ZYic6kLk6OYqKSUDZ0qfwwdbcNrk0uc4jh1gDRibBhHrlS67UKB7ibIickhoNWdQ6lGQfMkVyWXY1LFUEC0eUf9xGBptHhAoh7Yl7CsrTJQnZ8nlM0R58c/0",
    "smallHeadImgUrl": "https://wx.qlogo.cn/mmhead/ver_1/YbDSeSCFxQTo42ZYic6kLk6OYqKSUDZ0qfwwdbcNrk0uc4jh1gDRibBhHrlS67UKB7ibIickhoNWdQ6lGQfMkVyWXY1LFUEC0eUf9xGBptHhAoh7Yl7CsrTJQnZ8nlM0R58c/132",
    "mainAcctType": 0,
    "extXml": {},
    "safeDeviceList": {
      "count": 5,
      "list": [
        {
          "name": "设备名称",
          "uuid": "设备uuid",
          "deviceType": "android-33",
          "createTime": 1666420216
        }
      ]
    },
    "safeDevice": 0,
    "grayscaleFlag": 359,
    "regCountry": "CN",
    "linkedinContactItem": {},
    "patternLockInfo": {
      "patternVersion": 7,
      "sign": {
        "iLen": 156,
        "buffer": "*****"
      },
      "lockStatus": 0
    },
    "payWalletType": 0,
    "walletRegion": 1,
    "extStatus": "****",
    "userStatus": 1,
    "paySetting": "1",
    "patSuffix": "的钱包说请你吃饭",
    "patSuffixVersion": 2,
    "teenagerModeFinderSetting": 1,
    "teenagerModeBizAcctSetting": 0,
    "teenagerModeMiniProgramSetting": 0,
    "xagreementInfo": {
      "funcsSwitch": "0",
      "funcsUserChoiceSwitch": "0"
    },
    "salt": "******",
    "finderSetting": "0",
    "ringBackSetting": {
      "finderObjectId": "0",
      "startTs": 0,
      "endTs": 0
    },
    "smcryptoFlag": 0,
    "globalRingBackSetting": {
      "type": 0,
      "startTime": 0,
      "endTime": 0,
      "music": {
        "sid": 0
      },
      "finder": {
        "finderObjectId": "0"
      }
    },
    "newcomeMsgDefaultVoiceNumber": 0,
    "discoveryPageCtrlFlag": "1",
    "extStatus2": "128",
    "finderLiveAliasSync": {
      "updateTime": "0",
      "spamFlag": 0,
      "deleteTime": "0"
    },
    "liveAliasRoleType": 1,
    "verifyContentList": {
      "count": 0
    },
    "lqtversion": 0,
    "teenagerModeEmotionSetting": 0,
    "notificationBannerDisplayContentSetting": 0
  }
}
```

#### 关键字段

响应:
- `baseResponse (object)`
- `baseResponse.ret (int): 0`
- `baseResponse.errMsg (object)`
- `userInfo (object)`
- `userInfo.bitFlag (int): 190`
- `userInfo.userName (object)`
- `userInfo.userName.String (str): '你的wxid'`
- `userInfo.nickName (object)`
- `userInfo.nickName.String (str): '隔壁老陈'`
- `userInfo.bindUin (int): 0`
- `userInfo.bindEmail (object)`
- `userInfo.bindMobile (object)`
- `userInfo.bindMobile.String (str): '电话'`
- `userInfo.status (int): 234021`
- `userInfo.imgLen (int): 0`
- `userInfo.sex (int): 2`
- `userInfo.province (str): '省份'`
- `userInfo.city (str): '城市'`
- `userInfo.signature (str): '随便记住我 然后把我忘了吧'`
- `userInfo.personalCard (int): 1`
- `userInfo.disturbSetting (object)`
- `userInfo.disturbSetting.nightSetting (int): 0`
- `userInfo.disturbSetting.nightTime (object)`
- `userInfo.disturbSetting.nightTime.beginTime (int): 0`
- `userInfo.disturbSetting.nightTime.endTime (int): 0`
- `userInfo.disturbSetting.allDaySetting (int): 0`
- `userInfo.disturbSetting.allDayTime (object)`
- `userInfo.disturbSetting.allDayTime.beginTime (int): 0`
- `userInfo.disturbSetting.allDayTime.endTime (int): 0`
- `userInfo.pluginFlag (int): 16939169`
- `userInfo.verifyFlag (int): 0`
- `userInfo.point (int): 478`
- `userInfo.experience (int): 62`
- `userInfo.level (int): 1`
- `userInfo.levelLowExp (int): 0`
- `userInfo.levelHighExp (int): 200`
- `userInfo.pluginSwitch (int): 41984`
- `userInfo.gmailList (object)`
- `userInfo.gmailList.count (int): 0`
- `userInfo.alias (str): 'hbbhcds'`
- `userInfo.weiboFlag (int): 0`
- `userInfo.faceBookFlag (int): 0`
- `userInfo.fbuserId (str): '0'`
- `userInfo.albumStyle (int): 0`
- `userInfo.albumFlag (int): 0`
- `userInfo.txnewsCategory (int): 0`
- `userInfo.country (str): 'CN'`
- `userInfoExt (object)`
- `userInfoExt.snsUserInfo (object)`
- `userInfoExt.snsUserInfo.snsFlag (int): 1`
- `userInfoExt.snsUserInfo.snsBgimgId (str): 'http://shmmsns.qpic.cn/mmsns/VT6V5OXuTMxYhxJetaAnqELiclpwsucyHFO7656Ds1ztTH25ZhuUvUibwNFLL2LBlha5rVp4picviaY/0'`
- `userInfoExt.snsUserInfo.snsBgobjectId (str): '13647912401971261663'`
- `userInfoExt.snsUserInfo.snsFlagEx (int): 7297`
- `userInfoExt.snsUserInfo.snsPrivacyRecent (int): 72`
- `userInfoExt.myBrandList (str): '****'`
- `userInfoExt.bigChatRoomSize (int): 0`
- `userInfoExt.bigChatRoomQuota (int): 0`
- `userInfoExt.bigChatRoomInvite (int): 0`
- `userInfoExt.bigHeadImgUrl (str): 'https://wx.qlogo.cn/mmhead/ver_1/YbDSeSCFxQTo42ZYic6kLk6OYqKSUDZ0qfwwdbcNrk0uc4jh1gDRibBhHrlS67UKB7ibIickhoNWdQ6lGQfMkV...`
- `userInfoExt.smallHeadImgUrl (str): 'https://wx.qlogo.cn/mmhead/ver_1/YbDSeSCFxQTo42ZYic6kLk6OYqKSUDZ0qfwwdbcNrk0uc4jh1gDRibBhHrlS67UKB7ibIickhoNWdQ6lGQfMkV...`

### 获取所有群wxids(网络长耗时) — `/api/get_room_wxids`

- **Method**: `POST`
- **URL path**: `/api/get_room_wxids`
- **完整 URL**: `http://127.0.0.1:19088/api/get_room_wxids`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 获取群成员数量,群昵称 — `/api/get_rooms_info`

- **Method**: `POST`
- **URL path**: `/api/get_rooms_info`
- **完整 URL**: `http://127.0.0.1:19088/api/get_rooms_info`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 语音转文本 — `/api/get_voice_trans`

- **Method**: `POST`
- **URL path**: `/api/get_voice_trans`
- **完整 URL**: `http://127.0.0.1:19088/api/get_voice_trans`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "clientMsgId": "8111825985001399988",
  "newMsgId": 211095990,
  "length": 11832
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `clientMsgId (str): '8111825985001399988'`
- `newMsgId (int): 211095990`
- `length (int): 11832`

### 获取微信缓存目录 — `/api/getwxbasepath`

- **Method**: `POST`
- **URL path**: `/api/getwxbasepath`
- **完整 URL**: `http://127.0.0.1:19088/api/getwxbasepath`
- **Content-Type**: _未见_

#### 请求示例

_无请求体（curl 未带 `--data`）_

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 邀请进入群聊 — `/api/invite_member_to_chat_room`

- **Method**: `POST`
- **URL path**: `/api/invite_member_to_chat_room`
- **完整 URL**: `http://127.0.0.1:19088/api/invite_member_to_chat_room`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxids": "wxid_8543785438012",
  "roomId": "45220347292@chatroom"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxids (str): 'wxid_8543785438012'`
- `roomId (str): '45220347292@chatroom'`

### 获取小程序code — `/api/js_login`

- **Method**: `POST`
- **URL path**: `/api/js_login`
- **完整 URL**: `http://127.0.0.1:19088/api/js_login`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "waId": "wxfec93dd30abcc9ad"
}
```

#### 响应示例

```json
{
  "baseResponse": {
    "ret": 0,
    "errMsg": {
      "String": ""
    }
  },
  "jsapiBaseresponse": {
    "errcode": 0,
    "errmsg": "ok",
    "errorNumber": 0
  },
  "code": "小程序返回的code",
  "state": ""
}
```

#### 关键字段

请求:
- `waId (str): 'wxfec93dd30abcc9ad'`
响应:
- `baseResponse (object)`
- `baseResponse.ret (int): 0`
- `baseResponse.errMsg (object)`
- `baseResponse.errMsg.String (str): ''`
- `jsapiBaseresponse (object)`
- `jsapiBaseresponse.errcode (int): 0`
- `jsapiBaseresponse.errmsg (str): 'ok'`
- `jsapiBaseresponse.errorNumber (int): 0`
- `code (str): '小程序返回的code'`
- `state (str): ''`

### 退出登陆 — `/api/logout`

- **Method**: `POST`
- **URL path**: `/api/logout`
- **完整 URL**: `http://127.0.0.1:19088/api/logout`
- **Content-Type**: `application/json`

#### 请求示例

```json
{}
```

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 修改自己在群里的昵称 — `/api/mod_chat_room_self_nick_name`

- **Method**: `POST`
- **URL path**: `/api/mod_chat_room_self_nick_name`
- **完整 URL**: `http://127.0.0.1:19088/api/mod_chat_room_self_nick_name`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "roomId": "49767299448@chatroom",
  "nickName": "綦奕泽"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `roomId (str): '49767299448@chatroom'`
- `nickName (str): '綦奕泽'`

### 修改群名称 — `/api/mod_chatroom_topic`

- **Method**: `POST`
- **URL path**: `/api/mod_chatroom_topic`
- **完整 URL**: `http://127.0.0.1:19088/api/mod_chatroom_topic`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "45220347292@chatroom",
  "topic": "需要修改成的名称"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): '45220347292@chatroom'`
- `topic (str): '需要修改成的名称'`

### 修改自己昵称 — `/api/mod_self_nick_name`

- **Method**: `POST`
- **URL path**: `/api/mod_self_nick_name`
- **完整 URL**: `http://127.0.0.1:19088/api/mod_self_nick_name`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "newName": "鸭梨🍐大a"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `newName (str): '鸭梨🍐大a'`

### 修改个人签名 — `/api/mod_self_nick_signature`

- **Method**: `POST`
- **URL path**: `/api/mod_self_nick_signature`
- **完整 URL**: `http://127.0.0.1:19088/api/mod_self_nick_signature`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "newSignature": "666666666666666"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `newSignature (str): '666666666666666'`

### 修改好友标签 — `/api/modify_contact_label`

- **Method**: `POST`
- **URL path**: `/api/modify_contact_label`
- **完整 URL**: `http://127.0.0.1:19088/api/modify_contact_label`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxids": "wxid_8543785438012",
  "labelId": "2,6"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxids (str): 'wxid_8543785438012'`
- `labelId (str): '2,6'`

### 搜索微信号/手机号 — `/api/net_scene_search_contact`

- **Method**: `POST`
- **URL path**: `/api/net_scene_search_contact`
- **完整 URL**: `http://127.0.0.1:19088/api/net_scene_search_contact`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "search": "搜索微信号还是手机号"
}
```

#### 响应示例

```json
{
  "baseResponse": {
    "ret": 0,
    "errMsg": {
      "String": "Everything is OK"
    }
  },
  "userName": {
    "String": "v3_020b3826fd03010000000000c7f228b4f06efa000000501ea9a3dba12f95f6b60a0536a1adb6f580631340234a6fd1c318fd5de96fe637ffb434b1d4fe0f451c904eb4aba33f1a4b8c976735c47abb45dd77e67209c666ce8e85fbb59d586e0157f8@stranger"
  },
  "nickName": {
    "String": "悬淼"
  },
  "pyinitial": {
    "String": "wxid_hify7vdpvg5d22"
  },
  "quanPin": {
    "String": "wxid_hify7vdpvg5d22"
  },
  "sex": 0,
  "imgBuf": {
    "iLen": 0
  },
  "signature": "天上天下，唯我独尊",
  "personalCard": 1,
  "verifyFlag": 0,
  "weiboFlag": 0,
  "albumStyle": 0,
  "albumFlag": 0,
  "snsUserInfo": {
    "snsFlag": 0,
    "snsBgobjectId": "0",
    "snsFlagEx": 0,
    "snsPrivacyRecent": 0
  },
  "customizedInfo": {
    "brandFlag": 0
  },
  "contactCount": 0,
  "bigHeadImgUrl": "http://wx.qlogo.cn/mmhead/ver_1/H9ukOUmCmkabkwXmfTbiaNZLuFpLKzgGSxaZ5IzY0pUPdCshNmuzwgFSLLDe2mZlNUKysKGaqefgWUFseqTFdoviaW6Sny7kQ09iaiaH5go8LyNqBJw7Lzh2AyWPms2MoKef/0",
  "smallHeadImgUrl": "http://wx.qlogo.cn/mmhead/ver_1/H9ukOUmCmkabkwXmfTbiaNZLuFpLKzgGSxaZ5IzY0pUPdCshNmuzwgFSLLDe2mZlNUKysKGaqefgWUFseqTFdoviaW6Sny7kQ09iaiaH5go8LyNqBJw7Lzh2AyWPms2MoKef/132",
  "resBuf": {
    "iLen": 0
  },
  "antispamTicket": "v4_000b708f0b04000001000000000029ac3dc68a17dea708381427a5681000000050ded0b020927e3c97896a09d47e6e9eb3fbad3b5aa09a6124b67addb011f45e340030f8d0743331300126b01af1cbba4a7ecbfa7a7dfe8d3622d0412a8243b323aa2eac8f59b61aeeaaaeb5c083362db9bc4ab949ef4c89557f289493d143ec1f216ad6648091cf@stranger",
  "matchType": 2,
  "extFlag": 0,
  "searchContactJumpInfo": {}
}
```

#### 关键字段

请求:
- `search (str): '搜索微信号还是手机号'`
响应:
- `baseResponse (object)`
- `baseResponse.ret (int): 0`
- `baseResponse.errMsg (object)`
- `baseResponse.errMsg.String (str): 'Everything is OK'`
- `userName (object)`
- `userName.String (str): 'v3_020b3826fd03010000000000c7f228b4f06efa000000501ea9a3dba12f95f6b60a0536a1adb6f580631340234a6fd1c318fd5de96fe637ffb434...`
- `nickName (object)`
- `nickName.String (str): '悬淼'`
- `pyinitial (object)`
- `pyinitial.String (str): 'wxid_hify7vdpvg5d22'`
- `quanPin (object)`
- `quanPin.String (str): 'wxid_hify7vdpvg5d22'`
- `sex (int): 0`
- `imgBuf (object)`
- `imgBuf.iLen (int): 0`
- `signature (str): '天上天下，唯我独尊'`
- `personalCard (int): 1`
- `verifyFlag (int): 0`
- `weiboFlag (int): 0`
- `albumStyle (int): 0`
- `albumFlag (int): 0`
- `snsUserInfo (object)`
- `snsUserInfo.snsFlag (int): 0`
- `snsUserInfo.snsBgobjectId (str): '0'`
- `snsUserInfo.snsFlagEx (int): 0`
- `snsUserInfo.snsPrivacyRecent (int): 0`
- `customizedInfo (object)`
- `customizedInfo.brandFlag (int): 0`
- `contactCount (int): 0`
- `bigHeadImgUrl (str): 'http://wx.qlogo.cn/mmhead/ver_1/H9ukOUmCmkabkwXmfTbiaNZLuFpLKzgGSxaZ5IzY0pUPdCshNmuzwgFSLLDe2mZlNUKysKGaqefgWUFseqTFdov...`
- `smallHeadImgUrl (str): 'http://wx.qlogo.cn/mmhead/ver_1/H9ukOUmCmkabkwXmfTbiaNZLuFpLKzgGSxaZ5IzY0pUPdCshNmuzwgFSLLDe2mZlNUKysKGaqefgWUFseqTFdov...`
- `resBuf (object)`
- `resBuf.iLen (int): 0`
- `antispamTicket (str): 'v4_000b708f0b04000001000000000029ac3dc68a17dea708381427a5681000000050ded0b020927e3c97896a09d47e6e9eb3fbad3b5aa09a6124b6...`
- `matchType (int): 2`
- `extFlag (int): 0`
- `searchContactJumpInfo (object)`

### 二维码识别 — `/api/qrscan`

- **Method**: `POST`
- **URL path**: `/api/qrscan`
- **完整 URL**: `http://127.0.0.1:19088/api/qrscan`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "path": "d:\\qr2.png"
}
```

#### 响应示例

```json
{
  "account_wxid": "",
  "data": {
    "scan_res": "woaini"
  },
  "errCode": 1,
  "errMsg": "请求处理成功"
}
```

#### 关键字段

请求:
- `path (str): 'd:\\qr2.png'`
响应:
- `account_wxid (str): ''`
- `data (object)`
- `data.scan_res (str): 'woaini'`
- `errCode (int): 1`
- `errMsg (str): '请求处理成功'`

### 退出群聊 — `/api/quit_and_del_chat_room`

- **Method**: `POST`
- **URL path**: `/api/quit_and_del_chat_room`
- **完整 URL**: `http://127.0.0.1:19088/api/quit_and_del_chat_room`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "roomId": "xxxxxxxxxxxx"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `roomId (str): 'xxxxxxxxxxxx'`

### 获取登录二维码 — `/api/reflash_qrcode`

- **Method**: `POST`
- **URL path**: `/api/reflash_qrcode`
- **完整 URL**: `http://127.0.0.1:19088/api/reflash_qrcode`
- **Content-Type**: `application/xml`

#### 请求示例

```

```

#### 响应示例

```json
{
  "baseResponse": {
    "ret": 0,
    "errMsg": {}
  },
  "qrcode": {
    "iLen": 0,
    "buffer": "string"
  },
  "uuid": "string",
  "checkTime": 0,
  "notifyKey": {
    "iLen": 0,
    "buffer": "string"
  },
  "expiredTime": 0,
  "blueToothBroadCastContent": {
    "iLen": 0
  }
}
```

#### 关键字段

响应:
- `baseResponse (object)`
- `baseResponse.ret (int): 0`
- `baseResponse.errMsg (object)`
- `qrcode (object)`
- `qrcode.iLen (int): 0`
- `qrcode.buffer (str): 'string'`
- `uuid (str): 'string'`
- `checkTime (int): 0`
- `notifyKey (object)`
- `notifyKey.iLen (int): 0`
- `notifyKey.buffer (str): 'string'`
- `expiredTime (int): 0`
- `blueToothBroadCastContent (object)`
- `blueToothBroadCastContent.iLen (int): 0`

### 修改好友备注 — `/api/remark_contact`

- **Method**: `POST`
- **URL path**: `/api/remark_contact`
- **完整 URL**: `http://127.0.0.1:19088/api/remark_contact`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "wxid_ozyqateb85un22",
  "remark": "111"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): 'wxid_ozyqateb85un22'`
- `remark (str): '111'`

### 移除群聊通讯录 — `/api/remov_chatroom_to_contact`

- **Method**: `POST`
- **URL path**: `/api/remov_chatroom_to_contact`
- **完整 URL**: `http://127.0.0.1:19088/api/remov_chatroom_to_contact`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "roomId": "群id"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `roomId (str): '群id'`

### 撤回任何消息 — `/api/revoke_any`

- **Method**: `POST`
- **URL path**: `/api/revoke_any`
- **完整 URL**: `http://127.0.0.1:19088/api/revoke_any`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "newMsgId": 2050044161371926385,
  "createTime": 1761391928,
  "toUserName": "49767299448@chatroom"
}
```

#### 响应示例

```json
{
  "account_wxid": "string",
  "data": {
    "baseResponse": {
      "errMsg": {},
      "ret": 0
    },
    "sysWording": "string"
  },
  "errCode": 0,
  "errMsg": "string"
}
```

#### 关键字段

请求:
- `newMsgId (int): 2050044161371926385`
- `createTime (int): 1761391928`
- `toUserName (str): '49767299448@chatroom'`
响应:
- `account_wxid (str): 'string'`
- `data (object)`
- `data.baseResponse (object)`
- `data.baseResponse.errMsg (object)`
- `data.baseResponse.ret (int): 0`
- `data.sysWording (str): 'string'`
- `errCode (int): 0`
- `errMsg (str): 'string'`

### 保存群聊到通讯录 — `/api/save_chatroom_to_contact`

- **Method**: `POST`
- **URL path**: `/api/save_chatroom_to_contact`
- **完整 URL**: `http://127.0.0.1:19088/api/save_chatroom_to_contact`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "roomId": "群id"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `roomId (str): '群id'`

### 发送卡片/XML消息 — `/api/send_app_msg`

- **Method**: `POST`
- **URL path**: `/api/send_app_msg`
- **完整 URL**: `http://127.0.0.1:19088/api/send_app_msg`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "content": "<appmsg appid=\"\" sdkver=\"\"><title>霜尘与#跟你爹的聊天记录</title><des>#年 轻人:[图片]&#x0D;&#x0A;#年轻人:[图片]&#x0D;&#x0A;</des><action>view</action><type>19</type><showtype>0</showtype><content></content><url>http://support.weixin.qq.com/cgi-bin/mmsupport-bin/readtemplate?t=page/favorite_record__w_unsupport</url><dataurl></dataurl><lowurl></lowurl><lowdataurl></lowdataurl><recorditem>&lt;recordinfo&gt;&lt;title&gt;霜尘与#年轻人的聊天记录&lt;/title&gt;&lt;desc&gt;#年轻人:[图片]&#x0D;&#x0A;#年轻人:[图片]&#x0D;&#x0A;&lt;/desc&gt;&lt;data... (truncated, total 4423 chars)",
  "type": "19",
  "wxid": "filehelper"
}
```

_（已截断）_

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `content (str): '<appmsg appid="" sdkver=""><title>霜尘与#跟你爹的聊天记录</title><des>#年 轻人:[图片]&#x0D;&#x0A;#年轻人:[图片]&#x0D;&#x0A;</des><action>vie...`
- `type (str): '19'`
- `wxid (str): 'filehelper'`

### 发送小程序 — `/api/send_applet_msg`

- **Method**: `POST`
- **URL path**: `/api/send_applet_msg`
- **完整 URL**: `http://127.0.0.1:19088/api/send_applet_msg`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "content": "<appmsg appid=\"\" sdkver=\"0\"><title>多重爆款  满99减40</title><des>多重爆款  满99减40</des><action>view</action><type>33</type><showtype>0</showtype><content></content><url>https://mp.weixin.qq.com/mp/waerrpage?appid=wx92916b3adca84096&amp;type=upgrade&amp;upgradetype=3#wechat_redirect</url><dataurl></dataurl><lowurl></lowurl><lowdataurl></lowdataurl><recorditem></recorditem><thumburl></thumburl><messageaction></messageaction><laninfo></laninfo><md5>5aeb8bc7093630b0c4f87b12f471681a</md5><extinfo></extinfo><sourceusername>gh_2f6dc0344214@app</sourceusername><sourcedisplayname>小象超市丨原美团买菜</sourcedisplayname><commenturl></commenturl><appattach><totallen>0</totallen><attachid></attachid><emoticonmd5></emoticonmd5><fileext>jpg</fileext><filekey>29306497a7327a579a2e3631fd6d043c</filekey><cdnthumburl>3057020100044b304902010002045d270fce02032f559502041341f7df020466c2b9d5042431653266373266312d383735392d346534342d623761652d3131656439633834353164360204051408030201000405004c55cd00</cdnthumburl><aeskey>eebe704af1e02db0a03b0d621903b11a</aeskey><cdnthumbaeskey>eebe704af1e02db0a03b0d621903b11a</cdnthumbaeskey><cdnthumbmd5>5aeb8bc7093630b0c4f87b12f471681a</cdnthumbmd5><encryver>1</encryver><tpthumburl>https://wwfile.work.weixin.qq.com/cgi-bin/download?f=30680201020461305f020100020409bbe74602030f4241020452c06db4020466c171dc042466656333393662312d613464652d346163652d623935382d39323030353466393837313702010002027b5004105aeb8bc7093630b0c4f87b12f471681a0201010201000400&amp;t=396251A1439A2C5A4AF1FD8CC9B4425C2D4AFC4A5AB72C74A3205E4FC37C47DEF9D0633DC78301BFDDEAB8D1C4D4D261FA00339E8F0B0E55C6F80DEFB2F439AF6D39C6A7DD10A732B091E07B6C6E47DE96721DFED273915FD38DB0B98B78798E296FC0D43ECF90E6A30D45758B3C66C4878AABD89CCEF2A6427D1222A608E142DEBA1F3286AE0D5A304391A90826330309EE1DEBD096D2A4FE2FC5C1CD5CF646173741EB74B50D61C91BDC588022103C55A6DDD08439B0FB803A52719396DE70FFBDBF69B77DE92D9B31E69547E43524F58781A449E9C1C30C18EDBA6D8E803429803C1E60AEAE28216CA8B7C59E83A6E9742F488F114BF99A6CC2453595955237E5741ED2FBF12A28E21992BF896E69&amp;p=1</tpthumburl><cdnthumblength>31563</cdnthumblength><cdnthumbheight>100</cdnthumbheight><cdnthumbwidth>100</cdnthumbwidth></appattach><webviewshared><publisherId></publisherId><publisherReqId>0</publisherReqId></webviewshared><weappinfo><pagepath>pages/index/index.html?lch=mhqWnAlezjf5NdKIyvMLoraZg_ext(854503783,1825020016853749823,1)&amp;protocol=imaicai%3A%2F%2Fwww.maicai.com%2Fweb%3Ffuture%3D1%26url%3Dhttps%253A%252F%252Fi.meituan.com%252Fawp%252Fhfe%252Fblock%252Fmaicai%252Fba21b5723f0f%252F303147%252Findex.html%253Fcube_mc_activityId%253D43589708%2526cube_mc_cubeCityId%253D2%252C33%252C16%252C7%252C4%252C1%252C208%252C6%252C3%2526cube_mc_isLimitPoi%253Dtrue%2526cube_mc_poiGroupId%253D3747%2526cube_mc_skuGroupId%253D144555%2526mc_source%253D0akaamabmadh&amp;__XGAdfRSkC=STAXe</pagepath><username>gh_2f6dc0344214@app</username><appid>wx92916b3adca84096</appid><version>11</version><type>2</type><weappiconurl>https://p0.meituan.net/travelcube/4e9525f88d7d8120257baed9632cb8b28982.png</weappiconurl><appservicetype>0</appservicetype><shareId>0_wx92916b3adca84096_25984985516128883@openim_1723953798_0</shareId></weappinfo><websearch /></appmsg>",
  "type": "33",
  "wxid": "filehelper"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `content (str): '<appmsg appid="" sdkver="0"><title>多重爆款  满99减40</title><des>多重爆款  满99减40</des><action>view</action><type>33</type><show...`
- `type (str): '33'`
- `wxid (str): 'filehelper'`

### cdn发送图片(无源可用做转发消息) — `/api/send_cdn_img_msg`

- **Method**: `POST`
- **URL path**: `/api/send_cdn_img_msg`
- **完整 URL**: `http://127.0.0.1:19088/api/send_cdn_img_msg`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "toWxid": "filehelper",
  "totalLen": "4876183",
  "fileId": "307002010204693067020104020445fb609102030f4fed020432d1960902046562d5a0043634313562633635313933643562373533636663343035666165326632376236315f313730303937363033325f313436323030363137360204020400110202111004025348020227110201000400",
  "aesky": "cea539047a38291fb96776572f464625",
  "cdnmidImgSize": "4876183",
  "cdnthumbImgSize": "4876183",
  "encryVer": "1"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `toWxid (str): 'filehelper'`
- `totalLen (str): '4876183'`
- `fileId (str): '307002010204693067020104020445fb609102030f4fed020432d1960902046562d5a00436343135626336353139336435623735336366633430356...`
- `aesky (str): 'cea539047a38291fb96776572f464625'`
- `cdnmidImgSize (str): '4876183'`
- `cdnthumbImgSize (str): '4876183'`
- `encryVer (str): '1'`

### 发送本地GIF信息 — `/api/send_emotion_msg`

- **Method**: `POST`
- **URL path**: `/api/send_emotion_msg`
- **完整 URL**: `http://127.0.0.1:19088/api/send_emotion_msg`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "wxid_hv8oepkfkkml12",
  "filepath": "D:\\bqb\\3.gif"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): 'wxid_hv8oepkfkkml12'`
- `filepath (str): 'D:\\bqb\\3.gif'`

### 发送收藏表情 — `/api/send_fav_emotion`

- **Method**: `POST`
- **URL path**: `/api/send_fav_emotion`
- **完整 URL**: `http://127.0.0.1:19088/api/send_fav_emotion`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "string",
  "md5": "string",
  "length": 0
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): 'string'`
- `md5 (str): 'string'`
- `length (int): 0`

### 发送位置消息 — `/api/send_location_msg`

- **Method**: `POST`
- **URL path**: `/api/send_location_msg`
- **完整 URL**: `http://127.0.0.1:19088/api/send_location_msg`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "string",
  "x": "string",
  "y": "string",
  "lable": "string",
  "poiname": "string"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): 'string'`
- `x (str): 'string'`
- `y (str): 'string'`
- `lable (str): 'string'`
- `poiname (str): 'string'`

### 发送MP3语音 — `/api/send_mp3_voice`

- **Method**: `POST`
- **URL path**: `/api/send_mp3_voice`
- **完整 URL**: `http://127.0.0.1:19088/api/send_mp3_voice`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "string",
  "mp3Path": "string"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): 'string'`
- `mp3Path (str): 'string'`

### 发送拍一拍 — `/api/send_pat`

- **Method**: `POST`
- **URL path**: `/api/send_pat`
- **完整 URL**: `http://127.0.0.1:19088/api/send_pat`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "roomId": "wxid_hv8oepkfkkml12",
  "wxid": "wxid_hv8oepkfkkml12"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `roomId (str): 'wxid_hv8oepkfkkml12'`
- `wxid (str): 'wxid_hv8oepkfkkml12'`

### 发送链接信息 — `/api/send_xml`

- **Method**: `POST`
- **URL path**: `/api/send_xml`
- **完整 URL**: `http://127.0.0.1:19088/api/send_xml`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "63",
  "title": "由于向下更加全啊",
  "description": "再业最到。难族开常因团象后也。可命保口。行几布族道市打传段程。实看属处总义合色强明。们第运一型加始。周学金会劳。",
  "thumbUrl": "https://gleaming-nougat.org/",
  "url": "https://soggy-daddy.org/"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): '63'`
- `title (str): '由于向下更加全啊'`
- `description (str): '再业最到。难族开常因团象后也。可命保口。行几布族道市打传段程。实看属处总义合色强明。们第运一型加始。周学金会劳。'`
- `thumbUrl (str): 'https://gleaming-nougat.org/'`
- `url (str): 'https://soggy-daddy.org/'`

### 开启消息免打扰 — `/api/set_mute_user`

- **Method**: `POST`
- **URL path**: `/api/set_mute_user`
- **完整 URL**: `http://127.0.0.1:19088/api/set_mute_user`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "wxid_8543785438012"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): 'wxid_8543785438012'`

### 设置群公告 — `/api/set_room_announcement_pb`

- **Method**: `POST`
- **URL path**: `/api/set_room_announcement_pb`
- **完整 URL**: `http://127.0.0.1:19088/api/set_room_announcement_pb`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "roomId": "51687237616@chatroom",
  "announcement": "通知一下 下次别用之前的群公告版本了"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `roomId (str): '51687237616@chatroom'`
- `announcement (str): '通知一下 下次别用之前的群公告版本了'`

### 星标好友 — `/api/set_start`

- **Method**: `POST`
- **URL path**: `/api/set_start`
- **完整 URL**: `http://127.0.0.1:19088/api/set_start`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "群wxid"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): '群wxid'`

### 置顶好友 — `/api/set_top`

- **Method**: `POST`
- **URL path**: `/api/set_top`
- **完整 URL**: `http://127.0.0.1:19088/api/set_top`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "群wxid"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): '群wxid'`

### 朋友圈回复 — `/api/sns_comment_reply`

- **Method**: `POST`
- **URL path**: `/api/sns_comment_reply`
- **完整 URL**: `http://127.0.0.1:19088/api/sns_comment_reply`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "content": "66666666666",
  "sns_id": "14667428703163265648",
  "comment_id": 3
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `content (str): '66666666666'`
- `sns_id (str): '14667428703163265648'`
- `comment_id (int): 3`

### 删除朋友圈 — `/api/sns_del`

- **Method**: `POST`
- **URL path**: `/api/sns_del`
- **完整 URL**: `http://127.0.0.1:19088/api/sns_del`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "sns_id": "14667428703163265648"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `sns_id (str): '14667428703163265648'`

### 删除朋友圈评论 — `/api/sns_del_comment`

- **Method**: `POST`
- **URL path**: `/api/sns_del_comment`
- **完整 URL**: `http://127.0.0.1:19088/api/sns_del_comment`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "sns_id": "14661929784229180031",
  "commentId": "3"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `sns_id (str): '14661929784229180031'`
- `commentId (str): '3'`

### 获取朋友圈详情 — `/api/sns_get_detail`

- **Method**: `POST`
- **URL path**: `/api/sns_get_detail`
- **完整 URL**: `http://127.0.0.1:19088/api/sns_get_detail`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "sns_id": 14420282581074719279
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `sns_id (int): 14420282581074719279`

### 获取朋友圈首页 — `/api/sns_get_first_page`

- **Method**: `POST`
- **URL path**: `/api/sns_get_first_page`
- **完整 URL**: `http://127.0.0.1:19088/api/sns_get_first_page`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "firstPageMd5": "string",
  "maxId": "string"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `firstPageMd5 (str): 'string'`
- `maxId (str): 'string'`

### 获取朋友圈下一页 — `/api/sns_get_next_page`

- **Method**: `POST`
- **URL path**: `/api/sns_get_next_page`
- **完整 URL**: `http://127.0.0.1:19088/api/sns_get_next_page`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "lastItemid": "14689529228577936097"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `lastItemid (str): '14689529228577936097'`

### 发送朋友圈 — `/api/sns_post`

- **Method**: `POST`
- **URL path**: `/api/sns_post`
- **完整 URL**: `http://127.0.0.1:19088/api/sns_post`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "content": "6666666666666666666666",
  "blackList": "",
  "withauserList": ""
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `content (str): '6666666666666666666666'`
- `blackList (str): ''`
- `withauserList (str): ''`

### 发送图片朋友圈 — `/api/sns_send_img`

- **Method**: `POST`
- **URL path**: `/api/sns_send_img`
- **完整 URL**: `http://127.0.0.1:19088/api/sns_send_img`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "filelist": "D:\\7777777.jpg",
  "content": "测试文字朋友圈"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `filelist (str): 'D:\\7777777.jpg'`
- `content (str): '测试文字朋友圈'`

### 朋友圈图片上传 — `/api/sns_upload`

- **Method**: `POST`
- **URL path**: `/api/sns_upload`
- **完整 URL**: `http://127.0.0.1:19088/api/sns_upload`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "filePath": "D:\\QQ20251211-100920.png"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `filePath (str): 'D:\\QQ20251211-100920.png'`

### 获取好友列表数据库查询 — `/api/sqlite3_exec`

- **Method**: `POST`
- **URL path**: `/api/sqlite3_exec`
- **完整 URL**: `http://127.0.0.1:19088/api/sqlite3_exec`
- **Content-Type**: `application/json`

#### 请求示例

```
{"db_name": "contact.db", "sql_fmt": "    SELECT\n    cr.username              AS room_wxid,\n    cr.owner                 AS manager_wxid,\n    c_room.nick_name         AS nickname,\n    c_room.small_head_url    AS avatar,\n\n    CASE\n        WHEN cr.owner = 
```

#### 响应示例

_无响应体示例_

#### 关键字段

_无结构化字段可提取_

### 确认收款 — `/api/ten_pay_trans_fer_confirm`

- **Method**: `POST`
- **URL path**: `/api/ten_pay_trans_fer_confirm`
- **完整 URL**: `http://127.0.0.1:19088/api/ten_pay_trans_fer_confirm`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "invalid_time": 1765008493,
  "transferid": "1000050001202512051421286682351"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `invalid_time (int): 1765008493`
- `transferid (str): '1000050001202512051421286682351'`

### 转让群主 — `/api/transferchatroomowner`

- **Method**: `POST`
- **URL path**: `/api/transferchatroomowner`
- **完整 URL**: `http://127.0.0.1:19088/api/transferchatroomowner`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "to_wxid": "string",
  "roomId": "string"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `to_wxid (str): 'string'`
- `roomId (str): 'string'`

### 拒绝收款 — `/api/un_ten_pay_trans_fer_confirm`

- **Method**: `POST`
- **URL path**: `/api/un_ten_pay_trans_fer_confirm`
- **完整 URL**: `http://127.0.0.1:19088/api/un_ten_pay_trans_fer_confirm`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "invalid_time": 0,
  "transferid": "string"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `invalid_time (int): 0`
- `transferid (str): 'string'`

### 取消折叠群聊或者个人 — `/api/unfolding`

- **Method**: `POST`
- **URL path**: `/api/unfolding`
- **完整 URL**: `http://127.0.0.1:19088/api/unfolding`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "roomId": "string"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `roomId (str): 'string'`

### 更新好友列表 — `/api/update_all_friend`

- **Method**: `POST`
- **URL path**: `/api/update_all_friend`
- **完整 URL**: `http://127.0.0.1:19088/api/update_all_friend`
- **Content-Type**: `application/json`

#### 请求示例

```json
{}
```

#### 响应示例

```json
{
  "data": [
    {
      "contact": {
        "alias": "string",
        "bigHeadImgUrl": "string",
        "bitMask": 0,
        "bitVal": 0,
        "city": "string",
        "country": "string",
        "encryptUserName": "string",
        "hasWeiXinHdHeadImg": 0,
        "imgBuf": {
          "buffer": "string",
          "iLen": 0
        },
        "imgFlag": 0,
        "nickName": {
          "String": "string"
        },
        "province": "string",
        "pyinitial": {
          "String": "string"
        },
        "quanPin": {
          "String": "string"
        },
        "remark": {
          "String": "string"
        },
        "remarkPyinitial": {
          "String": "string"
        },
        "remarkQuanPin": {
          "String": "string"
        },
        "sex": 0,
        "smallHeadImgUrl": "string",
        "snsUserInfo": {
          "snsFlag": 0
        },
        "textStatusFlag": 0,
        "userName": {
          "String": "string"
        },
        "verifyFlag": 0,
        "customizedInfo": {
          "brandFlag": 0,
          "brandIconUrl": "string",
          "externalInfo": "string"
        },
        "description": "string",
        "labelIdlist": "string",
        "phoneNumListInfo": {
          "count": 0
        },
        "textStatusExtInfo": "string",
        "textStatusId": "string",
        "contactType": 0,
        "deleteFlag": 0,
        "chatroomVersion": 0
      },
      "ret": 0,
      "username": "string"
    }
  ],
  "friend_count": 0
}
```

#### 关键字段

响应:
- `data (array, len=1)`
- `data[].contact (object)`
- `data[].contact.alias (str): 'string'`
- `data[].contact.bigHeadImgUrl (str): 'string'`
- `data[].contact.bitMask (int): 0`
- `data[].contact.bitVal (int): 0`
- `data[].contact.city (str): 'string'`
- `data[].contact.country (str): 'string'`
- `data[].contact.encryptUserName (str): 'string'`
- `data[].contact.hasWeiXinHdHeadImg (int): 0`
- `data[].contact.imgBuf (object)`
- `data[].contact.imgBuf.buffer (str): 'string'`
- `data[].contact.imgBuf.iLen (int): 0`
- `data[].contact.imgFlag (int): 0`
- `data[].contact.nickName (object)`
- `data[].contact.nickName.String (str): 'string'`
- `data[].contact.province (str): 'string'`
- `data[].contact.pyinitial (object)`
- `data[].contact.pyinitial.String (str): 'string'`
- `data[].contact.quanPin (object)`
- `data[].contact.quanPin.String (str): 'string'`
- `data[].contact.remark (object)`
- `data[].contact.remark.String (str): 'string'`
- `data[].contact.remarkPyinitial (object)`
- `data[].contact.remarkPyinitial.String (str): 'string'`
- `data[].contact.remarkQuanPin (object)`
- `data[].contact.remarkQuanPin.String (str): 'string'`
- `data[].contact.sex (int): 0`
- `data[].contact.smallHeadImgUrl (str): 'string'`
- `data[].contact.snsUserInfo (object)`
- `data[].contact.snsUserInfo.snsFlag (int): 0`
- `data[].contact.textStatusFlag (int): 0`
- `data[].contact.userName (object)`
- `data[].contact.userName.String (str): 'string'`
- `data[].contact.verifyFlag (int): 0`
- `data[].contact.customizedInfo (object)`
- `data[].contact.customizedInfo.brandFlag (int): 0`
- `data[].contact.customizedInfo.brandIconUrl (str): 'string'`
- `data[].contact.customizedInfo.externalInfo (str): 'string'`
- `data[].contact.description (str): 'string'`
- `data[].contact.labelIdlist (str): 'string'`
- `data[].contact.phoneNumListInfo (object)`
- `data[].contact.phoneNumListInfo.count (int): 0`
- `data[].contact.textStatusExtInfo (str): 'string'`
- `data[].contact.textStatusId (str): 'string'`
- `data[].contact.contactType (int): 0`
- `data[].contact.deleteFlag (int): 0`
- `data[].contact.chatroomVersion (int): 0`
- `data[].ret (int): 0`
- `data[].username (str): 'string'`
- `friend_count (int): 0`

### 更新标签名字 — `/api/update_label_name`

- **Method**: `POST`
- **URL path**: `/api/update_label_name`
- **完整 URL**: `http://127.0.0.1:19088/api/update_label_name`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "labelId": 1,
  "newName": "新标签名字"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `labelId (int): 1`
- `newName (str): '新标签名字'`

### 更新单个用户资料 — `/api/update_single_profile`

- **Method**: `POST`
- **URL path**: `/api/update_single_profile`
- **完整 URL**: `http://127.0.0.1:19088/api/update_single_profile`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "filehelper"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): 'filehelper'`

### 修改头像 — `/api/upload_head_img`

- **Method**: `POST`
- **URL path**: `/api/upload_head_img`
- **完整 URL**: `http://127.0.0.1:19088/api/upload_head_img`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "filepath": "D:\\2.png"
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `filepath (str): 'D:\\2.png'`

### 同意好友申请(有变动) — `/api/verify_friend`

- **Method**: `POST`
- **URL path**: `/api/verify_friend`
- **完整 URL**: `http://127.0.0.1:19088/api/verify_friend`
- **Content-Type**: `application/json`

#### 请求示例

```json
{
  "wxid": "wxid_8543785438012",
  "v4": "v4_000b708f0b0400000100000000002c0b06c6e5b066d23eedebc223691000000050ded0b020927e3c97896a09d47e6e9e99a49ffcdc8bbe2894ddc5a9245107f13bb0ac48fe6fcb0345f2da3e01ca2da76c5617ebb83954f66c0b1ac33a3e1958625430adb8ca9c10a47078f33a545ec8b63dc95a456b4bb0fde045d9f6a2d61c4233f94b5c3e7d5ab2f2028ca35feebba88c71f62dd11667@stranger",
  "remark": "9763",
  "label": "13",
  "scene": 3
}
```

#### 响应示例

_无响应体示例_

#### 关键字段

请求:
- `wxid (str): 'wxid_8543785438012'`
- `v4 (str): 'v4_000b708f0b0400000100000000002c0b06c6e5b066d23eedebc223691000000050ded0b020927e3c97896a09d47e6e9e99a49ffcdc8bbe2894dd...`
- `remark (str): '9763'`
- `label (str): '13'`
- `scene (int): 3`

## 同主机非 `/api/*` 路径（附）

- `/kkzs/openwxhb_from_url_v2` — 测试用接口
- `/wx41614/send_card_msg` — 发送名片消息
