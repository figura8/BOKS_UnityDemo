# BOKS Bubble 3D — prototype v1

Sfera 3D trasparente con bordo Fresnel, riflessi bianchi procedurali, iridescenza leggera, wobble e pop con quattro archi e otto gocce. Nessuna texture o plugin richiesto. Riflessi stilizzati: non simula rifrazione o pellicola di sapone fisicamente corretta.

## Importazione URP

1. Estrai lo ZIP.
2. Copia la cartella `Assets/BOKS/Bubble3D` dentro `Assets/BOKS` del progetto, mantenendo la sottocartella `Bubble3D`.
3. Aspetta la compilazione. Usa il menu **BOKS > Bubble 3D > Create bubble in scene**.
4. Viene creato un oggetto nella scena corrente, all'origine, con materiali salvati nella cartella Bubble3D. Spostalo davanti alla tua camera e regola la scala del root.
5. Premi Play. Il wobble viene riprodotto automaticamente. Per provare il pop, seleziona il componente BOKSBubble3D in Inspector e usa il menu contestuale **Preview Pop (Play Mode)**.

Il comando modifica la scena corrente tramite Undo; non crea o sostituisce camere. La camera deve inquadrare la bolla. Assegna `Effect Camera` se la camera non è taggata MainCamera.

## Built-in Render Pipeline

Prima di importare, elimina i due file `*-URP.shader` dalla cartella Assets del pacchetto e sostituiscili con i due shader di `Variants/BuiltIn`. Poi segui gli stessi passi. HDRP non incluso. Non importare shader URP in un progetto privo del pacchetto URP.

## Collegamento al gioco

Chiama `bubble.Pop()` dal gestore già esistente del tap o del raggiungimento del goal. Non viene introdotto un secondo sistema di input.

```csharp
using BOKS.BubbleFX;
// Campo del tuo controller:
public BOKSBubble3D bubble;
// Nel callback del goal/tap:
bubble.Pop();
// Per riutilizzare la stessa bolla al prossimo livello:
bubble.ResetBubble();
```

`On Burst` scatta quando la superficie sparisce: collega qui il suono del pop. `On Pop Finished` scatta alla fine della coda: collega qui la transizione del livello. Le chiamate ripetute a Pop vengono ignorate durante/dopo il pop finché non chiami ResetBubble. Disattivare il componente interrompe l'effetto e lo ripristina.

## Parametri iniziali

Materiale Bubble: Center opacity 0.025, Rim opacity 0.48, Rim tightness 3.2, Highlight strength 0.85, Iridescence 0.16. Componente: Wobble 0.025, Wobble speed 1.5, Pop duration 0.28 s, più anticipazione 0.055 s.

Per avvicinarti al riferimento quasi incolore, imposta Iridescence a 0 e Membrane tint su un grigio azzurrato molto chiaro. I riflessi seguono la vista e si muovono lentamente; non sono riflessi dell'ambiente.

## Canvas e prestazioni

La bolla viene renderizzata dalla camera della scena: non è un elemento Image. Un Canvas Screen Space Overlay può coprirla. Per una bolla dentro la UI serve configurare Screen Space Camera, oppure un render separato con RenderTexture; questo collegamento non è incluso.

Il pop crea temporaneamente 4 LineRenderer e 8 sfere, poi le elimina. Adatto come primo prototipo per pochi scoppi; per molti scoppi simultanei aggiungere un pool. Prova il risultato su sfondo chiaro e scuro e sul dispositivo Android minimo previsto.

## Verifica

File controllati staticamente e ZIP verificato. Non compilato o renderizzato nell'Editor Unity: in questo ambiente non sono disponibili il progetto né Unity. Verificare Console senza errori, visibilità in Game View, Pop/Reset e callback prima di integrarlo. Nessun audio incluso. Target previsto Unity 2022.3 o Unity 6, URP/Built-in; compatibilità da verificare nel progetto.

Riferimenti API: https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/shaders/writing-custom-shaders-urp/writing-shaders-urp/basic-unlit-structure
https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/renderer/setpropertyblock
