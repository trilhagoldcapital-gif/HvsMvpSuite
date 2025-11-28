using System;
using System.IO;
using System.Text.Json;

namespace HvsMvp.Debug
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hvs-config.json");
                Console.WriteLine("Lendo JSON em: " + path);

                var json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<HvsMvp.App.HvsConfig>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                Console.WriteLine();
                Console.WriteLine("== Contagem via desserialização C# ==");
                Console.WriteLine("Metais (C#):   " + (cfg?.Materials?.Metais?.Count ?? 0));
                Console.WriteLine("Cristais (C#): " + (cfg?.Materials?.Cristais?.Count ?? 0));
                Console.WriteLine("Gemas (C#):    " + (cfg?.Materials?.Gemas?.Count ?? 0));
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERRO ao ler/desserializar JSON:");
                Console.WriteLine(ex);
            }

            Console.WriteLine();
            Console.WriteLine("Pressione ENTER para sair...");
            Console.ReadLine();
        }
    }
}
