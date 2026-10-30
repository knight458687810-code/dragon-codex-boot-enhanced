# 使用与配置

## 默认模式

完整解压后运行 `DragonCodexBoot.exe`。不要在压缩包内直接运行，也不要只拷贝 exe：`launcher.json`、视频、首帧和十张交互帧均需要保留。

配置：`FollowClientWindow=true`，`MatchClientToPlayer=false`，`InteractiveStart=true`，`StartupCurtain=true`。`PlayerWidth/Height` 是兼容/回退偏好，不会强制把实际客户端调整为 1440×810。

窗口和媒体独立：遮罩跟随客户区，内部视频等比16:9居中。最小化根据恢复位置与边距/DPI换算；隐藏窗口读实际客户区；销毁后新窗依赖最近普通窗口记录和新窗口事件校准。无可靠信息不显示猜测全屏遮罩。

## 快捷方式

- `Install-Start-Menu.cmd`：只创建/管理当前用户的独立 `Dragon Codex Boot.lnk`。卸载用 `Uninstall-Start-Menu.cmd`。
- `Rescan-Entrypoints.cmd`：可选，检查与配置匹配的官方快捷方式并备份替换；公开配置 `ScanAllLocalDrives=false`，不遍历所有磁盘。不是接管所有调用方式。
- `Restore-Original-Entrypoints.cmd`：恢复仍符合安装记录的批量替换入口，用户后来修改过的入口会保留。
- 先恢复快捷方式，再删除或移动启动器目录；固定任务栏/开始菜单项可能需要手动处理。

## 定位失败

默认使用 `shell:AppsFolder\OpenAI.Codex_2p2nqsd0c76g0!App`，进程名 `ChatGPT` / `Codex`，路径过滤 `OpenAI.Codex_`。安装渠道不同或官方标识变化时，使用 `Get-StartApps` 核对并修改 `AppLaunch`、`ProcessNames`、`ProcessPathContains`。不要绑定带版本号的 WindowsApps exe 路径。

启动失败时查看自己机器上的 `logs/launcher.log`；分享日志前检查路径信息。`--self-test` 仅检查配置和几何，结果写入 `qa/self-test.json`，不会打开桌面窗口或媒体。

## 自定义视频

原视频保持不变。`scripts/Import-Animation.ps1` 只负责导入视频，不会自动重做交互帧、首帧、热区、等待节点和交接关键帧。替换视频需要开发者重新标定相关资源，否则应关闭 `InteractiveStart` 和 `StartupCurtain` 并自行调整过渡设置；不保证与任意视频兼容。
