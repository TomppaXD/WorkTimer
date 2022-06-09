using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkTimer
{
    public class LogEntry
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Type { get; set; }
        public string Title { get; set; } = "Unknown";
        public string ProcessName { get; set; }
    }
}
