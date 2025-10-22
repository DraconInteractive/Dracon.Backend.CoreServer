# Core Server

Real-time WebSocket chat server with basic user management via REST and JWT. Supports tagging chat messages with an authenticated user identity by including a token in the message payload. Designed for Azure App Service + Azure SQL, but also runs locally with SQL Server.

## Features
- WebSocket chat hub (broadcast) with optional JSON message format: `{ "text": "hello", "token": "<JWT>" }`
- REST auth endpoints: `POST /auth/register`, `POST /auth/login`
- JWT issuance and validation (Issuer/Audience/Key configurable)
- Azure SQL storage for users; auto-creates schema on first run for new databases
- Targeted system responses so only the requesting client receives action results

## Getting started
### Prerequisites
- .NET 9 SDK
- SQL Server (local) or Azure SQL Database
- Strong JWT signing key (32+ random bytes)

### Clone
```bash
git clone <YOUR_PUBLIC_REPO_URL>.git
cd "Core AI/Core Server"
```

### Configure (local)
Set the following via `appsettings.Development.json` (not committed) or environment variables:
- `ConnectionStrings:Default` (ADO.NET connection string for SQL)
- `Jwt:Issuer`, `Jwt:Audience`, `Jwt:Key`

Local SQL example:
```
Server=localhost;Initial Catalog=CoreServerDb;Integrated Security=True;TrustServerCertificate=True;
```

Azure SQL (SQL auth) example:
```
Server=tcp:YOUR_SERVER.database.windows.net,1433;Initial Catalog=YOUR_DB;User ID=YOUR_USER;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

Azure SQL (Managed Identity / Entra ID) example:
```
Server=tcp:YOUR_SERVER.database.windows.net,1433;Initial Catalog=YOUR_DB;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

### Run
```bash
dotnet run
```
Open `http://localhost:5000/view` to use the simple chat UI.

### REST usage
- Register: `POST /auth/register`
  ```json
  { "displayName": "Alice", "password": "P@ssw0rd!", "email": "user@example.com" }
  ```
- Login: `POST /auth/login`
  ```json
  { "displayName": "Alice", "password": "P@ssw0rd!" }
  ```
- Chat (WebSocket): send `{ "text": "hello", "token": "<JWT>" }` to tag messages with your identity.

## Deploy to Azure App Service (summary)
1. Create/choose an Azure SQL Database and set firewall/permissions.
2. In App Service → Configuration:
   - Connection strings:
     - Name: `Default`, Type: `SQLAzure`, Value: your SQL connection string
   - Application settings:
     - `Jwt__Issuer`, `Jwt__Audience`, `Jwt__Key`
3. If using Managed Identity:
   - Enable System-assigned MI (or attach a UAMI)
   - In the target database (logged in as Entra admin):
     ```sql
     CREATE USER [<app-service-name-or-uami-name>] FROM EXTERNAL PROVIDER;
     ALTER ROLE db_owner ADD MEMBER [<app-service-name-or-uami-name>];
     ```
     After first run (schema created), reduce privileges to least privilege.
4. Deploy (e.g., `dotnet publish` or GitHub Actions).

## Security notes
- Use HTTPS in production; set App Service to HTTPS Only.
- Use a strong `Jwt:Key` and rotate it; consider Azure Key Vault.
- Rate limiting and HTTPS/HSTS middleware samples are present in `Program.cs` (commented out).
- System/action responses sent via WebSocket are targeted to the originating client by default.
