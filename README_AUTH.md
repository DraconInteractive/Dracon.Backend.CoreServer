User Management and Azure SQL Setup

This project now includes basic user management via REST API and JWT-based identity tokens that can be included in chat packets.

Important: DisplayName is now the unique identifier for accounts. Email is optional.

1) REST API Endpoints
- POST /auth/register
  Body (JSON): { "displayName": "Alice", "password": "P@ssw0rd!", "email": "user@example.com" }
  Notes: displayName and password are required; email is optional.
  Response: 201 Created on success.

- POST /auth/login
  Body (JSON): { "displayName": "Alice", "password": "P@ssw0rd!" }
  Response: 200 OK with { token, userId, email, displayName }

Include the returned token in WebSocket chat messages as JSON:
{ "text": "hello world", "token": "<JWT token>" }
The server will validate the token and tag messages with the authenticated user (displayName or userId) instead of the raw socket id.

2) Configuration
Edit appsettings.json (or provide via environment variables/config blob) and set:

ConnectionStrings:Default
  Example (SQL auth):
  Server=tcp:YOUR_SERVER.database.windows.net,1433;Initial Catalog=YOUR_DB;Persist Security Info=False;User ID=YOUR_USER;Password=YOUR_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;

Jwt:
  Issuer: CoreServer (or your issuer)
  Audience: CoreServerClients (or your audience)
  Key: a strong 256-bit secret (minimum 32 random bytes). Example: generate one and store in a secret store.

3) Azure SQL Quick Setup
- Create Azure SQL Server and Database (Basic/General Purpose is fine).
- Allow your app’s outbound IP or enable Azure services to connect.
- Create a SQL login (or use AAD if you extend the app for it). For SQL login:
  - In Azure Portal → your SQL server → Reset password or create a new login.
- Configure a firewall rule to allow your development machine or hosting environment.
- In the database, no manual schema setup required. On startup, the app ensures table dbo.Users exists.

Table schema created automatically (new installs only):
CREATE TABLE dbo.Users (
  Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
  DisplayName NVARCHAR(256) NOT NULL,
  Email NVARCHAR(256) NULL,
  PasswordHash VARBINARY(512) NOT NULL,
  PasswordSalt VARBINARY(128) NOT NULL,
  CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX IX_Users_DisplayName ON dbo.Users(DisplayName);

4) Security Notes
- Replace Jwt:Key with a long, random secret (at least 32 bytes). Do NOT commit real keys.
- Use Azure Key Vault or environment variables for secrets.
- Use HTTPS in production.
- Consider account lockouts, password complexity, email verification, refresh tokens, and token expiry policies for production.

5) Local Testing (examples)
- Register: curl -X POST http://localhost:5000/auth/register -H "Content-Type: application/json" -d "{\"email\":\"user@example.com\",\"password\":\"P@ssw0rd!\",\"displayName\":\"Alice\"}"
- Login: curl -X POST http://localhost:5000/auth/login -H "Content-Type: application/json" -d "{\"email\":\"user@example.com\",\"password\":\"P@ssw0rd!\"}"
- WebSocket Chat: send {"text":"hi","token":"<token>"} instead of a raw string to tag messages with identity.

6) Deployment Notes
- If using Azure App Service, set the following settings in Configuration:
  - ConnectionStrings__Default
  - Jwt__Issuer, Jwt__Audience, Jwt__Key
- If you use Config from Azure Blob (already supported by the app), you can also store these keys in that JSON file and reference it via BlobServiceUri, ConfigContainerName, ConfigBlobName.
