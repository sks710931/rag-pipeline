-- Re-creating file_ingestions with pipeline stage tracking
IF OBJECT_ID('dbo.file_ingestions', 'U') IS NOT NULL DROP TABLE dbo.file_ingestions;

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
