using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MiltonFretes
{
    internal static class LauncherSelfUpdate
    {
        internal static void Start(string staged, string target, string sha256)
        {
            if (!MatchesHash(staged, sha256))
                throw new InvalidDataException("O executável deixou de corresponder ao SHA-256 da release.");
            if (!String.Equals(Path.GetDirectoryName(Path.GetFullPath(staged)),
                Path.GetDirectoryName(Path.GetFullPath(target)), StringComparison.OrdinalIgnoreCase))
                throw new IOException("O arquivo de atualização precisa ficar ao lado do launcher.");

            var script = Path.Combine(Path.GetDirectoryName(target),
                ".milton-fretes-update-" + Guid.NewGuid().ToString("N") + ".cmd");
            var body = new StringBuilder();
            body.AppendLine("@echo off");
            body.AppendLine("setlocal DisableDelayedExpansion");
            body.AppendLine("for /L %%i in (1,1,60) do (");
            body.AppendLine("  move /Y \"" + BatchPath(staged) + "\" \"" + BatchPath(target) + "\" >nul 2>&1");
            body.AppendLine("  if not errorlevel 1 goto reopen");
            body.AppendLine("  timeout /t 1 /nobreak >nul");
            body.AppendLine(")");
            body.AppendLine("del \"" + BatchPath(staged) + "\" 2>nul");
            body.AppendLine(":reopen");
            body.AppendLine("start \"\" \"" + BatchPath(target) + "\"");
            body.AppendLine("del \"%~f0\"");
            body.AppendLine("endlocal");

            File.WriteAllText(script, body.ToString(), Encoding.ASCII);
            var command = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            Process.Start(new ProcessStartInfo
            {
                FileName = command,
                Arguments = "/d /c " + Path.GetFileName(script),
                WorkingDirectory = Path.GetDirectoryName(target),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }

        private static string BatchPath(string path)
        {
            return Path.GetFullPath(path).Replace("%", "%%");
        }

        private static bool MatchesHash(string path, string expected)
        {
            using (var stream = File.OpenRead(path))
            using (var sha256 = SHA256.Create())
            {
                var hash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "");
                return String.Equals(expected, hash, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
