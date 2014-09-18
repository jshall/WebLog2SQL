USE [WebLog]
GO

/****** Object:  Table [dbo].[Event]    Script Date: 07/18/2014 21:10:06 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

SET ANSI_PADDING ON
GO

CREATE TABLE [dbo].[Event](
	[FileID] [bigint] NOT NULL,
	[ID] [bigint] IDENTITY(1,1) NOT NULL,
	[date] [date] NOT NULL,
	[time] [time](7) NOT NULL,
	[c-ip] [varchar](15) NULL,
	[cs-username] [varchar](max) NULL,
	[s-sitename] [varchar](max) NULL,
	[s-computername] [varchar](max) NULL,
	[s-ip] [varchar](15) NULL,
	[s-port] [int] NULL,
	[cs-method] [varchar](20) NULL,
	[cs-uri-stem] [varchar](max) NULL,
	[cs-uri-query] [varchar](max) NULL,
	[sc-status] [int] NULL,
	[sc-substatus] [int] NULL,
	[sc-win32-status] [bigint] NULL,
	[sc-bytes] [int] NULL,
	[cs-bytes] [int] NULL,
	[time-taken] [int] NULL,
	[cs-host] [varchar](512) NULL,
	[cs(User-Agent)] [varchar](max) NULL,
	[cs(Referer)] [varchar](max) NULL,
 CONSTRAINT [PK_Event] PRIMARY KEY NONCLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO

SET ANSI_PADDING ON
GO

USE [WebLog]
GO

/****** Object:  Table [dbo].[File]    Script Date: 07/18/2014 21:10:06 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[File](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](max) NOT NULL,
	[Updated] [datetime] NOT NULL,
	[Scanned] [datetime] NULL,
	[BytesRead] [bigint] NOT NULL,
	[LastFields] [nvarchar](max) NULL,
	[EventCount] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO

ALTER TABLE [dbo].[Event]  WITH NOCHECK ADD  CONSTRAINT [FK_Event_File] FOREIGN KEY([FileID])
REFERENCES [dbo].[File] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [dbo].[Event] CHECK CONSTRAINT [FK_Event_File]
GO

ALTER TABLE [dbo].[File] ADD  DEFAULT ((0)) FOR [BytesRead]
GO

ALTER TABLE [dbo].[File] ADD  DEFAULT ((0)) FOR [EventCount]
GO

