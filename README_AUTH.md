# User Management and Azure SQL Setup

This project includes basic user management via REST API and JWT-based identity tokens that can be included in chat packets. DisplayName is the unique identifier; Email is optional.

## 1) REST API Endpoints
- POST /auth/register
  - Body (JSON):
    { "displayName": "Alice", "password": "P@ssw0rd!", "email": "user@example.com" }
  - Notes: displayName and password are required; email is optional.
  - Response: 201 Created on success.

- POST /auth/login
  - Body (JSON):
    { "displayName": "Alice", "password": "P@ssw0rd!" }
  - Response: 200 OK with
    { "token": "<JWT>", "userId": "<guid>", "email": "user@example.com", "displayName": "Alice" }

Include the returned token in WebSocket chat messages as JSON to tag your messages with identity:
{ "text": "hello world", "token": "<JWT>" }

## 2) Configuration
You can configure via appsettings.json, environment variables, or Azure App Service Configuration.

- ConnectionStrings:Default
  - SQL auth example:
    Server=tcp:YOUR_SERVER.database.windows.net,1433;Initial Catalog=YOUR_DB;User ID=YOUR_USER;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
  - Managed Identity / Entra ID example:
    Server=tcp:YOUR_SERVER.database.windows.net,1433;Initial Catalog=YOUR_DB;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;

- Jwt:
  - Issuer: CoreServer (or your issuer)
  - Audience: CoreServerClients (or your audience)
  - Key: a strong 256-bit secret (minimum 32 random bytes). Provide via environment or Azure App Settings.

### Azure App Service settings
- Connection strings → New connection string:
  - Name: Default (matches GetConnectionString("Default"))
  - Type: SQLAzure
  - Value: your connection string
- Application settings:
  - Jwt__Issuer, Jwt__Audience, Jwt__Key

## 3) Azure SQL Quick Setup
- Create Azure SQL Server and Database.
- If using Managed Identity with App Service:
  1. Enable the App Service Managed Identity (System-assigned or attach a UAMI).
  2. Set an Entra ID admin on the SQL Server.
  3. In the user database (not master), create the user and grant permissions (first run):
     CREATE USER [<app-service-name-or-uami-name>] FROM EXTERNAL PROVIDER;
     ALTER ROLE db_owner ADD MEMBER [<app-service-name-or-uami-name>];
     After the first run (schema created), reduce privileges to least privilege (e.g., db_datareader/db_datawriter).
- If using SQL authentication, ensure your login has access to the database.

## 4) Schema creation (new installs only)
On startup, the app ensures the dbo.Users table and unique index on DisplayName exist. There is no automatic migration of older schemas.

Schema:
CREATE TABLE dbo.Users (
  Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
  DisplayName NVARCHAR(256) NOT NULL,
  Email NVARCHAR(256) NULL,
  PasswordHash VARBINARY(512) NOT NULL,
  PasswordSalt VARBINARY(128) NOT NULL,
  CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX IX_Users_DisplayName ON dbo.Users(DisplayName);

## 5) Security Notes
- Use a long, random Jwt:Key (32+ bytes). Do NOT commit real keys.
- Prefer HTTPS in production (App Service HTTPS Only). Rate limiting and HTTPS enforcement samples are present in Program.cs (commented out).
- System/action responses in the WebSocket hub are targeted to the requesting client to avoid leaking sensitive data.
- Consider account lockouts, password complexity, email verification, and shorter token lifetimes for production.

## 6) Local Testing (examples)
- Register:
  curl -X POST http://localhost:5000/auth/register -H "Content-Type: application/json" -d '{"displayName":"Alice","password":"P@ssw0rd!","email":"user@example.com"}'
- Login:
  curl -X POST http://localhost:5000/auth/login -H "Content-Type: application/json" -d '{"displayName":"Alice","password":"P@ssw0rd!"}'
- WebSocket Chat: send {"text":"hi","token":"<token>"} to tag messages with identity.