using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MiltonFretes
{
    internal sealed class MainForm : Form
    {
        private static readonly bool HighContrast = SystemInformation.HighContrast;
        private static readonly Color Navy = HighContrast ? SystemColors.Window : Color.FromArgb(19, 34, 56);
        private static readonly Color Graphite = HighContrast ? SystemColors.Window : Color.FromArgb(25, 29, 36);
        private static readonly Color Ink = HighContrast ? SystemColors.WindowText : Color.FromArgb(233, 237, 242);
        private static readonly Color Muted = HighContrast ? Ink : Color.FromArgb(155, 166, 178);
        private static readonly Color Blue = HighContrast ? Ink : Color.FromArgb(50, 139, 200);
        private static readonly Color Amber = HighContrast ? Ink : Color.FromArgb(255, 191, 105);

        private readonly ModUpdater updater;
        private readonly Label statusMark;
        private readonly Label status;
        private readonly Label detail;
        private readonly Label installedVersion;
        private readonly TextBox folderField;
        private readonly ToolTip folderToolTip = new ToolTip();
        private readonly Button folderButton;
        private readonly Button actionButton;
        private readonly Button launcherUpdateButton;
        private readonly Label launcherStatus;
        private readonly Button restoreButton;
        private readonly ProgressBar progressBar;
        private CancellationTokenSource operation;
        private bool closeAfterOperation;
        private bool statusLayoutInitialized;
        private int expandedVersionGap;
        private string gameFolder;
        private bool actionInstalls;

        internal MainForm(ModUpdater updater, string gameFolder)
        {
            this.updater = updater;
            this.gameFolder = gameFolder;
            SuspendLayout();
            Text = "Milton Fretes Launcher";
            Font = new Font("Segoe UI", 9.5F);
            BackColor = Graphite;
            ForeColor = Ink;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ClientSize = new Size(540, 390);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            SetDarkTitleBar(this);

            var brandStrip = new Panel
            {
                Location = Point.Empty,
                Size = new Size(ClientSize.Width, 48),
                BackColor = Navy,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var brand = MakeLabel("MILTON FRETES", 22, 12, 300, 25, Ink, true);
            brand.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            brandStrip.Controls.Add(brand);
            Controls.Add(brandStrip);

            statusMark = MakeLabel("●", 23, 76, 20, 25, Color.FromArgb(70, 190, 118));
            statusMark.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            status = MakeLabel("Verificando atualizações…", 43, 76, 470, 25, Ink, true);
            status.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
            detail = MakeLabel("Consultando a release mais recente no GitHub.", 24, 104, 492, 35, Muted);
            installedVersion = MakeLabel("Instalada: —", 24, 142, 492, 22, Muted);
            Controls.Add(statusMark);
            Controls.Add(status);
            Controls.Add(detail);
            Controls.Add(installedVersion);

            actionButton = AddButton("Verificar atualizações", 24, 180, 224, 42, 0, true);
            actionButton.Click += async (sender, args) => await RunAsync(actionInstalls);
            restoreButton = AddButton("Restaurar backup", 264, 180, 224, 42, 1, false);
            restoreButton.Enabled = true;
            restoreButton.Click += async (sender, args) => await RestoreBackupAsync();

            progressBar = new ProgressBar
            {
                AccessibleName = "Progresso da atualização",
                Location = new Point(24, 258),
                Size = new Size(492, 5),
                Visible = false,
                MarqueeAnimationSpeed = 26
            };
            Controls.Add(progressBar);

            Controls.Add(MakeLabel("Pasta do jogo", 24, 286, 100, 25, Ink, true));
            folderField = new TextBox
            {
                AccessibleName = "Pasta do jogo",
                Location = new Point(128, 281),
                Width = 300,
                Height = 30,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(33, 39, 48),
                ForeColor = Ink,
                ReadOnly = true,
                TextAlign = HorizontalAlignment.Right,
                Text = gameFolder,
                TabIndex = 2
            };
            folderToolTip.SetToolTip(folderField, gameFolder);
            Controls.Add(folderField);
            folderButton = AddButton("Alterar", 440, 280, 76, 32, 3, false);
            folderButton.Click += ChooseFolder;

            var footerRule = new Panel
            {
                Location = new Point(24, 327),
                Size = new Size(492, 1),
                BackColor = HighContrast ? Ink : Color.FromArgb(51, 58, 68)
            };
            Controls.Add(footerRule);
            launcherStatus = MakeLabel("Launcher atualizado", 24, 343, 250, 22, Muted);
            Controls.Add(launcherStatus);
            launcherUpdateButton = AddButton("Atualizar launcher", 344, 338, 172, 30, 5, false);
            launcherUpdateButton.Enabled = false;
            launcherUpdateButton.Visible = false;
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
            actionButton.Enabled = launcherUpdateButton.Enabled = restoreButton.Enabled = folderButton.Enabled = false;
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.Visible = true;
            restoreButton.Visible = true;
            actionButton.Text = "Verificando…";
            launcherUpdateButton.Visible = false;
            launcherStatus.Visible = true;
            launcherStatus.Text = "Verificando launcher…";
            SetStatus("Verificando atualizações…", "Consultando a release mais recente no GitHub.", Ink);
            bool canInstall = false;
            try
            {
                ModUpdater.ReleaseResponse[] releases;
                ModRelease release;
                using (var lookupTimeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    lookupTimeout.CancelAfter(TimeSpan.FromSeconds(20));
                    releases = await updater.ListStableReleasesAsync(lookupTimeout.Token);
                    release = await updater.LatestAsync(lookupTimeout.Token, releases);
                }
                CheckLauncherUpdate(releases);
                if (!ModUpdater.IsGameFolder(gameFolder))
                {
                    installedVersion.Text = "Instalada: —";
                    SetStatus("Pasta do ETS2 não encontrada", "Abra o ETS2 uma vez ou selecione a pasta que contém os perfis do jogo.", Amber);
                    return;
                }

                var local = await Task.Run(() => ModUpdater.Inspect(gameFolder, release), token);
                canInstall = ShowResult(local, release.Version);
                if (install && canInstall)
                {
                    if (local.NeedsDownload)
                    {
                        var action = local.State == ModState.Missing ? "Instalar mod" :
                            local.State == ModState.Different ? "Reinstalar mod" : "Atualizar mod";
                        if (!ShowReleaseNotes(action, "v" + release.Version, release.Notes, true))
                            return;
                    }
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
                    restoreButton.Visible = true;
                    canInstall = ShowResult(updated, release.Version);
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
                actionButton.Enabled = folderButton.Enabled = true;
                actionInstalls = canInstall;
                actionButton.Text = canInstall ? actionButton.Text : "Verificar atualizações";
                actionButton.Enabled = true;
                launcherUpdateButton.Enabled = launcherUpdateButton.Visible;
                restoreButton.Enabled = true;
                if (closeAfterOperation)
                    Close();
                else if (ActiveControl == folderField)
                    actionButton.Focus();
            }
        }

        private async Task UpdateLauncherAsync()
        {
            if (operation != null)
                return;
            operation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var token = operation.Token;
            string staged = null;
            actionButton.Enabled = launcherUpdateButton.Enabled = folderButton.Enabled = restoreButton.Enabled = false;
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.Visible = true;
            try
            {
                var release = await updater.LatestLauncherAsync(token);
                if (release.Version <= ModUpdater.CurrentLauncherVersion)
                {
                    launcherUpdateButton.Visible = false;
                    launcherStatus.Visible = true;
                    launcherStatus.Text = "Launcher atualizado";
                    SetStatus("Launcher atualizado", "Você já está usando a versão mais recente.", Blue);
                    return;
                }
                if (!ShowReleaseNotes("Atualizar launcher", "v" + release.Version, release.Notes, true))
                    return;
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
                actionButton.Enabled = folderButton.Enabled = true;
                launcherUpdateButton.Enabled = launcherUpdateButton.Visible;
                restoreButton.Enabled = true;
                if (closeAfterOperation)
                    Close();
            }
        }

        private async Task RestoreBackupAsync()
        {
            if (operation != null)
                return;
            ModPackage[] backups;
            try { backups = ModUpdater.ListBackups(gameFolder); }
            catch (UnauthorizedAccessException)
            {
                SetStatus("Acesso aos backups bloqueado", "Verifique as permissões da pasta de backups do Milton Fretes.", Amber);
                return;
            }
            catch (IOException)
            {
                SetStatus("Não foi possível ler os backups", "Feche o ETS2 e tente novamente.", Amber);
                return;
            }
            if (backups.Length == 0)
            {
                restoreButton.Visible = restoreButton.Enabled = true;
                SetStatus("Nenhum backup encontrado", "As cópias guardadas pelo launcher aparecerão aqui.", Muted);
                return;
            }

            var selected = SelectBackup(backups);
            if (selected == null)
                return;
            var version = selected.Version == null ? "versão desconhecida" : "v" + selected.Version;
            if (!ConfirmRestore(version))
                return;

            operation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var token = operation.Token;
            actionButton.Enabled = launcherUpdateButton.Enabled = restoreButton.Enabled = folderButton.Enabled = false;
            SetStatus("Restaurando backup…", "O launcher está preservando a versão atual antes da troca.", Ink);
            try
            {
                var restored = await Task.Run(() => ModUpdater.RestoreBackup(gameFolder, selected.Path), token);
                installedVersion.Text = "Instalada: " + (restored.Version == null ? "Desconhecida" : "v" + restored.Version);
                restoreButton.Visible = true;
                SetStatus("Backup restaurado", "Confira Milton Fretes skins no Gerenciador de Mods do ETS2.", Blue);
            }
            catch (OperationCanceledException)
            {
                SetStatus("Restauração cancelada", "Tente novamente quando o ETS2 estiver fechado.", Amber);
            }
            catch (InvalidDataException error)
            {
                SetStatus("Backup inválido", error.Message, Amber);
            }
            catch (InvalidOperationException error)
            {
                SetStatus("Restauração interrompida", error.Message, Amber);
            }
            catch (UnauthorizedAccessException)
            {
                SetStatus("Acesso à pasta bloqueado", "Verifique se a pasta de dados do ETS2 permite mover os arquivos.", Amber);
            }
            catch (IOException)
            {
                SetStatus("Não foi possível restaurar", "Feche o ETS2 e confira o acesso à pasta do mod.", Amber);
            }
            finally
            {
                operation.Dispose();
                operation = null;
                actionButton.Enabled = folderButton.Enabled = true;
                launcherUpdateButton.Enabled = launcherUpdateButton.Visible;
                restoreButton.Enabled = true;
                if (closeAfterOperation)
                    Close();
            }
        }

        private void CheckLauncherUpdate(ModUpdater.ReleaseResponse[] releases)
        {
            var version = updater.LatestLauncherVersion(releases);
            if (version == null)
            {
                ShowLauncherCheckUnavailable();
                return;
            }

            launcherUpdateButton.Visible = version > ModUpdater.CurrentLauncherVersion;
            if (launcherUpdateButton.Visible)
            {
                launcherUpdateButton.Text = "Atualizar launcher v" + version;
                launcherStatus.Text = "Nova versão do launcher disponível";
            }
            else
            {
                launcherStatus.Text = "Launcher atualizado";
            }
            launcherStatus.Visible = !launcherUpdateButton.Visible;
        }

        private void ShowLauncherCheckUnavailable()
        {
            launcherUpdateButton.Visible = false;
            launcherStatus.Visible = true;
            launcherStatus.Text = "Não foi possível verificar o launcher";
        }

        private bool ConfirmRestore(string version)
        {
            using (var dialog = new Form())
            using (var message = new Label())
            using (var accept = new Button())
            using (var cancel = new Button())
            {
                dialog.Text = "Restaurar backup";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(430, 160);
                dialog.BackColor = Navy;
                dialog.ForeColor = Ink;
                dialog.Font = Font;
                SetDarkTitleBar(dialog);
                message.Text = "Restaurar " + version + "? O mod atual será guardado como outro backup.";
                message.Location = new Point(20, 20);
                message.Size = new Size(390, 58);
                message.ForeColor = Ink;
                dialog.Controls.Add(message);
                accept.Text = "Restaurar";
                accept.Location = new Point(218, 102);
                accept.Size = new Size(96, 34);
                accept.DialogResult = DialogResult.Yes;
                StyleDialogButton(accept, true);
                cancel.Text = "Cancelar";
                cancel.Location = new Point(320, 102);
                cancel.Size = new Size(90, 34);
                cancel.DialogResult = DialogResult.No;
                StyleDialogButton(cancel, false);
                dialog.Controls.Add(accept);
                dialog.Controls.Add(cancel);
                dialog.AcceptButton = accept;
                dialog.CancelButton = cancel;
                return dialog.ShowDialog(this) == DialogResult.Yes;
            }
        }

        private ModPackage SelectBackup(ModPackage[] backups)
        {
            using (var dialog = new Form())
            using (var list = new ListBox())
            using (var restore = new Button())
            using (var cancel = new Button())
            {
                dialog.Text = "Restaurar backup do Milton Fretes";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(520, 340);
                dialog.BackColor = Navy;
                dialog.ForeColor = Ink;
                dialog.Font = Font;
                SetDarkTitleBar(dialog);
                dialog.Controls.Add(new Label { Text = "Escolha a cópia do mod que deseja restaurar:", Location = new Point(18, 14), Size = new Size(480, 26), ForeColor = Ink, Font = new Font(Font, FontStyle.Bold) });
                list.Location = new Point(18, 48);
                list.Size = new Size(484, 230);
                list.BackColor = Graphite;
                list.ForeColor = Ink;
                list.IntegralHeight = false;
                foreach (var package in backups)
                    list.Items.Add(package);
                list.SelectedIndex = 0;
                dialog.Controls.Add(list);
                restore.Text = "Restaurar selecionado";
                restore.Location = new Point(258, 294);
                restore.Size = new Size(154, 34);
                restore.DialogResult = DialogResult.OK;
                StyleDialogButton(restore, true);
                restore.Enabled = list.SelectedItem != null;
                list.SelectedIndexChanged += (sender, args) => restore.Enabled = list.SelectedItem != null;
                cancel.Text = "Cancelar";
                cancel.Location = new Point(420, 294);
                cancel.Size = new Size(82, 34);
                cancel.DialogResult = DialogResult.Cancel;
                StyleDialogButton(cancel, false);
                dialog.Controls.Add(restore);
                dialog.Controls.Add(cancel);
                dialog.AcceptButton = restore;
                dialog.CancelButton = cancel;
                return dialog.ShowDialog(this) == DialogResult.OK ? list.SelectedItem as ModPackage : null;
            }
        }

        private bool ShowReleaseNotes(string action, string version, string notes, bool confirm)
        {
            using (var dialog = new Form())
            using (var content = new TextBox())
            using (var accept = new Button())
            using (var cancel = new Button())
            {
                dialog.Text = action + " — " + version;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(540, 400);
                dialog.BackColor = Navy;
                dialog.ForeColor = Ink;
                dialog.Font = Font;
                SetDarkTitleBar(dialog);
                dialog.Controls.Add(new Label { Text = "O que mudou nesta versão", Location = new Point(20, 17), Size = new Size(490, 28), ForeColor = Ink, Font = new Font(Font, FontStyle.Bold) });
                content.Location = new Point(20, 52);
                content.Size = new Size(500, 282);
                content.Multiline = true;
                content.ReadOnly = true;
                content.ScrollBars = ScrollBars.Vertical;
                content.WordWrap = true;
                content.BackColor = Graphite;
                content.ForeColor = Ink;
                content.Text = String.IsNullOrWhiteSpace(notes) ? "Esta release não tem notas de alterações publicadas." : notes.Trim();
                dialog.Controls.Add(content);
                accept.Text = confirm ? "Continuar" : "Fechar";
                accept.Location = new Point(confirm ? 350 : 420, 350);
                accept.Size = new Size(confirm ? 90 : 100, 34);
                accept.DialogResult = confirm ? DialogResult.Yes : DialogResult.OK;
                StyleDialogButton(accept, true);
                dialog.Controls.Add(accept);
                if (confirm)
                {
                    cancel.Text = "Cancelar";
                    cancel.Location = new Point(450, 350);
                    cancel.Size = new Size(70, 34);
                    cancel.DialogResult = DialogResult.No;
                    StyleDialogButton(cancel, false);
                    dialog.Controls.Add(cancel);
                    dialog.CancelButton = cancel;
                }
                dialog.AcceptButton = accept;
                return dialog.ShowDialog(this) == (confirm ? DialogResult.Yes : DialogResult.OK);
            }
        }

        private bool ShowResult(InstalledMod local, Version availableVersion)
        {
            var installed = local.Primary == null ? "—" :
                local.Primary.Version == null ? "Desconhecida" : "v" + local.Primary.Version;
            installedVersion.Text = local.NeedsDownload
                ? "Instalada: " + installed + "   ·   Disponível: v" + availableVersion
                : "Instalada: " + installed;
            var duplicates = local.Copies.Length != 0;
            if ((duplicates || local.NeedsRename) && !local.NeedsDownload)
            {
                actionButton.Text = "Organizar mod";
                actionInstalls = true;
                if (duplicates)
                    SetStatus("Mais de uma cópia do mod", "Feche o ETS2 para guardar as cópias extras fora da pasta mod.", Amber);
                else
                    SetStatus("O mod está com outro nome", "Feche o ETS2 para padronizar o arquivo como Milton_Fretes_skins.scs.", Amber);
                return true;
            }

            switch (local.State)
            {
                case ModState.Current:
                    actionButton.Text = "Verificar atualizações";
                    actionInstalls = false;
                    SetStatus("Mod atualizado", "", Ink);
                    statusMark.ForeColor = HighContrast ? Ink : Color.FromArgb(104, 195, 133);
                    return false;
                case ModState.Newer:
                    actionButton.Text = "Verificar atualizações";
                    actionInstalls = false;
                    SetStatus("Versão local mais recente", "O mod instalado é mais novo que a release do GitHub.", Blue);
                    return false;
                case ModState.Update:
                    actionButton.Text = "Atualizar mod";
                    actionInstalls = true;
                    SetStatus("Atualização disponível", duplicates ?
                        "Feche o ETS2 para atualizar e guardar as cópias extras fora da pasta mod." :
                        "Feche o ETS2 antes de atualizar. A versão atual ficará como backup.", Ink);
                    return true;
                case ModState.Different:
                    actionButton.Text = "Reinstalar mod";
                    actionInstalls = true;
                    SetStatus("Arquivo local diferente da release", duplicates ?
                        "Reinstale para usar a versão publicada e guardar as cópias extras fora da pasta mod." :
                        "Reinstale para usar a versão publicada. A cópia atual ficará como backup.", Amber);
                    return true;
                default:
                    actionButton.Text = "Instalar mod";
                    actionInstalls = true;
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
                folderToolTip.SetToolTip(folderField, chosen);
                installedVersion.Text = "Instalada: —";
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

        private void SetStatus(string title, string message, Color color)
        {
            status.Text = title;
            status.ForeColor = color;
            statusMark.ForeColor = color;
            if (!statusLayoutInitialized)
            {
                expandedVersionGap = Math.Max(1, installedVersion.Top - detail.Bottom);
                statusLayoutInitialized = true;
            }
            var compact = String.IsNullOrEmpty(message);
            detail.Visible = !compact;
            installedVersion.Top = compact ? detail.Top : detail.Bottom + expandedVersionGap;
            detail.Text = message;
        }

        private static void SetDarkTitleBar(Form form)
        {
            if (HighContrast)
                return;
            try
            {
                int enabled = 1;
                int result = DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int));
                if (result < 0)
                    DwmSetWindowAttribute(form.Handle, 19, ref enabled, sizeof(int));
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int valueSize);

        private static void StyleDialogButton(Button button, bool primary)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = HighContrast ? SystemColors.Window :
                primary ? Color.FromArgb(35, 112, 160) : Color.FromArgb(33, 39, 48);
            button.ForeColor = HighContrast ? SystemColors.WindowText : Ink;
            button.FlatAppearance.BorderColor = HighContrast ? Ink : Color.FromArgb(85, 108, 134);
            button.FlatAppearance.MouseOverBackColor = HighContrast ? SystemColors.Window :
                primary ? Color.FromArgb(30, 98, 143) : Color.FromArgb(43, 51, 62);
            if (primary && !HighContrast)
            {
                button.FlatAppearance.BorderSize = 0;
                button.Font = new Font(button.Font, FontStyle.Bold);
            }
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
                BackColor = HighContrast ? SystemColors.Window :
                    primary ? Color.FromArgb(35, 112, 160) : Color.FromArgb(33, 39, 48),
                ForeColor = HighContrast ? SystemColors.WindowText : Ink
            };
            button.FlatAppearance.BorderColor = HighContrast ? Ink : Color.FromArgb(85, 108, 134);
            button.FlatAppearance.MouseOverBackColor = HighContrast ? SystemColors.Window :
                primary ? Color.FromArgb(30, 98, 143) : Color.FromArgb(43, 51, 62);
            if (primary && !HighContrast)
            {
                button.Font = new Font(Font, FontStyle.Bold);
                button.FlatAppearance.BorderSize = 0;
                button.EnabledChanged += (sender, args) =>
                {
                    button.BackColor = button.Enabled ? Color.FromArgb(35, 112, 160) : Color.FromArgb(33, 39, 48);
                    button.ForeColor = button.Enabled ? Ink : Muted;
                };
            }
            Controls.Add(button);
            return button;
        }
    }
}
