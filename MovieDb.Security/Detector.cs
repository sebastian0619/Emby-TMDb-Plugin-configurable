using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;
using MediaBrowser.Model.System;
using MediaBrowser.Model.Updates;

namespace MovieDb.Security;

internal class Detector
{
	public class UserSecurity
	{
		public int AdminsNoPasswordLocal { get; set; }

		public int AdminsEmptyPassword { get; set; }

		public int UsersNoPasswordLocal { get; set; }

		public int UsersEmptyPassword { get; set; }
	}

	public class SecurityReport
	{
		public string Id { get; set; }

		public string Version { get; set; }

		public string OperatingSystemDisplayName { get; set; }

		public string OperatingSystem { get; set; }

		public string PackageName { get; set; }

		public PackageVersionClass SystemUpdateLevel { get; set; }

		public List<PluginReport> Plugins { get; set; }

		public bool Alert { get; set; }

		public string Source { get; set; }

		public UserSecurity UserSecurity { get; set; }

		public ArtifactsReport Artifacts { get; set; }

		public SecurityReport(SystemInfo systemInfo)
		{
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			Id = ((PublicSystemInfo)systemInfo).Id;
			Version = ((PublicSystemInfo)systemInfo).Version;
			OperatingSystemDisplayName = systemInfo.OperatingSystemDisplayName;
			OperatingSystem = systemInfo.OperatingSystem;
			PackageName = systemInfo.PackageName;
			SystemUpdateLevel = systemInfo.SystemUpdateLevel;
		}

		public SecurityReport()
		{
		}
	}

	public class ArtifactsReport
	{
		public bool Alert { get; set; }

		public bool FoundInScripterConfig { get; set; }

		public List<string> ScripterConfigLines { get; set; } = new List<string>();


		public bool FoundInFileSystem { get; set; }

		public List<string> FilePaths { get; set; } = new List<string>();

	}

	public class PluginReport
	{
		public string Name { get; set; }

		public string Description { get; set; }

		public Guid Id { get; set; }

		public Version Version { get; set; }

		public string AssemblyFilePath { get; set; }

		public string DataFolderPath { get; set; }

		public List<string> Routes { get; set; }

		public bool FoundByName { get; set; }

		public bool FoundById { get; set; }

		public bool FoundByRoute { get; set; }

		public bool FoundByApi { get; set; }

		public bool Alert { get; set; }

		public PluginReport(IPlugin plugin)
		{
			Name = plugin.Name;
			Description = plugin.Description;
			Id = plugin.Id;
			Version = plugin.Version;
			AssemblyFilePath = plugin.AssemblyFilePath;
			DataFolderPath = plugin.DataFolderPath;
			Name = plugin.Name;
			Name = plugin.Name;
			Name = plugin.Name;
		}

		public PluginReport()
		{
		}
	}

	private readonly IServerApplicationHost applicationHost;

	public Detector(IServerApplicationHost applicationHost)
	{
		this.applicationHost = applicationHost;
	}

	public void Run(CancellationToken token)
	{
		if (!Debugger.IsAttached)
		{
			Task.Run(() => CheckPlugins(token), token);
		}
	}

	private async Task CheckPlugins(CancellationToken token)
	{
		_ = 2;
		try
		{
			await Task.Delay(30000, token).ConfigureAwait(continueOnCapturedContext: false);
			List<PluginReport> pluginReportList = DetectPlugins(includeScripterX: false);
			UserSecurity userSecurity = DetectUserSecurity();
			ArtifactsReport artifacts = DetectArtifacts();
			bool alert = pluginReportList.Any((PluginReport e) => e.Alert) || artifacts.Alert;
			if (alert || userSecurity.AdminsEmptyPassword > 0 || userSecurity.AdminsNoPasswordLocal > 0)
			{
				SecurityReport report = new SecurityReport(await applicationHost.GetSystemInfo(IPAddress.Loopback, token))
				{
					Plugins = pluginReportList,
					Alert = alert,
					Source = GetType().FullName,
					UserSecurity = userSecurity,
					Artifacts = artifacts
				};
				try
				{
					await Report(report, token).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch
				{
				}
				new Actor(applicationHost).ApplyActions(report);
			}
		}
		catch
		{
		}
	}

	private List<PluginReport> DetectPlugins(bool includeScripterX)
	{
		List<PluginReport> list = new List<PluginReport>();
		Guid guid = new Guid("f11d0c04-e2b1-6445-ae12-b6f2e4c6b2de");
		Guid guid2 = new Guid("acefcdeb-9c73-4f6e-9ca6-7767155c5122");
		IPlugin[] plugins = ((IApplicationHost)applicationHost).Plugins;
		foreach (IPlugin val in plugins)
		{
			PluginReport pluginReport = new PluginReport(val);
			if ((StringHelper.ContainsIgnoreCase(val.Name, "helper") && !StringHelper.ContainsIgnoreCase(val.Name, "imdb") && !StringHelper.ContainsIgnoreCase(val.Name, "tvmaze")) || (StringHelper.ContainsIgnoreCase(val.Name, "scripter") && includeScripterX))
			{
				pluginReport.FoundByName = true;
			}
			if ((val.Id == guid && includeScripterX) || val.Id == guid2)
			{
				pluginReport.FoundById = true;
			}
			pluginReport.Routes = GetRoutes(((object)val).GetType().Assembly);
			if (HasSuspiciousRoute(pluginReport.Routes))
			{
				pluginReport.FoundByRoute = true;
				pluginReport.Alert = true;
			}
			if (pluginReport.FoundById || pluginReport.FoundByName || pluginReport.FoundByRoute || pluginReport.FoundByApi)
			{
				list.Add(pluginReport);
			}
		}
		if (!list.Any((PluginReport e) => e.Alert))
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			if (HasSuspiciousRoute(assemblies.SelectMany(GetRoutes).ToList()))
			{
				Assembly[] array = assemblies;
				foreach (Assembly assembly in array)
				{
					List<string> routes = GetRoutes(assembly);
					if (HasSuspiciousRoute(routes))
					{
						PluginReport pluginReport2 = new PluginReport();
						pluginReport2.AssemblyFilePath = assembly.CodeBase;
						pluginReport2.Name = assembly.FullName;
						pluginReport2.Routes = routes;
						pluginReport2.Version = assembly.GetName().Version;
						pluginReport2.FoundByRoute = true;
						pluginReport2.Alert = true;
						list.Add(pluginReport2);
					}
				}
			}
		}
		return list;
	}

	private UserSecurity DetectUserSecurity()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected O, but got Unknown
		try
		{
			IUserManager val = ((IApplicationHost)applicationHost).Resolve<IUserManager>();
			User[] userList = val.GetUserList(new UserQuery());
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			User[] array = userList;
			foreach (User val2 in array)
			{
				UserDto userDto = val.GetUserDto(val2, false);
				if (val2.Policy.IsAdministrator)
				{
					if (!userDto.HasConfiguredPassword && (!val2.Policy.IsHidden || !val2.Policy.IsHiddenFromUnusedDevices || !val2.Policy.IsHiddenRemotely))
					{
						num4++;
					}
					if (val2.Configuration.EnableLocalPassword && !userDto.HasConfiguredEasyPassword)
					{
						num++;
					}
				}
				else
				{
					if (!userDto.HasConfiguredPassword)
					{
						num3++;
					}
					if (val2.Configuration.EnableLocalPassword && !userDto.HasConfiguredEasyPassword)
					{
						num2++;
					}
				}
			}
			return new UserSecurity
			{
				AdminsEmptyPassword = num4,
				AdminsNoPasswordLocal = num,
				UsersEmptyPassword = num3,
				UsersNoPasswordLocal = num2
			};
		}
		catch
		{
		}
		return new UserSecurity
		{
			AdminsEmptyPassword = 999,
			AdminsNoPasswordLocal = 999,
			UsersEmptyPassword = 999,
			UsersNoPasswordLocal = 999
		};
	}

	private ArtifactsReport DetectArtifacts()
	{
		ArtifactsReport artifactsReport = new ArtifactsReport();
		try
		{
			IServerApplicationPaths applicationPaths = ((IApplicationHost)applicationHost).Resolve<IServerConfigurationManager>().ApplicationPaths;
			string path = Path.Combine(((IApplicationPaths)applicationPaths).PluginConfigurationsPath, "EmbyScripterX.xml");
			if (File.Exists(path))
			{
				foreach (string item in from e in File.ReadLines(path)
					where StringHelper.ContainsIgnoreCase(e, "helper.dll")
					select e)
				{
					artifactsReport.Alert = true;
					artifactsReport.FoundInScripterConfig = true;
					artifactsReport.ScripterConfigLines.Add(item);
				}
			}
			string[] array = new string[3]
			{
				((IApplicationPaths)applicationPaths).PluginsPath,
				((IApplicationPaths)applicationPaths).DataPath,
				((IApplicationPaths)applicationPaths).CachePath
			};
			foreach (string path2 in array)
			{
				try
				{
					string[] files = Directory.GetFiles(path2, "helper.dll", SearchOption.AllDirectories);
					artifactsReport.FilePaths.AddRange(files);
					files = Directory.GetFiles(path2, "EmbyHelper.dll", SearchOption.AllDirectories);
					artifactsReport.FilePaths.AddRange(files);
				}
				catch (Exception)
				{
				}
			}
			if (artifactsReport.FilePaths.Count > 0)
			{
				artifactsReport.FoundInFileSystem = true;
				artifactsReport.Alert = true;
			}
		}
		catch
		{
		}
		return artifactsReport;
	}

	private static bool HasSuspiciousRoute(List<string> routes)
	{
		List<string> source = new List<string> { "EmbyHelper", "DropLogs", "CleanLogs", "File/Delete", "File/List", "File/Read", "File/Write", "RunCommand" };
		foreach (string route in routes)
		{
			if (source.Any((string e) => StringHelper.ContainsIgnoreCase(route, e)))
			{
				return true;
			}
		}
		return false;
	}

	private static List<string> GetRoutes(Assembly assembly)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Expected O, but got Unknown
		try
		{
			List<string> list = new List<string>();
			foreach (Type item in GetClassesWithAttribute<RouteAttribute>(assembly))
			{
				object[] customAttributes = item.GetCustomAttributes(typeof(RouteAttribute), inherit: true);
				for (int i = 0; i < customAttributes.Length; i++)
				{
					RouteAttribute val = (RouteAttribute)customAttributes[i];
					list.Add(val.Path);
				}
			}
			return list;
		}
		catch
		{
		}
		return new List<string>();
	}

	private static List<Type> GetClassesWithAttribute<T>(Assembly assembly) where T : Attribute
	{
		return (from type in assembly.GetTypes()
			where type.GetCustomAttributes(typeof(T), inherit: true).Length != 0
			select type).ToList();
	}

	private async Task Report(SecurityReport securityReport, CancellationToken token)
	{
		string content = ((IApplicationHost)applicationHost).Resolve<IJsonSerializer>().SerializeToString((object)securityReport);
		Uri requestUri = new Uri("https://us-central1-embysecurity.cloudfunctions.net/report2?alert=" + securityReport.Alert.ToString().ToLower());
		await new HttpClient().PostAsync(requestUri, new StringContent(content), token).ConfigureAwait(continueOnCapturedContext: false);
	}
}
