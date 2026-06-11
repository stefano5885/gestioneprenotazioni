# Gestione Prenotazioni

Applicazione web per gestire prenotazioni tavoli di fiere e sagre.

## Stack

- ASP.NET Core 10
- Razor Pages
- C#
- Docker
- Test xUnit

## Avvio sviluppo

```powershell
dotnet restore
dotnet run --project GestionePrenotazioni.Web --urls http://localhost:5088
```

Apri `http://localhost:5088`.

Credenziali demo locali:

- Utente: `admin`
- Password: `admin`
- Utente: `prenotazioni`
- Password: `prenotazioni`
- Utente: `accoglienza`
- Password: `accoglienza`

## Funzioni disponibili nello scaffolding

- Login con cookie authentication.
- Dashboard mobile-first.
- Pagine separate per panoramica, prenotazioni, accoglienza, assegnazioni tavoli, export e configurazione.
- Focus preservato dopo inserimento prenotazioni e azioni di accoglienza.
- Dati demo in memoria.
- Persistenza locale JSON in `App_Data`.
- Gerarchia organizzazione, evento, data, turno.
- Gestione organizzazioni, utenti, eventi, date, turni e tavoli.
- Inserimento prenotazioni.
- Modifica e annullamento prenotazioni.
- Aggiunta rapida tavoli.
- Creazione rapida turni.
- Assegnazione automatica tavoli con conferma prima del ricalcolo.
- Assegnazione manuale prenotazione/tavoli.
- Accoglienza con ricerca, filtri, presente e non presentato.
- Export Excel/PDF con colonne e ordine configurabili.
- Audit log persistente e consultabile.
- Test unitari sulle regole principali di assegnazione.

## Build Docker

```powershell
docker build -t gestione-prenotazioni .
docker run --rm -p 8080:8080 gestione-prenotazioni
```

Oppure con Compose:

```powershell
docker compose up --build
```

Poi apri `http://localhost:5088`.

## Pubblicazione GHCR e Traefik

Il repository include la workflow GitHub Actions `.github/workflows/docker-publish.yml`.
Ogni push su `main` pubblica l'immagine:

```text
ghcr.io/stefano5885/gestioneprenotazioni:latest
```

Per il server con Traefik usa:

```powershell
docker compose -f docker-compose.traefik.yml up -d
```

Il compose espone l'app su:

```text
https://prenotazioni.prolocovillanovadelghebbo.it
```

I dati applicativi e le chiavi cookie vengono salvati nel volume Docker
`prenotazioni-data`, montato in `/app/App_Data`.

## Documenti

- [PRD](PRD.md)
- [Architettura](ARCHITECTURE.md)
