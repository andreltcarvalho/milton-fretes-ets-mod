using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Milton Fretes Launcher")]
[assembly: AssemblyProduct("Milton Fretes")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]

namespace MiltonFretes
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool first;
            using (var instance = new Mutex(true, @"Local\MiltonFretesLauncher", out first))
            {
                if (!first)
                {
                    MessageBox.Show("O launcher do Milton Fretes já está aberto.", "Milton Fretes",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var http = CreateHttpClient())
                    Application.Run(new MainForm(new ModUpdater(http), GameFolderSettings.Load()));
            }
        }

        internal static HttpClient CreateHttpClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MiltonFretesLauncher/1.0.1");
            http.Timeout = TimeSpan.FromSeconds(40);
            http.MaxResponseContentBufferSize = 1024 * 1024;
            return http;
        }
    }

    internal static class GameFolderSettings
    {
        private static readonly string Settings = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MiltonFretesLauncher", "ets2-folder.txt");
        internal static readonly string DefaultFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Euro Truck Simulator 2");

        internal static string Load()
        {
            try
            {
                if (File.Exists(Settings))
                {
                    var folder = File.ReadAllText(Settings).Trim();
                    if (Path.IsPathRooted(folder))
                        return Path.GetFullPath(folder);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
            return DefaultFolder;
        }

        internal static void Save(string folder)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Settings));
            File.WriteAllText(Settings, folder);
        }
    }
}
