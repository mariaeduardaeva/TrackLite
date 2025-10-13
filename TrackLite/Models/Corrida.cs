using SQLite;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        public string TemposPorKmJson { get; set; } = "[]";
        public bool Lixeira { get; set; } = false;

        public string Title { get; set; } = "Corrida";
        public string Description { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [Ignore, JsonIgnore]
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

        [Ignore, JsonIgnore]
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

        [Ignore, JsonIgnore]
        public string DataFormatada => Data.ToString("dd/MM/yyyy HH:mm");
    }
}