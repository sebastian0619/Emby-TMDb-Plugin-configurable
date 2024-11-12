using System;
using System.IO;
using MediaBrowser.Common;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;

namespace MovieDb;

public class Plugin : BasePluginSimpleUI<PluginOptions>, IHasThumbImage
{
	private readonly Guid _id = new Guid("3A63A9F3-810E-44F6-910A-14D6AD1255EC");

	private readonly ILogger _logger;

	private const string ErrorMessage = "TMDB插件出错: ";

	public static Plugin Instance { get; private set; }

	public static string StaticName => "Configurable MovieDb";

	public override string Name => StaticName;

	public override string Description => "MovieDb metadata for movies, configurable for url and api key";

	public ImageFormat ThumbImageFormat => ImageFormat.Png;

	public PluginOptions Configuration
	{
		get
		{
			return GetOptions();
		}
		set
		{
			if (value != null)
			{
				SaveOptions(value);
			}
		}
	}

	public override Guid Id => _id;

	public Plugin(IApplicationHost applicationHost, ILogManager logManager)
		: base(applicationHost)
	{
		Instance = this;
		_logger = logManager.GetLogger(Name);
		if (Configuration == null)
		{
			Configuration = new PluginOptions
			{
				TmdbApiBaseUrl = "https://tmdb.kingscross.online:8333",
				TmdbImageBaseUrl = "https://image.kingscross.online:8333/t/p/",
				TmdbHomeUrl = "https://tmdb.kingscross.online:8333",
				ApiKey = "59ef6336a19540cd1e254cae0565906e"
			};
		}
		_logger.Info("TMDB Plugin (" + Name + ") 正在初始化");
	}

	protected override void OnOptionsSaved(PluginOptions options)
	{
		try
		{
			_logger.Info("TMDB Plugin (" + Name + ") 配置已保存");
			base.OnOptionsSaved(options);
		}
		catch (Exception ex)
		{
			_logger.Error("TMDB插件出错: 配置保存失败: " + ex.Message);
		}
	}

	protected override void OnCreatePageInfo(PluginPageInfo pageInfo)
	{
		try
		{
			pageInfo.Name = "TMDB 配置";
			pageInfo.EnableInMainMenu = true;
			base.OnCreatePageInfo(pageInfo);
		}
		catch (Exception ex)
		{
			_logger.Error("TMDB插件出错: 创建页面信息失败: " + ex.Message);
		}
	}

	public Stream GetThumbImage()
	{
		Type type = GetType();
		return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.png");
	}

	protected override PluginOptions OnBeforeShowUI(PluginOptions options)
	{
		try
		{
			if (options == null)
			{
				options = new PluginOptions
				{
					TmdbApiBaseUrl = "https://tmdb.kingscross.online:8333",
					TmdbImageBaseUrl = "https://image.kingscross.online:8333/t/p/",
					ApiKey = "59ef6336a19540cd1e254cae0565906e"
				};
			}
			return base.OnBeforeShowUI(options);
		}
		catch (Exception ex)
		{
			_logger.Error("TMDB插件出错: 准备UI配置失败: " + ex.Message);
			return options;
		}
	}
}
