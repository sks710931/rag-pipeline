CREATE TABLE uploads (
    UploadId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    OriginalFileName NVARCHAR(512) NOT NULL,
    StoredFileName NVARCHAR(1024) NOT NULL,
    FilePath NVARCHAR(MAX) NOT NULL,
    Status NVARCHAR(50) DEFAULT 'Pending', -- Pending, Processing, Completed, Error
    UploadedBy NVARCHAR(256),
    UploadDate DATETIME2 DEFAULT GETDATE(),
    ProcessedDate DATETIME2
);

CREATE INDEX IX_Uploads_Status ON uploads(Status);
