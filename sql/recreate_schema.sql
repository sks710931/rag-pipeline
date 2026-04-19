-- 1. Drop Child Tables First
IF OBJECT_ID('dbo.file_ingestions', 'U') IS NOT NULL DROP TABLE dbo.file_ingestions;
IF OBJECT_ID('dbo.file_metadata', 'U') IS NOT NULL DROP TABLE dbo.file_metadata;
IF OBJECT_ID('dbo.uploads', 'U') IS NOT NULL DROP TABLE dbo.uploads;

-- 2. Create Parent Tables
CREATE TABLE uploads (
    UploadId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    OriginalFileName NVARCHAR(512) NOT NULL,
    StoredFileName NVARCHAR(1024) NOT NULL,
    FilePath NVARCHAR(MAX) NOT NULL,
    Status NVARCHAR(50) DEFAULT 'Pending', 
    -- Admission Details
    BinaryHash CHAR(64),
    DetectedMimeType NVARCHAR(100),
    Extension NVARCHAR(20),
    QuarantinePath NVARCHAR(MAX),
    -- Error Tracking
    FailureCode NVARCHAR(50),
    FailureMessage NVARCHAR(MAX),
    FailureStage NVARCHAR(50),
    -- Audit & Retry
    UploadedBy NVARCHAR(256),
    UploadDate DATETIME2 DEFAULT GETDATE(),
    ProcessedDate DATETIME2,
    RetryCount INT DEFAULT 0,
    LastAttemptAt DATETIME2,
    AdmissionVersion NVARCHAR(20) DEFAULT '1.0'
);

CREATE INDEX IX_Uploads_Status ON uploads(Status);
CREATE INDEX IX_Uploads_BinaryHash ON uploads(BinaryHash);

-- 3. Create Child Tables
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

CREATE TABLE file_ingestions (
    IngestionId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    BinaryHash CHAR(64) NOT NULL,
    Status NVARCHAR(50) DEFAULT 'Pending', 
    Stage NVARCHAR(50),
    ErrorMessage NVARCHAR(MAX),
    -- Worker Info
    WorkerId NVARCHAR(100),
    AttemptCount INT DEFAULT 0,
    -- Logic Versions
    PreprocessingVersion NVARCHAR(20),
    ChunkingVersion NVARCHAR(20),
    EmbeddingModel NVARCHAR(100),
    EmbeddingVersion NVARCHAR(20),
    -- Timestamps
    StartTime DATETIME2,
    EndTime DATETIME2,
    CreatedAt DATETIME2 DEFAULT GETDATE(),

    CONSTRAINT FK_FileIngestions_Metadata FOREIGN KEY (BinaryHash) REFERENCES file_metadata(BinaryHash)
);

CREATE INDEX IX_FileIngestions_Status ON file_ingestions(Status);
CREATE INDEX IX_FileIngestions_Stage ON file_ingestions(Stage);
