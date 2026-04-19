CREATE TABLE file_ingestions (
    IngestionId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ContentHash CHAR(64) NOT NULL,
    Status NVARCHAR(50) DEFAULT 'Pending', -- Pending, Ingesting, Completed, Error
    ErrorMessage NVARCHAR(MAX),
    StartTime DATETIME2,
    EndTime DATETIME2,
    CreatedAt DATETIME2 DEFAULT GETDATE(),

    CONSTRAINT FK_FileIngestions_Metadata FOREIGN KEY (ContentHash) REFERENCES file_metadata(ContentHash)
);

CREATE INDEX IX_FileIngestions_Status ON file_ingestions(Status);
