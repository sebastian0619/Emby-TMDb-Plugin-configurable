using System;
using System.IO;
using MediaBrowser.Common;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;

namespace MovieDb
{
    /// <summary>
    /// TMDB 插件的主类
    /// </summary>
    public class Plugin : BasePluginSimpleUI<PluginOptions>
    {
        private readonly Guid _id = new Guid("3A63A9F3-810E-44F6-910A-14D6AD1255EC");
        private readonly ILogger _logger;
        private const string ErrorMessage = "TMDB插件出错: ";

        /// <summary>
        /// 插件实例
        /// </summary>
        public static Plugin Instance { get; private set; }

        /// <summary>
        /// 插件静态名称
        /// </summary>
        public static string StaticName => "TheMovieDb";

        /// <summary>
        /// 插件配置
        /// </summary>
        public PluginOptions Configuration { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public Plugin(IApplicationHost applicationHost, ILogManager logManager)
            : base(applicationHost)
        {
            Instance = this;
            _logger = logManager.GetLogger(Name);
            _logger.Info($"TMDB Plugin ({Name}) 正在初始化");
            
            // 初始化配置
            Configuration = new PluginOptions();
        }

        /// <summary>
        /// 插件名称
        /// </summary>
        public override string Name => StaticName;

        /// <summary>
        /// 插件描述
        /// </summary>
        public override string Description => "从 TMDB 获取电影和电视节目的元数据";

        /// <summary>
        /// 插件ID
        /// </summary>
        public override Guid Id => _id;

        /// <summary>
        /// 在选项保存后调用
        /// </summary>
        protected override void OnOptionsSaved(PluginOptions options)
        {
            try
            {
                _logger.Info($"TMDB Plugin ({Name}) 配置已保存");
                base.OnOptionsSaved(options);
            }
            catch (Exception ex)
            {
                _logger.Error($"{ErrorMessage}配置保存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建页面信息时调用
        /// </summary>
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
                _logger.Error($"{ErrorMessage}创建页面信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 准备UI配置时调用
        /// </summary>
        protected override PluginOptions OnBeforeShowUI(PluginOptions options)
        {
            try
            {
                if (options == null)
                {
                    options = new PluginOptions
                    {
                        TmdbApiBaseUrl = "https://api.themoviedb.org",
                        TmdbImageBaseUrl = "https://image.tmdb.org/t/p/",
                        ApiKey = ""
                    };
                }
                return base.OnBeforeShowUI(options);
            }
            catch (Exception ex)
            {
                _logger.Error($"{ErrorMessage}准备UI配置失败: {ex.Message}");
                return options;
            }
        }
    }
}