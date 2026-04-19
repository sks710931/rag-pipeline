# RAG Pipeline Architecture

This document provides a detailed overview of the system architecture, components, and data flows for the RAG (Retrieval-Augmented Generation) Pipeline project.

---

## 1. System Overview
The RAG Pipeline is a resilient, full-stack application designed to ingest, process, and vectorize documents. It features a modern React frontend, a modular FastAPI backend, and an independent asynchronous worker service for production-grade document admission and preprocessing.

---

## 2. Authentication & Security
The system uses a centralized identity provider for Single Sign-On (SSO).

- **Provider**: Azure-hosted OIDC Authority.
- **Protocol**: OAuth2 with Authorization Code Flow + PKCE.
- **Frontend Integration**: Managed via `oidc-client-ts` and a custom React `AuthContext`. It handles login, logout, and automatic silent token renewal.
- **Backend Security**: 
    - Token validation using **JWKS** (JSON Web Key Sets) fetched from the authority.
    - Verification of `aud` (audience: `api://rag-pipeline`), `iss` (issuer), and signatures (RS256).
    - Custom FastAPI dependency `get_current_user` to secure routes.

---

## 3. Frontend Architecture
Built with **React 19**, **Vite 8**, **TypeScript**, and **Material UI (MUI) v9**.

### Core Modules
- **Layout System**: 
    - `MainLayout`: Orchestrates the overall page structure with a **fixed AppBar** and **fluid (full-width) containers**.
    - `Header`: Contains navigation, brand identity, **ThemeToggle** (Light/Dark mode), and a **UserProfile** menu with Gravatar integration.
    - `Sidebar`: A persistent, toggleable (mini/full) drawer for navigation, visible only to authenticated users, with active-route highlighting.
- **Contexts**:
    - `AuthContext`: Manages OIDC sessions and user claims.
    - `ThemeContext`: Provides global Light/Dark mode support with local storage persistence.

### Key Pages
- **Home**: Public landing page for guests.
- **Dashboard**: Authorized-only overview showing pipeline stats, recent activity, and system health.
- **Upload Documents**: Advanced interface for single/multiple/folder uploads with real-time XHR progress tracking.
- **Process Documents**: Tabbed view (**Pending** vs **Processed**) for monitoring the multi-stage ingestion lifecycle.
- **Account**: Displays detailed user profile information and OIDC token claims.

---

## 4. Backend Architecture
Built with **FastAPI** using an **OOP-based Route-Controller-Service** pattern.

### Layered Structure
1. **API Layer (`src/api`)**: Defines REST endpoints and integrates security dependencies.
2. **Controller Layer (`src/controllers`)**: Orchestrates requests (e.g., `UploadController`, `IngestionController`).
3. **Service Layer (`src/services`)**: Contains core business logic:
    - `FileService`: Handles atomic file saves (write to `.tmp` -> atomic `os.rename`).
    - `AuthService`: Manages OIDC discovery and JWT validation.
4. **Model Layer (`src/models`)**: SQLAlchemy ORM models for SQL Server.
5. **Core Layer (`src/core`)**: Centralized Pydantic configuration and base resilience utilities (e.g., `@handle_errors` decorator).

---

## 5. Worker Architecture (Admission Layer)
The `worker/FileWatcher.py` is a standalone, resilient service that monitors the storage directory using OS-level events via `watchfiles`.

### Processing Pipeline
1. **Stability**: Ignores `.tmp` files; only processes files after the atomic rename to ensure they are fully written.
2. **Validation**: 
    - **Whitelisting**: Only allows supported document formats (PDF, DOCX, TXT, MD, etc.).
    - **Content Sniffing**: Uses the `filetype` library to verify MIME types by signature, preventing extension spoofing.
    - **Structural Checks**: Basic PDF validation (encryption/corruption) using `pypdf`.
3. **Quarantine**: Rejected files are moved to `uploads/quarantine/` instead of being deleted, with failure reasons stored in the DB.
4. **Fingerprinting**: Generates a **SHA-256 BinaryHash** for unique content identification.
5. **Deduplication**: 
    - If `BinaryHash` exists: Marks upload as `DuplicateBinary`.
    - If new: Creates canonical `file_metadata` and enqueues work in `file_ingestions`.

---

## 6. Database Schema (SQL Server)
The system uses **SQL Server** with **Windows Authentication**.

### Core Tables
- **`uploads`**: Tracks every physical upload attempt. Includes detailed admission states (`Validating`, `Accepted`, `Rejected`, `DuplicateBinary`).
- **`file_metadata`**: The canonical record for unique content. Stores `BinaryHash`, MIME type, file size, page counts, and encryption status.
- **`file_ingestions`**: Tracks the searchability lifecycle (Stages: `AdmissionComplete`, `Preprocessing`, `Parsed`, `Chunking`, `Embedding`, `Indexed`).

---

## 7. Data Lifecycle
1. **Upload**: User uploads file -> Backend saves as `.tmp` -> Renames to `filename__{GUID}.ext` -> DB record `Pending`.
2. **Admission**: Worker detects rename -> Validates content -> Computes `BinaryHash`.
3. **Identity**: 
    - Duplicate content found -> Upload marked `DuplicateBinary`.
    - New content found -> `file_metadata` created -> `file_ingestions` entry added with status `Pending`.
4. **Ingestion (Next Stage)**: Downstream workers will pick up `Pending` ingestions for parsing and vectorization.
