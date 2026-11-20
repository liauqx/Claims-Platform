# Claims Portal

Angular 19 standalone frontend for the Claims Platform API.

## Run locally

1. Start the backend at `http://localhost:5266` and configure its database and JWT secrets as described in `backend`.
2. From this folder, install dependencies and start Angular:

   ```powershell
   npm install
   npm start
   ```

3. Open `http://localhost:4200`.

The API base URL is set in `src/environments/environment.ts`. The backend development CORS policy allows the Angular dev server on `localhost:4200` and `127.0.0.1:4200`.

## Build

```powershell
npm run build
```

## Included pages

- Sign in and claimant registration
- Role-aware claim list, search, status filtering, and manager assignment
- New claim and claim assessment forms
- Claim details with status history, document upload/download, and information requests
- Notification inbox and read state
