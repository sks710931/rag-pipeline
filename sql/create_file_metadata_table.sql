-- Re-creating file_metadata with refined canonical fields
IF OBJECT_ID('dbo.file_metadata', 'U') IS NOT NULL DROP TABLE dbo.file_metadata;

CREATE TABLE file_metadata (
    BinaryHash CHAR(64) PRIMARY KEY, -- SHA-256 fingerprint
    Extension NVARCHAR(20) NOT NULL,
    DetectedMimeType NVARCHAR(100) NOT NULL,
    OriginalMimeTypeSource NVARCHAR(50) NOT NULL, -- extension, sniffed, parser
    FileSize BIGINT NOT NULL,
    IsEncrypted BIT DEFAULT 0,
    IsTextBased BIT,
    PageCount INT,
    ContentHashNormalized CHAR(64), -- Level 2 dedupe
    ParserHint NVARCHAR(50),
    FirstUploadId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedByAdmissionVersion NVARCHAR(20) DEFAULT '1.0',
    
    CONSTRAINT FK_FileMetadata_Uploads FOREIGN KEY (FirstUploadId) REFERENCES uploads(UploadId)
);

CREATE INDEX IX_FileMetadata_NormalizedHash ON file_metadata(ContentHashNormalized);
