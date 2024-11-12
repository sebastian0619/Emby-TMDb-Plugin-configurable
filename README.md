# 🎬 Emby-TMDb-Plugin-configurable

这是一个基于Emby官方MovieDb插件反编译并修改的版本。主要目的是为了让网络连通性较差的Emby用户能够自定义TMDB的API地址和密钥,从而获得更好的使用体验。

## ✨ 主要特性

- 🔧 支持自定义TMDB API基础URL
- 🖼️ 支持自定义TMDB图片服务器URL 
- 🏠 支持自定义TMDB主页URL
- 🔑 支持自定义API密钥
- ⭐ 保留了原版插件的所有功能

## ⚙️ 配置说明

插件提供以下配置项:

1. TMDB API 基础URL
   - 默认值: https://api.themoviedb.org
   - 可以配置为自己的代理地址

2. TMDB 图片基础URL  
   - 默认值: https://image.tmdb.org/t/p
   - 可以配置为自己的图片代理地址

3. TMDB 主页URL
   - 默认值: https://www.themoviedb.org
   - 可以配置为自己的网页代理地址

4. API Key
   - 默认值为EMBY官方插件的API密钥
   - 可以配置为自己申请的TMDB API密钥

## 📥 安装方法

1. 下载发布页面的最新版本插件
2. 将插件文件放入Emby的插件目录
3. 重启Emby服务
4. 在Emby管理页面的"插件"部分配置插件参数

### 📂 常见插件目录

#### Docker 环境
- 插件目录通常映射为: `/config/plugins/`
- 请确保目录权限正确

#### Windows 环境
- 默认目录: `C:\ProgramData\Emby-Server\plugins\`
- 便携版目录: `[Emby程序目录]\programdata\plugins\`

#### Linux 环境
- 默认目录: `/var/lib/emby/plugins/`
- 自定义安装目录: `[emby-server目录]/plugins/`

#### 群晖 NAS
- 套件中心安装目录: `/volume1/@appstore/EmbyServer/plugins/`
- Docker安装同Docker环境配置

## ⚠️ 注意事项

- 本插件基于Emby官方MovieDb插件修改,仅添加了配置相关功能
- 使用自定义地址时请确保地址的可用性
- 建议在修改配置前先备份原配置
- 确保插件目录具有正确的读写权限

## 📢 免责声明

本插件仅供学习交流使用,请遵守相关法律法规。使用本插件所造成的任何问题由使用者自行承担。

## 🙏 致谢

感谢EMBY官方开发团队提供的优秀插件基础。

