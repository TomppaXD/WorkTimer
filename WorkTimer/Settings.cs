using System.Collections.Generic;

namespace WorkTimer
{
    public class Settings
    {
        public int InactivityTresholdMinutes { get; set; }
        public string LogPath { get; set; }
        public List<CategorySettings> Categories { get; set; } = new List<CategorySettings>();
    }
}
