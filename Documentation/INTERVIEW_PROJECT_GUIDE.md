# Guida BOKS per il colloquio

## SCHEDA RAPIDA PER IL COLLOQUIO

### Il progetto in cinque frasi

BOKS è un puzzle game in cui il giocatore programma un personaggio con blocchi visivi. Compone una sequenza breve, la esegue e porta BOKS al traguardo su una griglia 6 x 6. Il progetto è stato portato in Unity come campagna guidata dai dati, non come una scena per livello. La scena Campaign riusa una plancia e legge configurazioni da campaign-levels.json. Il Level Editor riusa plancia e runner reali, quindi un livello creato viene testato con le regole viste dal giocatore.

### Cinque sistemi chiave

1. BOKSLevelDefinition: contratto di un livello: start, goal, ostacoli, personaggio, blocchi e slot.
2. BOKSLevelLoader: legge JSON da Unity Resources e lo normalizza.
3. BOKSCampaignController: gestisce progressione 1–10 e transizioni.
4. BOKSCampaignView: trasforma una definizione nella plancia visibile.
5. BOKSLevel2Controller: runner condiviso per programma, movimento, ostacoli e successo.

### Cinque decisioni di design

1. Livelli data-driven: si cambia contenuto senza duplicare scene.
2. Editor e gioco usano lo stesso runtime.
3. Root logica separata da Visual e Art.
4. Dati, comportamento e asset sono separati.
5. Sistemi piccoli e leggibili, quindi facili da iterare e insegnare.

### Cinque domande probabili

1. Perché non una scena per livello? “Regole e plancia sono condivise; il layout è un dato.”
2. Cosa fa Run? “Blocca input, copia i blocchi, li esegue in ordine e controlla il risultato.”
3. Come funziona Function? “Main chiama una riga Function di quattro slot; non è ricorsiva.”
4. Cosa riusa l’editor? “Plancia, palette, slot e runner reali.”
5. Come hai usato AI? “Come assistenza all’implementazione, in un processo guidato, controllato e testato.”

### Cinque promemoria per la demo

1. Parti da BOKS_MainMenu, poi entra in BOKS_Campaign.
2. Indica una sola plancia riconfigurata dal JSON.
3. Level 1 o 2 è la demo Run più sicura; Level 6 mostra Function.
4. Nell’editor fai solo una modifica temporanea e non salvare.
5. Il Level 11 custom non fa parte della progressione, che termina al 10.

## Spiegazione in 60–90 secondi

“BOKS è un piccolo puzzle game di programmazione. Un livello è soprattutto un dato: griglia, posizione e direzione iniziale di BOKS, goal, ostacoli, blocchi disponibili e slot sbloccati. Unity carica questi dati da un unico JSON. La scena Campaign usa BOKSCampaignView per aggiornare la plancia riutilizzabile, mentre BOKSLevel2Controller esegue i comandi del giocatore e gestisce movimento, rotazioni, collisioni e arrivo al goal.

Il flusso è Main Menu, Campaign, caricamento della definizione, composizione del programma, Run, feedback e passaggio al livello successivo. Il Level Editor modifica una bozza della stessa definizione, la visualizza subito sulla plancia reale, la salva nel JSON e la prova con gli stessi controlli del giocatore.”

## Mappa dell’architettura

    MainMenu -> BOKSMainMenu -> Campaign
    CampaignController -> LevelLoader -> campaign-levels.json
                       -> CampaignView.ApplyLevel(definizione)
                       -> Level2Controller.LoadLevel(definizione)
                       -> programma -> Run -> risultato
                       <- evento LevelCompleted
                       -> LevelTransition -> livello successivo

    LevelEditor -> CampaignAuthoringMode -> bozza LevelDefinition
                -> stessa View + stesso Controller
                -> Save -> JSON -> import/cache
                -> Test Level -> stesso runner -> ripristino

## Scene e responsabilità

### BOKS_MainMenu

È la prima scena nelle Build Settings. L’oggetto BOKS Main Menu ha BOKSMainMenu: costruisce menu a runtime, avvia musica e carica BOKS_Campaign dopo il click su Challenge. Da dire: “È la scena di ingresso e presentazione; non contiene le regole.”

### BOKS_Campaign

Contiene griglia, gerarchia BOKS, goal, palette, otto slot Main e quattro Function. BOKSCampaignController parte dal Level 1, carica una definizione e la applica tramite view e runner. Dopo LevelCompleted blocca input e BOKSLevelTransition copre lo schermo, cambia definizione e rivela il livello. La campagna player è intenzionalmente limitata a 1–10.

### BOKS_LevelEditor

Usa la stessa composizione della plancia. BOKSCampaignAuthoringMode è un overlay editor con ExecuteAlways e pannello OnGUI. Non costruisce una simulazione parallela.

## Chi possiede cosa

| Sistema | Cosa fa | Cosa dire |
|---|---|---|
| BOKSLevelDefinition | Regole e layout di un livello | “È il contratto del livello.” |
| BOKSLevelLoader | Carica/cache JSON | “Rende il JSON utilizzabile in Unity.” |
| BOKSCampaignController | Progressione e transizioni | “Cambia livello, non implementa movimento.” |
| BOKSCampaignView | Applica dati alla plancia | “È il ponte fra dati e schermo.” |
| BOKSLevel2Controller | Programma ed esecuzione | “È il motore delle regole, nonostante il nome storico.” |
| BOKSCampaignAuthoringMode | Bozza e UI di authoring | “Modifica dati e li prova nel gioco reale.” |

## Dati, plancia e comandi

Il file runtime è Assets/BOKS/Resources/BOKS/Data/campaign-levels.json. Contiene i livelli 1–10 e un Level 11 custom. Ogni record include personaggio, start, goal, startOri, ostacoli, decorazioni, otto mainSlotEnabled, quattro fnSlotEnabled e blocchi abilitati. NormalizeSourceFields porta il JSON nel modello condiviso; LoadCampaign carica e mette in cache; l’editor svuota la cache dopo un salvataggio.

CampaignView.ApplyLevel elimina ostacoli e decorazioni dinamici precedenti, aggiorna goal, slot e palette, crea i nuovi elementi, sceglie gli sprite e chiama gameplay.LoadLevel. Per questo una sola scena può giocare molti puzzle.

La griglia è 6 x 6 con coordinate zero-based. x cresce a destra, y verso il basso. Forward calcola la cella davanti, la limita ai bordi e controlla gli ostacoli. Un ostacolo lascia BOKS fermo; entrare nel goal completa il run.

Ci sono otto slot Main e quattro Function. I blocchi sono Forward, Turn Left, Turn Right e Function. Function è una sottoroutine: in Main chiama la riga Function in ordine. Function non può stare nella riga Function, quindi non c’è ricorsione.

## Cosa succede con Run

1. Play chiama TryPlay.
2. Rifiuta programma vuoto, input bloccato o run già attivo.
3. Blocca input, disabilita palette/Run, riproduce il cue e copia il programma.
4. ExecuteProgram evidenzia ed esegue Main; Function passa alla riga Function.
5. ExecuteCommand muove o ruota BOKS.
6. Forward controlla bordi e ostacoli; il goal attiva VFX/audio.
7. Il successo emette LevelCompleted; il fallimento dà feedback e resetta.

La gerarchia è Root -> BOKS Visual -> BOKS Art. Root conserva la cella logica; Visual esegue rotazione e shake; Art cambia sprite. Da dire: “La logica conosce sempre la cella della root, quindi l’animazione non rende ambiguo lo stato.”

## Level Editor: New, Save, Delete, Test

L’editor carica un livello, lo copia in una bozza e chiama ApplyDraft. I tool piazzano BOKS, goal e ostacoli; altri controlli definiscono direzione, blocchi e slot. La validazione impedisce celle fuori plancia e sovrapposizioni.

* New Level crea una bozza 6 x 6 non salvata; al Save riceve numero successivo e ID custom.
* Save scrive campaign-levels.json, verifica il record, forza import Unity, svuota cache e verifica il reload.
* Delete è consentito solo sopra il Level 10, con conferma e verifica.
* Test Level salva bozza e programma in SessionState, usa il runner reale e poi ripristina lo stato editor.

## Asset, audio e feedback

CampaignView usa riferimenti serializzati a sprite; gli asset runtime sono in Assets/BOKS/RuntimeAssets. BOKSAudioManager è un servizio persistente per cue come ForwardStep, Turn, BlockedMove, goal pop e completion, e gestisce musica menu/gioco. BOKSLevelTransition crea l’overlay fullscreen dei cambi livello. Blink, squint, rebuke, bee hover e reazioni alle decorazioni sono presentazione: rendono chiaro lo stato, ma non decidono le regole.

## Walkthrough Unity sicuro: 5–7 minuti

1. Apri BOKS_MainMenu; seleziona BOKS Main Menu, mostra BOKSMainMenu nell’Inspector, Play e Challenge.
2. In BOKS_Campaign seleziona la root. Indica griglia, Root/Visual/Art, palette e gruppi Main/Function.
3. Apri campaign-levels.json e mostra start, goal, mainSlotEnabled ed enabledBlocks.
4. Level 1/2: piazza Forward, premi Run e narra blocco input, highlight, movimento e goal.
5. Apri BOKS_LevelEditor e indica BOKSCampaignAuthoringMode.
6. Attiva/disattiva uno slot o aggiungi/rimuovi un ostacolo su una cella vuota.
7. Premi Test Level, non Save, e prova una sequenza breve.

## Domande e risposte

1. Perché dati e non scene? “Regole condivise, layout variabile.” Più a fondo: meno duplicazione e iterazione più rapida.
2. Cos’è una scena qui? “Un contenitore riutilizzabile di componenti e UI.” Campaign contiene la plancia, non un singolo puzzle.
3. Come funzionano i componenti? “Separano responsabilità.” Controller, view, audio e feedback restano focalizzati.
4. Come funziona il programma? “Un array di comandi dietro le card.” Run ne fa una copia.
5. Perché Function semplice? “Main chiama una riga, che non chiama se stessa.” Evita ricorsione.
6. Collisioni? “Forward controlla la cella seguente contro gli ostacoli.” È logica discreta, non fisica.
7. Bordi? “La cella viene limitata alla griglia.” Stato sempre valido.
8. Debug? “Controllo JSON/bozza, log e highlight.” L’editor valida coordinate e overlap.
9. Perché editor/runtime condivisi? “Il tool deve comportarsi come il gioco.” Le correzioni valgono per entrambi.
10. Workflow asset? “Distinguo sorgenti e asset pronti al runtime.” I riferimenti serializzati restano stabili.
11. Version control? “JSON è testo e si revisiona bene; i meta mantengono GUID Unity.”
12. Cosa miglioreresti? “Campagna meno hard-coded e più validazione/test.”
13. Come lo insegneresti? “Forward, orientamento, Function, poi authoring dati.”
14. Come hai iterato sul visual? “Feedback sopra logica stabile.” Root/Visual/Art protegge le regole.
15. Come hai usato AI? “Come supporto all’implementazione, non sostituto di decisioni, review o test.”

## Cinque cose da capire assolutamente

1. Una plancia, molte definizioni; Campaign non carica scene Level 2/3.
2. Definition -> View -> Runner: dati, visualizzazione, regole.
3. Otto Main e quattro Function; Function è chiamabile solo da Main.
4. Cella/direzione sono logica; animazione/audio sono feedback.
5. Editor lavora su una bozza: Save persiste, Test usa il runner e ripristina.

## Limiti onesti e AI

Questo è un progetto derivato da un prototipo, non un’architettura enterprise. BOKSLevel2Controller conserva un nome storico da Level 2 pur essendo il runner condiviso: “il nome è legacy, la responsabilità è cresciuta.” La campagna è hard-coded a 1–10 anche se l’editor può creare dati successivi: “i primi dieci sono una progressione curata, gli altri contenuto custom.” Il pannello usa OnGUI: “è un tool pragmatico in-scene, pensato per iterare sulla plancia viva.”

Risposta a “quanto hai codificato tu?”: “Non metterei una percentuale. Ho guidato requisiti, prototipazione, direzione visiva e animazione, iterazione e test, usando AI come assistenza all’implementazione. La mia responsabilità è capire, validare e integrare il risultato; posso spiegare il flusso dati e le decisioni di gameplay senza attribuirmi output che non saprei spiegare.”

## Perché è utile per il primo anno

| Concetto | Esempio BOKS |
|---|---|
| Prototipo | Regole piccole e feedback immediato |
| Asset | Sprite, logo, decorazioni e audio |
| Engine/componenti | Scene, GameObject e script focalizzati |
| Stato | Cella, direzione, programma, running/success |
| Dati | JSON guida puzzle diversi |
| Logica | Sequenze, collisioni e sottoroutine |
| UI | Palette, drag/drop, slot e Run |
| Tool | Bozza, validazione, Save/Delete/Test |
| Version control | JSON leggibile e meta per riferimenti |

## GLOSSARIO TECNICO

**Asset:** file usato da Unity, come sprite, audio o JSON.

**Authoring:** creare contenuto; qui layout e regole di un livello.

**Campaign:** sequenza giocabile, qui Level 1–10.

**Componente:** comportamento collegato a un GameObject.

**Definition:** descrizione strutturata di un livello.

**Function:** riga di quattro slot richiamata da Main, senza ricorsione.

**Grid cell:** coordinata discreta (x,y) della griglia.

**JSON:** formato testuale usato per salvare livelli.

**Logical state:** stato affidabile delle regole, come cella e direzione.

**Presentazione:** feedback visuale/audio che comunica stato senza decidere regole.

**Resources:** cartella Unity da cui il runtime carica JSON e audio.

**Runner:** sistema che esegue i comandi.

**View:** ponte fra definizione e plancia visibile.
