# LumaWake

Windows 11 局域网 WOL 桌面软件，使用 C# WinForms 和 Windows 自带的 .NET Framework，不需要安装第三方运行库。

## 运行

双击 `release\LumaWake-Windows11.exe` 即可启动。应用图标资源是 `assets\lumawake.ico`，已嵌入 EXE。

设备列表保存在 `%APPDATA%\LumaWake\devices.json`。添加设备时填写设备名称、MAC 地址和局域网 IP。

## 重新编译

在装有 Windows .NET Framework 4.x 开发工具的机器上运行：

```powershell
New-Item -ItemType Directory -Force .\release | Out-Null
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /out:release\LumaWake-Windows11.exe /platform:anycpu /win32icon:assets\lumawake.ico /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll LumaWakeStandalone.cs
```

## 功能和状态说明

- 通过 UDP 广播（端口 9）发送标准 WOL 魔术包。
- 每 5 秒对设备 IP 执行 ICMP Ping，显示在线或离线。
- “在线”表示该 IP 响应 Ping，不代表可以查看目标电脑的登录用户、CPU、应用等系统信息。要读取更完整的系统状态，需要在目标电脑部署配套服务。
- 目标电脑的 BIOS/UEFI、网卡驱动和电源设置需启用 Wake-on-LAN。路由器或 VLAN 的广播策略也可能影响唤醒。
