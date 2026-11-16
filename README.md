# Milton Fretes para ETS2

Mod da frota Milton Fretes para Euro Truck Simulator 2. Inclui pinturas de caminhões e carretas, carretas dos fretes rápidos, veículos no tráfego e letreiros da empresa. A Logística usa azul-marinho e o Intermodal usa grafite. As carretas trazem “Qualidade em segundo lugar” e frases que variam conforme a carroceria.

## Launcher para Windows

Baixe e abra o [Milton Fretes Launcher](https://github.com/andreltcarvalho/milton-fretes-ets-mod/releases/download/launcher-v1.0.0/milton-fretes-launcher.exe). Ele procura a última versão do mod no GitHub ao abrir. Com o ETS2 fechado, clique em **Instalar mod** ou **Atualizar mod**.

O launcher usa a pasta de dados do ETS2 em Documentos por padrão. Use **Escolher…** se o jogo estiver usando outra pasta de dados. Ao atualizar, ele guarda a cópia anterior como `Milton Fretes skins.scs.bak`. Na primeira instalação, ative **Milton Fretes skins** no Gerenciador de Mods do perfil.

## Instalação manual

1. Feche o ETS2 e baixe o [milton-fretes.scs](https://github.com/andreltcarvalho/milton-fretes-ets-mod/releases/latest/download/milton-fretes.scs).
2. Renomeie o download para `Milton Fretes skins.scs` e coloque em `Documentos\Euro Truck Simulator 2\mod`. Se ele já existir, substitua pelo novo.
3. Abra o jogo e ative **Milton Fretes skins** no Gerenciador de Mods do perfil.

No Comboio, todos devem usar a mesma versão do mod. Confira a versão e as mudanças na [página da release mais recente](https://github.com/andreltcarvalho/milton-fretes-ets-mod/releases/latest).

## Publicação de uma atualização

Atualize `package_version` no `manifest.sii` do mod e gere o `.scs`. Revise o conteúdo do pacote antes de publicar, incluindo texturas, configurações e metadados.

Na raiz deste repositório, prepare os arquivos da release com PowerShell:

```powershell
.\scripts\Preparar-Release.ps1 -Pacote 'C:\caminho\Milton Fretes - v1.12.3.scs' -Versao '1.12.3'
```

O script confere a versão no manifesto e gera `milton-fretes.scs` e `SHA256SUMS.txt` em `dist\v1.12.3`. No GitHub, crie a release com a tag correspondente, como `v1.12.3`, anexe os dois arquivos e marque a release como a mais recente.

Mantenha o nome `milton-fretes.scs` em cada release. O launcher usa a [última release do GitHub](https://api.github.com/repos/andreltcarvalho/milton-fretes-ets-mod/releases/latest) para localizar o pacote e verificar o SHA-256 antes de substituir o mod.

## Compilar o launcher

No Windows com .NET Framework 4.8, execute a partir da raiz:

~~~powershell
.\scripts\Compilar-Launcher.ps1
~~~

O executável fica em `dist\launcher\milton-fretes-launcher.exe`. Publique versões do launcher com tags como `launcher-v1.0.0`, mantendo a release mais recente reservada ao `.scs`.

Mod não oficial para Euro Truck Simulator 2, da SCS Software.
