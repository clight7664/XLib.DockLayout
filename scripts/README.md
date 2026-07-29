# Scripts

| Script | Purpose |
|---|---|
| `static-verify.py` | 无需 .NET SDK 的结构、命名、依赖边界、安全和发布资产检查 |
| `install-playwright.ps1` / `.sh` | E2E 工程完成 restore/build 后安装 Chromium |
| `publish-nuget.ps1` / `.sh` | restore、format、build、test 和 pack；只有显式参数才推送 NuGet |

## Local verification

```bash
python3 scripts/static-verify.py
node --check src/XLib.DockLayout/wwwroot/js/xlib-dock-layout.js
```

## NuGet pack only

```bash
./scripts/publish-nuget.sh 0.1.0
```

```powershell
./scripts/publish-nuget.ps1 -Version 0.1.0
```

## Explicit push

```bash
export NUGET_API_KEY="<secret>"
./scripts/publish-nuget.sh 0.1.0 --push
```

```powershell
$env:NUGET_API_KEY = '<secret>'
./scripts/publish-nuget.ps1 -Version 0.1.0 -Push
```

密钥不得写入仓库、脚本或命令日志。
