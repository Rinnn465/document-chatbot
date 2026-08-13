/*
    PRN222 Document Chatbot
    SQL Server schema for Assignment 1, Assignment 2 and the later group project.

    SQL Server stores application data and document metadata.
    Chroma stores document chunks and embeddings, linked by Documents.DocumentId.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF DB_ID(N'DocumentChatbotDb') IS NULL
BEGIN
    CREATE DATABASE DocumentChatbotDb;
END;
GO

USE DocumentChatbotDb;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Roles
        (
            RoleId      tinyint      NOT NULL,
            Name        varchar(30)  NOT NULL,

            CONSTRAINT PK_Roles PRIMARY KEY (RoleId),
            CONSTRAINT UQ_Roles_Name UNIQUE (Name),
            CONSTRAINT CK_Roles_Name CHECK (Name IN ('SubjectLeader', 'Student'))
        );
    END;

    IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Users
        (
            UserId          uniqueidentifier NOT NULL
                CONSTRAINT DF_Users_UserId DEFAULT NEWSEQUENTIALID(),
            Email           varchar(256)     NOT NULL,
            DisplayName     nvarchar(150)    NOT NULL,
            PasswordHash    nvarchar(500)    NULL,
            RoleId          tinyint          NOT NULL,
            IsActive        bit              NOT NULL
                CONSTRAINT DF_Users_IsActive DEFAULT (1),
            CreatedAtUtc    datetime2(0)     NOT NULL
                CONSTRAINT DF_Users_CreatedAtUtc DEFAULT SYSUTCDATETIME(),

            CONSTRAINT PK_Users PRIMARY KEY (UserId),
            CONSTRAINT UQ_Users_Email UNIQUE (Email),
            CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId)
                REFERENCES dbo.Roles(RoleId)
        );
    END;

    IF OBJECT_ID(N'dbo.Courses', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Courses
        (
            CourseId            int              IDENTITY(1, 1) NOT NULL,
            Code                varchar(20)      NOT NULL,
            Name                nvarchar(200)    NOT NULL,
            SubjectLeaderId     uniqueidentifier NOT NULL,
            IsActive            bit              NOT NULL
                CONSTRAINT DF_Courses_IsActive DEFAULT (1),
            CreatedAtUtc        datetime2(0)     NOT NULL
                CONSTRAINT DF_Courses_CreatedAtUtc DEFAULT SYSUTCDATETIME(),

            CONSTRAINT PK_Courses PRIMARY KEY (CourseId),
            CONSTRAINT UQ_Courses_Code UNIQUE (Code),
            CONSTRAINT FK_Courses_SubjectLeader FOREIGN KEY (SubjectLeaderId)
                REFERENCES dbo.Users(UserId)
        );
    END;

    IF OBJECT_ID(N'dbo.CourseEnrollments', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.CourseEnrollments
        (
            CourseId       int              NOT NULL,
            StudentId      uniqueidentifier NOT NULL,
            EnrolledAtUtc  datetime2(0)     NOT NULL
                CONSTRAINT DF_CourseEnrollments_EnrolledAtUtc DEFAULT SYSUTCDATETIME(),

            CONSTRAINT PK_CourseEnrollments PRIMARY KEY (CourseId, StudentId),
            CONSTRAINT FK_CourseEnrollments_Courses FOREIGN KEY (CourseId)
                REFERENCES dbo.Courses(CourseId) ON DELETE CASCADE,
            CONSTRAINT FK_CourseEnrollments_Students FOREIGN KEY (StudentId)
                REFERENCES dbo.Users(UserId)
        );
    END;

    IF OBJECT_ID(N'dbo.Documents', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Documents
        (
            DocumentId          uniqueidentifier NOT NULL
                CONSTRAINT DF_Documents_DocumentId DEFAULT NEWSEQUENTIALID(),
            CourseId            int              NOT NULL,
            Title               nvarchar(255)    NOT NULL,
            Chapter             nvarchar(255)    NULL,
            OriginalFileName    nvarchar(255)    NOT NULL,
            FileType            varchar(10)      NOT NULL,
            FileSizeBytes       bigint           NOT NULL,
            Status              varchar(20)      NOT NULL
                CONSTRAINT DF_Documents_Status DEFAULT ('Processing'),
            ChunkCount          int              NOT NULL
                CONSTRAINT DF_Documents_ChunkCount DEFAULT (0),
            ProcessingError     nvarchar(2000)   NULL,
            UploadedByUserId    uniqueidentifier NOT NULL,
            UploadedAtUtc       datetime2(0)     NOT NULL
                CONSTRAINT DF_Documents_UploadedAtUtc DEFAULT SYSUTCDATETIME(),

            CONSTRAINT PK_Documents PRIMARY KEY (DocumentId),
            CONSTRAINT FK_Documents_Courses FOREIGN KEY (CourseId)
                REFERENCES dbo.Courses(CourseId),
            CONSTRAINT FK_Documents_UploadedBy FOREIGN KEY (UploadedByUserId)
                REFERENCES dbo.Users(UserId),
            CONSTRAINT CK_Documents_FileType
                CHECK (FileType IN ('Pdf', 'Docx', 'Slide')),
            CONSTRAINT CK_Documents_Status
                CHECK (Status IN ('Processing', 'Indexed', 'Failed')),
            CONSTRAINT CK_Documents_FileSize
                CHECK (FileSizeBytes > 0 AND FileSizeBytes <= 26214400)
        );
    END;

    IF OBJECT_ID(N'dbo.ChatSessions', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ChatSessions
        (
            ChatSessionId   uniqueidentifier NOT NULL
                CONSTRAINT DF_ChatSessions_ChatSessionId DEFAULT NEWSEQUENTIALID(),
            CourseId        int              NOT NULL,
            UserId          uniqueidentifier NOT NULL,
            Title           nvarchar(255)    NOT NULL,
            CreatedAtUtc    datetime2(0)     NOT NULL
                CONSTRAINT DF_ChatSessions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),

            CONSTRAINT PK_ChatSessions PRIMARY KEY (ChatSessionId),
            CONSTRAINT FK_ChatSessions_Courses FOREIGN KEY (CourseId)
                REFERENCES dbo.Courses(CourseId),
            CONSTRAINT FK_ChatSessions_Users FOREIGN KEY (UserId)
                REFERENCES dbo.Users(UserId)
        );
    END;

    IF OBJECT_ID(N'dbo.ChatMessages', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ChatMessages
        (
            ChatMessageId   uniqueidentifier NOT NULL
                CONSTRAINT DF_ChatMessages_ChatMessageId DEFAULT NEWSEQUENTIALID(),
            ChatSessionId   uniqueidentifier NOT NULL,
            Role            varchar(10)      NOT NULL,
            Content         nvarchar(max)    NOT NULL,
            SentAtUtc       datetime2(7)     NOT NULL
                CONSTRAINT DF_ChatMessages_SentAtUtc DEFAULT SYSUTCDATETIME(),

            CONSTRAINT PK_ChatMessages PRIMARY KEY (ChatMessageId),
            CONSTRAINT FK_ChatMessages_Sessions FOREIGN KEY (ChatSessionId)
                REFERENCES dbo.ChatSessions(ChatSessionId) ON DELETE CASCADE,
            CONSTRAINT CK_ChatMessages_Role
                CHECK (Role IN ('User', 'Assistant'))
        );
    END;

    -- Older copies used datetime2(0), which rounded messages to whole seconds
    -- and made their order ambiguous after a session was loaded again.
    IF EXISTS
    (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.ChatMessages')
          AND name = N'SentAtUtc'
          AND scale < 7
    )
    BEGIN
        IF EXISTS
        (
            SELECT 1
            FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'dbo.ChatMessages')
              AND name = N'IX_ChatMessages_Session_SentAt'
        )
            DROP INDEX IX_ChatMessages_Session_SentAt
                ON dbo.ChatMessages;

        ALTER TABLE dbo.ChatMessages
            ALTER COLUMN SentAtUtc datetime2(7) NOT NULL;
    END;

    IF OBJECT_ID(N'dbo.Citations', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Citations
        (
            CitationId      bigint           IDENTITY(1, 1) NOT NULL,
            ChatMessageId   uniqueidentifier NOT NULL,
            DocumentId      uniqueidentifier NOT NULL,
            ChunkId         varchar(150)     NOT NULL,
            PageNumber      int              NULL,
            SlideNumber     int              NULL,
            Excerpt         nvarchar(1000)   NOT NULL,
            RelevanceScore  decimal(6, 5)    NOT NULL,

            CONSTRAINT PK_Citations PRIMARY KEY (CitationId),
            CONSTRAINT FK_Citations_ChatMessages FOREIGN KEY (ChatMessageId)
                REFERENCES dbo.ChatMessages(ChatMessageId) ON DELETE CASCADE,
            CONSTRAINT FK_Citations_Documents FOREIGN KEY (DocumentId)
                REFERENCES dbo.Documents(DocumentId) ON DELETE CASCADE
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'IX_Users_RoleId'
    )
        CREATE INDEX IX_Users_RoleId ON dbo.Users(RoleId);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.CourseEnrollments')
          AND name = N'IX_CourseEnrollments_StudentId'
    )
        CREATE INDEX IX_CourseEnrollments_StudentId
            ON dbo.CourseEnrollments(StudentId, CourseId);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Documents')
          AND name = N'IX_Documents_Course_Status'
    )
        CREATE INDEX IX_Documents_Course_Status
            ON dbo.Documents(CourseId, Status);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.ChatSessions')
          AND name = N'IX_ChatSessions_User_Created'
    )
        CREATE INDEX IX_ChatSessions_User_Created
            ON dbo.ChatSessions(UserId, CreatedAtUtc DESC);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.ChatMessages')
          AND name = N'IX_ChatMessages_Session_SentAt'
    )
        CREATE INDEX IX_ChatMessages_Session_SentAt
            ON dbo.ChatMessages(ChatSessionId, SentAtUtc);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Citations')
          AND name = N'IX_Citations_ChatMessageId'
    )
        CREATE INDEX IX_Citations_ChatMessageId
            ON dbo.Citations(ChatMessageId);

    IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleId = 1)
        INSERT INTO dbo.Roles (RoleId, Name) VALUES (1, 'SubjectLeader');

    IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleId = 2)
        INSERT INTO dbo.Roles (RoleId, Name) VALUES (2, 'Student');

    DECLARE @LeaderId uniqueidentifier = '11111111-1111-1111-1111-111111111111';
    DECLARE @StudentId uniqueidentifier = '22222222-2222-2222-2222-222222222222';
    DECLARE @LeaderPasswordHash nvarchar(500) =
        N'AQAAAAEAAYagAAAAEH0NlhODHqAAeeRIqlvkWKcCv8NO03Ql+/hgRPaFa7DW+lF/l8S4Xf9qeLihslriRw==';
    DECLARE @StudentPasswordHash nvarchar(500) =
        N'AQAAAAEAAYagAAAAEJVFekWTGSHJ+K+670mxTKAa3Tx5cFUd92iIuL8AW7VcuQ7MOXQD6kmnAAmaAWUGiw==';

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @LeaderId)
    BEGIN
        INSERT INTO dbo.Users (UserId, Email, DisplayName, PasswordHash, RoleId)
        VALUES
        (
            @LeaderId,
            'hungdt0546@fpt.edu.vn',
            N'PRN222 Subject Leader',
            @LeaderPasswordHash,
            1
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @StudentId)
    BEGIN
        INSERT INTO dbo.Users (UserId, Email, DisplayName, PasswordHash, RoleId)
        VALUES
        (
            @StudentId,
            'hungdt0546@gmail.com',
            N'Đặng Trường Hưng',
            @StudentPasswordHash,
            2
        );
    END;

    UPDATE dbo.Users
    SET PasswordHash = @LeaderPasswordHash
    WHERE UserId = @LeaderId;

    UPDATE dbo.Users
    SET PasswordHash = @StudentPasswordHash
    WHERE UserId = @StudentId;

    IF NOT EXISTS (SELECT 1 FROM dbo.Courses WHERE Code = 'PRN222')
    BEGIN
        INSERT INTO dbo.Courses (Code, Name, SubjectLeaderId)
        VALUES ('PRN222', N'Advanced Programming with .NET', @LeaderId);
    END;

    DECLARE @CourseId int = (SELECT CourseId FROM dbo.Courses WHERE Code = 'PRN222');

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.CourseEnrollments
        WHERE CourseId = @CourseId AND StudentId = @StudentId
    )
    BEGIN
        INSERT INTO dbo.CourseEnrollments (CourseId, StudentId)
        VALUES (@CourseId, @StudentId);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

PRINT 'DocumentChatbotDb is ready.';
GO
