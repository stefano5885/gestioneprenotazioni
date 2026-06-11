# Product Requirements Document (PRD)

## 1. Overview

- **Product / Feature Name:** Gestione Prenotazioni Tavoli per Fiere e Sagre
- **Owner:** TODO: definire owner del prodotto
- **Status:** Draft
- **Last Updated:** 2026-05-25

## 2. Summary

Il prodotto e un'applicazione web multi-organizzazione per gestire le prenotazioni dei tavoli durante fiere, sagre ed eventi temporanei della durata tipica di 7-10 giorni. Gli utenti principali sono organizzatori, volontari e personale addetto all'accoglienza, spesso operativi da smartphone e in condizioni di lavoro rapide. L'applicazione deve consentire di configurare organizzazioni, eventi, date, turni, tavoli e prenotazioni, stimare l'occupazione e assegnare automaticamente i tavoli ottimizzando i posti disponibili. La soluzione sara pubblicata su internet tramite Docker e gestita con repository GitHub.

## 3. Problem Statement

Durante fiere e sagre, la gestione delle prenotazioni avviene spesso con fogli, messaggi, telefonate o liste cartacee. Questo rende difficile stimare la disponibilita residua, evitare sovrapposizioni, ottimizzare l'uso dei tavoli e sapere rapidamente chi si e presentato al momento del servizio.

Il problema e particolarmente critico per eventi brevi, con picchi di lavoro concentrati su pranzo e cena, personale non sempre tecnico e uso prevalente da smartphone. Serve quindi uno strumento semplice, veloce e affidabile che riduca gli errori operativi e renda automatica la fase piu delicata: l'assegnazione dei tavoli.

## 4. Goals and Non-Goals

**Goals**

- Gestire piu organizzazioni separate nello stesso deployment.
- Gestire piu eventi temporanei per organizzazione, ad esempio "Fiera 1", "Fiera 2" o edizioni annuali della stessa sagra.
- Gestire piu date per ciascun evento.
- Configurare turni per ciascuna data, ad esempio pranzo, cena o turni personalizzati.
- Configurare tavoli disponibili e capienza massima per turno.
- Registrare, modificare e annullare prenotazioni in modo rapido da smartphone.
- Stimare in tempo reale tavoli occupati, posti prenotati e capacita residua.
- Assegnare automaticamente i tavoli prima del servizio secondo regole di ottimizzazione.
- Supportare la fase di accoglienza con lista presenze e stato "presentato".
- Proteggere l'accesso tramite utente e password.
- Rendere l'applicazione pubblicabile via Docker e mantenibile tramite GitHub.
- Offrire una grafica moderna, leggera e adatta a schermi piccoli.

**Non-Goals**

- Non gestire pagamenti online nella prima versione.
- Non gestire menu, ordini, cucina o cassa.
- Non sostituire un gestionale completo per ristoranti.
- Non richiedere una mappa grafica avanzata della sala nella prima versione.
- Non implementare billing, piani commerciali o self-service pubblico per nuove organizzazioni nella prima versione.

## 5. Success Metrics

| Metric | Baseline | Target | Timeframe | Notes |
|---|---:|---:|---|---|
| Tempo medio inserimento prenotazione | TODO | < 30 secondi | MVP | Da smartphone con campi minimi |
| Prenotazioni assegnate automaticamente senza intervento manuale | TODO | >= 90% | Primo evento reale | Esclusi casi limite e overbooking |
| Errori di sovra-assegnazione tavoli | TODO | 0 | Ogni turno | Nessun tavolo oltre capienza |
| Utilizzo posti disponibili | TODO | >= 80% nei turni pieni | Primo evento reale | Misurato come posti prenotati / posti totali assegnabili |
| Tempo per depennare una persona arrivata | TODO | < 5 secondi | MVP | Azione singola da lista accoglienza |
| Usabilita su schermo piccolo | TODO | Nessun flusso critico bloccato su 360px di larghezza | MVP | Verifica responsive |

## 6. Users and Use Cases

**Primary Users**

- Organizzatore dell'evento: configura evento, turni, tavoli e regole operative.
- Addetto prenotazioni: inserisce prenotazioni ricevute da telefono, messaggio o di persona.
- Addetto accoglienza: controlla la lista durante il servizio, cerca i nominativi e segna gli arrivi.
- Amministratore tecnico: pubblica e aggiorna l'applicazione tramite Docker e GitHub.

**Key Use Cases**

- Creare una nuova organizzazione.
- Creare un nuovo evento associato a una organizzazione.
- Creare date operative per l'evento.
- Creare turni per ciascuna data, ad esempio pranzo e cena.
- Definire i tavoli disponibili per ciascun turno e la loro capienza.
- Inserire una prenotazione con nome, data, numero persone e campi facoltativi.
- Visualizzare la stima di occupazione del turno mentre arrivano le prenotazioni.
- Eseguire manualmente l'assegnazione automatica dei tavoli tramite pulsante.
- Correggere manualmente una assegnazione quando necessario.
- Durante il turno, cercare una prenotazione e segnarla come "presentata".
- Consultare prenotazioni non presentate, presenti e annullate.

## 7. Requirements

| ID | Requirement | Priority | Rationale | Acceptance Criteria |
|---|---|---|---|---|
| R1 | L'app deve richiedere login con utente e password. | P0 | Evitare uso da parte di terzi. | Un utente non autenticato non puo accedere a eventi, turni, tavoli o prenotazioni. |
| R1A | L'app deve prevedere ruoli utente Admin, Operatore prenotazioni e Operatore accoglienza. | P0 | Limitare le funzioni in base al compito operativo. | Admin gestisce configurazione e utenti; Operatore prenotazioni gestisce prenotazioni; Operatore accoglienza consulta e segna i presenti. |
| R2 | L'app deve consentire la gestione di piu organizzazioni separate. | P0 | Lo stesso deployment deve servire piu realta operative. | Ogni organizzazione ha dati separati e puo avere piu eventi. |
| R3 | L'app deve consentire la creazione e modifica di eventi per organizzazione. | P0 | Gli eventi sono il contenitore operativo principale dell'organizzazione. | Si possono creare eventi con nome, stato attivo/archiviato e associazione a una organizzazione. |
| R4 | L'app deve consentire la gestione di piu date per evento. | P0 | Un evento puo durare piu giorni. | Ogni evento puo avere una o piu date operative. |
| R4A | L'app deve consentire la gestione dei turni per data evento. | P0 | Le prenotazioni sono legate a una data e a un servizio. | Si possono creare turni con nome, data evento associata, orario indicativo e stato. |
| R4B | L'app deve consentire la gestione dei tavoli per turno. | P0 | L'assegnazione richiede capacita disponibile. | Nell'MVP ogni turno usa tavoli standard da 8 posti; il modello dati deve comunque prevedere una capienza configurabile per supportare valori diversi in futuro. |
| R5 | L'app deve consentire inserimento, modifica e annullamento prenotazioni. | P0 | Funzione operativa primaria. | Una prenotazione contiene nome, data, turno, numero persone, ora prevista facoltativa e cellulare facoltativo. |
| R6 | L'app deve validare il numero di persone. | P0 | Evitare dati inutilizzabili per assegnazione. | Il numero persone e obbligatorio, intero positivo e maggiore di 0. |
| R7 | L'app deve mostrare stima tavoli occupati e posti occupati durante la raccolta prenotazioni. | P0 | Serve capire se il turno sta andando verso il pieno. | La vista turno mostra posti prenotati, posti totali, tavoli stimati occupati e disponibilita residua. |
| R8 | L'app deve eseguire assegnazione automatica tavoli per turno. | P0 | Parte intelligente e differenziante del prodotto. | Avviando la procedura su un turno, ogni prenotazione assegnabile riceve uno o piu tavoli rispettando le regole. |
| R9 | L'assegnazione non deve mai superare la capienza del tavolo o del gruppo di tavoli. | P0 | Vincolo fondamentale. | Nessun tavolo assegnato contiene piu persone della propria capienza. |
| R10 | L'algoritmo deve assegnare generalmente una prenotazione a un tavolo. | P0 | Semplifica accoglienza e riduce convivenze indesiderate. | Le prenotazioni vengono accorpate solo quando necessario o quando esplicitamente consentito dalle regole. |
| R11 | L'algoritmo deve permettere accorpamenti controllati quando i tavoli non bastano. | P0 | Ottimizzare posti nei turni pieni. | Sono ammessi solo gli accorpamenti: massimo 3 prenotazioni da 2; massimo 1 prenotazione da 4 con 1 da 2; massimo 2 prenotazioni da 3. |
| R12 | L'algoritmo deve gestire prenotazioni superiori a 8 persone usando blocchi di tavoli da 8 posti. | P0 | Gestire gruppi grandi senza limite artificiale a 16 persone. | Il numero di tavoli necessari e calcolato arrotondando per eccesso `numero persone / 8`. |
| R13 | Per prenotazioni superiori a 8 persone, l'algoritmo deve consentire una prenotazione aggiuntiva da massimo 2 persone se restano almeno 4 posti liberi nel blocco tavoli assegnato. | P0 | Regola specifica di ottimizzazione. | Se `posti disponibili nel blocco - persone prenotazione principale >= 4`, puo essere associata una prenotazione da 1 o 2 persone. |
| R14 | L'app deve permettere revisione manuale delle assegnazioni. | P1 | Gli operatori devono poter gestire eccezioni reali. | Un utente autorizzato puo spostare prenotazioni tra tavoli prima o durante il turno. |
| R15 | L'assegnazione automatica deve essere avviata manualmente con pulsante e deve essere rieseguibile previa conferma. | P0 | L'operatore deve controllare quando cristallizzare o ricalcolare il turno. | Se il turno non ha assegnazioni, il pulsante avvia la procedura; se esistono gia assegnazioni, l'app mostra un avviso e richiede conferma prima di ricalcolare. |
| R16 | L'app deve mostrare una lista accoglienza ottimizzata per smartphone. | P0 | Durante il servizio serve rapidita. | La lista consente ricerca per nome/cellulare, filtro stato e azione rapida "presentato". |
| R17 | L'app deve gestire stati prenotazione. | P0 | Serve distinguere flussi operativi. | Stati minimi: inserita, assegnata, presentata, annullata, non presentata. |
| R18 | L'interfaccia deve essere mobile-first. | P0 | Uso prevalente da smartphone e schermi piccoli. | Tutti i flussi P0 funzionano senza zoom orizzontale su larghezza 360px. |
| R19 | L'app deve essere pubblicabile tramite Docker. | P0 | Vincolo di deployment. | Il repository include Dockerfile e istruzioni minime di avvio. |
| R20 | Il codice deve essere gestibile tramite GitHub. | P0 | Vincolo operativo. | Il repository contiene README, configurazione ambiente e flusso di rilascio documentato. |
| R21 | L'app dovrebbe supportare importazione prenotazioni da Telegram o WhatsApp. | P2 | Ridurre inserimento manuale. | Possibile ricevere o importare messaggi strutturati in una coda prenotazioni da confermare. |
| R22 | L'app deve esportare liste prenotazioni e accoglienza in Excel e PDF. | P1 | Gli operatori possono aver bisogno di stampe, condivisione o controllo esterno all'app. | L'utente puo esportare per evento/turno in formato Excel e PDF, scegliendo colonne incluse e ordine di visualizzazione. |
| R23 | L'app deve mantenere un audit log delle modifiche operative. | P1 | Serve ricostruire chi ha modificato prenotazioni, tavoli e assegnazioni. | Per ogni modifica rilevante vengono registrati utente, data/ora, entita modificata, azione e valori principali prima/dopo quando disponibili. |

## 8. User Experience and Flows

**UX Notes**

- Design moderno, leggero e mobile-first.
- Navigazione essenziale, con pochi livelli e azioni principali sempre evidenti.
- Schermate dense ma leggibili per uso operativo durante il servizio.
- Pulsanti grandi e facilmente tappabili, soprattutto per ricerca, nuova prenotazione e "presentato".
- Evitare elementi decorativi pesanti: priorita a velocita, chiarezza e leggibilita.
- Supportare condizioni d'uso reali: luce esterna, mani occupate, connessione non sempre perfetta.

**Key Screens / States**

- Login.
- Selezione organizzazione.
- Dashboard eventi attivi.
- Dettaglio evento con elenco date.
- Dettaglio data con elenco turni.
- Dettaglio turno con riepilogo posti, tavoli, prenotazioni e stato assegnazione.
- Gestione tavoli del turno.
- Form prenotazione mobile-first.
- Risultato assegnazione automatica.
- Lista accoglienza con ricerca e filtri.
- Vista tavolo con prenotazioni assegnate.
- Configurazione export con selezione colonne e ordinamento.

**Core Flow 1: Raccolta Prenotazioni**

1. L'utente accede all'app.
2. Seleziona organizzazione, evento, data e turno.
3. Inserisce una nuova prenotazione.
4. L'app aggiorna la stima di posti occupati e tavoli stimati.
5. L'utente continua finche il turno resta aperto.

**Core Flow 2: Assegnazione Automatica**

1. Prima del servizio, l'utente apre il turno.
2. Avvia la procedura tramite pulsante "Assegna tavoli".
3. L'app calcola assegnazioni, accorpamenti e prenotazioni non assegnabili.
4. L'utente verifica il risultato.
5. L'utente conferma o corregge manualmente.
6. Se l'utente preme di nuovo "Assegna tavoli", l'app mostra un avviso e richiede conferma prima di ricalcolare.

**Core Flow 3: Accoglienza**

1. Durante il servizio, l'utente apre la lista accoglienza del turno.
2. Cerca il nome o scorre la lista.
3. Verifica tavolo assegnato e numero persone.
4. Segna la prenotazione come "presentata".
5. A fine turno, l'utente puo visualizzare non presentati e statistiche.

## 9. Assignment Logic

**Input principali**

- Turno selezionato.
- Elenco tavoli disponibili con capienza.
- Elenco prenotazioni attive del turno.
- Capacita standard tavolo: 8 posti nell'MVP, memorizzata come valore configurabile per future capienze diverse.

**Vincoli obbligatori**

- Nessun tavolo puo superare la propria capienza.
- Ogni prenotazione deve essere assegnata a un solo tavolo o, per gruppi grandi, a un gruppo di tavoli.
- Le prenotazioni annullate non partecipano all'assegnazione.
- Se l'assegnazione viene rieseguita, l'app deve avvisare che il ricalcolo puo modificare assegnazioni automatiche e manuali gia presenti.

**Priorita operative**

1. Assegnare le prenotazioni grandi o difficili da collocare.
2. Assegnare una prenotazione per tavolo quando possibile.
3. Usare accorpamenti solo se necessario per far entrare piu prenotazioni.
4. Minimizzare posti inutilizzati senza rendere complessa l'accoglienza.
5. Evidenziare prenotazioni non assegnabili.

**Accorpamenti consentiti su tavolo standard da 8**

| Pattern | Condizione | Esempio |
|---|---|---|
| 3 prenotazioni da 2 persone | Massimo 3 prenotazioni, tutte da 2 | 2 + 2 + 2 = 6 |
| 1 prenotazione da 4 e 1 da 2 | Massimo una da 4 e una da 2 | 4 + 2 = 6 |
| 2 prenotazioni da 3 | Massimo 2 prenotazioni, entrambe da 3 | 3 + 3 = 6 |

**Prenotazioni superiori a 8 persone**

- Una prenotazione superiore a 8 persone viene trattata come prenotazione principale su un blocco di tavoli standard da 8 nell'MVP.
- Il numero di tavoli necessari e calcolato arrotondando per eccesso `numero persone / 8`.
- La capacita totale del blocco tavoli e `numero tavoli assegnati * 8`.
- Se `capacita blocco - numero_persone_prenotazione >= 4`, allora e consentito associare una prenotazione aggiuntiva da massimo 2 persone.
- Esempio: prenotazione da 12 persone, restano 4 posti teorici. E possibile aggiungere una prenotazione da 2 persone.
- Esempio: prenotazione da 14 persone, restano 2 posti teorici. Non e possibile aggiungere un'altra prenotazione, perche il residuo e minore di 4.
- Esempio: prenotazione da 17 persone, servono 3 tavoli per 24 posti teorici. Restano 7 posti, quindi e possibile aggiungere una prenotazione da massimo 2 persone.

**Output procedura**

- Tavoli assegnati.
- Prenotazioni accorpate.
- Prenotazioni non assegnabili.
- Riepilogo posti usati, posti liberi e tavoli occupati.
- Eventuali avvisi su casi limite.

## 10. Data and Analytics

**Core Data Entities**

- Organization: nome, stato, impostazioni.
- User: credenziali, ruolo tra Admin/Operatore prenotazioni/Operatore accoglienza e organizzazioni abilitate.
- Event: organizzazione, nome, stato.
- EventDate: evento, data operativa, stato.
- Shift: data evento, nome turno, ora inizio indicativa, stato.
- Table: turno, codice tavolo, capienza, note.
- Reservation: turno, nome prenotante, data, ora prevista facoltativa, cellulare facoltativo, numero persone, stato.
- Assignment: tavolo o gruppo tavoli, prenotazioni assegnate, origine automatica/manuale, timestamp.
- AuditLog: utente, timestamp, entita, id entita, azione, dettaglio modifica.

**Events to Track**

- Login riuscito/fallito.
- Creazione/modifica organizzazione.
- Creazione/modifica evento.
- Creazione/modifica data evento.
- Creazione/modifica turno.
- Creazione/modifica/annullamento prenotazione.
- Avvio assegnazione automatica.
- Esito assegnazione automatica.
- Modifica manuale assegnazione.
- Prenotazione segnata come presentata.
- Scrittura audit log per modifiche a prenotazioni, tavoli, turni e assegnazioni.

**Dashboards / Reports**

- Riepilogo per evento: prenotazioni totali, persone totali, presenze effettive.
- Riepilogo per turno: posti prenotati, tavoli occupati, non presentati.
- Report assegnazione: accorpamenti effettuati e prenotazioni non assegnate.
- Export Excel/PDF per liste prenotazioni e accoglienza con colonne e ordine configurabili.

## 11. Dependencies and Integrations

- Docker per pubblicazione e gestione ambiente.
- GitHub per versionamento e deployment workflow.
- Database persistente da definire, preferibilmente semplice da deployare in Docker.
- Sistema di autenticazione interno con utente/password.
- Bonus Telegram: integrazione con bot Telegram per ricezione prenotazioni o notifiche.
- Bonus WhatsApp: integrazione tramite WhatsApp Business API o provider esterno; da valutare per costi, policy e complessita.

## 12. Risks, Assumptions, and Open Questions

**Risks**

- Regole di assegnazione non complete rispetto a casi reali non ancora emersi.
- Uso da smartphone durante momenti concitati: ogni passaggio extra puo rallentare il personale.
- Connessione internet instabile presso fiere e sagre, dato che la prima versione e solo online.
- Integrazione WhatsApp potenzialmente complessa per costi, approvazioni e vincoli API.
- Overbooking se la stima preliminare differisce troppo dall'assegnazione finale.

**Assumptions**

- La capienza standard dei tavoli e 8 persone nell'MVP; il modello deve essere predisposto per capienze diverse in versioni successive.
- Un evento dura tipicamente massimo 7-10 giorni.
- Gli utenti sono interni alle organizzazioni abilitate.
- Nella prima versione sono previsti tre ruoli: Admin, Operatore prenotazioni, Operatore accoglienza.
- L'app sara usata principalmente in italiano.
- Le prenotazioni sono associate a un turno specifico, che appartiene a una data evento, evento e organizzazione.

**Open Questions**

- Telegram/WhatsApp: integrazione sospesa, da decidere in una fase successiva.

## 13. Rollout Plan

**Phases**

- Phase 1 - MVP operativo:
  - Login.
  - Organizzazioni, eventi, date, turni, tavoli.
  - Prenotazioni.
  - Stima occupazione.
  - Assegnazione automatica base.
  - Lista accoglienza.
  - Dockerfile e README.
- Phase 2 - Ottimizzazione operativa:
  - Correzione manuale avanzata.
  - Export Excel/PDF configurabile.
  - Audit log consultabile.
  - Report turno/evento.
  - Miglioramenti algoritmo su casi reali.
  - Ruoli utente.
- Phase 3 - Integrazioni:
  - Telegram.
  - WhatsApp o importazione messaggi.
  - Notifiche o promemoria.

**Launch Criteria**

- Tutti i requisiti P0 implementati e testati.
- Flussi principali verificati su smartphone o viewport 360px.
- Procedura di assegnazione testata con dataset realistici.
- Deployment Docker documentato e ripetibile.
- Backup dati o istruzioni di persistenza documentate.

**Rollback Plan**

- Conservare export delle prenotazioni prima di ogni aggiornamento.
- Possibilita di riavviare versione Docker precedente.
- Migrazione database documentata per ogni release che modifica lo schema.

## 14. Testing and QA

**Test Strategy**

- Test unitari per regole di assegnazione tavoli.
- Test di integrazione per prenotazioni, turni e assegnazioni.
- Test end-to-end dei flussi principali mobile-first.
- Test manuale durante simulazione di un turno reale.

**Key Test Cases**

- Inserimento prenotazione con solo campi obbligatori.
- Inserimento prenotazione con ora prevista e cellulare.
- Calcolo stima posti e tavoli su turno vuoto, parziale e pieno.
- Assegnazione con una prenotazione per tavolo.
- Assegnazione con 3 prenotazioni da 2 sullo stesso tavolo.
- Assegnazione con 1 prenotazione da 4 e 1 da 2.
- Assegnazione con 2 prenotazioni da 3.
- Prenotazione da 9-16 persone su 2 tavoli.
- Prenotazione da 12 persone con aggiunta di prenotazione da 2.
- Prenotazione da 14 persone senza aggiunta.
- Prenotazioni non assegnabili quando i tavoli sono insufficienti.
- Ricerca e marcatura "presentato" da lista accoglienza.
- Export Excel e PDF con colonne selezionate e ordine configurato.
- Audit log creato quando un utente modifica prenotazioni, tavoli o assegnazioni.
- Accesso negato a utente non autenticato.

**Performance / Security**

- Le schermate principali devono caricarsi rapidamente anche su connessione mobile.
- Password salvate con hashing sicuro.
- Sessioni protette e scadenza sessione configurabile.
- Protezione base contro accessi non autorizzati alle API.
- Audit log non modificabile dagli operatori standard.
- Backup e persistenza dati documentati per deployment Docker.

## 15. Appendix

**Alternatives Considered**

- Foglio di calcolo condiviso: semplice ma fragile, difficile da usare in accoglienza e non automatizza assegnazioni.
- Gestionale ristorante completo: troppo complesso e non ottimizzato per eventi brevi.
- App solo mobile nativa: migliore integrazione telefono, ma maggiore costo di sviluppo e distribuzione.

**References / Links**

- GitHub owner indicato: https://github.com/stefano5885
- TODO: aggiungere repository definitivo quando creato.
