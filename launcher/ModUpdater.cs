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
        internal ModPackage Primary;
        internal ModPackage[] Copies;
        internal ModState State;
        internal bool NeedsRename;

        internal bool NeedsDownload
        {
            get { return State != ModState.Current && State != ModState.Newer; }
        }
    }

    internal sealed class ModPackage
    {
        internal string Path;
        internal Version Version;
        internal bool IsDirectory;
    }

    internal sealed class ModRelease
    {
        internal Version Version;
        internal string Download;
        internal long Size;
        internal string Sha256;
    }

    internal sealed class LauncherRelease
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
        internal static readonly Version CurrentLauncherVersion = new Version(1, 1, 0);
        internal const string InstalledFileName = "Milton_Fretes_skins.scs";
        private const string DisplayName = "Milton Fretes skins";
        private const string AssetName = "milton-fretes.scs";
        private const string LauncherAssetName = "milton-fretes-launcher.exe";
        private const string ChecksumName = "SHA256SUMS.txt";
        private readonly HttpClient http;

        public sealed class ReleaseResponse
        {
            public string tag_name { get; set; }
            public ReleaseAsset[] assets { get; set; }
            public bool draft { get; set; }
            public bool prerelease { get; set; }
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

        internal async Task<LauncherRelease> LatestLauncherAsync(CancellationToken cancel)
        {
            const string url = "https://api.github.com/repos/" + Repository + "/releases?per_page=100";
            ReleaseResponse[] releases;
            using (var response = await http.GetAsync(url, cancel).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                releases = new JavaScriptSerializer().Deserialize<ReleaseResponse[]>(json);
            }

            var candidate = (releases ?? new ReleaseResponse[0])
                .Where(item => item != null && !item.draft && !item.prerelease)
                .Select(item => new { Release = item, Version = ParseLauncherTag(item.tag_name) })
                .Where(item => item.Version != null)
                .OrderByDescending(item => item.Version)
                .FirstOrDefault();
            if (candidate == null)
                throw new InvalidDataException("Não há uma versão estável do launcher publicada.");

            var assets = candidate.Release.assets ?? new ReleaseAsset[0];
            var package = assets.FirstOrDefault(asset => asset.name == LauncherAssetName);
            var checksums = assets.FirstOrDefault(asset => asset.name == ChecksumName);
            if (package == null || package.size <= 0 || checksums == null)
                throw new InvalidDataException("A release do launcher está incompleta.");

            var releasePath = Releases + "/download/" + candidate.Release.tag_name + "/";
            string checksum;
            using (var response = await http.GetAsync(releasePath + ChecksumName, cancel).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var contents = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var line = Regex.Match(contents, @"(?m)^([a-fA-F0-9]{64})[ \t]+\*?milton-fretes-launcher\.exe[ \t]*\r?$");
                if (!line.Success)
                    throw new InvalidDataException("O GitHub não publicou um SHA-256 válido para o launcher.");
                checksum = line.Groups[1].Value;
            }
            if (!String.IsNullOrEmpty(package.digest) && !String.Equals(
                package.digest, "sha256:" + checksum, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("O SHA-256 do launcher não confere com a release.");

            return new LauncherRelease
            {
                Version = candidate.Version,
                Download = releasePath + LauncherAssetName,
                Size = package.size,
                Sha256 = checksum
            };
        }

        internal async Task<string> DownloadLauncherAsync(LauncherRelease release, CancellationToken cancel)
        {
            var path = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath),
                ".milton-fretes-launcher-" + Guid.NewGuid().ToString("N") + ".download");
            try
            {
                long received = 0;
                using (var response = await http.GetAsync(release.Download, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    using (var remote = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var local = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                    {
                        var buffer = new byte[65536];
                        int length;
                        while ((length = await remote.ReadAsync(buffer, 0, buffer.Length, cancel).ConfigureAwait(false)) != 0)
                        {
                            received += length;
                            if (received > release.Size)
                                throw new InvalidDataException("O download do launcher veio com tamanho inválido.");
                            await local.WriteAsync(buffer, 0, length, cancel).ConfigureAwait(false);
                        }
                        await local.FlushAsync(cancel).ConfigureAwait(false);
                    }
                }
                if (received != release.Size || !MatchesHash(path, release.Sha256))
                    throw new InvalidDataException("O download do launcher não passou na verificação SHA-256.");
                return path;
            }
            catch
            {
                if (File.Exists(path))
                    File.Delete(path);
                throw;
            }
        }

        private static Version ParseLauncherTag(string tag)
        {
            return Regex.IsMatch(tag ?? "", @"^launcher-v[0-9]+\.[0-9]+\.[0-9]+\z")
                ? ParseVersion(tag.Substring("launcher-v".Length)) : null;
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
            var folder = Path.Combine(gameFolder, "mod");
            var packages = Directory.Exists(folder) ? Directory.EnumerateFileSystemEntries(folder)
                .Select(FindPackage)
                .Where(package => package != null)
                .OrderByDescending(package => package.Version)
                .ThenByDescending(package => IsDefaultName(package.Path, package.IsDirectory))
                .ThenBy(package => package.IsDirectory)
                .ThenBy(package => package.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray() : new ModPackage[0];

            var primary = packages.FirstOrDefault();
            var state = ModState.Missing;
            if (primary != null)
            {
                state = ModState.Different;
                if (primary.Version > release.Version)
                    state = ModState.Newer;
                else if (primary.Version != null && primary.Version < release.Version)
                    state = ModState.Update;
                else if (primary.Version == release.Version && !primary.IsDirectory &&
                    MatchesHash(primary.Path, release.Sha256))
                    state = ModState.Current;
            }
            return new InstalledMod
            {
                Primary = primary,
                State = state,
                Copies = packages.Skip(1).ToArray(),
                NeedsRename = primary != null && !IsDefaultName(primary.Path, primary.IsDirectory)
            };
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

        internal async Task<InstalledMod> InstallAsync(string gameFolder, ModRelease release,
            IProgress<int> progress, CancellationToken cancel)
        {
            if (!IsGameFolder(gameFolder))
                throw new InvalidOperationException("Escolha a pasta de dados do ETS2 antes de instalar.");
            RequireGameClosed();

            var folder = Path.Combine(gameFolder, "mod");
            var installed = Inspect(gameFolder, release);
            string staging = null;
            try
            {
                if (installed.NeedsDownload)
                {
                    Directory.CreateDirectory(folder);
                    staging = Path.Combine(folder, ".milton-fretes-" + Guid.NewGuid().ToString("N") + ".download");
                    await DownloadAsync(release, staging, progress, cancel).ConfigureAwait(false);
                    var package = ReadPackage(staging, false);
                    if (!MatchesHash(staging, release.Sha256) || package == null || package.Version != release.Version)
                        throw new InvalidDataException("O download falhou na verificação. Tente baixar novamente.");

                    installed = Inspect(gameFolder, release);
                }

                cancel.ThrowIfCancellationRequested();
                RequireGameClosed();
                ApplyChanges(gameFolder, folder, installed, staging);
                return Inspect(gameFolder, release);
            }
            finally
            {
                if (staging != null && File.Exists(staging))
                    File.Delete(staging);
            }
        }

        private static void ApplyChanges(string gameFolder, string folder, InstalledMod installed, string staging)
        {
            var primary = installed.Primary;
            var replace = installed.NeedsDownload && staging != null;
            var keepFolder = !replace && primary != null && primary.IsDirectory;
            var destination = Path.Combine(folder, keepFolder ?
                Path.GetFileNameWithoutExtension(InstalledFileName) : InstalledFileName);
            var primaryAtDestination = primary != null && SamePath(primary.Path, destination);
            if (!primaryAtDestination && (File.Exists(destination) || Directory.Exists(destination)) &&
                !installed.Copies.Any(copy => SamePath(copy.Path, destination)))
                throw new InvalidOperationException("O nome do arquivo que seria instalado já é usado por outro mod.");

            string backup = null;
            if (installed.Copies.Length != 0 || (replace && primary != null))
            {
                var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                backup = Path.Combine(gameFolder, "Milton Fretes backups", stamp);
                Directory.CreateDirectory(backup);
            }

            foreach (var copy in installed.Copies)
                MovePackage(copy.Path, Path.Combine(backup, Path.GetFileName(copy.Path)), copy.IsDirectory);

            if (!replace)
            {
                if (primary != null && !primaryAtDestination)
                    MovePackage(primary.Path, destination, primary.IsDirectory);
                return;
            }
            if (primary == null)
            {
                File.Move(staging, destination);
                return;
            }

            var backedUp = Path.Combine(backup, Path.GetFileName(primary.Path));
            if (primaryAtDestination && !primary.IsDirectory)
            {
                File.Copy(destination, backedUp);
                File.Replace(staging, destination, null);
                return;
            }

            MovePackage(primary.Path, backedUp, primary.IsDirectory);
            try
            {
                File.Move(staging, destination);
            }
            catch
            {
                MovePackage(backedUp, primary.Path, primary.IsDirectory);
                throw;
            }
        }

        private static void MovePackage(string source, string destination, bool isDirectory)
        {
            if (isDirectory)
                Directory.Move(source, destination);
            else
                File.Move(source, destination);
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

        private static bool SamePath(string left, string right)
        {
            return String.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDefaultName(string path, bool isDirectory)
        {
            return SamePath(Path.GetFileName(path), isDirectory ?
                Path.GetFileNameWithoutExtension(InstalledFileName) : InstalledFileName);
        }

        private static ModPackage FindPackage(string path)
        {
            var isDirectory = Directory.Exists(path);
            var extension = Path.GetExtension(path);
            if (!isDirectory && !SamePath(extension, ".scs") && !SamePath(extension, ".zip"))
                return null;
            return ReadPackage(path, isDirectory);
        }

        private static ModPackage ReadPackage(string path, bool isDirectory)
        {
            var manifest = ReadManifest(path, isDirectory);
            if (manifest == null)
            {
                // Preserve repair for a damaged archive at the default installation path.
                return !isDirectory && IsDefaultName(path, false) ? new ModPackage { Path = path } : null;
            }
            if (!String.Equals(ManifestValue(manifest, "display_name"), DisplayName, StringComparison.OrdinalIgnoreCase))
                return null;

            Version version;
            Version.TryParse(ManifestValue(manifest, "package_version"), out version);
            if (version != null && version.Revision <= 0)
                version = new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
            return new ModPackage { Path = path, Version = version, IsDirectory = isDirectory };
        }

        private static string ManifestValue(string manifest, string key)
        {
            var setting = Regex.Match(manifest, @"(?m)^[ \t]*" + key + @"[ \t]*:[ \t]*""([^""\r\n]+)""");
            return setting.Success ? setting.Groups[1].Value.Trim() : null;
        }

        private static string ReadManifest(string path, bool isDirectory)
        {
            if (isDirectory)
            {
                var manifest = new FileInfo(Path.Combine(path, "manifest.sii"));
                return manifest.Exists && manifest.Length <= 65536 ? File.ReadAllText(manifest.FullName) : null;
            }

            try
            {
                using (var zip = ZipFile.OpenRead(path))
                {
                    var entry = zip.GetEntry("manifest.sii");
                    if (entry == null || entry.Length > 65536)
                        return null;
                    using (var reader = new StreamReader(entry.Open()))
                        return reader.ReadToEnd();
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
