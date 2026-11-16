using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MiltonFretes
{
    internal sealed class MainForm : Form
    {
        private static readonly bool HighContrast = SystemInformation.HighContrast;
        private static readonly Color Navy = HighContrast ? SystemColors.Window : Color.FromArgb(17, 27, 42);
        private static readonly Color Graphite = HighContrast ? SystemColors.Window : Color.FromArgb(31, 44, 62);
        private static readonly Color Ink = HighContrast ? SystemColors.WindowText : Color.FromArgb(244, 249, 253);
        private static readonly Color Muted = HighContrast ? Ink : Color.FromArgb(181, 199, 220);
        private static readonly Color Blue = HighContrast ? Ink : Color.FromArgb(109, 204, 249);
        private static readonly Color Amber = HighContrast ? Ink : Color.FromArgb(255, 207, 126);

        private readonly ModUpdater updater;
        private readonly Label status;
        private readonly Label detail;
        private readonly Label installedVersion;
        private readonly Label releaseVersion;
        private readonly TextBox folderField;
        private readonly Button folderButton;
        private readonly Button checkButton;
        private readonly Button installButton;
        private readonly Button launcherUpdateButton;
        private readonly ProgressBar progressBar;
        private CancellationTokenSource operation;
        private bool closeAfterOperation;
        private string gameFolder;

        internal MainForm(ModUpdater updater, string gameFolder)
        {
            this.updater = updater;
            this.gameFolder = gameFolder;
            SuspendLayout();
            Text = "Milton Fretes Launcher";
            Font = new Font("Segoe UI", 10F);
            BackColor = Navy;
            ForeColor = Ink;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ClientSize = new Size(620, 456);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            var wordmark = new FlowLayoutPanel
            {
                Location = new Point(30, 21),
                Size = new Size(550, 53),
                WrapContents = false
            };
            var titleFont = new Font("Segoe UI", 28F, FontStyle.Bold | FontStyle.Italic);
            wordmark.Controls.Add(new Label { Text = "MILTON", AutoSize = true, Font = titleFont, Margin = Padding.Empty });
            wordmark.Controls.Add(new Label { Text = "FRETES", AutoSize = true, Font = titleFont, ForeColor = Blue, Margin = Padding.Empty });
            Controls.Add(wordmark);
            Controls.Add(MakeLabel("Atualizador de mod para Euro Truck Simulator 2", 33, 78, 550, 24, Muted));

            var summary = new Panel { Location = new Point(32, 115), Size = new Size(556, 151), BackColor = Graphite };
            status = MakeLabel("Verificando atualizações…", 17, 12, 520, 30, Ink);
            status.Font = new Font(Font.FontFamily, 15F, FontStyle.Bold);
            detail = MakeLabel("Consultando a release mais recente no GitHub.", 17, 46, 522, 43, Muted);
            summary.Controls.Add(status);
            summary.Controls.Add(detail);
            summary.Controls.Add(new Panel { Location = new Point(17, 93), Size = new Size(522, 1), BackColor = HighContrast ? Ink : Color.FromArgb(63, 82, 105) });
            summary.Controls.Add(MakeLabel("Versão instalada", 17, 103, 256, 22, Muted));
            summary.Controls.Add(MakeLabel("Versão no GitHub", 286, 103, 248, 22, Muted));
            installedVersion = MakeLabel("—", 17, 123, 256, 24, Ink, true);
            releaseVersion = MakeLabel("—", 286, 123, 248, 24, Ink, true);
            summary.Controls.Add(installedVersion);
            summary.Controls.Add(releaseVersion);
            Controls.Add(summary);

            Controls.Add(MakeLabel("Pasta de dados do ETS2", 32, 279, 550, 24, Ink, true));
            folderField = new TextBox
            {
                AccessibleName = "Pasta de dados do ETS2",
                Location = new Point(32, 307),
                Width = 444,
                BackColor = Graphite,
                ForeColor = Ink,
                ReadOnly = true,
                Text = gameFolder,
                TabIndex = 0
            };
            Controls.Add(folderField);
            folderButton = AddButton("Escolher…", 488, 302, 100, 34, 1, false);
            folderButton.Click += ChooseFolder;

            progressBar = new ProgressBar
            {
                AccessibleName = "Progresso da atualização",
                Location = new Point(32, 345),
                Size = new Size(556, 7),
                Visible = false,
                MarqueeAnimationSpeed = 26
            };
            Controls.Add(progressBar);

            var releasesLink = new LinkLabel
            {
                Text = "Ver última versão",
                Location = new Point(32, 367),
                Size = new Size(140, 32),
                TextAlign = ContentAlignment.MiddleLeft,
                LinkColor = Blue,
                VisitedLinkColor = Blue,
                ActiveLinkColor = Ink,
                TabIndex = 2
            };
            releasesLink.LinkClicked += OpenReleases;
            Controls.Add(releasesLink);
            checkButton = AddButton("Verificar agora", 415, 362, 173, 40, 3, false);
            installButton = AddButton("Instalar mod", 415, 362, 173, 40, 4, true);
            installButton.Enabled = false;
            installButton.Visible = false;
            launcherUpdateButton = AddButton("Atualizar launcher", 415, 410, 173, 34, 5, false);
            launcherUpdateButton.Enabled = false;
            launcherUpdateButton.Visible = false;
            checkButton.Click += async (sender, args) => await RunAsync(false);
            installButton.Click += async (sender, args) => await RunAsync(true);
            launcherUpdateButton.Click += async (sender, args) => await UpdateLauncherAsync();
            Shown += async (sender, args) => await RunAsync(false);
            ResumeLayout(false);
        }

        private async Task RunAsync(bool install)
        {
            if (operation != null)
                return;

            operation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var token = operation.Token;
            checkButton.Enabled = installButton.Enabled = launcherUpdateButton.Enabled = folderButton.Enabled = false;
            installButton.Visible = false;
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.Visible = true;
            if (!install)
                releaseVersion.Text = "—";
            SetStatus("Verificando atualizações…", "Consultando a release mais recente no GitHub.", Ink);
            bool canInstall = false;
            try
            {
                var release = await updater.LatestAsync(token);
                releaseVersion.Text = "v" + release.Version;
                var launcherRelease = await updater.LatestLauncherAsync(token);
                launcherUpdateButton.Visible = launcherRelease.Version > ModUpdater.CurrentLauncherVersion;
                if (launcherUpdateButton.Visible)
                    launcherUpdateButton.Text = "Atualizar launcher v" + launcherRelease.Version;
                if (!ModUpdater.IsGameFolder(gameFolder))
                {
                    installedVersion.Text = "—";
                    SetStatus("Pasta do ETS2 não encontrada", "Abra o ETS2 uma vez ou selecione a pasta que contém os perfis do jogo.", Amber);
                    return;
                }

                var local = await Task.Run(() => ModUpdater.Inspect(gameFolder, release), token);
                canInstall = ShowResult(local);
                if (install && canInstall)
                {
                    IProgress<int> progress = null;
                    if (local.NeedsDownload)
                    {
                        SetStatus("Baixando v" + release.Version + "…", "O arquivo será verificado antes de entrar na pasta do jogo.", Ink);
                        progressBar.Value = 0;
                        progressBar.Style = ProgressBarStyle.Continuous;
                        progress = new Progress<int>(value => progressBar.Value = value);
                    }
                    else
                    {
                        SetStatus("Organizando o mod…", "O launcher está conferindo as cópias na pasta mod.", Ink);
                    }

                    var updated = await Task.Run(() => updater.InstallAsync(gameFolder, release, progress, token), token);
                    canInstall = ShowResult(updated);
                    if (!canInstall && local.State == ModState.Missing)
                        SetStatus("Mod instalado", "Ative Milton Fretes skins no Gerenciador de Mods do ETS2.", Blue);
                    else if (!canInstall && (local.Copies.Length != 0 || local.NeedsRename))
                        SetStatus("Mod organizado", "Confira se Milton Fretes skins está ativo no Gerenciador de Mods.", Blue);
                }
            }
            catch (OperationCanceledException)
            {
                if (!closeAfterOperation)
                    SetStatus("Tempo de conexão esgotado", "Tente novamente em alguns instantes.", Amber);
            }
            catch (HttpRequestException)
            {
                SetStatus("Não foi possível acessar o GitHub", "Confira a conexão e tente verificar de novo.", Amber);
            }
            catch (UnauthorizedAccessException)
            {
                SetStatus("Acesso à pasta bloqueado", "Verifique se a pasta selecionada permite gravar arquivos.", Amber);
            }
            catch (InvalidDataException error)
            {
                SetStatus("Não foi possível validar o mod", error.Message, Amber);
            }
            catch (InvalidOperationException error)
            {
                SetStatus("Instalação interrompida", error.Message, Amber);
            }
            catch (IOException)
            {
                SetStatus("Não foi possível acessar o arquivo", "Feche o ETS2 e confira o espaço livre e o acesso à pasta.", Amber);
            }
            catch (ArgumentException)
            {
                SetStatus("Resposta inválida do GitHub", "Tente verificar novamente em alguns instantes.", Amber);
            }
            finally
            {
                progressBar.Visible = false;
                operation.Dispose();
                operation = null;
                checkButton.Enabled = folderButton.Enabled = true;
                var scale = installButton.Width / 173f;
                checkButton.Width = canInstall ? (int)Math.Round(151 * scale) : installButton.Width;
                checkButton.Left = canInstall ? installButton.Left - (int)Math.Round(163 * scale) : installButton.Left;
                installButton.Enabled = installButton.Visible = canInstall;
                launcherUpdateButton.Enabled = launcherUpdateButton.Visible;
                if (closeAfterOperation)
                    Close();
                else if (ActiveControl == folderField)
                    checkButton.Focus();
            }
        }

        private async Task UpdateLauncherAsync()
        {
            if (operation != null)
                return;
            operation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var token = operation.Token;
            string staged = null;
            checkButton.Enabled = installButton.Enabled = launcherUpdateButton.Enabled = folderButton.Enabled = false;
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.Visible = true;
            try
            {
                var release = await updater.LatestLauncherAsync(token);
                if (release.Version <= ModUpdater.CurrentLauncherVersion)
                {
                    launcherUpdateButton.Visible = false;
                    SetStatus("Launcher atualizado", "Você já está usando a versão mais recente.", Blue);
                    return;
                }
                SetStatus("Baixando launcher v" + release.Version, "O executável será conferido pelo SHA-256 antes da troca.", Ink);
                staged = await updater.DownloadLauncherAsync(release, token);
                LauncherSelfUpdate.Start(staged, Application.ExecutablePath, release.Sha256);
                staged = null;
                closeAfterOperation = true;
            }
            catch (OperationCanceledException)
            {
                SetStatus("Tempo de conexão esgotado", "Tente atualizar o launcher novamente.", Amber);
            }
            catch (HttpRequestException)
            {
                SetStatus("Não foi possível acessar o GitHub", "Confira a conexão e tente atualizar de novo.", Amber);
            }
            catch (InvalidDataException error)
            {
                SetStatus("Não foi possível validar o launcher", error.Message, Amber);
            }
            catch (UnauthorizedAccessException)
            {
                SetStatus("A pasta do launcher está protegida", "Execute-o de uma pasta onde você possa gravar arquivos para atualizar.", Amber);
            }
            catch (IOException)
            {
                SetStatus("Não foi possível substituir o launcher", "Feche outras cópias e confira o acesso à pasta.", Amber);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                SetStatus("Não foi possível iniciar a atualização", "Confira se o launcher pode iniciar arquivos na própria pasta.", Amber);
            }
            finally
            {
                if (!closeAfterOperation && staged != null && File.Exists(staged))
                {
                    try { File.Delete(staged); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                progressBar.Visible = false;
                operation.Dispose();
                operation = null;
                checkButton.Enabled = folderButton.Enabled = true;
                installButton.Enabled = installButton.Visible;
                launcherUpdateButton.Enabled = launcherUpdateButton.Visible;
                if (closeAfterOperation)
                    Close();
            }
        }

        private bool ShowResult(InstalledMod local)
        {
            installedVersion.Text = local.Primary == null ? "—" :
                local.Primary.Version == null ? "Desconhecida" : "v" + local.Primary.Version;
            var duplicates = local.Copies.Length != 0;
            if ((duplicates || local.NeedsRename) && !local.NeedsDownload)
            {
                installButton.Text = "Organizar mod";
                if (duplicates)
                    SetStatus("Mais de uma cópia do mod", "Feche o ETS2 para guardar as cópias extras fora da pasta mod.", Amber);
                else
                    SetStatus("O mod está com outro nome", "Feche o ETS2 para padronizar o arquivo como Milton_Fretes_skins.scs.", Amber);
                return true;
            }

            switch (local.State)
            {
                case ModState.Current:
                    SetStatus("Mod atualizado", "Você já tem a versão mais recente publicada.", Blue);
                    return false;
                case ModState.Newer:
                    SetStatus("Versão local mais recente", "O mod instalado é mais novo que a release do GitHub.", Blue);
                    return false;
                case ModState.Update:
                    installButton.Text = "Atualizar mod";
                    SetStatus("Atualização disponível", duplicates ?
                        "Feche o ETS2 para atualizar e guardar as cópias extras fora da pasta mod." :
                        "Feche o ETS2 antes de atualizar. A versão atual ficará como backup.", Ink);
                    return true;
                case ModState.Different:
                    installButton.Text = "Reinstalar mod";
                    SetStatus("Arquivo local diferente da release", duplicates ?
                        "Reinstale para usar a versão publicada e guardar as cópias extras fora da pasta mod." :
                        "Reinstale para usar a versão publicada. A cópia atual ficará como backup.", Amber);
                    return true;
                default:
                    installButton.Text = "Instalar mod";
                    SetStatus("Mod pronto para instalar", "Depois da instalação, ative o mod no Gerenciador de Mods do ETS2.", Ink);
                    return true;
            }
        }

        private async void ChooseFolder(object sender, EventArgs args)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Escolha a pasta de dados do Euro Truck Simulator 2, onde ficam os perfis.";
                dialog.ShowNewFolderButton = false;
                dialog.SelectedPath = Directory.Exists(gameFolder) ? gameFolder :
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                var chosen = dialog.SelectedPath;
                if (String.Equals(Path.GetFileName(chosen), "mod", StringComparison.OrdinalIgnoreCase))
                    chosen = Path.GetDirectoryName(chosen);
                gameFolder = chosen;
                folderField.Text = chosen;
                installedVersion.Text = "—";
                await RunAsync(false);
                if (IsDisposed || !ModUpdater.IsGameFolder(chosen))
                    return;
                try
                {
                    GameFolderSettings.Save(chosen);
                }
                catch (IOException) { detail.Text += " O Windows não salvou a pasta escolhida."; }
                catch (UnauthorizedAccessException) { detail.Text += " O Windows não salvou a pasta escolhida."; }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (operation != null)
            {
                closeAfterOperation = true;
                operation.Cancel();
                e.Cancel = true;
            }
            base.OnFormClosing(e);
        }

        private void OpenReleases(object sender, LinkLabelLinkClickedEventArgs args)
        {
            try
            {
                Process.Start(new ProcessStartInfo(ModUpdater.Releases + "/latest") { UseShellExecute = true });
            }
            catch (System.ComponentModel.Win32Exception)
            {
                SetStatus("Não foi possível abrir o navegador", "Abra o repositório milton-fretes-ets-mod no GitHub.", Amber);
            }
        }

        private void SetStatus(string title, string message, Color color)
        {
            status.Text = title;
            status.ForeColor = color;
            detail.Text = message;
        }

        private Label MakeLabel(string text, int x, int y, int width, int height, Color color, bool bold = false)
        {
            var label = new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, height),
                ForeColor = color
            };
            if (bold)
                label.Font = new Font(Font, FontStyle.Bold);
            return label;
        }

        private Button AddButton(string text, int x, int y, int width, int height, int tabIndex, bool primary)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, height),
                TabIndex = tabIndex,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = primary && !HighContrast ? Blue : Graphite,
                ForeColor = primary && !HighContrast ? Navy : Ink
            };
            button.FlatAppearance.BorderColor = HighContrast ? Ink : Color.FromArgb(85, 108, 134);
            button.FlatAppearance.MouseOverBackColor = HighContrast ? SystemColors.Window :
                primary ? Color.FromArgb(160, 222, 250) : Color.FromArgb(48, 67, 93);
            if (primary && !HighContrast)
            {
                button.Font = new Font(Font, FontStyle.Bold);
                button.EnabledChanged += (sender, args) => button.BackColor = button.Enabled ? Blue : Graphite;
            }
            Controls.Add(button);
            return button;
        }
    }
}
