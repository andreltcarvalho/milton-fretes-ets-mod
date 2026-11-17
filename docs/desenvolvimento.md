# Publicação e compilação

Notas para manter o mod e publicar releases. O guia para jogadores fica no [README](../README.md).

## Pacote de trabalho com a Tahoe

O script `scripts/Preparar-Tahoe.py` reúne a versão de teste da caneca e o mod Chevrolet Tahoe autorizado pelo autor em um único pacote de trabalho, incluindo a pintura Milton Fretes. Ele espera os arquivos locais indicados no início do script e valida seus hashes antes de montar o `.scs`; os arquivos de estudo e pacotes de origem não são versionados neste repositório. A configuração de venda usa peças específicas para não alterar os preços das rodas compartilhadas. Confira a pintura, o preço e a compatibilidade no jogo antes de preparar uma release.

O ajuste posterior das faixas e do sinalizador usa `scripts/Ajustar-Tahoe.py`, que também depende dos arquivos de estudo e do cache de conversão locais indicados no script. O sinalizador não apareceu no teste informado pelo jogador.

## Publicar uma versão do mod

1. Atualize `package_version` e mantenha `display_name: "Milton Fretes skins"` no `manifest.sii` do mod.
2. Gere o arquivo `.scs` e confira o conteúdo do pacote, incluindo texturas, configurações e metadados.
3. Na raiz do repositório, prepare os arquivos da release com PowerShell:

   ```powershell
   .\scripts\Preparar-Release.ps1 -Pacote 'C:\caminho\Milton Fretes - v1.13.0.scs' -Versao '1.13.0'
   ```

   O script confere a versão no manifesto e cria `milton-fretes.scs` e `SHA256SUMS.txt` em `dist\v1.13.0`.

4. Crie no GitHub uma release com a tag correspondente, por exemplo `v1.13.0`, anexe os dois arquivos e marque a release como a mais recente.

Mantenha o nome `milton-fretes.scs` em cada release. A partir do launcher 1.3.1, o launcher procura a maior versão estável com tag `vX.Y.Z` e confere seu SHA-256 antes da instalação. O endereço [da última release do GitHub](https://api.github.com/repos/andreltcarvalho/milton-fretes-ets-mod/releases/latest) continua apontando para o mod para downloads manuais e launchers anteriores. Na pasta `mod`, o launcher instala como `Milton_Fretes_skins.scs`; versões anteriores com espaços no nome são reconhecidas e renomeadas ao organizar ou atualizar.

## Compilar o launcher

No Windows com .NET Framework 4.8, execute na raiz do repositório:

```powershell
.\scripts\Compilar-Launcher.ps1
```

O executável e seu `SHA256SUMS.txt` são gerados em `dist\launcher`. Publique ambos em uma release estável com tag `launcher-vX.Y.Z` e desmarque **Set as the latest release** (`make_latest: "false"` pela API). Depois da publicação, confira se `/releases/latest` ainda aponta para a tag do mod. Escreva as alterações no campo de notas da release. O launcher consulta as releases `launcher-v...`, mostra as notas antes de instalar e oferece a atualização quando encontra uma versão mais nova, validando o executável com SHA-256 antes de substituir a cópia local.
