# Supported File Types

The RAG Pipeline worker service enforces a whitelist of supported document formats. Files that do not match these extensions are automatically rejected, marked in the database as `Rejected`, and deleted from the storage.

## Document Formats

| Format | Extension | Description |
| :--- | :--- | :--- |
| **Plain Text** | `.txt` | Standard text files |
| **PDF** | `.pdf` | Portable Document Format |
| **Word** | `.doc`, `.docx` | Microsoft Word documents |
| **Markdown** | `.md`, `.markdown` | Markdown formatted text |
| **HTML** | `.html`, `.htm` | Web pages and HTML documents |
| **Rich Text** | `.rtf`, `.odt` | Rich Text and OpenDocument text |

## Structured Data

| Format | Extension | Description |
| :--- | :--- | :--- |
| **CSV** | `.csv` | Comma-separated values |
| **TSV** | `.tsv` | Tab-separated values |
| **JSON** | `.json` | JavaScript Object Notation |

---

## Behavior for Unsupported Files
If a file with an extension not listed above is detected in the `uploads` directory:
1. The system logs a `REJECTED` warning.
2. The database record in the `uploads` table is updated with `Status = 'Rejected'`.
3. **The file is physically deleted** from the disk to maintain storage hygiene.
