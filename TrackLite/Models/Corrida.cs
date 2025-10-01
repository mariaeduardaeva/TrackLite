using SQLite;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace TrackLite.Models
{
    public class Corrida
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public DateTime Data { get; set; }
        public string Distancia { get; set; }
        public string Ritmo { get; set; }
        public string TempoDecorrido { get; set; }
        public string RotaJson { get; set; }

        public bool Lixeira { get; set; } = false;

        [Ignore]
        public List<(double lat, double lng)> Rota
        {
            get
            {
                if (string.IsNullOrWhiteSpace(RotaJson))
                    return new List<(double lat, double lng)>();
                return JsonSerializer.Deserialize<List<(double lat, double lng)>>(RotaJson);
            }
            set
            {
                RotaJson = JsonSerializer.Serialize(value);
            }
        }

        [Ignore]
        public string DataFormatada => Data.ToString("dd/MM/yyyy HH:mm");
    }
}