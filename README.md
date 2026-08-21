# 微信 AI 聊天助手 (WechatAIClient)

Avalonia + .NET 8 桌面客户端（Phase 1：Mock 微信 / Mock AI）。

## 环境

- .NET 8 SDK
- Windows x64

## 还原与构建

```powershell
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
cd "E:\我的源码目录\微信聊天"
dotnet restore WechatAIClient\WechatAIClient.csproj
dotnet build WechatAIClient\WechatAIClient.csproj -c Release
```

NuGet 使用仓库根目录 `NuGet.Config` 中的华为云镜像。

## 运行

```powershell
dotnet run --project WechatAIClient\WechatAIClient.csproj -c Release
```

## 发布单文件 EXE

```powershell
dotnet publish WechatAIClient\WechatAIClient.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "E:\我的源码目录\微信聊天\publish"
```

发布产物：`publish\WechatAIClient.exe`

## 功能概览

- 三栏玻璃拟态 UI：联系人 / 聊天 / AI 助手
- 深色 / 浅色 / 跟随系统主题
- Mock 联系人、群聊与消息
- Mock DeepSeek 风格 AI 回复（自动 / 手动确认 / 关闭）
