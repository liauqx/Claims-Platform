# Claims Platform

A claims management application with an Angular frontend, an ASP.NET Core REST API, and PostgreSQL persistence.

## Features

- Claimant registration and sign-in using password hashing and JWT authentication.
- Role-aware claim submission, tracking, editing, and deletion.
- Officer claim pickup and assessment, with manager assignment and oversight.
- Claim status history and information-request/response workflows.
- Claim document upload, download, and deletion.
- In-app notifications for claim and information-request events.

The API uses REST for synchronous application workflows. PostgreSQL stores claims, history, documents metadata, and notifications together. Kafka is not currently used.

## Requirements

- .NET 10 SDK
- Node.js compatible with Angular 19 and npm
- PostgreSQL

## Configure the backend

The default PostgreSQL host, port, and database name are in [`backend/appsettings.json`](./backend/appsettings.json). Update them if your PostgreSQL server uses a different address.

Keep database credentials and the JWT signing key out of source control. The backend project has .NET User Secrets enabled:

```powershell
dotnet user-secrets set "ConnectionStrings:ClaimsDatabase" "Host=YOUR_HOST;Port=YOUR_PORT;Database=ClaimDB;Username=YOUR_USERNAME;Password=YOUR_PASSWORD" --project .\backend\backend.csproj
dotnet user-secrets set "Jwt:Key" "REPLACE_WITH_A_RANDOM_SECRET_AT_LEAST_32_UTF8_BYTES" --project .\backend\backend.csproj
```

Replace the example username, password, and signing key before running the application. User Secrets override the checked-in connection string in development. Environment variables can also be used: `ConnectionStrings__ClaimsDatabase` and `Jwt__Key`.

The PostgreSQL database must exist before applying migrations. For example, create `ClaimDB` in your database administration tool if it does not already exist. PostgreSQL database names are case-sensitive when created with quoted mixed-case names; use the exact same name in the connection string.

## Apply database migrations

Install the EF Core CLI once if it is not already available:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.0
```

From the repository root, apply the schema:

```powershell
dotnet ef database update --project .\backend\backend.csproj --startup-project .\backend\backend.csproj
```

The initial migration creates the users, claims, documents, history, information requests, and notifications tables.

## Run locally

Start the backend from the repository root:

```powershell
dotnet run --project .\backend\backend.csproj
```

The HTTP development profile listens at `http://localhost:5266`. OpenAPI is available in development at `http://localhost:5266/openapi/v1.json`.

In another terminal, start the Angular app:

```powershell
Set-Location .\frontend
npm install
npm start
```

Open `http://localhost:4200`. The Angular API base URL is configured in [`frontend/src/environments/environment.ts`](./frontend/src/environments/environment.ts). The backend development CORS policy allows `localhost:4200` and `127.0.0.1:4200`.

## Roles

- **Claimant:** Public registration creates claimant accounts. Claimants submit claims, update submitted claims, provide requested information, and manage eligible documents.
- **Officer:** Officers are provisioned separately. They can pick up claims, assess assigned claims, and request additional information.
- **Manager:** Managers are provisioned separately. They can view all claims, assign officers, assess claims, and receive new-claim notifications.

Authenticated API calls use `Authorization: Bearer <access-token>`. New public registrations cannot choose an officer or manager role.

## API overview

| Area | Routes |
| --- | --- |
| Authentication | `POST /api/auth/register`, `POST /api/auth/login` |
| Claims | `GET /api/claims`, `POST /api/claims`, `GET /api/claims/{id}`, `PATCH /api/claims/{id}`, `DELETE /api/claims/{id}` |
| Assignment | `POST /api/claims/{id}/assign-to-me`, `PUT /api/claims/{id}/assignment`, `GET /api/users/officers` (managers) |
| History | `GET /api/claims/{claimId}/history` |
| Information requests | `GET/POST /api/claims/{claimId}/information-requests`, plus `POST .../{requestId}/respond` and `POST .../{requestId}/cancel` |
| Documents | `GET/POST /api/claims/{claimId}/documents`, `GET/DELETE .../{documentId}`, `GET .../{documentId}/content` |
| Notifications | `GET /api/notifications`, `GET /api/notifications/unread-count`, `PATCH /api/notifications/{id}/read`, `PATCH /api/notifications/read-all` |

Claim listing and notification listing support pagination. Claims can only transition through the status flow enforced by the API, and changes are recorded in claim history.

## Document storage

Uploaded document files are stored locally under `backend\App_Data\claim-documents` (ignored by Git). Uploads currently accept PDF, PNG, and JPEG files up to 10 MB. Production deployments should use persistent shared storage and configure production CORS origins.

## Build

From the repository root:

```powershell
dotnet build .\backend\backend.csproj
Set-Location .\frontend
npm run build
```
