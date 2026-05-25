# Recruiter Outreach Automation

A .NET-based automation tool that streamlines bulk recruiter outreach and AI-assisted resume tailoring. The goal is to have one reusable core that can power:

- A console/CLI tool (current implementation).
- A web API + React UI (next step).
- Future mobile/desktop frontends (e.g., .NET MAUI).

---

## 1. What the app does (current console version)

### 1.1 Config-driven bulk email outreach

- Uses `appsettings.json` to configure:
  - **Recruiters**: list of email addresses to contact.
  - **Email templates**:
    - `SubjectTemplate` with placeholders like `{Company}` and `{Role}`.
    - `BodyTemplate` with `{Company}` / `{Role}` and proper line breaks (`\n`).
  - **Resume**:
    - `DefaultAttachmentPath` – path to a PDF file to attach to emails.
  - **SMTP (Gmail)**:
    - `Host`, `Port`, `UseSsl`, `UserName`, `FromAddress`.
  - **Gemini**:
    - API `Endpoint`, `Model`, `ApiKey` placeholder for future real integration.

- At runtime, the console app:
  1. Prompts for **Company** and **Role**.
  2. Prompts for an optional **Job Description (JD)** (multi-line; end with an empty line).

### 1.2 Two modes based on JD

- **Send mode (no JD)**
  - If JD is left empty:
    - Builds subject/body from the selected template, substituting `{Company}` and `{Role}`.
    - Loops over all configured recruiter emails.
    - Sends emails via Gmail SMTP using:
      - Username from config.
      - App password from `GMAIL_APP_PASSWORD` environment variable.
    - Attaches the resume from `Resume.DefaultAttachmentPath`.

- **Suggestions mode (JD provided)**
  - If JD text is provided:
    - Calls a `GeminiPersonalizationService` placeholder that:
      - Extracts naive keywords from the JD.
      - Builds a suggestions text block including:
        - Keywords to emphasize.
        - JD excerpt that drove those suggestions.
    - Writes a `ResumeSuggestions_{Company}_{Role}_timestamp.txt` file in a `Suggestions` folder next to the default resume PDF.
    - Logs the path of the suggestions file.
    - **Does not send any emails** in this mode.

### 1.3 Secrets handling

- **Gmail App Password**: `GMAIL_APP_PASSWORD` environment variable.
- **Gemini API key**: `GEMINI_API_KEY` environment variable (ready for real HTTP calls later).
- When running under Visual Studio, these are set via `Properties/launchSettings.json` and **should not be committed** to source control.

### 1.4 Logging and error handling

- Uses `Microsoft.Extensions.Logging.Console` for structured logs.
- Logs:
  - Startup and mode (JD/no JD).
  - SMTP warnings (missing attachment, missing password env var).
  - Errors per recipient on send failures.
  - Suggestions file path for JD mode.

---

## 2. Target architecture (Core + API + React UI)

To make the app accessible from any device via a browser and prepare for future frontends, the target architecture is:

### 2.1 Projects

- **RecruiterOutreach.Core** (Class Library)
  - Shared domain + services:
    - Config models: `OutreachSettings`, `EmailTemplateSettings`, `ResumeSettings`, `SmtpSettings`, `GeminiSettings`.
    - Services: `OutreachService` (or split into `OutreachSuggestionsService` / `OutreachSendService`).
    - Email abstraction: `IEmailSender`, `SmtpEmailSender`.
    - Gemini abstraction: `IGeminiPersonalizationService`, `GeminiPersonalizationService`.
  - No UI, no console, no web-specific code.

- **RecruiterOutreach.Api** (ASP.NET Core Web API, planned)
  - References `RecruiterOutreach.Core`.
  - Exposes HTTP endpoints for the UI:
    - `POST /api/outreach/suggestions` – JD in, suggestions out.
    - `POST /api/outreach/send` – details + optional resume upload, performs SMTP send.
  - Binds `OutreachSettings` from `appsettings.json` and registers all Core services with DI.

- **RecruiterOutreach.Web UI (React)** (planned)
  - React SPA (e.g., Vite or CRA) calling the API.
  - Runs in the browser on any device.

The current console project can be gradually retired once the API + React UI are fully in place, but it remains a useful CLI front-end for now.

---

## 3. Planned web UI features

### 3.1 Template selection (dropdown)

- UI will have a **dropdown** listing different email templates, e.g.:
  - `Company HR – Direct` (current HR template).
  - `Friend Referral` (future template).
  - Other contexts (networking, recruiter follow-up, etc.).

- Backend config change:
  - Replace single `EmailTemplate` with an array `EmailTemplates` in config.
  - Each template has: `Key`, `DisplayName`, `SubjectTemplate`, `BodyTemplate`.
  - UI sends `templateKey` to the API; API selects the right template from `OutreachSettings`.

### 3.2 Resume upload (override default attachment)

- UI adds a **file upload** control that accepts `.pdf`, `.docx`, `.doc`.
- On send:
  - If a file is uploaded:
    - API saves it to a temp location per request.
    - Passes that path as the attachment to `IEmailSender`.
  - If no file is uploaded:
    - Falls back to `Resume.DefaultAttachmentPath` from config.

This supports the flow:
- Run JD suggestions → manually edit your resume locally → upload the updated resume → send emails with the updated file.

### 3.3 JD-based suggestions in the browser

- The React UI will:
  - Send JD + fields to `POST /api/outreach/suggestions`.
  - Display structured suggestions:
    - Keywords to add / remove.
    - JD excerpt.
    - Possibly example lines/bullets for the resume.
- No emails are sent in this flow.

---

## 4. Tech stack

- **Runtime / language**:
  - .NET 8
  - C# 12

- **Backend infrastructure**:
  - `Microsoft.Extensions.Hosting` / Generic Host (console; planned API as well).
  - `Microsoft.Extensions.Configuration` + `appsettings.json`.
  - `Microsoft.Extensions.Logging.Console`.
  - `System.Net.Mail` for SMTP.

- **AI integration (planned)**:
  - Google Gemini API via `HttpClient` and JSON.
  - API key via `GEMINI_API_KEY` environment variable.

- **Frontend (planned)**:
  - React (SPA) – UI for:
    - Template selection.
    - JD input.
    - Resume upload.
    - Triggering suggestions and send.

---

## 5. High-level flow (future API + React)

```text
[Browser: React UI]
    |
    | 1) User fills form (Template, Company, Role, JD, optional resume file)
    |
    v
[Backend: ASP.NET Core API]
    - Validates input
    - Binds OutreachSettings from config
    - Uses DI to resolve OutreachService, IEmailSender, IGeminiPersonalizationService

  A) Suggestions Mode (JD present, user clicks "Generate Suggestions")
      -> POST /api/outreach/suggestions
      -> GeminiPersonalizationService analyzes JD
      -> Returns keywords + suggested lines + JD excerpt
      -> React UI displays suggestions; user edits resume/email manually

  B) Send Mode (JD empty, or user clicks "Send Emails")
      -> POST /api/outreach/send (with optional resume upload)
      -> OutreachService builds subject/body from selected template
      -> Uses IEmailSender (SmtpEmailSender) with:
           - Default resume PDF or uploaded file
           - Gmail SMTP (App Password via env var)
      -> Responds with list of recipients + status
```

---

## 6. Start points

- **Console app (current)**
  - Entry: `Program.cs` in `RecruiterOutreachConsole`.
  - Use when running locally from CLI or Visual Studio.

- **Core library (shared logic)**
  - Namespace: `RecruiterOutreach.*` under `RecruiterOutreach.Core`.
  - Contains all business logic, reusable across console / web / mobile.

- **Web API (planned)**
  - Entry: `Program.cs` in `RecruiterOutreach.Api`.
  - Exposes REST endpoints for React UI.

- **React UI (planned)**
  - Entry: `src/main.tsx` / `src/index.tsx` (depending on the chosen tool).
  - Runs in any modern browser.

---

## 7. Future extensions

- **Real Gemini integration**
  - Replace placeholder `GeminiPersonalizationService` with real HTTP calls.
  - More nuanced suggestions: full resume bullets, tailored email bodies.

- **More template types**
  - Referral, networking, follow-up, cold outbound.
  - Additional form fields (FriendName, RecruiterName, etc.) per template.

- **Multi-tenant / multi-profile support**
  - Store multiple recruiter lists and resume paths.
  - Switch between profiles from the UI.

- **Mobile/desktop app**
  - Use the same Core + API with:
    - .NET MAUI app for Android/iOS/Windows.
    - Or an Electron/Tauri desktop app wrapping the web UI.

- **Job site helpers (manual assist, not full automation)**
  - Tools that take pasted job descriptions or messages from LinkedIn/Naukri and:
    - Generate tailored replies.
    - Suggest profile summary updates.
  - You paste content into the app, it generates responses you paste back manually, avoiding ToS violations.

---

This README should give any developer a clear picture of what the app does, how it’s structured, and where it’s headed (Core + API + React). As we implement the API and React UI, we can update this document with concrete endpoint definitions and screenshots of the UI.
