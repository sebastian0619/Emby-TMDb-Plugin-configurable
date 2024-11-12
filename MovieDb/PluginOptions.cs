using System;
using System.ComponentModel;
using Emby.Web.GenericEdit;

namespace MovieDb;

public class PluginOptions : EditableOptionsBase
{
	private const string DefaultApiBaseUrl = "https://api.themoviedb.org/3";

	private const string DefaultImageBaseUrl = "https://image.tmdb.org/t/p";

	private const string DefaultHomeUrl = "https://www.themoviedb.org";

	private const string DefaultApiKey = "f6bd687ffa63cd282b6ff2c6877f2669";

	public override string EditorTitle => "TMDB 插件配置";

	[DisplayName("TMDB API 基础URL")]
	[Description("TMDB API 的基础 URL 地址，默认：https://api.themoviedb.org")]
	public string TmdbApiBaseUrl { get; set; } = "https://api.themoviedb.org";


	[DisplayName("TMDB 图片基础URL")]
	[Description("TMDB 图片服务的基础 URL 地址，默认：https://image.tmdb.org/t/p")]
	public string TmdbImageBaseUrl { get; set; } = "https://image.tmdb.org/t/p";


	[DisplayName("TMDB 主页URL")]
	[Description("TMDB 网站的主页地址，默认：https://www.themoviedb.org")]
	public string TmdbHomeUrl { get; set; } = "https://www.themoviedb.org";


	[DisplayName("API Key")]
	[Description("TMDB API 密钥，默认值为EMBY官方插件的原值")]
	public string ApiKey { get; set; } = "f6bd687ffa63cd282b6ff2c6877f2669";


	public string GetImageUrl(string size)
	{
		if (string.IsNullOrWhiteSpace(size))
		{
			throw new ArgumentNullException("size");
		}
		return TmdbImageBaseUrl?.TrimEnd(new char[1] { '/' }) + "/" + size;
	}

	public new bool Validate()
	{
		if (string.IsNullOrWhiteSpace(ApiKey))
		{
			return false;
		}
		if (string.IsNullOrWhiteSpace(TmdbApiBaseUrl) || string.IsNullOrWhiteSpace(TmdbImageBaseUrl) || string.IsNullOrWhiteSpace(TmdbHomeUrl))
		{
			return false;
		}
		Uri result;
		return Uri.TryCreate(TmdbApiBaseUrl, UriKind.Absolute, out result) && Uri.TryCreate(TmdbImageBaseUrl, UriKind.Absolute, out result) && Uri.TryCreate(TmdbHomeUrl, UriKind.Absolute, out result);
	}

	public void ResetToDefaults()
	{
		TmdbApiBaseUrl = "https://api.themoviedb.org";
		TmdbImageBaseUrl = "https://image.tmdb.org/t/p";
		TmdbHomeUrl = "https://www.themoviedb.org";
		ApiKey = string.Empty;
	}
}
