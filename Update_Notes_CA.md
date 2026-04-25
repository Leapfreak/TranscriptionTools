# Notes d'actualització — v1.1.0

## Nou motor de transcripció en directe

El motor de transcripció en directe s'ha reconstruït completament. L'antic processador d'àudio en C++ (whisper-stream) feia servir finestres de temps fixes per tallar l'àudio en blocs, cosa que sovint partia les frases per la meitat. S'ha substituït per un motor basat en Python que utilitza **faster-whisper** i **Silero VAD** (detecció d'activitat de veu). En lloc de dividir l'àudio en intervals de temps arbitraris, el sistema ara detecta les pauses naturals de la parla i només confirma el text quan s'ha completat una idea. Això produeix frases més netes i completes — i com que les traduccions ara reben frases senceres en lloc de fragments, la qualitat de la traducció és significativament millor.

El nou motor també inclou detecció d'al·lucinacions (filtrant text fantasma com "Thank you for watching" que whisper de vegades genera sobre el silenci), detecció d'autorepetició i una gestió més intel·ligent de la parla contínua llarga sense pauses.

## 85 idiomes de traducció

El suport de traducció s'ha ampliat de 16 a **85 idiomes** utilitzant el model NLLB-200. La llista completa:

Afrikaans, amhàric, àrab, armeni, azerbaidjanès, basc, bielorús, bengalí, bosnià, búlgar, català, txec, xinès, croat, danès, neerlandès, anglès, estonià, finès, francès, gallec, georgià, alemany, grec, gujarati, crioll haitià, haussa, hebreu, hindi, hongarès, islandès, indonesi, italià, japonès, javanès, kannada, kazakh, khmer, coreà, laosià, letó, lituà, luxemburguès, macedoni, malai, malaiàlam, maltès, maori, marathi, mongol, birmà, nepalès, noruec, persa, polonès, portuguès, panjabi, romanès, rus, serbi, shona, sindhi, singalès, eslovac, eslovè, somali, espanyol, sundanès, suahili, suec, tagal/filipí, tadjik, tàmil, tàtar, telugu, tailandès, turc, turcman, ucraïnès, urdú, uzbek, vietnamita, gal·lès, ioruba, zulu.

Cada espectador pot triar independentment el seu propi idioma de traducció des del client de subtítols — no cal reiniciar l'aplicació.

## Configuració unificada de primer ús

Totes les dependències (binaris de whisper, entorn Python, paquets pip, model faster-whisper, model de traducció NLLB) es descarreguen ara en un únic flux de configuració unificat durant el primer inici. El botó "Setup Translation" de la pestanya Subtitle Server s'ha substituït per un botó "Check Dependencies" que torna a executar la mateixa comprovació unificada.

## Altres millores

- **Detecció automàtica d'idioma**: El sistema pot detectar automàticament quin idioma s'està parlant en temps real, de manera que si un orador canvia d'idioma durant la sessió, cada línia s'identifica correctament i es tradueix des de l'idioma d'origen correcte.
- **Registre a la pestanya Main/Job**: El registre de sortida del procés whisper (anteriorment en una pestanya de registre separada) ara es mostra a la part inferior de la pestanya Main/Job, redimensionant-se per omplir l'espai disponible.
- **Fitxers d'ajuda reescrits**: Els 8 fitxers d'ajuda (anglès + 7 traduccions) s'han reescrit des de zero per reflectir el disseny actual de la interfície i el conjunt de funcionalitats.
- **Tancament net**: Els processos Python (live-server, nllb-server) ara es tanquen de manera fiable quan l'aplicació surt, fins i tot si el servidor no s'havia aturat prèviament.
- **Millores del visor de subtítols**: Els menús es tanquen correctament en tocar botons o tocar fora, i canviar l'idioma de traducció ja no requereix refrescar la pàgina.

## Pila tecnològica

| Component | Tecnologia | Empresa/Organització |
|-----------|-----------|---------------------|
| Model de transcripció en directe | Whisper (large-v3) | OpenAI |
| Motor de transcripció en directe | faster-whisper / CTranslate2 | SYSTRAN |
| Detecció d'activitat de veu | Silero VAD | Silero AI |
| Model de traducció | NLLB-200 (No Language Left Behind) | Meta AI |
| Motor de traducció | CTranslate2 | SYSTRAN |
| Tokenitzador (traducció) | SentencePiece | Google |
| Inferència GPU | CUDA / cuBLAS | NVIDIA |
| Framework web Python | FastAPI / Uvicorn | Sebastián Ramírez (codi obert) |
| Captura d'àudio | sounddevice (binding PortAudio) | PortAudio (codi obert) |
| Transcripció pestanya Job | whisper.cpp / whisper-cli | Georgi Gerganov (codi obert) |
| Framework de l'aplicació | .NET 8 / WinForms | Microsoft |
| Entorn Python | Python 3.12 embedded | Python Software Foundation |
| Instal·lador | Inno Setup | Jordan Russell (codi obert) |
| CI/CD | GitHub Actions | Microsoft (GitHub) |
