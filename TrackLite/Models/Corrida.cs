using SQLite;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace TrackLite.Models
{
    public class Ponto
    {
        public double lat { get; set; }
        public double lng { get; set; }
        public double accuracy { get; set; } = 0;
        public DateTime timestamp { get; set; } = DateTime.Now;
    }

    public class Corrida
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public DateTime Data { get; set; }
        public string Distancia { get; set; }
        public string Ritmo { get; set; }
        public string TempoDecorrido { get; set; }
        public string RotaJson { get; set; }
        public string TemposPorKmJson { get; set; } = "[]";

        public bool Lixeira { get; set; } = false;

        [Ignore]
        public List<Ponto> Rota
        {
            get
            {
                if (string.IsNullOrWhiteSpace(RotaJson))
                    return new List<Ponto>();
                try
                {
                    return JsonSerializer.Deserialize<List<Ponto>>(RotaJson) ?? new List<Ponto>();
                }
                catch
                {
                    return new List<Ponto>();
                }
            }
            set
            {
                RotaJson = JsonSerializer.Serialize(value);
            }
        }

        [Ignore]
        public List<TimeSpan> TemposPorKm
        {
            get
            {
                if (string.IsNullOrWhiteSpace(TemposPorKmJson))
                    return new List<TimeSpan>();
                try
                {
                    return JsonSerializer.Deserialize<List<TimeSpan>>(TemposPorKmJson) ?? new List<TimeSpan>();
                }
                catch
                {
                    return new List<TimeSpan>();
                }
            }
            set
            {
                TemposPorKmJson = JsonSerializer.Serialize(value);
            }
        }

        [Ignore]
        public string DataFormatada => Data.ToString("dd/MM/yyyy HH:mm");
    }
}