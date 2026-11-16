---
name: Milton Fretes Launcher
description: Identidade visual compacta e escura para instalar, atualizar e restaurar o mod Milton Fretes.
colors:
  brand-navy: "#132238"
  surface-graphite: "#191D24"
  surface-control: "#212730"
  text-primary: "#E9EDF2"
  text-muted: "#9BA6B2"
  action-blue: "#2370A0"
  action-blue-hover: "#1E628F"
  border-muted: "#556C86"
  divider: "#333A44"
  status-amber: "#FFBF69"
  status-green: "#46BE76"
typography:
  body:
    fontFamily: "Segoe UI, sans-serif"
    fontSize: "9.5pt"
  status:
    fontFamily: "Segoe UI, sans-serif"
    fontSize: "15pt"
    fontWeight: 700
  brand:
    fontFamily: "Segoe UI, sans-serif"
    fontSize: "12pt"
    fontWeight: 700
components:
  action-primary:
    backgroundColor: "{colors.action-blue}"
    textColor: "{colors.text-primary}"
    typography: "{typography.body}"
    size: "224px × 42px"
  action-secondary:
    backgroundColor: "{colors.surface-control}"
    textColor: "{colors.text-primary}"
    typography: "{typography.body}"
    size: "224px × 42px"
  game-folder-field:
    backgroundColor: "{colors.surface-control}"
    textColor: "{colors.text-primary}"
---

# Design System: Milton Fretes Launcher

## Overview

**Creative North Star: Uma atualização simples e clara.**

O launcher é uma ferramenta compacta para tarefas práticas do mod Milton Fretes. Sua identidade mantém o modo escuro e combina uma faixa de marca azul-marinho com uma superfície grafite, texto claro e ações diretas em português. A interface usa controles nativos do Windows Forms e Segoe UI.

O conteúdo fica em uma coluna única: estado e versão do mod, ações de atualizar e restaurar, pasta do jogo e estado do launcher. O fluxo prioriza uma próxima ação compreensível sem adicionar navegação ou painéis decorativos.

**Key Characteristics:**
- Superfície escura com faixa de marca navy.
- Ação principal azul e ação secundária discreta.
- Texto curto em português e controles nativos.
- Estados de operação e diálogo mantêm a mesma paleta escura.

## Colors

A paleta usa navy e grafite como superfícies, texto claro para leitura e azul Milton Fretes nas ações principais; âmbar e verde aparecem em estados.

### Primary
- **Azul de ação** (`action-blue`): preenche a ação principal de verificar, instalar ou atualizar. No hover, o botão usa o azul mais escuro definido no código.
- **Navy da marca** (`brand-navy`): identifica a faixa superior e serve de superfície aos diálogos.

### Neutral
- **Grafite** (`surface-graphite`): fundo principal da janela e superfície de conteúdo dos diálogos.
- **Grafite de controle** (`surface-control`): fundo do campo da pasta e dos botões secundários.
- **Texto claro** (`text-primary`): texto principal e identificação da marca.
- **Texto atenuado** (`text-muted`): detalhes secundários e estado do launcher.
- **Borda discreta** (`border-muted`): contorno dos controles secundários.
- **Divisor** (`divider`): linha horizontal que separa a pasta do jogo do rodapé.

### Tertiary
- **Âmbar de atenção** (`status-amber`): estados que precisam de atenção, como falha de acesso, versão diferente ou cópias duplicadas.
- **Verde de sucesso** (`status-green`): marcador de estado durante a verificação inicial. O estado de mod atualizado usa um verde próprio, ligeiramente mais claro.

## Typography

**Display Font:** Segoe UI (com fallback sans-serif do sistema)
**Body Font:** Segoe UI (com fallback sans-serif do sistema)

**Character:** Tipografia de sistema familiar, com títulos em negrito e texto de apoio mais discreto. Não há uma família display separada.

### Hierarchy
- **Status** (negrito, 15 pt): título do estado atual do mod.
- **Marca** (negrito, 12 pt): nome MILTON FRETES na faixa superior.
- **Body** (peso padrão, 9.5 pt): rótulos, botões e informações auxiliares.

## Layout

A janela usa tamanho de cliente fixo de 540 × 390 px e não permite maximização. Uma faixa navy de 48 px ocupa o topo. Abaixo, os controles formam uma coluna compacta; os dois botões principais ficam lado a lado, cada um com 224 × 42 px. A pasta do jogo aparece em uma linha e o estado do launcher fica no rodapé, abaixo de um divisor. A captura da janela completa mede 542 × 417 px com a moldura do Windows.

A interface usa a barra de título escura do Windows e o ícone fornecido. O layout é fixo; não há comportamento responsivo documentado.

## Elevation & Depth

A interface é plana. Não há sombras ou animações declaradas no código. A hierarquia vem do contraste entre superfícies, da faixa de marca, do azul da ação primária e de um divisor fino no rodapé.

## Shapes

Os controles usam estilo plano do Windows Forms. O campo da pasta tem borda simples; botões secundários usam contorno. Não há escala de raio ou cantos arredondados explícitos no código. A janela principal e os diálogos usam molduras fixas do Windows.

## Components

### Botões de ação
Ação principal com preenchimento azul e ação secundária com superfície grafite e contorno.
- **Tamanho das ações principais:** 224 × 42 px.
- **Primária:** texto claro em negrito; no hover, o azul escurece.
- **Secundária:** texto claro, fundo de controle e contorno; o hover clareia ligeiramente o fundo.
- **Estado desabilitado:** a primária troca para o fundo de controle e texto atenuado.
- **Uso:** a ação principal muda entre instalar, atualizar, reinstalar, organizar e verificar conforme o estado do mod. Restaurar backup permanece visível.

### Campo da pasta do jogo
Campo de leitura com caminho alinhado à direita, superfície de controle e borda simples. A ação “Alterar” abre a seleção de pasta.

### Estado do mod
Um marcador circular e um título em negrito comunicam o estado; uma linha secundária apresenta orientação ou versão instalada. A cor do título e do marcador acompanha estados de sucesso, atenção e operação.

### Diálogos
Seleção e restauração de backups, confirmação e notas de versão aparecem em diálogos Windows Forms com superfícies navy e grafite e botões no mesmo idioma visual.

### Alto contraste
Quando o Windows informa modo de alto contraste, a implementação substitui as cores fixas por cores do sistema. Preserve essa adaptação ao estender a interface.

## Do's and Don'ts

### Do:
- **Do** manter o modo escuro com navy de marca, superfícies grafite e azul na ação principal.
- **Do** usar Segoe UI e rótulos curtos e diretos em português.
- **Do** manter a hierarquia da coluna: estado do mod, ações, pasta do jogo e estado do launcher.
- **Do** preservar controles nativos e a adaptação de alto contraste do Windows.

### Don't:
- **Don't** acrescentar navegação lateral, painéis extras ou imagens decorativas ao launcher compacto.
- **Don't** simular a moldura do Windows como conteúdo rasterizado; a barra de título é nativa.
- **Don't** tratar versões ilustrativas da composição como estado real; release e instalação atuais determinam os dados exibidos.
