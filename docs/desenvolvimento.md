# Publicação e compilação

Notas para manter o mod e publicar releases. O guia para jogadores fica no [README](../README.md).

## Publicar uma versão do mod

1. Atualize `package_version` e mantenha `display_name: "Milton Fretes skins"` no `manifest.sii` do mod.
2. Gere o arquivo `.scs` e confira o conteúdo do pacote, incluindo texturas, configurações e metadados.
3. Na raiz do repositório, prepare os arquivos da release com PowerShell:

   ```powershell
   .\scripts\Preparar-Release.ps1 -Pacote 'C:\caminho\Milton Fretes - v1.13.0.scs' -Versao '1.13.0'
   ```

   O script confere a versão no manifesto e cria `milton-fretes.scs` e `SHA256SUMS.txt` em `dist\v1.13.0`.

4. Crie no GitHub uma release com a tag correspondente, por exemplo `v1.13.0`, anexe os dois arquivos e marque a release como a mais recente.

Mantenha o nome `milton-fretes.scs` em cada release. O launcher consulta a [última release do GitHub](https://api.github.com/repos/andreltcarvalho/milton-fretes-ets-mod/releases/latest) para localizar o pacote e conferir seu SHA-256 antes da instalação. Na pasta `mod`, o launcher instala como `Milton_Fretes_skins.scs`; versões anteriores com espaços no nome são reconhecidas e renomeadas ao organizar ou atualizar.

## Compilar o launcher

No Windows com .NET Framework 4.8, execute na raiz do repositório:

```powershell
.\scripts\Compilar-Launcher.ps1
```

O executável e seu `SHA256SUMS.txt` são gerados em `dist\launcher`. Publique ambos em uma release com tag `launcher-vX.Y.Z`; mantenha a release mais recente (`latest`) reservada ao pacote `.scs` do mod. Escreva as alterações no campo de notas da release. O launcher consulta as releases `launcher-v...`, mostra as notas antes de instalar e oferece a atualização quando encontra uma versão mais nova, validando o executável com SHA-256 antes de substituir a cópia local.
