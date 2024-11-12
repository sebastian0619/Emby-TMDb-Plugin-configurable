using System;
using System.Threading;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;

namespace MovieDb.Security;

public class PluginStartup : IServerEntryPoint, IDisposable
{
	private readonly IServerApplicationHost applicationHost;

	private readonly CancellationTokenSource cancellationTokenSource;

	public PluginStartup(IServerApplicationHost applicationHost)
	{
		this.applicationHost = applicationHost;
		cancellationTokenSource = new CancellationTokenSource();
	}

	public void Dispose()
	{
		cancellationTokenSource.Cancel();
	}

	public void Run()
	{
		new Detector(applicationHost).Run(cancellationTokenSource.Token);
		new Actor(applicationHost).CheckStartupLock();
	}
}
