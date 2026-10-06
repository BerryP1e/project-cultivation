# Windows 打包与发布

目标平台为 Windows x64，使用团结 1.6.13 / 2022.3.61t14。软件包发布到 [GitHub Releases](https://github.com/BerryP1e/project-cultivation/releases)，二进制文件不进入源码仓库。

## 构建

先保存手工场景修改并退出 Play，然后执行编辑器菜单 `Cultivation / Release / Build Windows x64`。构建工具只读取已保存的场景，不调用场景生成器，不保存或重建场景。

入口是 `StartScene`，采用 Build Settings 中已启用、实际存在的场景；同一场景的大小写重复项只构建一次。动态查找的自定义着色器会登记到 Graphics Settings，避免正式运行时被剥离。

输出程序为 `cultivation/Builds/Windows-x64/cultivation.exe`，构建报告为 `cultivation/Builds/windows-release-report.json`。必须确认报告 `status` 为 `Succeeded` 且 `errors` 为 0。

也可以在编辑器未占用该工程时进行命令行构建：

```powershell
& 'D:\Tuanjie 2022.3.61t14\Editor\Tuanjie.exe' -batchmode -projectPath 'D:\project：cultivation\cultivation' -executeMethod WindowsReleaseBuilder.Build -logFile 'D:\project：cultivation\cultivation\Builds\build.log'
```

`Assets/Editor` 下的工具代码不会进入软件包，包括 `DshBridge`，无需删除源码。

## 软件包

保留同一输出目录下的 exe、数据目录、引擎 DLL 和 Mono 运行库，整目录压缩成 ZIP。解压到可写目录后运行 `cultivation.exe`；不能只复制 exe。

正式上传前检查程序启动日志。发布说明标明对应源码提交，附上 ZIP 和 SHA-256 校验文件。存档由游戏写入系统用户数据目录，不随软件包打包。邮箱恢复文档、访问令牌和临时诊断文件不发布。
