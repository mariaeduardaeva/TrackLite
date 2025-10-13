using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace TrackLite.Models
{
    public class Ponto
    {
        public double lat { get; set; }
        public double lng { get; set; }
        public double accuracy { get; set; } = 0;
        public DateTime timestamp { get; set; } = DateTime.Now;
    }
}
