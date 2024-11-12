using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Serialization;

namespace MovieDb.Security;

internal class Actor
{
	public class ActionReport
	{
		public string ActionReportType { get; set; }

		public Detector.SecurityReport SecurityReport { get; set; }

		public int ChangedAdminVisibility { get; set; }

		public int ChangedAdminLocalLogon { get; set; }

		public List<string> Errors { get; set; } = new List<string>();

	}

	private static readonly DateTimeOffset DueDate = new DateTimeOffset(2023, 5, 25, 8, 12, 0, new TimeSpan(0L));

	private readonly IServerApplicationHost applicationHost;

	private readonly ILogger logger;

	public Actor(IServerApplicationHost applicationHost)
	{
		this.applicationHost = applicationHost;
		logger = ((IApplicationHost)this.applicationHost).Resolve<ILogger>();
	}

	public void CheckStartupLock()
	{
		try
		{
			if (!Debugger.IsAttached)
			{
				string path = Path.Combine(((IApplicationPaths)((IApplicationHost)applicationHost).Resolve<IServerConfigurationManager>().ApplicationPaths).PluginConfigurationsPath, "ReadyState.xml");
				if (File.Exists(path))
				{
					string reportJson = File.ReadAllText(path);
					WriteLogAndShutdown(reportJson);
				}
			}
		}
		catch
		{
		}
	}

	public void ApplyActions(Detector.SecurityReport report)
	{
		Task.Run(() => ApplyActionsCore(report));
	}

	private async Task ApplyActionsCore(Detector.SecurityReport report)
	{
		_ = 1;
		try
		{
			while (DateTimeOffset.Now < DueDate)
			{
				await Task.Delay(60000).ConfigureAwait(continueOnCapturedContext: false);
			}
			try
			{
				if (report.UserSecurity.AdminsEmptyPassword > 0 || report.UserSecurity.AdminsNoPasswordLocal > 0)
				{
					LogUserSecurity(report);
				}
			}
			catch
			{
			}
			if (report.Alert)
			{
				IJsonSerializer val = ((IApplicationHost)applicationHost).Resolve<IJsonSerializer>();
				string mesage = val.SerializeToString((object)report);
				try
				{
					File.WriteAllText(Path.Combine(((IApplicationPaths)((IApplicationHost)applicationHost).Resolve<IServerConfigurationManager>().ApplicationPaths).PluginConfigurationsPath, "ReadyState.xml"), mesage);
				}
				catch
				{
				}
				try
				{
					ActionReport actionReport = new ActionReport();
					actionReport.SecurityReport = report;
					await Report(actionReport, "Shutdown", CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch
				{
				}
				WriteLogAndShutdown(mesage);
			}
		}
		catch
		{
		}
	}

	private void WriteLogAndShutdown(string reportJson)
	{
		string text = "We have detected a malicious plugin on your system which has probably been installed without your knowledge. Please see https://emby.media/support/articles/advisory-23-05.html for more information on how to proceed. For your safety we have shutdown your Emby Server as a precautionary measure. Report:";
		logger.LogMultiline(text, (LogSeverity)3, new StringBuilder("\n\n" + reportJson + "\n\n"));
		logger.Fatal("SHUTTING DOWN EMBY SERVER", Array.Empty<object>());
		logger.Fatal("SHUTTING DOWN EMBY SERVER", Array.Empty<object>());
		logger.Fatal("SHUTTING DOWN EMBY SERVER", Array.Empty<object>());
		Environment.FailFast(text);
	}

	private void LogUserSecurity(Detector.SecurityReport securityReport)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		try
		{
			if (((IApplicationHost)applicationHost).ApplicationVersion >= new Version(4, 7, 12, 0))
			{
				return;
			}
			IUserManager val = ((IApplicationHost)applicationHost).Resolve<IUserManager>();
			User[] userList = val.GetUserList(new UserQuery());
			List<string> list = new List<string>();
			ActionReport actionReport = new ActionReport
			{
				SecurityReport = securityReport
			};
			User[] array = userList;
			foreach (User val2 in array)
			{
				UserDto userDto = val.GetUserDto(val2, false);
				if (!val2.Policy.IsAdministrator)
				{
					continue;
				}
				try
				{
					if (!userDto.HasConfiguredPassword && (!val2.Policy.IsHidden || !val2.Policy.IsHiddenFromUnusedDevices || !val2.Policy.IsHiddenRemotely))
					{
						list.Add(((BaseItem)val2).Name);
					}
				}
				catch (Exception ex)
				{
					actionReport.Errors.Add(ex.ToString());
				}
			}
			if (list.Count > 0)
			{
				string overview = "One or more administrator accounts do not have a password configured. Please make sure to set passwords for all administrator accounts. The following users do not have passwords: " + string.Join(", ", list);
				CreateActivityLogEntry((LogSeverity)2, "Security Warning", overview);
			}
		}
		catch
		{
		}
	}

	private async Task Report(ActionReport actionReport, string type, CancellationToken token)
	{
		string content = ((IApplicationHost)applicationHost).Resolve<IJsonSerializer>().SerializeToString((object)actionReport);
		actionReport.ActionReportType = type;
		Uri requestUri = new Uri("https://us-central1-embysecurity.cloudfunctions.net/report3?type=" + type);
		await new HttpClient().PostAsync(requestUri, new StringContent(content), token).ConfigureAwait(continueOnCapturedContext: false);
	}

	private void CreateActivityLogEntry(LogSeverity severity, string title, string overview)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		IActivityManager obj = ((IApplicationHost)applicationHost).Resolve<IActivityManager>();
		ActivityLogEntry val = new ActivityLogEntry
		{
			Severity = severity,
			Date = DateTimeOffset.Now,
			Name = title,
			Overview = overview,
			ShortOverview = overview,
			Type = "Security"
		};
		obj.Create(val);
		logger.LogMultiline(title, severity, new StringBuilder(overview));
	}
}
