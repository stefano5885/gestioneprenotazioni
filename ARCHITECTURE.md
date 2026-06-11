# Stack tecnico

## Scelta

- **Framework:** ASP.NET Core 10
- **UI:** Razor Pages server-side, mobile-first
- **Dominio:** classi C# e servizi applicativi testabili
- **Persistenza MVP:** JSON locale in `App_Data` montabile come volume Docker
- **Database futuro:** SQLite/PostgreSQL, mantenendo il dominio indipendente dal provider
- **Autenticazione:** cookie authentication con utenti e ruoli applicativi
- **Export:** Excel e PDF da implementare come servizi applicativi
- **Deploy:** Docker multi-stage
- **Repository:** GitHub

## Motivazione

Razor Pages e adatto a un gestionale operativo fatto di liste, form, dashboard e azioni rapide da smartphone. Evita una SPA separata, riduce complessita di build e deployment, e mantiene l'app semplice da pubblicare in un container.

La persistenza JSON e sufficiente per un test locale completo e per validare i flussi operativi senza introdurre migrazioni premature. Il modello dati mantiene la capienza dei tavoli configurabile e separa organizzazioni, eventi, date e turni, cosi il passaggio a SQLite/PostgreSQL resta possibile senza riscrivere le regole di dominio.

## Struttura dati principale

- Organization
- ApplicationUser
- Event
- EventDate
- Shift
- DiningTable
- Reservation
- TableAssignment
- AuditLogEntry

## Regole di progetto

- Le regole di assegnazione tavoli vivono in servizi testabili, non nelle pagine Razor.
- Le pagine Razor orchestrano input, validazione e navigazione.
- Ogni query o modifica deve essere filtrata per organizzazione.
- Le modifiche operative devono creare audit log.
- L'interfaccia deve restare usabile da viewport piccoli, con azioni primarie grandi e percorsi brevi.
- Il file dati in `App_Data` non deve essere versionato; in Docker va montato come volume persistente.
