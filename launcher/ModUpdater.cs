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
        internal string BackupFolder;

        public override string ToString()
        {
            var version = Version == null ? "versão desconhecida" : "v" + Version;
            var backup = String.IsNullOrEmpty(BackupFolder) ? "" : " — backup " + BackupFolder;
            return version + backup + " — " + System.IO.Path.GetFileName(Path);
        }
    }

    internal sealed class ModRelease
    {
        internal Version Version;
        internal string Download;
        internal long Size;
        internal string Sha256;
        internal string Notes;
    }

    internal sealed class LauncherRelease
    {
        internal Version Version;
        internal string Download;
        internal long Size;
        internal string Sha256;
        internal string Notes;
    }

    internal sealed class ModUpdater
    {
        internal const string Repository = "andreltcarvalho/milton-fretes-ets-mod";
        internal const string Releases = "https://github.com/" + Repository + "/releases";
        internal const string LauncherVersion = "1.3.2";
        internal static readonly Version CurrentLauncherVersion = new Version(LauncherVersion);
        internal const string InstalledFileName = "Milton_Fretes_skins.scs";
        private const string DisplayName = "Milton Fretes skins";
        private const string AssetName = "milton-fretes.scs";
        private const string LauncherAssetName = "milton-fretes-launcher.exe";
        private const string ChecksumName = "SHA256SUMS.txt";
        private readonly HttpClient http;

        public sealed class ReleaseResponse
        {
            public string tag_name { get; set; }
            public string body { get; set; }
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

        internal async Task<ReleaseResponse[]> ListStableReleasesAsync(CancellationToken cancel)
        {
            const string url = "https://api.github.com/repos/" + Repository + "/releases?per_page=100";
            ReleaseResponse[] releases;
            using (var response = await http.GetAsync(url, cancel).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                releases = new JavaScriptSerializer().Deserialize<ReleaseResponse[]>(json);
            }

            return (releases ?? new ReleaseResponse[0])
                .Where(item => item != null && !item.draft && !item.prerelease)
                .ToArray();
        }

        internal async Task<LauncherRelease> LatestLauncherAsync(CancellationToken cancel)
        {
            var releases = await ListStableReleasesAsync(cancel).ConfigureAwait(false);
            return await LatestLauncherAsync(cancel, releases).ConfigureAwait(false);
        }

        internal Version LatestLauncherVersion(ReleaseResponse[] releases)
        {
            var candidate = releases
                .Select(item => new { Release = item, Version = ParseLauncherTag(item.tag_name) })
                .Where(item => item.Version != null)
                .OrderByDescending(item => item.Version)
                .FirstOrDefault();
            if (candidate == null)
                return null;

            var assets = candidate.Release.assets ?? new ReleaseAsset[0];
            var package = assets.FirstOrDefault(asset => asset.name == LauncherAssetName);
            var checksums = assets.FirstOrDefault(asset => asset.name == ChecksumName);
            if (package == null || package.size <= 0 || checksums == null)
                return null;

            return candidate.Version;
        }

        private async Task<LauncherRelease> LatestLauncherAsync(CancellationToken cancel, ReleaseResponse[] releases)
        {
            var candidate = releases
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
            var checksum = Sha256FromDigest(package.digest);
            if (checksum == null)
            {
                using (var response = await http.GetAsync(releasePath + ChecksumName, cancel).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    var contents = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var line = Regex.Match(contents, @"(?m)^([a-fA-F0-9]{64})[ \t]+\*?milton-fretes-launcher\.exe[ \t]*\r?$");
                    if (!line.Success)
                        throw new InvalidDataException("O GitHub não publicou um SHA-256 válido para o launcher.");
                    checksum = line.Groups[1].Value;
                }
            }

            return new LauncherRelease
            {
                Version = candidate.Version,
                Download = releasePath + LauncherAssetName,
                Size = package.size,
                Sha256 = checksum,
                Notes = candidate.Release.body
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

        private static string Sha256FromDigest(string digest)
        {
            if (String.IsNullOrEmpty(digest))
                return null;
            var match = Regex.Match(digest, @"^sha256:([a-fA-F0-9]{64})$");
            if (!match.Success)
                throw new InvalidDataException("O GitHub publicou um digest SHA-256 inválido para o arquivo.");
            return match.Groups[1].Value;
        }

        internal async Task<ModRelease> LatestAsync(CancellationToken cancel)
        {
            var releases = await ListStableReleasesAsync(cancel).ConfigureAwait(false);
            return await LatestAsync(cancel, releases).ConfigureAwait(false);
        }

        internal async Task<ModRelease> LatestAsync(CancellationToken cancel, ReleaseResponse[] releases)
        {
            var candidate = releases
                .Where(item => Regex.IsMatch(item.tag_name ?? "", @"^v[0-9]+\.[0-9]+\.[0-9]+\z"))
                .Select(item => new { Release = item, Version = ParseVersion(item.tag_name.Substring(1)) })
                .Where(item => item.Version != null)
                .OrderByDescending(item => item.Version)
                .FirstOrDefault();
            if (candidate == null)
                throw new InvalidDataException("Não há uma versão estável do mod publicada.");

            var info = candidate.Release;
            var assets = info.assets ?? new ReleaseAsset[0];
            var package = assets.FirstOrDefault(asset => asset.name == AssetName);
            var checksums = assets.FirstOrDefault(asset => asset.name == ChecksumName);
            if (package == null || package.size <= 0 || checksums == null)
                throw new InvalidDataException("A release está incompleta. Tente novamente mais tarde.");

            var releasePath = Releases + "/download/" + info.tag_name + "/";
            var checksum = Sha256FromDigest(package.digest);
            if (checksum == null)
            {
                using (var response = await http.GetAsync(releasePath + ChecksumName, cancel).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    var contents = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var line = Regex.Match(contents, @"(?m)^([a-fA-F0-9]{64})[ \t]+\*?milton-fretes\.scs[ \t]*\r?$");
                    if (!line.Success)
                        throw new InvalidDataException("O GitHub não publicou um SHA-256 válido para o mod.");
                    checksum = line.Groups[1].Value;
                }
            }

            return new ModRelease
            {
                Version = candidate.Version,
                Download = releasePath + AssetName,
                Size = package.size,
                Sha256 = checksum,
                Notes = info.body
            };
        }

        internal static ModPackage[] ListBackups(string gameFolder)
        {
            var root = Path.Combine(gameFolder, "Milton Fretes backups");
            if (!Directory.Exists(root))
                return new ModPackage[0];

            return Directory.EnumerateDirectories(root)
                .SelectMany(folder => Directory.EnumerateFileSystemEntries(folder)
                    .Select(FindPackage)
                    .Where(package => package != null)
                    .Select(package =>
                    {
                        package.BackupFolder = Path.GetFileName(folder);
                        return package;
                    }))
                .OrderByDescending(package => package.Version)
                .ThenByDescending(package => Path.GetFileName(Path.GetDirectoryName(package.Path)), StringComparer.OrdinalIgnoreCase)
                .ThenBy(package => package.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal static ModPackage RestoreBackup(string gameFolder, string backupPath)
        {
            if (!IsGameFolder(gameFolder))
                throw new InvalidOperationException("Escolha a pasta de dados do ETS2 antes de restaurar.");
            RequireGameClosed();

            var backupRoot = Path.GetFullPath(Path.Combine(gameFolder, "Milton Fretes backups"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var source = Path.GetFullPath(backupPath);
            if (!source.StartsWith(backupRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("O backup selecionado não está na pasta de backups do Milton Fretes.");
            var selected = FindPackage(source);
            if (selected == null)
                throw new InvalidDataException("O backup selecionado não contém um pacote válido do Milton Fretes.");

            var folder = Path.Combine(gameFolder, "mod");
            Directory.CreateDirectory(folder);
            var installed = Directory.EnumerateFileSystemEntries(folder)
                .Select(FindPackage)
                .Where(package => package != null)
                .ToArray();
            var destination = Path.Combine(folder, selected.IsDirectory ?
                Path.GetFileNameWithoutExtension(InstalledFileName) : InstalledFileName);
            if ((File.Exists(destination) || Directory.Exists(destination)) &&
                !installed.Any(package => SamePath(package.Path, destination)))
                throw new InvalidOperationException("O nome padrão do mod está sendo usado por outro arquivo. Ele foi mantido intacto.");

            string safetyBackup = null;
            if (installed.Length != 0)
            {
                var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                safetyBackup = Path.Combine(backupRoot.TrimEnd(Path.DirectorySeparatorChar), stamp);
                Directory.CreateDirectory(safetyBackup);
            }

            var moved = new System.Collections.Generic.List<ModPackage>();
            try
            {
                foreach (var package in installed)
                {
                    var target = Path.Combine(safetyBackup, Path.GetFileName(package.Path));
                    MovePackage(package.Path, target, package.IsDirectory);
                    moved.Add(package);
                }
                MovePackage(source, destination, selected.IsDirectory);
                TryRemoveEmptyBackupFolder(Path.GetDirectoryName(source), backupRoot.TrimEnd(Path.DirectorySeparatorChar));
                return ReadPackage(destination, selected.IsDirectory);
            }
            catch
            {
                foreach (var package in moved.AsEnumerable().Reverse())
                {
                    var target = Path.Combine(safetyBackup, Path.GetFileName(package.Path));
                    if ((File.Exists(target) || Directory.Exists(target)) &&
                        !File.Exists(package.Path) && !Directory.Exists(package.Path))
                        MovePackage(target, package.Path, package.IsDirectory);
                }
                TryRemoveEmptyBackupFolder(safetyBackup, backupRoot.TrimEnd(Path.DirectorySeparatorChar));
                throw;
            }
        }

        private static void TryRemoveEmptyBackupFolder(string folder, string root)
        {
            try
            {
                if (!String.IsNullOrEmpty(folder) && Directory.Exists(folder) &&
                    !Directory.EnumerateFileSystemEntries(folder).Any())
                    Directory.Delete(folder);
                if (Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root).Any())
                    Directory.Delete(root);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
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
