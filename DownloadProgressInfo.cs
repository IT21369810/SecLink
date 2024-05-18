using System;

namespace SecLinkApp
{
    public class DownloadProgressInfo
    {
        public string FileName { get; set; }
        public long BytesReceived { get; set; }
        public long TotalBytes { get; set; }
        public double Percentage => TotalBytes > 0 ? (double)BytesReceived / TotalBytes * 100 : 0;
        public DateTime StartTime { get; set; } = DateTime.Now;
        public TimeSpan TimeElapsed => DateTime.Now - StartTime;
        public double Speed => TimeElapsed.TotalSeconds > 0 ? BytesReceived / TimeElapsed.TotalSeconds : 0; // bytes per second
        public TimeSpan EstimatedTimeRemaining => Speed > 0 ? TimeSpan.FromSeconds((TotalBytes - BytesReceived) / Speed) : TimeSpan.Zero;
    }
}
