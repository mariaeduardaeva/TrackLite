using System.Collections.ObjectModel;
using System.Collections.Generic;
using TrackLite.Models;

namespace TrackLite.Models
{
    public class CorridaGroup : ObservableCollection<Corrida>
    {
        public string DataChave { get; }

        public CorridaGroup(string dataChave, IEnumerable<Corrida> corridas) : base(corridas)
        {
            DataChave = dataChave;
        }
    }
}