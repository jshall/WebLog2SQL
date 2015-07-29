CREATE TABLE [dbo].[File] (
    [Id]         BIGINT             IDENTITY (1, 1) NOT NULL,
    [Name]       NVARCHAR (MAX)     NOT NULL,
    [Created]    DATETIMEOFFSET (7) NOT NULL,
    [Updated]    DATETIMEOFFSET (7) NOT NULL,
    [Scanned]    DATETIMEOFFSET (7) NULL,
    [BytesRead]  BIGINT             DEFAULT ((0)) NOT NULL,
    [LastFields] NVARCHAR (MAX)     NULL,
    [EventCount] INT                DEFAULT ((0)) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);

CREATE TABLE [dbo].[Event] (
    [ID]                BIGINT          IDENTITY (1, 1) NOT NULL,
    [FileID]            BIGINT          NOT NULL,
    [datetime]          AS              (dateadd(day,datediff(day,'19000101',[date]),CONVERT([datetimeoffset](7),[time],(0)))),
    [date]              DATE            NOT NULL,
    [time]              TIME (7)        NOT NULL,
    [c-ip]              NVARCHAR (80)   NULL,
    [cs-method]         NVARCHAR (20)   NULL,
    [cs-host]           NVARCHAR (512)  NULL,
    [cs-uri-stem]       NVARCHAR (4000) NULL,
    [cs-uri-query]      NVARCHAR (4000) NULL,
    [cs-bytes]          INT             NULL,
    [s-ip]              NVARCHAR (80)   NULL,
    [s-port]            INT             NULL,
    [sc-status]         INT             NULL,
    [sc-substatus]      INT             NULL,
    [sc-win32-status]   BIGINT          NULL,
    [sc-bytes]          INT             NULL,
    [time-taken]        INT             NULL,
    PRIMARY KEY NONCLUSTERED ([ID] ASC),
    CONSTRAINT [FK_Event_File] FOREIGN KEY ([FileID]) REFERENCES [dbo].[File] ([Id]) ON DELETE CASCADE
);

CREATE CLUSTERED INDEX [IX_EventOrder]
    ON [dbo].[Event]([FileID] ASC, [date] ASC, [time] ASC) WITH (DATA_COMPRESSION = PAGE);

