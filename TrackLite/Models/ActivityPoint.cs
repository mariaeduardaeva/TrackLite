using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrackLite.Models
{
    public class ActivityPoint
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int ActivityId { get; set; } 

        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Accuracy { get; set; }
        public DateTime Timestamp { get; set; }
        public int Sequence { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}