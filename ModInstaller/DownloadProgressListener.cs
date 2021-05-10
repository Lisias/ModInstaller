using System;
namespace ModInstaller
{
	public interface DownloadProgressListener
	{
		void Progress(long bytesReceived, long totalBytesToReceive, long elapsedMilliseconds);
		void Completed(long sizeInBytes, long timeInMilliseconds);
		void Aborted(Exception e);
		void Cancelled();
	}
}
