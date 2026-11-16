using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace MiltonFretes
{
    internal enum ModState { Missing, Current, Update, Different, Newer }

    internal sealed class InstalledMod
    {
        internal Version Version;
        internal ModState State;
        internal bool HasUnrenamedDownload;
    }

    internal sealed class ModRelease
    {
        internal Version Version;
        internal string Download;
        internal long Size;
        internal string Sha256;
    }

    internal sealed class ModUpdater
    {
        internal const string Repository = "andreltcarvalho/milton-fretes-ets-mod";
        internal const string Releases = "https://github.com/" + Repository + "/releases";
        internal const string InstalledFileName = "Milton Fretes skins.scs";
        private const string AssetName = "milton-fretes.scs";
        private const string ChecksumName = "SHA256SUMS.txt";
        private readonly HttpClient http;

        public sealed class ReleaseResponse
        {
            public string tag_name { get; set; }
            public ReleaseAsset[] assets { get; set; }
        }

        public sealed class ReleaseAsset
        {
            public string name { get; set; }
            public long size { get; set; }
            public string digest { get; set; }
        }

        internal ModUpdater(HttpClient http)
        {
            this.http = http;
        }

        internal async Task<ModRelease> LatestAsync(CancellationToken cancel)
        {
            const string url = "https://api.github.com/repos/" + Repository + "/releases/latest";
            ReleaseResponse info;
            using (var response = await http.GetAsync(url, cancel).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                info = new JavaScriptSerializer().Deserialize<ReleaseResponse>(json);
            }

            var tag = info == null ? "" : info.tag_name ?? "";
            var version = Regex.IsMatch(tag, @"^v[0-9]+\.[0-9]+\.[0-9]+\z") ? ParseVersion(tag.Substring(1)) : null;
            if (version == null || info.assets == null)
                throw new InvalidDataException("A versão publicada no GitHub é inválida.");

            var package = info.assets.FirstOrDefault(asset => asset.name == AssetName);
            var checksums = info.assets.FirstOrDefault(asset => asset.name == ChecksumName);
            if (package == null || package.size <= 0 || checksums == null)
                throw new InvalidDataException("A release está incompleta. Tente novamente mais tarde.");

            var releasePath = Releases + "/download/" + tag + "/";
            string checksum;
            using (var response = await http.GetAsync(releasePath + ChecksumName, cancel).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var contents = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var line = Regex.Match(contents, @"(?m)^([a-fA-F0-9]{64})[ \t]+\*?milton-fretes\.scs[ \t]*\r?$");
                if (!line.Success)
                    throw new InvalidDataException("O GitHub não publicou um SHA-256 válido para o mod.");
                checksum = line.Groups[1].Value;
            }

            if (!String.IsNullOrEmpty(package.digest) && !String.Equals(
                package.digest, "sha256:" + checksum, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("O SHA-256 do arquivo não confere com a release.");

            return new ModRelease
            {
                Version = version,
                Download = releasePath + AssetName,
                Size = package.size,
                Sha256 = checksum
            };
        }

        internal static InstalledMod Inspect(string gameFolder, ModRelease release)
        {
            var path = InstalledPath(gameFolder);
            var unrenamed = HasUnrenamedDownload(gameFolder);
            if (!File.Exists(path))
                return new InstalledMod { State = ModState.Missing, HasUnrenamedDownload = unrenamed };

            var local = ReadVersion(path);
            var state = ModState.Different;
            if (local != null)
            {
                if (local > release.Version)
                    state = ModState.Newer;
                else if (local < release.Version)
                    state = ModState.Update;
                else if (MatchesHash(path, release.Sha256))
                    state = ModState.Current;
            }
            return new InstalledMod { Version = local, State = state, HasUnrenamedDownload = unrenamed };
        }

        internal static bool IsGameFolder(string folder)
        {
            if (!Directory.Exists(folder))
                return false;

            var path = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
            return String.Equals(path, GameFolderSettings.DefaultFolder, StringComparison.OrdinalIgnoreCase) ||
                File.Exists(Path.Combine(folder, "game.log.txt")) ||
                Directory.Exists(Path.Combine(folder, "profiles")) ||
                Directory.Exists(Path.Combine(folder, "steam_profiles"));
        }

        internal async Task InstallAsync(string gameFolder, ModRelease release,
            IProgress<int> progress, CancellationToken cancel)
        {
            var destination = InstalledPath(gameFolder);
            EnsureCanInstall(gameFolder, destination, release.Version);
            var folder = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(folder);
            var staging = Path.Combine(folder, ".milton-fretes-" + Guid.NewGuid().ToString("N") + ".download");
            try
            {
                await DownloadAsync(release, staging, progress, cancel).ConfigureAwait(false);
                if (!MatchesHash(staging, release.Sha256) || ReadVersion(staging) != release.Version)
                    throw new InvalidDataException("O download falhou na verificação. Tente baixar novamente.");

                cancel.ThrowIfCancellationRequested();
                EnsureCanInstall(gameFolder, destination, release.Version);
                if (File.Exists(destination))
                    File.Replace(staging, destination, destination + ".bak");
                else
                    File.Move(staging, destination);
            }
            finally
            {
                if (File.Exists(staging))
                    File.Delete(staging);
            }
        }

        private async Task DownloadAsync(ModRelease release, string path,
            IProgress<int> progress, CancellationToken cancel)
        {
            using (var response = await http.GetAsync(release.Download, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                using (var remote = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var local = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                {
                    var buffer = new byte[65536];
                    long received = 0;
                    var percent = -1;
                    int length;
                    while ((length = await remote.ReadAsync(buffer, 0, buffer.Length, cancel).ConfigureAwait(false)) != 0)
                    {
                        received += length;
                        if (received > release.Size)
                            throw new InvalidDataException("O download veio com um tamanho diferente do anunciado.");
                        await local.WriteAsync(buffer, 0, length, cancel).ConfigureAwait(false);
                        var next = (int)(100 * received / release.Size);
                        if (next != percent)
                        {
                            percent = next;
                            if (progress != null)
                                progress.Report(percent);
                        }
                    }

                    if (received != release.Size)
                        throw new InvalidDataException("O download veio incompleto. Tente novamente.");
                    await local.FlushAsync(cancel).ConfigureAwait(false);
                }
            }
        }

        private static string InstalledPath(string gameFolder)
        {
            return Path.Combine(gameFolder, "mod", InstalledFileName);
        }

        private static bool HasUnrenamedDownload(string gameFolder)
        {
            return File.Exists(Path.Combine(gameFolder, "mod", AssetName));
        }

        private static void EnsureCanInstall(string gameFolder, string destination, Version version)
        {
            if (!IsGameFolder(gameFolder))
                throw new InvalidOperationException("Escolha a pasta de dados do ETS2 antes de instalar.");
            RequireGameClosed();
            if (HasUnrenamedDownload(gameFolder))
                throw new InvalidOperationException("Há um milton-fretes.scs na pasta mod. Confira o nome do arquivo antes de instalar.");
            if (File.Exists(destination) && ReadVersion(destination) > version)
                throw new InvalidOperationException("O mod instalado é mais novo que a release do GitHub.");
        }

        private static Version ReadVersion(string path)
        {
            try
            {
                using (var zip = ZipFile.OpenRead(path))
                {
                    var entry = zip.GetEntry("manifest.sii");
                    if (entry == null || entry.Length > 65536)
                        return null;
                    using (var reader = new StreamReader(entry.Open()))
                    {
                        var match = Regex.Match(reader.ReadToEnd(), @"(?m)^\s*package_version\s*:\s*""([^""]+)""");
                        return match.Success ? ParseVersion(match.Groups[1].Value) : null;
                    }
                }
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        private static Version ParseVersion(string value)
        {
            Version version;
            return Version.TryParse(value, out version) && version.Build >= 0 && version.Revision < 0 ? version : null;
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

        private static void RequireGameClosed()
        {
            var games = Process.GetProcessesByName("eurotrucks2");
            foreach (var game in games)
                game.Dispose();
            if (games.Length > 0)
                throw new InvalidOperationException("Feche o Euro Truck Simulator 2 e tente de novo.");
        }
    }
}
