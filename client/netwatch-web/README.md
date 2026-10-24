# NetWatch web client

The Angular 19 front end for NetWatch: dashboard, devices, probes and incidents, in
English or Arabic (RTL), with live updates over SignalR.

See the [main README](../../README.md) for the full project.

## Run it

Start the API first (it listens on http://localhost:5080, see
`src/environments/environment.development.ts`), then:

```bash
npm install
npm start        # http://localhost:4200
```

## Test and build

```bash
npm test         # Karma unit tests (19)
npm run build    # production build into dist/
```
