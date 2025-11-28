using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace HvsMvp.App
{
    /// <summary>
    /// Representa um exemplo de treinamento (partícula rotulada manualmente).
    /// </summary>
    public class TrainingExample
    {
        /// <summary>
        /// ID único do exemplo.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// ID da análise original onde a partícula foi identificada.
        /// </summary>
        public Guid AnalysisId { get; set; }

        /// <summary>
        /// ID da partícula original (se disponível).
        /// </summary>
        public Guid? OriginalParticleId { get; set; }

        /// <summary>
        /// Rótulo atribuído manualmente pelo usuário.
        /// </summary>
        public string ManualLabel { get; set; } = string.Empty;

        /// <summary>
        /// Classificação automática original (antes da correção manual).
        /// </summary>
        public string? OriginalClassification { get; set; }

        /// <summary>
        /// Confiança da classificação automática original.
        /// </summary>
        public double OriginalConfidence { get; set; }

        /// <summary>
        /// Coordenada X do centro da partícula.
        /// </summary>
        public int CenterX { get; set; }

        /// <summary>
        /// Coordenada Y do centro da partícula.
        /// </summary>
        public int CenterY { get; set; }

        /// <summary>
        /// Área aproximada em pixels.
        /// </summary>
        public int AreaPixels { get; set; }

        /// <summary>
        /// Valores HSV médios da partícula.
        /// </summary>
        public double AvgH { get; set; }
        public double AvgS { get; set; }
        public double AvgV { get; set; }

        /// <summary>
        /// Data/hora UTC da rotulação.
        /// </summary>
        public DateTime LabeledAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Nome do operador que rotulou (se disponível).
        /// </summary>
        public string? LabeledBy { get; set; }

        /// <summary>
        /// Caminho relativo da imagem de recorte (se exportada).
        /// </summary>
        public string? CropImagePath { get; set; }

        /// <summary>
        /// Notas/comentários adicionais.
        /// </summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Sessão de treinamento contendo múltiplos exemplos.
    /// </summary>
    public class TrainingSession
    {
        public Guid SessionId { get; set; } = Guid.NewGuid();
        public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? EndedAtUtc { get; set; }
        public string? OperatorName { get; set; }
        public List<TrainingExample> Examples { get; set; } = new List<TrainingExample>();
    }

    /// <summary>
    /// Serviço para modo de treinamento/QA básico.
    /// Permite rotulação manual de partículas para ground truth.
    /// </summary>
    public class TrainingModeService
    {
        private TrainingSession? _currentSession;
        private readonly string _trainingDataDir;

        /// <summary>
        /// Lista de materiais disponíveis para rotulação.
        /// </summary>
        public static readonly string[] AvailableLabels = new[]
        {
            "Au",           // Ouro
            "Pt",           // Platina
            "PGM",          // Outros PGMs
            "Sulfeto",      // Sulfetos
            "Silicato",     // Silicatos
            "Ganga",        // Material sem valor
            "Quartzo",      // Quartzo
            "Pirita",       // Pirita
            "Outros",       // Outros materiais
            "Incerto",      // Incerto/não classificável
            "Artefato"      // Artefato/ruído
        };

        public TrainingModeService()
        {
            _trainingDataDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "datasets",
                "training-data");
        }

        /// <summary>
        /// Indica se há uma sessão de treinamento ativa.
        /// </summary>
        public bool IsSessionActive => _currentSession != null;

        /// <summary>
        /// Obtém a sessão atual.
        /// </summary>
        public TrainingSession? CurrentSession => _currentSession;

        /// <summary>
        /// Inicia uma nova sessão de treinamento.
        /// </summary>
        public void StartSession(string? operatorName = null)
        {
            if (_currentSession != null)
            {
                EndSession();
            }

            _currentSession = new TrainingSession
            {
                OperatorName = operatorName
            };

            Directory.CreateDirectory(_trainingDataDir);
        }

        /// <summary>
        /// Finaliza a sessão atual e exporta os dados.
        /// </summary>
        public string? EndSession()
        {
            if (_currentSession == null)
                return null;

            _currentSession.EndedAtUtc = DateTime.UtcNow;

            string? exportPath = null;
            if (_currentSession.Examples.Count > 0)
            {
                exportPath = ExportSession(_currentSession);
            }

            _currentSession = null;
            return exportPath;
        }

        /// <summary>
        /// Adiciona um exemplo de treinamento a partir de uma partícula.
        /// </summary>
        public void AddExample(
            ParticleRecord particle,
            string manualLabel,
            Guid analysisId,
            string? operatorName = null,
            string? notes = null)
        {
            if (_currentSession == null)
            {
                StartSession(operatorName);
            }

            var example = new TrainingExample
            {
                AnalysisId = analysisId,
                OriginalParticleId = particle.ParticleId,
                ManualLabel = manualLabel,
                OriginalClassification = particle.MaterialId,
                OriginalConfidence = particle.Confidence,
                CenterX = particle.CenterX,
                CenterY = particle.CenterY,
                AreaPixels = particle.ApproxAreaPixels,
                AvgH = particle.AvgH,
                AvgS = particle.AvgS,
                AvgV = particle.AvgV,
                LabeledBy = operatorName ?? _currentSession!.OperatorName,
                Notes = notes
            };

            _currentSession!.Examples.Add(example);
        }

        /// <summary>
        /// Adiciona um exemplo de treinamento a partir de coordenadas clicadas.
        /// </summary>
        public void AddExampleFromClick(
            int x, int y,
            string manualLabel,
            FullSceneAnalysis scene,
            string? operatorName = null,
            string? notes = null)
        {
            if (_currentSession == null)
            {
                StartSession(operatorName);
            }

            // Encontrar partícula próxima às coordenadas
            ParticleRecord? nearestParticle = null;
            double minDist = double.MaxValue;

            foreach (var p in scene.Summary.Particles)
            {
                double dist = Math.Sqrt(
                    Math.Pow(p.CenterX - x, 2) +
                    Math.Pow(p.CenterY - y, 2));

                if (dist < minDist)
                {
                    minDist = dist;
                    nearestParticle = p;
                }
            }

            if (nearestParticle != null && minDist < 100) // Tolerância de 100 pixels
            {
                AddExample(nearestParticle, manualLabel, scene.Summary.Id, operatorName, notes);
            }
            else
            {
                // Criar exemplo a partir do pixel/região clicada
                var example = new TrainingExample
                {
                    AnalysisId = scene.Summary.Id,
                    ManualLabel = manualLabel,
                    CenterX = x,
                    CenterY = y,
                    LabeledBy = operatorName ?? _currentSession!.OperatorName,
                    Notes = notes
                };

                // Tentar obter informações do pixel
                if (x >= 0 && y >= 0 && x < scene.Width && y < scene.Height)
                {
                    var lbl = scene.Labels[x, y];
                    if (lbl != null)
                    {
                        example.OriginalClassification = lbl.MaterialId;
                        example.OriginalConfidence = lbl.MaterialConfidence;
                        example.AvgH = lbl.H;
                        example.AvgS = lbl.S;
                        example.AvgV = lbl.V;
                    }
                }

                _currentSession!.Examples.Add(example);
            }
        }

        /// <summary>
        /// Exporta a sessão para arquivos CSV e JSON.
        /// </summary>
        private string ExportSession(TrainingSession session)
        {
            Directory.CreateDirectory(_trainingDataDir);

            string timestamp = session.StartedAtUtc.ToString("yyyyMMdd_HHmmss");
            string baseName = $"training_{timestamp}_{session.SessionId:N}";

            // Exportar JSON
            string jsonPath = Path.Combine(_trainingDataDir, baseName + ".json");
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(session, jsonOptions);
            File.WriteAllText(jsonPath, json, Encoding.UTF8);

            // Exportar CSV
            string csvPath = Path.Combine(_trainingDataDir, baseName + ".csv");
            ExportToCsv(session, csvPath);

            return jsonPath;
        }

        /// <summary>
        /// Exporta sessão para CSV.
        /// </summary>
        private void ExportToCsv(TrainingSession session, string path)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine("Id,AnalysisId,ParticleId,ManualLabel,OriginalClassification,OriginalConfidence,CenterX,CenterY,AreaPixels,AvgH,AvgS,AvgV,LabeledAtUtc,LabeledBy,Notes");

            // Data
            foreach (var ex in session.Examples)
            {
                sb.AppendLine(
                    $"{ex.Id}," +
                    $"{ex.AnalysisId}," +
                    $"{ex.OriginalParticleId?.ToString() ?? ""}," +
                    $"\"{EscapeCsv(ex.ManualLabel)}\"," +
                    $"\"{EscapeCsv(ex.OriginalClassification ?? "")}\"," +
                    $"{ex.OriginalConfidence:F4}," +
                    $"{ex.CenterX}," +
                    $"{ex.CenterY}," +
                    $"{ex.AreaPixels}," +
                    $"{ex.AvgH:F2}," +
                    $"{ex.AvgS:F4}," +
                    $"{ex.AvgV:F4}," +
                    $"{ex.LabeledAtUtc:O}," +
                    $"\"{EscapeCsv(ex.LabeledBy ?? "")}\"," +
                    $"\"{EscapeCsv(ex.Notes ?? "")}\"");
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Escapa string para CSV.
        /// </summary>
        private static string EscapeCsv(string s)
        {
            return s.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");
        }

        /// <summary>
        /// Obtém estatísticas da sessão atual.
        /// </summary>
        public (int total, Dictionary<string, int> byLabel) GetSessionStats()
        {
            if (_currentSession == null)
                return (0, new Dictionary<string, int>());

            var byLabel = new Dictionary<string, int>();
            foreach (var ex in _currentSession.Examples)
            {
                if (!byLabel.ContainsKey(ex.ManualLabel))
                    byLabel[ex.ManualLabel] = 0;
                byLabel[ex.ManualLabel]++;
            }

            return (_currentSession.Examples.Count, byLabel);
        }

        /// <summary>
        /// Carrega sessões de treinamento existentes.
        /// </summary>
        public List<TrainingSession> LoadExistingSessions()
        {
            var sessions = new List<TrainingSession>();

            if (!Directory.Exists(_trainingDataDir))
                return sessions;

            foreach (var file in Directory.GetFiles(_trainingDataDir, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var session = JsonSerializer.Deserialize<TrainingSession>(json);
                    if (session != null)
                        sessions.Add(session);
                }
                catch
                {
                    // Ignorar arquivos corrompidos
                }
            }

            return sessions;
        }
    }
}
