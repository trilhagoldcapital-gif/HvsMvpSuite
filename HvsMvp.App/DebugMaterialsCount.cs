using System;
using System.IO;
using System.Text.Json;

namespace HvsMvp.App
{
    internal static class DebugMaterialsCount
    {
        public static void Run()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hvs-config.json");
            var json = File.ReadAllText(path);
            var cfg = JsonSerializer.Deserialize<HvsConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Console.WriteLine("Metais:   " + (cfg?.Materials?.Metais?.Count ?? 0));
            Console.WriteLine("Cristais: " + (cfg?.Materials?.Cristais?.Count ?? 0));
            Console.WriteLine("Gemas:    " + (cfg?.Materials?.Gemas?.Count ?? 0));
        }
    }
}
