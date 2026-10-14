using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace GameSystem.Editor
{
    /// <summary>Windows 构建完成后生成便携启动入口，启动前将日志与进程临时目录定位到游戏目录。</summary>
    internal sealed class WindowsPortableBuild : IPostprocessBuildWithReport
    {
        /// <summary>使用默认顺序处理构建后回调。</summary>
        public int callbackOrder => 0;

        /// <summary>Windows 可执行文件生成后创建入口，不依赖回调期间尚未最终确定的构建结果。</summary>
        /// <param name="report">包含输出路径与目标平台的构建报告。</param>
        public void OnPostprocessBuild(BuildReport report)
        {
            if (!File.Exists(report.summary.outputPath))
            {
                return;
            }

            GenerateLauncher(report.summary.outputPath, report.summary.platform);
        }

        /// <summary>生成相对自身定位的入口；确认数据目录可写后才启动，避免静默丢失存档或日志。</summary>
        /// <param name="outputPath">Windows 可执行文件输出路径。</param>
        /// <param name="target">构建目标平台。</param>
        private static void GenerateLauncher(string outputPath, BuildTarget target)
        {
            if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64)
            {
                return;
            }

            string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            string executable = Path.GetFileName(outputPath).Replace("%", "%%");
            // UTF-8 切换后才读取含中文的命令；禁用延迟展开以保留路径中的感叹号。
            string launcher = "@echo off\r\n"
                + "setlocal DisableDelayedExpansion\r\n"
                + "chcp 65001 >nul\r\n"
                + "set \"probe=.portable-write-%RANDOM%-%RANDOM%.tmp\"\r\n"
                + "for %%D in (SaveData Logs Temp) do (\r\n"
                + "  if not exist \"%~dp0%%D\\\" mkdir \"%~dp0%%D\"\r\n"
                + "  if not exist \"%~dp0%%D\\\" goto failed\r\n"
                + "  >\"%~dp0%%D\\%probe%\" echo writable\r\n"
                + "  if errorlevel 1 goto failed\r\n"
                + "  del \"%~dp0%%D\\%probe%\"\r\n"
                + "  if exist \"%~dp0%%D\\%probe%\" goto failed\r\n"
                + ")\r\n"
                + "set \"TEMP=%~dp0Temp\"\r\n"
                + "set \"TMP=%~dp0Temp\"\r\n"
                + $"\"%~dp0{executable}\" -logFile \"%~dp0Logs\\Player.log\"\r\n"
                + "exit /b %errorlevel%\r\n"
                + ":failed\r\n"
                + "echo Cannot create local data folders. Extract the game to a writable folder.\r\n"
                + "pause\r\n"
                + "exit /b 1\r\n";

            File.WriteAllText(Path.Combine(directory, "启动游戏.cmd"), launcher, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(directory, "便携版说明.txt"),
                "请通过“启动游戏.cmd”启动游戏。\r\n"
                + "存档、设置和按键配置位于 SaveData；运行日志位于 Logs；进程临时文件位于 Temp。\r\n"
                + "请解压到可写目录，移动游戏时复制整个文件夹；更新游戏时保留 SaveData。\r\n"
                + "直接运行 exe 会绕过本地日志与临时目录设置。系统和显卡驱动缓存不由本入口控制。\r\n",
                new UTF8Encoding(true));
        }
    }
}
