# Todo frontend (Angular)

See the [root README](../README.md) for architecture, setup and the API reference.

```powershell
npm install
npm start                      # http://localhost:4200  (expects the API on http://localhost:5080)
npm test -- --no-watch         # Vitest unit tests
npm run build                  # production bundle in dist/todo-app
node scripts/check-contrast.mjs  # WCAG AA check for every theme
```

The API base URL comes from `src/environments/environment.development.ts` (dev) and `environment.ts` (production, `/api`).
