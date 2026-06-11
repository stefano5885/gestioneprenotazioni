# Stato lavori

## Fatto

- PRD iniziale e aggiornato con decisioni prese.
- Soluzione .NET 10 `GestionePrenotazioni.slnx`.
- App ASP.NET Core Razor Pages `GestionePrenotazioni.Web`.
- Test xUnit `GestionePrenotazioni.Tests`.
- Dockerfile multi-stage.
- Login demo `admin/admin`.
- Store persistente JSON con dati demo in `App_Data`.
- Dominio iniziale:
  - Organization
  - ApplicationUser
  - FairEvent
  - EventDate
  - Shift
  - DiningTable
  - Reservation
  - TableAssignment
  - AuditLogEntry
- Dashboard mobile-first con:
  - menu operativo laterale/responsive
  - menu configurazione completo
  - pagine Razor separate per panoramica, prenotazioni, accoglienza, assegnazioni, export e configurazione
  - redirect post-azione sulla pagina corrente con focus preservato per prenotazioni e accoglienza
  - selezione organizzazione/evento/data/turno
  - gestione organizzazioni e utenti
  - gestione eventi e date
  - riepilogo prenotazioni/persone/tavoli/posti
  - inserimento prenotazione
  - modifica e annullamento prenotazione
  - aggiunta tavoli
  - creazione turno
  - modifica turni e tavoli
  - assegnazione automatica
  - assegnazione manuale
  - lista accoglienza
  - ricerca e filtro stato
  - stato presente/annullato
  - stato non presentato
  - permessi operativi per ruoli admin/prenotazioni/accoglienza
  - protezione POST con verifica organizzazione/entita
  - conferma server-side per ricalcolo assegnazioni
  - evidenza prenotazioni non assegnate
  - export Excel/PDF configurabile
  - audit log consultabile con login riusciti/falliti
- Servizio di assegnazione tavoli testabile.
- Password hash PBKDF2.

## Verifiche

- `dotnet build GestionePrenotazioni.slnx --no-restore`: OK
- `dotnet test GestionePrenotazioni.slnx --no-restore`: OK, 21 test superati
- `docker compose config`: OK, configurazione valida; presenti warning sandbox su `~/.docker/config.json`.
- `docker build -t gestione-prenotazioni .`: bloccato dalla sandbox su `~/.docker/buildx/instances`; richiesta fuori sandbox respinta dal sistema per limite di utilizzo.
- `dotnet run --no-build --urls http://127.0.0.1:5127`: avviato localmente e verificato su `/Login` con HTTP 200.

## Da fare dopo

- Sostituire persistenza JSON con SQLite/PostgreSQL se serve concorrenza multi-operatore robusta.
- Rendere l'export Excel un vero `.xlsx` con libreria dedicata, se richiesto.
- Test end-to-end browser.
- Workflow GitHub e deploy Docker.
