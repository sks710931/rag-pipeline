-- Re-creating uploads table with admission tracking columns
IF OBJECT_ID('dbo.uploads', 'U') IS NOT NULL DROP TABLE dbo.uploads;

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
