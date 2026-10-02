# Chrome Telas e Abas Kiosk

Aplicativo portátil para Windows que abre o Google Chrome em modo quiosque nas telas físicas selecionadas.

## Download

Baixe `ChromeTelasAbasKiosk.exe` na seção **Releases** do GitHub ou diretamente nos arquivos deste repositório. Não é necessário instalar.

## Recursos

- seleção independente das telas 1, 2 e 3, incluindo combinações como Tela 1 + Tela 3;
- uma ou várias URLs por tela, informadas uma por linha;
- perfil isolado do Chrome para cada tela;
- rotação opcional entre abas a cada 15, 30, 60 ou 120 segundos;
- atualização das páginas a cada 1, 3, 5 ou 10 minutos;
- opção **Não atualizar** para páginas que precisam manter o estado atual;
- inicialização automática com o Windows;
- watchdog para reabrir uma janela do quiosque caso ela seja fechada;
- botões para salvar, iniciar e parar o quiosque.

## Como usar

1. Configure os monitores do Windows no modo **Estender estes vídeos**.
2. Execute `ChromeTelasAbasKiosk.exe`.
3. Marque as telas físicas que serão usadas.
4. Digite uma URL por linha para cada tela selecionada.
5. Escolha os intervalos de atualização e rotação.
6. Clique em **Salvar** e depois em **Iniciar**.

As configurações, perfis e logs ficam em `%LOCALAPPDATA%\DualChromeKiosk` e não são enviados ao repositório.

## Compilação

O projeto usa o compilador C# incluído no Windows/.NET Framework e não precisa baixar dependências:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build_chrome.ps1
```

O código também mantém a variante Chrome + Edge, que pode ser compilada com `build.ps1`.

## Requisitos

- Windows 10 ou Windows 11;
- Google Chrome instalado;
- até três monitores configurados no modo estendido.

> O Windows pode exibir um aviso do SmartScreen porque o executável não possui assinatura digital comercial.
