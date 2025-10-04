using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices.Sensors;

namespace TrackLite
{
    public static class AppSettings
    {
        public static int FrequenciaColeta
        {
            get => Preferences.Get("FrequenciaColeta", 1); 
            set => Preferences.Set("FrequenciaColeta", value);
        }

        public static int LimiarAccuracy
        {
            get => Preferences.Get("LimiarAccuracy", 50);
            set => Preferences.Set("LimiarAccuracy", value);
        }

        public static bool VibracaoKm
        {
            get => Preferences.Get("VibracaoKm", true); 
            set => Preferences.Set("VibracaoKm", value);
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
    }
}