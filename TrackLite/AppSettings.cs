using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices.Sensors;

namespace TrackLite
{
    public static class AppSettings
    {
        private const int DEFAULT_FREQUENCIA = 1;
        private const int DEFAULT_ACCURACY = 50;
        private const bool DEFAULT_VIBRACAO = true;

        public static int FrequenciaColeta
        {
            get
            {
                var value = Preferences.Get("FrequenciaColeta", DEFAULT_FREQUENCIA);
                Console.WriteLine($"Lendo FrequenciaColeta: {value}");

                return Math.Max(1, Math.Min(60, value));
            }
            set
            {
                Console.WriteLine($"Salvando FrequenciaColeta: {value}");
                Preferences.Set("FrequenciaColeta", value);
            }
        }

        public static int LimiarAccuracy
        {
            get
            {
                var value = Preferences.Get("LimiarAccuracy", DEFAULT_ACCURACY);
                Console.WriteLine($"Lendo LimiarAccuracy: {value}");
                return Math.Max(1, Math.Min(100, value));
            }
            set
            {
                Console.WriteLine($"Salvando LimiarAccuracy: {value}");
                Preferences.Set("LimiarAccuracy", value);
            }
        }

        public static bool VibracaoKm
        {
            get
            {
                var value = Preferences.Get("VibracaoKm", DEFAULT_VIBRACAO);
                Console.WriteLine($"Lendo VibracaoKm: {value}");
                return value;
            }
            set
            {
                Console.WriteLine($"Salvando VibracaoKm: {value}");
                Preferences.Set("VibracaoKm", value);
            }
        }

        public static double GetAccuracyInMeters()
        {
            return LimiarAccuracy switch
            {
                <= 33 => 50.0,
                <= 66 => 20.0,
                _ => 5.0
            };
        }

        public static GeolocationAccuracy GetGeolocationAccuracy()
        {
            return LimiarAccuracy switch
            {
                <= 33 => GeolocationAccuracy.Low,
                <= 66 => GeolocationAccuracy.Medium,
                _ => GeolocationAccuracy.High
            };
        }

        public static void VerificarConfiguracoes()
        {
            Console.WriteLine("=== VERIFICAÇÃO DE CONFIGURAÇÕES ===");
            Console.WriteLine($"FrequenciaColeta existe: {Preferences.ContainsKey("FrequenciaColeta")}");
            Console.WriteLine($"LimiarAccuracy existe: {Preferences.ContainsKey("LimiarAccuracy")}");
            Console.WriteLine($"VibracaoKm existe: {Preferences.ContainsKey("VibracaoKm")}");
            Console.WriteLine($"FrequenciaColeta: {FrequenciaColeta}");
            Console.WriteLine($"LimiarAccuracy: {LimiarAccuracy}");
            Console.WriteLine($"VibracaoKm: {VibracaoKm}");
            Console.WriteLine("=====================================");
        }
    }
}
