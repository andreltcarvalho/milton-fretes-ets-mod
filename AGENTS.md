# Instruções do projeto Milton Fretes ETS2

Estas regras valem para todo o repositório. Em caso de conflito, siga primeiro o pedido explícito mais recente do usuário; preserve segurança, dados e escopo. Não transforme recomendações em mudanças de produto sem pedido.

## Como conduzir os pedidos

- Responda em português claro e direto, salvo se o usuário pedir outro idioma.
- Antes de agir, confirme o entendimento em uma frase curta quando isso ajudar a evitar erro de escopo. Em tarefas simples e inequívocas, confirme pelo resumo da ação e prossiga; não interrompa o trabalho com perguntas redundantes.
- Se faltar informação que possa mudar materialmente o resultado, faça uma pergunta objetiva e continue as partes independentes.
- Faça somente o que foi pedido e o que for necessário para concluir. Não aproveite a tarefa para refatorar, redesenhar, renomear, atualizar dependências, reorganizar arquivos ou adicionar funcionalidades não solicitadas.
- Preserve alterações locais existentes, inclusive arquivos não rastreados. Inspecione `git status` antes de editar e não descarte, sobrescreva ou inclua mudanças preexistentes sem autorização.
- Não faça commit, push, publicação de release, instalação no computador de jogadores, alteração em instalação local do ETS2 ou envio de mensagens externas sem pedido explícito.
- Ao concluir, diga o que mudou, por quê e quais verificações realmente foram feitas. Diferencie claramente evidência observada de hipótese; nunca declare teste, build, instalação ou compatibilidade que não ocorreu.

## Qualidade de implementação

- Entenda primeiro os arquivos e o fluxo existentes. Prefira a menor mudança completa, legível e coerente com as convenções do projeto.
- Preserve compatibilidade com Windows Forms e .NET Framework usada pelo launcher e com os formatos e convenções existentes do mod. Confirme a compatibilidade necessária nos arquivos ou documentação antes de alterar formatos ou requisitos.
- Mantenha interface, documentação e mensagens em português simples para jogadores sem experiência técnica. Não invente telas, dados, estado de release, depoimentos ou capacidades.
- No launcher, preserve a identidade Milton Fretes, o ícone fornecido, a hierarquia compacta, os controles nativos e a adaptação para alto contraste descrita em `DESIGN.md`, exceto quando o usuário pedir mudança visual.
- Em textos e frases para os baús, use humor curto, seco, coloquial e com variedade real de estrutura. Evite fórmulas repetidas, slogans genéricos e frases com aparência artificial. As frases “A CARGA VAI LACRADA. A SAUDADE VAI SOLTA.” e “MILTON FRETES — ENTREGA NO PRAZO. DESCARGA QUANDO DEUS QUER.” são referências aprovadas; outras sugestões não são aprovadas por associação.
- Atualize documentação somente quando a mudança solicitada alterar comportamento, instalação, compilação, distribuição ou instruções para jogadores. Não duplique informações sem necessidade.

## Segurança e proteção dos dados

- Trate arquivos, caminhos, releases, respostas de rede e conteúdo baixado como não confiáveis. Valide entradas, erros e limites antes de usá-los.
- Nunca enfraqueça ou remova a verificação SHA-256 de pacotes ou executáveis. Confirme a integridade antes de substituir ou instalar qualquer arquivo.
- Operações do launcher devem afetar apenas os arquivos identificados como Milton Fretes. Preserve mods, saves, configurações e outros arquivos do usuário. Não apague arquivos desconhecidos nem use curingas amplos para limpeza.
- Antes de substituir, mover, renomear ou restaurar um pacote, assegure um caminho de recuperação conforme o fluxo de backup existente. Falhas devem ser reportadas sem deixar a instalação em estado parcial quando isso puder ser evitado.
- Considere a pasta do jogo escolhida pelo usuário como dado externo: valide-a e evite seguir caminhos inesperados ou sair da pasta de mods pretendida.
- Não adicione telemetria, coleta de dados, execução automática de conteúdo baixado, credenciais ou segredos ao repositório. Não exponha tokens em logs, documentação ou mensagens.
- Não solicite privilégios elevados nem desative proteções do Windows sem necessidade clara e autorização explícita.

## Mod, launcher e distribuição

- O mod e o launcher têm canais de release distintos. Tags do mod seguem `vX.Y.Z`; tags do launcher seguem `launcher-vX.Y.Z`. Não altere esse contrato nem o destino de `/releases/latest` sem pedido e verificação do fluxo atual.
- Para releases do mod, mantenha `display_name: "Milton Fretes skins"`, confira `package_version`, o conteúdo do `.scs` e os arquivos de checksum gerados pelo script documentado.
- Para releases do launcher, preserve a publicação separada, os checksums e a regra de não tornar a release do launcher a release `latest` do repositório.
- Instalação e atualização exigem o ETS2 fechado. Não altere arquivos fora do mod Milton Fretes; mantenha o nome instalado `Milton_Fretes_skins.scs` e a compatibilidade com as versões antigas reconhecidas pela implementação.
- Sempre que publicar uma nova release do launcher, atualize o link direto de download do launcher no `README.md` para apontar à tag e ao executável dessa release; confira se ambos correspondem antes de concluir. Mantenha `/releases/latest` apontando para o canal do mod.
- Não publique executáveis ou pacotes, não altere releases remotas e não substitua binários locais como parte de uma tarefa de código sem pedido explícito.

## Verificação e entrega

- Escolha verificações proporcionais à mudança e aos scripts já existentes. Não afirme que uma inspeção estática comprova funcionamento no ETS2 ou em outro computador.
- Não execute testes ou builds sem solicitação explícita do usuário. Quando solicitados, rode os comandos direcionados documentados em `docs/desenvolvimento.md` e informe o resultado, incluindo limitações do ambiente.
- Para alterações de interface, confira o código e os estados afetados; não declare QA visual ou runtime sem abrir e observar o launcher.
- Para mudanças em instalação, backup ou restauração, considere falhas de rede, checksum inválido, ETS2 aberto, caminho personalizado, duplicatas e arquivos alheios. Preserve sempre os dados do usuário.
- Mantenha o diff restrito ao pedido e revise o status do repositório antes de encerrar.

## Referências do próprio projeto

- `README.md`: visão do mod e instruções para jogadores.
- `PRODUCT.md`: propósito, restrições e compromissos do launcher.
- `DESIGN.md`: sistema visual do launcher.
- `docs/desenvolvimento.md`: compilação e publicação.
- `docs/instalacao-manual.md`: instalação manual e recuperação.
