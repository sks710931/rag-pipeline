CREATE TABLE file_metadata (
    ContentHash CHAR(64) PRIMARY KEY, -- SHA-256 fingerprint
    MimeType NVARCHAR(100) NOT NULL,
    FileSize BIGINT NOT NULL,
    FirstUploadId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    
    CONSTRAINT FK_FileMetadata_Uploads FOREIGN KEY (FirstUploadId) REFERENCES uploads(UploadId)
);
