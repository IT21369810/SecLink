using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SecLinkApp
{
    public class UploadProgressInfo
    {
        public string FileName { get; set; }
        public long BytesSent { get; set; }
        public long TotalBytes { get; set; }
        public double Percentage { get; set; }
        public string TimeRemaining { get; set; }

        public int FileIndex { get; set; }
    }
}
