# RAG Pipeline Architecture

This document provides a detailed overview of the system architecture, components, and data flows built for the RAG (Retrieval-Augmented Generation) Pipeline project.

---

## 1. System Overview
The RAG Pipeline is a resilient, full-stack application designed to ingest, process, and vectorize documents. It features a modern React frontend, a modular FastAPI backend, and an independent asynchronous worker service for background file processing.

---

## 2. Authentication & Security
The system uses a centralized identity provider for Single Sign-On (SSO).

- **Provider**: Azure-hosted OIDC Authority.
- **Protocol**: OAuth2 with Authorization Code Flow + PKCE.
- **Frontend Integration**: Managed via `oidc-client-ts` and a custom React `AuthContext`. It handles login, logout, and automatic silent token renewal.
- **Backend Security**: 
    - Token validation using **JWKS** (JSON Web Key Sets) fetched from the authority.
    - Verification of `aud` (audience), `iss` (issuer), and signatures (RS256).
    - Custom FastAPI dependency `get_current_user` to secure routes.

---

## 3. Frontend Architecture
Built with **React 19**, **Vite 8**, **TypeScript**, and **Material UI (MUI) v9**.

### Core Modules
- **Layout System**: 
    - `MainLayout`: Orchestrates the overall page structure.
    - `Header`: Contains navigation, brand identity, and the user profile menu.
    - `Sidebar`: A persistent, toggleable (mini/full) drawer for navigation, visible only to authenticated users.
- **Contexts**:
    - `AuthContext`: Manages OIDC sessions and user claims.
    - `ThemeContext`: Provides global Light/Dark mode support with local storage persistence.

### Key Pages
- **Home**: Public landing page for unauthorized users.
- **Dashboard**: Authorized-only overview showing pipeline stats and system health.
- **Upload Documents**: Interactive interface for single/multiple/folder uploads with real-time progress bars using XHR.
- **Process Documents**: Tabbed view (Pending/Processed) for monitoring the ingestion status of unique files.

---

## 4. Backend Architecture
Built with **FastAPI** using an **OOP-based Route-Controller-Service** pattern.

### Layered Structure
1. **API Layer (`src/api`)**: Defines REST endpoints and integrates security dependencies.
2. **Controller Layer (`src/controllers`)**: Orchestrates requests, calling multiple services if needed.
3. **Service Layer (`src/services`)**: Contains core business logic (File I/O, JWT validation, DB operations).
4. **Model Layer (`src/models`)**: SQLAlchemy ORM models for SQL Server.
5. **Core Layer (`src/core`)**: Centralized Pydantic configuration and base resilience utilities.

### Resilience Features
- **Error Handling**: A custom `@handle_errors` decorator wraps service methods to ensure consistent logging and HTTP responses.
- **Non-blocking I/O**: Uses `aiofiles` for asynchronous disk operations.
- **Environment Driven**: Fully configurable via `.env` files.

---

## 5. Worker Architecture
The `worker/FileWatcher.py` is a standalone, resilient service that monitors the storage directory.

- **Technology**: Built with `watchfiles` for high-performance OS-level file event detection.
- **Independent Context**: It is decoupled from the backend package to allow independent scaling and execution.
- **Processing Pipeline**:
    1. **Validation**: Checks file extensions against a whitelist (PDF, DOCX, TXT, MD, etc.).
    2. **Fingerprinting**: Generates a **SHA-256 hash** of file content for unique identification.
    3. **Deduplication**: Checks the database to see if the content has already been processed.
    4. **Status Management**: Updates the lifecycle of the document in the database (`Pending` -> `Processing` -> `Processed`/`Duplicate`).

---

## 6. Database Schema (SQL Server)
The system uses **SQL Server** with **Windows Authentication**.

- **`uploads`**: Records every physical file upload, generating a unique GUID for storage (`filename__{GUID}.ext`).
- **`file_metadata`**: Stores metadata for unique file content (Hash, MimeType, Size) to prevent redundant vectorization.
- **`file_ingestions`**: Tracks the status of the RAG ingestion process for each unique document.

---

## 7. Data Ingestion Flow
1. **User Uploads**: Frontend sends file + Bearer token to Backend API.
2. **Backend**: Generates GUID -> Saves to disk -> Creates record in `uploads` table (Status: `Pending`).
3. **Worker**: Detects new file -> Validates format -> Calculates SHA-256 hash.
4. **Deduplication**: 
    - If hash exists: Update upload record (Status: `Duplicate`).
    - If hash is new: Create `file_metadata` record -> Create `file_ingestions` record (Status: `Pending`) -> Update upload record (Status: `Processed`).
5. **Frontend**: The "Process Documents" page polls the API and reflects the status update in the UI.
