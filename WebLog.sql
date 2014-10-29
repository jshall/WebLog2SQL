USE [WebLog]
GO

/****** Object:  Table [dbo].[Event]    Script Date: 10/29/2014 18:47:21 ******/
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
	[datetime]  AS (dateadd(day,datediff(day,'19000101',[date]),CONVERT([datetimeoffset](7),[time],(0)))),
	[x-session] [varchar](max) NULL,
	[x-fullpath] [varchar](max) NULL,
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

/****** Object:  Table [dbo].[File]    Script Date: 10/29/2014 18:47:21 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[File](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](max) NOT NULL,
	[Created] [datetimeoffset](7) NOT NULL,
	[Updated] [datetimeoffset](7) NOT NULL,
	[Scanned] [datetimeoffset](7) NULL,
	[BytesRead] [bigint] NOT NULL,
	[LastFields] [nvarchar](max) NULL,
	[EventCount] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO

USE [WebLog]
GO

/****** Object:  Table [dbo].[GeoData]    Script Date: 10/29/2014 18:47:21 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

SET ANSI_PADDING ON
GO

CREATE TABLE [dbo].[GeoData](
	[ip] [varchar](50) NOT NULL,
	[country_code] [char](2) NULL,
	[country_name] [nvarchar](100) NULL,
	[region_code] [char](2) NULL,
	[region_name] [nvarchar](100) NULL,
	[city] [nvarchar](100) NULL,
	[zipcode] [varchar](20) NULL,
	[latitude] [real] NULL,
	[longitude] [real] NULL,
	[metro_code] [int] NULL,
	[area_code] [int] NULL,
	[date_added] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[ip] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

SET ANSI_PADDING ON
GO

USE [WebLog]
GO

/****** Object:  Table [dbo].[UserAgent]    Script Date: 10/29/2014 18:47:21 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

SET ANSI_PADDING ON
GO

CREATE TABLE [dbo].[UserAgent](
	[agent_string] [varchar](1024) NOT NULL,
	[agent_type] [varchar](50) NULL,
	[agent_name] [varchar](1024) NOT NULL,
	[agent_version] [varchar](50) NULL,
	[os_type] [varchar](50) NULL,
	[os_name] [varchar](50) NULL,
	[os_versionName] [varchar](50) NULL,
	[os_versionNumber] [varchar](50) NULL,
	[linux_distribution] [varchar](50) NULL,
	[date_added] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[agent_string] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

SET ANSI_PADDING ON
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

ALTER TABLE [dbo].[GeoData] ADD  DEFAULT (getdate()) FOR [date_added]
GO

ALTER TABLE [dbo].[UserAgent] ADD  DEFAULT (getdate()) FOR [date_added]
GO


