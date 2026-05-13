using System;
using System.Collections.Generic;

namespace CameraTimelapseMod.Data
{
    public class FilterOpts
    {
        public string Prefix;
        public DateTime? BeforeDate;
        public DateTime? AfterDate;
        public List<string> Ignore = new List<string>();
        public int MaxCount;
    }
}