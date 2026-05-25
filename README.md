## 1. Overview

This application is designed to make **cold outreach to recruiters fast, consistent and AI‑assisted**.

Instead of manually crafting similar emails for every role, you:

- Enter **company**, **role** and optionally **job description** once.
- Let the system generate **resume improvement suggestions** and highlight useful keywords.
- With a single action, send **personalised emails to multiple recruiters** using a configurable template.

The focus is on:

- **Speed** – one workflow to reach many recruiters at once.
- **Consistency** – standardised, configurable templates so your message is always professional.
- **AI support** – suggestions to refine your resume based on the JD before you apply.

The solution has two main parts you run together:

- A backend service that prepares suggestions and sends the emails.
- A browser‑based UI that guides you through the flow step by step.

Everything lives in one repository so you can track the complete application (API + UI) from a single place.

---

## 2. Modules and their description

- **RecruiterOutreach.Core**  
  The **brain** of the system.
  - Holds the rules for how outreach runs, how templates are applied and how suggestions are produced.
  - Shared by any frontend (web, future desktop/mobile) so behaviour stays consistent everywhere.

- **RecruiterOutreach.Api**  
  The **backend service** that exposes the core features over HTTP.
  - What it does:
    - Accepts requests from the UI.
    - Coordinates resume suggestions and email sending.
    - Applies your configuration (recruiter list, templates, SMTP settings, resume location).
  - Main endpoints:
    - `POST /api/outreach/suggestions`
      - Given a job description, returns structured suggestions and keywords to help you tune your resume.
    - `POST /api/outreach/send`
      - Triggers the actual outreach run: builds emails from your templates and sends them to the configured recruiters.
  - Provides a **Swagger UI** in development so you can explore and try these endpoints from the browser.

- **recruiter-outreach-ui**  
  The **web application** basically the user interface to interact with.
  - What it does:
    - Provides an interactive UI to enter company, role and job description.
    - Calls the API to fetch AI‑style suggestions and shows them in a friendly way.
    - Lets you trigger sending emails with a single action once you are happy with the content.
  - Built as a modern React single‑page application and designed to be easy to use on a laptop during job search sessions.

---

## 3. Tech stacks and AI used

- **Backend**
  - .NET 8, C#.
  - ASP.NET Core minimal API (`RecruiterOutreach.Api`).
  - Configuration via `appsettings.json` bound to `OutreachSettings`.
  - Logging via `Microsoft.Extensions.Logging`.
  - SMTP email sending using `System.Net.Mail` through `SmtpEmailSender`.

- **Frontend**
  - React (Create React App).
  - TypeScript.
  - Local dev server proxied to the API via the `proxy` setting in `package.json`.

- **AI / personalization**
  - `IGeminiPersonalizationService` and `GeminiPersonalizationService` encapsulate AI‑driven resume suggestions.
  - Intended to integrate with **Google Gemini API** using an API key supplied via environment variable (e.g. `GEMINI_API_KEY`).
  - Currently the API surface is ready; you can plug in real HTTP calls when you are ready.

---

## 4. Prerequisites to start the app

- **Common**
  - Git clone of this repository.

- **Backend (RecruiterOutreach.Api)**
  - [.NET 8 SDK](https://dotnet.microsoft.com/download).
  - SMTP account (e.g. Gmail) and an **app password**.
  - Configuration in `appsettings.json` (or user secrets / environment variables):
    - `OutreachSettings.Recruiters` – list of recruiter email addresses.
    - `OutreachSettings.EmailTemplate.SubjectTemplate` / `BodyTemplate` – can contain `{Company}` and `{Role}` placeholders.
    - `OutreachSettings.Resume.DefaultAttachmentPath` – absolute path to your resume file **outside the repo**.
    - `OutreachSettings.SmtpSettings` – `Host`, `Port`, `UseSsl`, `UserName`, `FromAddress`.
    - `OutreachSettings.Gemini` – `Endpoint`, `Model`, `ApiKey` (placeholder if not yet integrated).
  - Environment variables:
    - `GMAIL_APP_PASSWORD` – SMTP app password for the configured account.
    - Optionally `GEMINI_API_KEY` – for AI integration.

- **Frontend (recruiter-outreach-ui)**
  - Node.js (LTS) + npm.
  - The `proxy` in `recruiter-outreach-ui/package.json` is set to `https://localhost:7200`, which matches the default HTTPS URL of `RecruiterOutreach.Api` when run from Visual Studio.

Secrets such as SMTP app passwords and API keys **must not be committed** to source control. Use environment variables, user secrets or local configuration files excluded via `.gitignore`.

---

## 5. How to start the app

### 5.1 Start the API

- **From Visual Studio**
  - Open `AutomateJobSearch.sln`.
  - Set `RecruiterOutreach.Api` as the startup project.
  - Run with **F5** or **Ctrl+F5**.
  - In Development, the API will start on something like `https://localhost:7200`.

- **From command line**
  - Navigate to the API project folder:
    ```bash
    cd RecruiterOutreach.Api
    dotnet run
    ```

### 5.2 Check Swagger for the API

- With the API running in Development, open a browser and navigate to:
  - `https://localhost:7200/swagger`
- You should see the automatically generated **Swagger UI** for:
  - `POST /api/outreach/suggestions`
  - `POST /api/outreach/send`

From Swagger you can test endpoints directly by supplying the request payloads.

### 5.3 Start the React UI

- In a separate terminal:
  ```bash
  cd recruiter-outreach-ui
  npm install
  npm start
  ```
- This starts the CRA development server on `http://localhost:3000`.
- Because `package.json` defines a `proxy` to `https://localhost:7200`, calls from the UI to `/api/...` are forwarded to the API.

**End‑to‑end flow:**

- Start **API** (`RecruiterOutreach.Api`).
- Start **UI** (`recruiter-outreach-ui`).
- Open `http://localhost:3000` in a browser and use the app to:
  - Enter company, role and JD.
  - Generate suggestions via `/api/outreach/suggestions`.
  - Trigger sending emails via `/api/outreach/send` when ready.

---

## 6. Future

- **Stronger Gemini integration**
  - Replace placeholder logic with real calls to Gemini.
  - Provide richer suggestions (rewritten bullets, tailored email bodies, etc.).

- **More template types and personalization**
  - Multiple named templates (HR, referral, networking, follow‑up).
  - Additional placeholders beyond `{Company}` and `{Role}`.

- **Profile and configuration UI**
  - Manage recruiter lists and templates directly from the UI instead of editing JSON.

- **Deployment pipelines**
  - CI/CD for API and UI (e.g. Azure, Vercel/Netlify).

---

## 7. Demo

- **Local demo**
  - Follow the steps in **5. How to start the app**.
  - Use Swagger (`/swagger`) to manually exercise the endpoints.
  - Use the React UI to run through the full flow.

- **Hosted demo (future)**
  - Once deployed, document the public API base URL and UI URL here.

This README reflects the current architecture: a shared Core library, a .NET API (`RecruiterOutreach.Api`) and a React UI (`recruiter-outreach-ui`) co‑located in a single repository.
– Direct` (current HR template).
  - `Friend Referral` (future template).
  - Other contexts (networking, recruiter follow-up, etc.).

- Backend config change:
  - Replace single `EmailTemplate` with an array `EmailTemplates` in config.
  - Each template has: `Key`, `DisplayName`, `SubjectTemplate`, `BodyTemplate`.
  - UI sends `templateKey` to the API; API selects the right template from `OutreachSettings`.