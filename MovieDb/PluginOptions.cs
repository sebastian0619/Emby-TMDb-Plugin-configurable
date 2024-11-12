using System.ComponentModel;
using Emby.Web.GenericEdit;
namespace MovieDb
{
    /// <summary>
    /// TMDB 插件配置类
    /// </summary>
    public class PluginOptions : EditableOptionsBase
    {
        public override string EditorTitle => "TMDB 插件配置";

        private const string DefaultApiBaseUrl = "https://api.themoviedb.org/3";
        private const string DefaultImageBaseUrl = "https://image.tmdb.org/t/p";
        private const string DefaultHomeUrl = "https://www.themoviedb.org";

        private const string DefaultApiKey = "59ef6336a19540cd1e254cae0565906e";

        [DisplayName("TMDB API 基础URL")]
        [Description("TMDB API 的基础 URL 地址")]
        public string TmdbApiBaseUrl { get; set; } = DefaultApiBaseUrl;

        [DisplayName("TMDB 图片基础URL")]
        [Description("TMDB 图片服务的基础 URL 地址")]
        public string TmdbImageBaseUrl { get; set; } = DefaultImageBaseUrl;

        [DisplayName("TMDB 主页URL")]
        [Description("TMDB 网站的主页地址")]
        public string TmdbHomeUrl { get; set; } = DefaultHomeUrl;

        [DisplayName("API Key")]
        [Description("TMDB API 密钥")]
        public string ApiKey { get; set; } = DefaultApiKey;

        /// <summary>
        /// 获取完整的图片URL
        /// </summary>
        /// <param name="size">图片尺寸</param>
        /// <returns>完整的图片URL</returns>
        public string GetImageUrl(string size)
        {
            if (string.IsNullOrWhiteSpace(size))
            {
                throw new System.ArgumentNullException(nameof(size));
            }
            return $"{TmdbImageBaseUrl?.TrimEnd('/')}/{size}";
        }

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public new bool Validate()
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(TmdbApiBaseUrl) ||
                string.IsNullOrWhiteSpace(TmdbImageBaseUrl) ||
                string.IsNullOrWhiteSpace(TmdbHomeUrl))
            {
                return false;
            }

            return System.Uri.TryCreate(TmdbApiBaseUrl, System.UriKind.Absolute, out _) &&
                   System.Uri.TryCreate(TmdbImageBaseUrl, System.UriKind.Absolute, out _) &&
                   System.Uri.TryCreate(TmdbHomeUrl, System.UriKind.Absolute, out _);
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public void ResetToDefaults()
        {
            TmdbApiBaseUrl = DefaultApiBaseUrl;
            TmdbImageBaseUrl = DefaultImageBaseUrl;
            TmdbHomeUrl = DefaultHomeUrl;
            ApiKey = string.Empty;
        }
    }
} 