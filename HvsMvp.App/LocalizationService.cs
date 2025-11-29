using System;
using System.Collections.Generic;
using System.Globalization;

namespace HvsMvp.App
{
    /// <summary>
    /// PR17: Centralized localization service for multi-language support.
    /// Supports PT-BR, EN-US, ES-ES, FR-FR, AR, ZH-CN.
    /// </summary>
    public class LocalizationService
    {
        private static LocalizationService? _instance;
        private string _currentLocale = "pt-BR";
        
        /// <summary>
        /// Supported locales with display names.
        /// </summary>
        public static readonly Dictionary<string, string> SupportedLocales = new()
        {
            ["pt-BR"] = "Português (Brasil)",
            ["en-US"] = "English (US)",
            ["es-ES"] = "Español",
            ["fr-FR"] = "Français",
            ["ar"] = "العربية",
            ["zh-CN"] = "中文 (简体)"
        };
        
        /// <summary>
        /// All translations organized by locale and key.
        /// </summary>
        private readonly Dictionary<string, Dictionary<string, string>> _translations;
        
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static LocalizationService Instance
        {
            get
            {
                _instance ??= new LocalizationService();
                return _instance;
            }
        }
        
        /// <summary>
        /// Current active locale.
        /// </summary>
        public string CurrentLocale
        {
            get => _currentLocale;
            set
            {
                if (SupportedLocales.ContainsKey(value))
                {
                    _currentLocale = value;
                    LocaleChanged?.Invoke(this, value);
                }
            }
        }
        
        /// <summary>
        /// Event fired when locale changes.
        /// </summary>
        public event EventHandler<string>? LocaleChanged;
        
        private LocalizationService()
        {
            _translations = BuildTranslations();
        }
        
        /// <summary>
        /// Get translated string for a key.
        /// Returns key if translation not found.
        /// </summary>
        public string Get(string key)
        {
            if (_translations.TryGetValue(_currentLocale, out var localeStrings))
            {
                if (localeStrings.TryGetValue(key, out var value))
                    return value;
            }
            
            // Fallback to pt-BR
            if (_translations.TryGetValue("pt-BR", out var fallbackStrings))
            {
                if (fallbackStrings.TryGetValue(key, out var value))
                    return value;
            }
            
            return key;
        }
        
        /// <summary>
        /// Get translated string with format parameters.
        /// </summary>
        public string Get(string key, params object[] args)
        {
            var template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }
        
        /// <summary>
        /// Build all translations.
        /// </summary>
        private Dictionary<string, Dictionary<string, string>> BuildTranslations()
        {
            return new Dictionary<string, Dictionary<string, string>>
            {
                ["pt-BR"] = BuildPortugueseBrazil(),
                ["en-US"] = BuildEnglishUS(),
                ["es-ES"] = BuildSpanish(),
                ["fr-FR"] = BuildFrench(),
                ["ar"] = BuildArabic(),
                ["zh-CN"] = BuildChineseSimplified()
            };
        }
        
        private Dictionary<string, string> BuildPortugueseBrazil()
        {
            return new Dictionary<string, string>
            {
                // Window titles
                ["title"] = "TGC Metal Analítico · HVS-MVP",
                ["title.analysis"] = "Análise de Metais",
                ["title.settings"] = "Configurações",
                ["title.about"] = "Sobre",
                ["title.welcome"] = "Bem-vindo",
                
                // Material categories
                ["metals"] = "Metais",
                ["crystals"] = "Cristais",
                ["gems"] = "Gemas",
                
                // Status messages
                ["status.ready"] = "Pronto · HVS-MVP carregado",
                ["status.analyzing"] = "Analisando...",
                ["status.complete"] = "Análise concluída",
                ["status.live.on"] = "Câmera ativa",
                ["status.live.off"] = "Câmera parada",
                ["status.continuous"] = "Análise contínua ativa",
                ["status.error"] = "Erro: {0}",
                ["status.image.loaded"] = "Imagem carregada",
                ["status.frame.frozen"] = "Frame congelado",
                
                // Main buttons
                ["btn.open"] = "📂 Abrir imagem",
                ["btn.live"] = "▶ Live",
                ["btn.stop"] = "⏹ Parar",
                ["btn.analyze"] = "🧪 Analisar",
                ["btn.cont"] = "⚙ Contínuo",
                ["btn.cont.stop"] = "⏸ Parar contínuo",
                
                // Visualization buttons
                ["btn.mask"] = "🎨 Máscara",
                ["btn.mask.bg"] = "🖼 Fundo mascarado",
                ["btn.phase.map"] = "🗺 Mapa de Fases",
                ["btn.heatmap"] = "🔥 Heatmap Alvo",
                ["btn.brightpoints"] = "✨ Pontos brilhantes",
                ["btn.selective"] = "🎯 Seletiva",
                
                // Tool buttons
                ["btn.training"] = "🎯 Modo Treino",
                ["btn.ai"] = "🔬 Partículas / Dataset IA",
                ["btn.zoom.in"] = "🔍 Zoom +",
                ["btn.zoom.out"] = "🔎 Zoom -",
                ["btn.wb"] = "⚪ Balanço de branco",
                ["btn.scale"] = "📏 Escala",
                ["btn.camera"] = "🎥 Câmera...",
                ["btn.res"] = "⚙️ Resolução...",
                ["btn.uv"] = "🔮 Modo UV",
                ["btn.roi"] = "⬜ ROI",
                ["btn.roi.clear"] = "❌ Limpar ROI",
                ["btn.image.controls"] = "🎚️ Controles de imagem",
                
                // Export buttons
                ["btn.txt"] = "📝 Laudo TXT",
                ["btn.pdf"] = "📄 Laudo PDF",
                ["btn.json"] = "{} JSON",
                ["btn.csv"] = "📊 CSV",
                ["btn.bi.csv"] = "📈 BI CSV",
                ["btn.export.ia"] = "🤖 Dataset IA",
                ["btn.whatsapp"] = "💬 WhatsApp",
                
                // System buttons
                ["btn.qa.panel"] = "✅ QA Partículas",
                ["btn.debug"] = "🛠 Debug HVS",
                ["btn.calib"] = "📸 Calibrar (auto)",
                ["btn.settings"] = "⚙️ Configurações",
                ["btn.about"] = "ℹ️ Sobre",
                
                // Labels
                ["label.target"] = "Alvo:",
                ["label.file"] = "ARQUIVO:",
                ["label.camera"] = "CÂMERA:",
                ["label.analysis"] = "ANÁLISE:",
                ["label.zoom"] = "ZOOM:",
                ["label.view"] = "VER:",
                ["label.selective"] = "SELETIVA:",
                ["label.export"] = "EXPORTAR:",
                ["label.report"] = "LAUDO:",
                ["label.utils"] = "UTIL:",
                ["label.system"] = "SIS:",
                ["label.log"] = "📋 Log / Console",
                ["label.origin"] = "ORIGEM:",
                ["label.mode"] = "MODO:",
                ["label.focus"] = "FOCO:",
                ["label.mask.status"] = "MÁSCARA:",
                
                // Analysis results
                ["result.gold"] = "Ouro (Au)",
                ["result.platinum"] = "Platina (Pt)",
                ["result.silver"] = "Prata (Ag)",
                ["result.copper"] = "Cobre (Cu)",
                ["result.iron"] = "Ferro (Fe)",
                ["result.palladium"] = "Paládio (Pd)",
                ["result.rhodium"] = "Ródio (Rh)",
                ["result.indeterminate"] = "Indeterminado",
                ["result.confidence.high"] = "Alta confiança",
                ["result.confidence.medium"] = "Média confiança",
                ["result.confidence.low"] = "Baixa confiança",
                ["result.confidence.indeterminate"] = "Indeterminado",
                
                // Quality indicators
                ["quality.official"] = "Oficial",
                ["quality.preliminary"] = "Preliminar",
                ["quality.invalid"] = "Inválido",
                ["quality.review"] = "Requer revisão",
                ["quality.ok"] = "OK",
                ["quality.attention"] = "Atenção",
                ["quality.bad"] = "Ruim",
                
                // Messages
                ["msg.no.image"] = "Nenhuma imagem carregada",
                ["msg.no.analysis"] = "Execute uma análise primeiro",
                ["msg.gold.detected"] = "Ouro detectado: {0:P2}",
                ["msg.gold.not.detected"] = "Ouro não detectado com confiança suficiente",
                ["msg.analysis.complete"] = "Análise completa - {0} metais, {1} cristais, {2} gemas",
                ["msg.export.success"] = "Exportado com sucesso: {0}",
                ["msg.export.error"] = "Erro ao exportar: {0}",
                ["msg.live.started"] = "Live iniciado - câmera {0}, {1}x{2}",
                ["msg.live.stopped"] = "Live parado - Frame congelado para análise",
                ["msg.image.loaded"] = "Imagem carregada: {0}",
                ["msg.tools.enabled"] = "Ferramentas de suporte ativadas. Pronto para análise.",
                ["msg.checklist.ok"] = "Checklist pré-operação: todos os itens OK.",
                
                // Menu items
                ["menu.file"] = "📁 Arquivo",
                ["menu.file.open"] = "📂 Abrir imagem...",
                ["menu.file.recent"] = "📋 Arquivos recentes",
                ["menu.file.save.log"] = "💾 Salvar log...",
                ["menu.file.clear.log"] = "🗑 Limpar log",
                ["menu.file.settings"] = "⚙️ Configurações...",
                ["menu.file.exit"] = "❌ Sair",
                ["menu.camera"] = "🎥 Câmera",
                ["menu.camera.start"] = "▶️ Iniciar Live",
                ["menu.camera.stop"] = "⏹️ Parar Live",
                ["menu.camera.select"] = "🎥 Selecionar câmera...",
                ["menu.camera.resolution"] = "📐 Selecionar resolução...",
                ["menu.camera.wb"] = "⚪ Balanço de branco",
                ["menu.camera.calibrate"] = "📸 Calibrar (snapshot)",
                ["menu.analysis"] = "🧪 Análise",
                ["menu.analysis.run"] = "🧪 Analisar",
                ["menu.analysis.continuous"] = "⚙️ Análise contínua",
                ["menu.analysis.stop.continuous"] = "⏸️ Parar contínua",
                ["menu.analysis.selective"] = "🎯 Análise seletiva",
                ["menu.analysis.visualizations"] = "👁️ Visualizações",
                ["menu.analysis.mask"] = "🎨 Máscara",
                ["menu.analysis.background"] = "🖼️ Fundo mascarado",
                ["menu.analysis.phase.map"] = "🗺️ Mapa de fases",
                ["menu.analysis.heatmap"] = "🔥 Heatmap do alvo",
                ["menu.analysis.debug"] = "🛠️ Debug HVS...",
                ["menu.reports"] = "📄 Relatórios",
                ["menu.reports.pdf"] = "📄 Exportar PDF...",
                ["menu.reports.txt"] = "📝 Exportar TXT...",
                ["menu.reports.whatsapp"] = "💬 Compartilhar WhatsApp",
                ["menu.reports.view.last"] = "👁️ Ver último relatório",
                ["menu.reports.open.folder"] = "📂 Abrir pasta de relatórios",
                ["menu.reports.json"] = "{} Exportar JSON",
                ["menu.reports.csv"] = "📊 Exportar CSV",
                ["menu.reports.bi.csv"] = "📈 Exportar BI CSV",
                ["menu.reports.ia.dataset"] = "🤖 Exportar Dataset IA",
                ["menu.reports.open.datasets"] = "📁 Abrir pasta datasets",
                ["menu.wizards"] = "🧙 Assistentes",
                ["menu.wizards.gold"] = "🥇 Análise de Ouro (Au) com Live",
                ["menu.wizards.image"] = "📷 Análise de Imagem com Laudo",
                ["menu.wizards.checklist"] = "📋 Verificar Checklist de Sistema",
                ["menu.tools"] = "🔧 Ferramentas",
                ["menu.tools.qa"] = "✅ QA de Partículas...",
                ["menu.tools.training"] = "🎯 Modo treino",
                ["menu.tools.scale"] = "📏 Ferramenta de escala",
                ["menu.tools.zoom.in"] = "🔍 Zoom +",
                ["menu.tools.zoom.out"] = "🔍 Zoom -",
                ["menu.tools.export.config"] = "💾 Exportar configurações...",
                ["menu.tools.import.config"] = "📥 Importar configurações...",
                ["menu.tools.export.logs"] = "📋 Exportar logs de sessão...",
                ["menu.help"] = "❓ Ajuda",
                ["menu.help.about"] = "ℹ️ Sobre...",
                ["menu.help.updates"] = "🔄 Verificar atualizações...",
                
                // Wizard labels
                ["wizard.gold.title"] = "Assistente: Análise de Ouro (Au)",
                ["wizard.step.source"] = "Fonte da Imagem",
                ["wizard.step.sample"] = "Amostra",
                ["wizard.step.info"] = "Informações",
                ["wizard.step.analysis"] = "Análise e Laudo",
                
                // Confidence indicators
                ["confidence.indicator"] = "Indicador de Confiança",
                ["confidence.very.high"] = "Muito alta (> 85%)",
                ["confidence.high"] = "Alta (68-85%)",
                ["confidence.medium"] = "Média (48-68%)",
                ["confidence.low"] = "Baixa (35-48%)",
                ["confidence.indeterminate"] = "Indeterminado (< 35%)",
                
                // Report labels
                ["report.gold.indicator"] = "🥇 INDICADOR DE OURO (Au)",
                ["report.gold.score"] = "Score: {0:F3} | Confiança: {1}",
                ["report.gold.fraction"] = "Fração: {0:P4} | PPM: {1}",
                ["report.gold.high.confidence"] = "✅ Detecção de ALTA CONFIANÇA - Ouro identificado com segurança",
                ["report.gold.medium.confidence"] = "⚠️ Detecção de MÉDIA CONFIANÇA - Provável ouro, confirmar com análise adicional",
                ["report.gold.low.confidence"] = "⚠️ Detecção de BAIXA CONFIANÇA - Possível ouro, recomenda-se verificação",
                ["report.gold.indeterminate"] = "❌ Detecção INDETERMINADA - Não foi possível confirmar ouro nesta análise",
                ["report.metals.detected"] = "METAIS DETECTADOS",
                ["report.table.metal"] = "Metal",
                ["report.table.score"] = "Score",
                ["report.table.confidence"] = "Confiança",
                ["report.table.sample.pct"] = "% Amostra",
                ["report.table.ppm"] = "PPM",
                ["report.table.group"] = "Grupo",
                ["report.confidence.very.high"] = "Muito Alta",
                ["report.confidence.high"] = "Alta",
                ["report.confidence.medium"] = "Média",
                ["report.confidence.low"] = "Baixa",
                ["report.confidence.indet"] = "Indet.",
                
                // Dialog buttons
                ["dialog.ok"] = "OK",
                ["dialog.cancel"] = "Cancelar",
                ["dialog.yes"] = "Sim",
                ["dialog.no"] = "Não",
                ["dialog.save"] = "Salvar",
                ["dialog.open"] = "Abrir",
                ["dialog.close"] = "Fechar",
                
                // Welcome screen
                ["welcome.title"] = "TGC Metal Analítico – HVS-MVP",
                ["welcome.subtitle"] = "HVS · IA · Microscopia Metalúrgica · Laudos Automatizados",
                ["welcome.new.image"] = "Nova análise de imagem",
                ["welcome.new.image.desc"] = "Carregar imagem de amostra para análise detalhada",
                ["welcome.live"] = "Análise ao vivo",
                ["welcome.live.desc"] = "Iniciar captura com análise em tempo real (câmera)",
                ["welcome.explore"] = "Explorar amostras",
                ["welcome.explore.desc"] = "Abrir pasta de amostras, laudos e exports",
                ["welcome.skip.checkbox"] = "Não mostrar ao iniciar (modo operador)",
                ["welcome.go.direct"] = "Ir direto para a interface principal",
                ["welcome.initial.settings"] = "Configurações iniciais",
                ["welcome.status.ready"] = "Sistema pronto",
                ["welcome.status.check"] = "Verificar configuração",
                
                // Settings form
                ["settings.title"] = "Configurações",
                ["settings.general"] = "Geral",
                ["settings.camera"] = "Câmera",
                ["settings.analysis"] = "Análise",
                ["settings.reports"] = "Relatórios",
                ["settings.updates"] = "Atualizações",
                ["settings.interface"] = "Interface",
                ["settings.profile"] = "Perfil",
                
                // Error messages
                ["error.camera.not.found"] = "Câmera não encontrada",
                ["error.image.load.failed"] = "Erro ao carregar imagem",
                ["error.analysis.failed"] = "Erro na análise",
                ["error.export.failed"] = "Erro ao exportar"
            };
        }
        
        private Dictionary<string, string> BuildEnglishUS()
        {
            return new Dictionary<string, string>
            {
                // Window titles
                ["title"] = "TGC Metal Analytics · HVS-MVP",
                ["title.analysis"] = "Metal Analysis",
                ["title.settings"] = "Settings",
                ["title.about"] = "About",
                
                // Material categories
                ["metals"] = "Metals",
                ["crystals"] = "Crystals",
                ["gems"] = "Gems",
                
                // Status messages
                ["status.ready"] = "Ready · HVS-MVP loaded",
                ["status.analyzing"] = "Analyzing...",
                ["status.complete"] = "Analysis complete",
                ["status.live.on"] = "Camera active",
                ["status.live.off"] = "Camera stopped",
                ["status.continuous"] = "Continuous analysis active",
                ["status.error"] = "Error: {0}",
                
                // Main buttons
                ["btn.open"] = "📂 Open image",
                ["btn.live"] = "▶ Live",
                ["btn.stop"] = "⏹ Stop",
                ["btn.analyze"] = "🧪 Analyze",
                ["btn.cont"] = "⚙ Continuous",
                ["btn.cont.stop"] = "⏸ Stop continuous",
                
                // Visualization buttons
                ["btn.mask"] = "🎨 Mask",
                ["btn.mask.bg"] = "🖼 Background masked",
                ["btn.phase.map"] = "🗺 Phase Map",
                ["btn.heatmap"] = "🔥 Target Heatmap",
                ["btn.brightpoints"] = "✨ Bright points",
                ["btn.selective"] = "🎯 Selective",
                
                // Tool buttons
                ["btn.training"] = "🎯 Training Mode",
                ["btn.ai"] = "🔬 Particles / AI Dataset",
                ["btn.zoom.in"] = "🔍 Zoom +",
                ["btn.zoom.out"] = "🔎 Zoom -",
                ["btn.wb"] = "⚪ White balance",
                ["btn.scale"] = "📏 Scale",
                ["btn.camera"] = "🎥 Camera...",
                ["btn.res"] = "⚙️ Resolution...",
                ["btn.uv"] = "🔮 UV Mode",
                ["btn.roi"] = "⬜ ROI",
                ["btn.roi.clear"] = "❌ Clear ROI",
                ["btn.image.controls"] = "🎚️ Image controls",
                
                // Export buttons
                ["btn.txt"] = "📝 TXT Report",
                ["btn.pdf"] = "📄 PDF Report",
                ["btn.json"] = "{} JSON",
                ["btn.csv"] = "📊 CSV",
                ["btn.bi.csv"] = "📈 BI CSV",
                ["btn.export.ia"] = "🤖 AI Dataset",
                ["btn.whatsapp"] = "💬 WhatsApp",
                
                // System buttons
                ["btn.qa.panel"] = "✅ QA Particles",
                ["btn.debug"] = "🛠 HVS Debug",
                ["btn.calib"] = "📸 Calibrate (auto)",
                ["btn.settings"] = "⚙️ Settings",
                ["btn.about"] = "ℹ️ About",
                
                // Labels
                ["label.target"] = "Target:",
                ["label.file"] = "FILE:",
                ["label.camera"] = "CAMERA:",
                ["label.analysis"] = "ANALYSIS:",
                ["label.zoom"] = "ZOOM:",
                ["label.view"] = "VIEW:",
                ["label.selective"] = "SELECTIVE:",
                ["label.export"] = "EXPORT:",
                ["label.report"] = "REPORT:",
                ["label.utils"] = "UTILS:",
                ["label.system"] = "SYS:",
                
                // Analysis results
                ["result.gold"] = "Gold (Au)",
                ["result.platinum"] = "Platinum (Pt)",
                ["result.silver"] = "Silver (Ag)",
                ["result.copper"] = "Copper (Cu)",
                ["result.indeterminate"] = "Indeterminate",
                ["result.confidence.high"] = "High confidence",
                ["result.confidence.medium"] = "Medium confidence",
                ["result.confidence.low"] = "Low confidence",
                ["result.confidence.indeterminate"] = "Indeterminate",
                
                // Quality indicators
                ["quality.official"] = "Official",
                ["quality.preliminary"] = "Preliminary",
                ["quality.invalid"] = "Invalid",
                ["quality.review"] = "Needs review",
                
                // Messages
                ["msg.no.image"] = "No image loaded",
                ["msg.no.analysis"] = "Run an analysis first",
                ["msg.gold.detected"] = "Gold detected: {0:P2}",
                ["msg.gold.not.detected"] = "Gold not detected with sufficient confidence",
                ["msg.analysis.complete"] = "Analysis complete - {0} metals, {1} crystals, {2} gems",
                ["msg.export.success"] = "Exported successfully: {0}",
                ["msg.export.error"] = "Export error: {0}",
                
                // Menu items
                ["menu.file"] = "📁 File",
                ["menu.camera"] = "🎥 Camera",
                ["menu.analysis"] = "🧪 Analysis",
                ["menu.reports"] = "📄 Reports",
                ["menu.wizards"] = "🧙 Wizards",
                ["menu.tools"] = "🔧 Tools",
                ["menu.help"] = "❓ Help",
                
                // Wizard labels
                ["wizard.gold.title"] = "Wizard: Gold (Au) Analysis",
                ["wizard.step.source"] = "Image Source",
                ["wizard.step.sample"] = "Sample",
                ["wizard.step.info"] = "Information",
                ["wizard.step.analysis"] = "Analysis and Report",
                
                // Confidence indicators
                ["confidence.indicator"] = "Confidence Indicator",
                ["confidence.very.high"] = "Very high (> 85%)",
                ["confidence.high"] = "High (72-85%)",
                ["confidence.medium"] = "Medium (52-72%)",
                ["confidence.low"] = "Low (38-52%)",
                ["confidence.indeterminate"] = "Indeterminate (< 38%)",
                
                // Report labels
                ["report.gold.indicator"] = "🥇 GOLD INDICATOR (Au)",
                ["report.gold.score"] = "Score: {0:F3} | Confidence: {1}",
                ["report.gold.fraction"] = "Fraction: {0:P4} | PPM: {1}",
                ["report.gold.high.confidence"] = "✅ HIGH CONFIDENCE Detection - Gold identified reliably",
                ["report.gold.medium.confidence"] = "⚠️ MEDIUM CONFIDENCE Detection - Likely gold, confirm with additional analysis",
                ["report.gold.low.confidence"] = "⚠️ LOW CONFIDENCE Detection - Possible gold, verification recommended",
                ["report.gold.indeterminate"] = "❌ INDETERMINATE Detection - Could not confirm gold in this analysis",
                ["report.metals.detected"] = "METALS DETECTED",
                ["report.table.metal"] = "Metal",
                ["report.table.score"] = "Score",
                ["report.table.confidence"] = "Confidence",
                ["report.table.sample.pct"] = "% Sample",
                ["report.table.ppm"] = "PPM",
                ["report.table.group"] = "Group",
                ["report.confidence.very.high"] = "Very High",
                ["report.confidence.high"] = "High",
                ["report.confidence.medium"] = "Medium",
                ["report.confidence.low"] = "Low",
                ["report.confidence.indet"] = "Indet."
            };
        }
        
        private Dictionary<string, string> BuildSpanish()
        {
            return new Dictionary<string, string>
            {
                // Window titles
                ["title"] = "TGC Análisis de Metales · HVS-MVP",
                ["title.analysis"] = "Análisis de Metales",
                ["title.settings"] = "Configuración",
                ["title.about"] = "Acerca de",
                
                // Material categories
                ["metals"] = "Metales",
                ["crystals"] = "Cristales",
                ["gems"] = "Gemas",
                
                // Status messages
                ["status.ready"] = "Listo · HVS-MVP cargado",
                ["status.analyzing"] = "Analizando...",
                ["status.complete"] = "Análisis completado",
                ["status.live.on"] = "Cámara activa",
                ["status.live.off"] = "Cámara detenida",
                ["status.continuous"] = "Análisis continuo activo",
                ["status.error"] = "Error: {0}",
                
                // Main buttons
                ["btn.open"] = "📂 Abrir imagen",
                ["btn.live"] = "▶ En vivo",
                ["btn.stop"] = "⏹ Detener",
                ["btn.analyze"] = "🧪 Analizar",
                ["btn.cont"] = "⚙ Continuo",
                ["btn.cont.stop"] = "⏸ Detener continuo",
                
                // Visualization buttons
                ["btn.mask"] = "🎨 Máscara",
                ["btn.mask.bg"] = "🖼 Fondo enmascarado",
                ["btn.phase.map"] = "🗺 Mapa de Fases",
                ["btn.heatmap"] = "🔥 Mapa de calor",
                ["btn.brightpoints"] = "✨ Puntos brillantes",
                ["btn.selective"] = "🎯 Selectivo",
                
                // Labels
                ["label.target"] = "Objetivo:",
                ["label.file"] = "ARCHIVO:",
                ["label.camera"] = "CÁMARA:",
                ["label.analysis"] = "ANÁLISIS:",
                
                // Analysis results
                ["result.gold"] = "Oro (Au)",
                ["result.platinum"] = "Platino (Pt)",
                ["result.silver"] = "Plata (Ag)",
                ["result.copper"] = "Cobre (Cu)",
                ["result.indeterminate"] = "Indeterminado",
                ["result.confidence.high"] = "Alta confianza",
                ["result.confidence.medium"] = "Media confianza",
                ["result.confidence.low"] = "Baja confianza",
                
                // Quality indicators
                ["quality.official"] = "Oficial",
                ["quality.preliminary"] = "Preliminar",
                ["quality.invalid"] = "Inválido",
                
                // Menu items
                ["menu.file"] = "📁 Archivo",
                ["menu.camera"] = "🎥 Cámara",
                ["menu.analysis"] = "🧪 Análisis",
                ["menu.reports"] = "📄 Informes",
                ["menu.tools"] = "🔧 Herramientas",
                ["menu.help"] = "❓ Ayuda"
            };
        }
        
        private Dictionary<string, string> BuildFrench()
        {
            return new Dictionary<string, string>
            {
                // Window titles
                ["title"] = "TGC Analyse des Métaux · HVS-MVP",
                ["title.analysis"] = "Analyse des Métaux",
                ["title.settings"] = "Paramètres",
                ["title.about"] = "À propos",
                
                // Material categories
                ["metals"] = "Métaux",
                ["crystals"] = "Cristaux",
                ["gems"] = "Gemmes",
                
                // Status messages
                ["status.ready"] = "Prêt · HVS-MVP chargé",
                ["status.analyzing"] = "Analyse en cours...",
                ["status.complete"] = "Analyse terminée",
                ["status.live.on"] = "Caméra active",
                ["status.live.off"] = "Caméra arrêtée",
                ["status.continuous"] = "Analyse continue active",
                ["status.error"] = "Erreur: {0}",
                
                // Main buttons
                ["btn.open"] = "📂 Ouvrir image",
                ["btn.live"] = "▶ En direct",
                ["btn.stop"] = "⏹ Arrêter",
                ["btn.analyze"] = "🧪 Analyser",
                ["btn.cont"] = "⚙ Continu",
                ["btn.cont.stop"] = "⏸ Arrêter continu",
                
                // Visualization buttons
                ["btn.mask"] = "🎨 Masque",
                ["btn.mask.bg"] = "🖼 Fond masqué",
                ["btn.phase.map"] = "🗺 Carte des phases",
                ["btn.heatmap"] = "🔥 Carte thermique",
                ["btn.brightpoints"] = "✨ Points lumineux",
                ["btn.selective"] = "🎯 Sélectif",
                
                // Labels
                ["label.target"] = "Cible:",
                ["label.file"] = "FICHIER:",
                ["label.camera"] = "CAMÉRA:",
                ["label.analysis"] = "ANALYSE:",
                
                // Analysis results
                ["result.gold"] = "Or (Au)",
                ["result.platinum"] = "Platine (Pt)",
                ["result.silver"] = "Argent (Ag)",
                ["result.copper"] = "Cuivre (Cu)",
                ["result.indeterminate"] = "Indéterminé",
                ["result.confidence.high"] = "Haute confiance",
                ["result.confidence.medium"] = "Confiance moyenne",
                ["result.confidence.low"] = "Faible confiance",
                
                // Quality indicators
                ["quality.official"] = "Officiel",
                ["quality.preliminary"] = "Préliminaire",
                ["quality.invalid"] = "Invalide",
                
                // Menu items
                ["menu.file"] = "📁 Fichier",
                ["menu.camera"] = "🎥 Caméra",
                ["menu.analysis"] = "🧪 Analyse",
                ["menu.reports"] = "📄 Rapports",
                ["menu.tools"] = "🔧 Outils",
                ["menu.help"] = "❓ Aide"
            };
        }
        
        private Dictionary<string, string> BuildArabic()
        {
            return new Dictionary<string, string>
            {
                // Window titles
                ["title"] = "TGC تحليل المعادن · HVS-MVP",
                ["title.analysis"] = "تحليل المعادن",
                ["title.settings"] = "الإعدادات",
                ["title.about"] = "حول",
                
                // Material categories
                ["metals"] = "المعادن",
                ["crystals"] = "البلورات",
                ["gems"] = "الأحجار الكريمة",
                
                // Status messages
                ["status.ready"] = "جاهز · HVS-MVP محمل",
                ["status.analyzing"] = "جاري التحليل...",
                ["status.complete"] = "اكتمل التحليل",
                ["status.live.on"] = "الكاميرا نشطة",
                ["status.live.off"] = "الكاميرا متوقفة",
                
                // Main buttons
                ["btn.open"] = "📂 فتح صورة",
                ["btn.live"] = "▶ مباشر",
                ["btn.stop"] = "⏹ إيقاف",
                ["btn.analyze"] = "🧪 تحليل",
                
                // Labels
                ["label.target"] = "الهدف:",
                ["label.file"] = "ملف:",
                ["label.camera"] = "كاميرا:",
                ["label.analysis"] = "تحليل:",
                
                // Analysis results
                ["result.gold"] = "ذهب (Au)",
                ["result.platinum"] = "بلاتين (Pt)",
                ["result.silver"] = "فضة (Ag)",
                ["result.indeterminate"] = "غير محدد",
                ["result.confidence.high"] = "ثقة عالية",
                ["result.confidence.medium"] = "ثقة متوسطة",
                ["result.confidence.low"] = "ثقة منخفضة",
                
                // Menu items
                ["menu.file"] = "📁 ملف",
                ["menu.camera"] = "🎥 كاميرا",
                ["menu.analysis"] = "🧪 تحليل",
                ["menu.help"] = "❓ مساعدة"
            };
        }
        
        private Dictionary<string, string> BuildChineseSimplified()
        {
            return new Dictionary<string, string>
            {
                // Window titles
                ["title"] = "TGC 金属分析 · HVS-MVP",
                ["title.analysis"] = "金属分析",
                ["title.settings"] = "设置",
                ["title.about"] = "关于",
                
                // Material categories
                ["metals"] = "金属",
                ["crystals"] = "晶体",
                ["gems"] = "宝石",
                
                // Status messages
                ["status.ready"] = "就绪 · HVS-MVP 已加载",
                ["status.analyzing"] = "分析中...",
                ["status.complete"] = "分析完成",
                ["status.live.on"] = "相机已启动",
                ["status.live.off"] = "相机已停止",
                
                // Main buttons
                ["btn.open"] = "📂 打开图像",
                ["btn.live"] = "▶ 实时",
                ["btn.stop"] = "⏹ 停止",
                ["btn.analyze"] = "🧪 分析",
                ["btn.cont"] = "⚙ 连续",
                
                // Labels
                ["label.target"] = "目标:",
                ["label.file"] = "文件:",
                ["label.camera"] = "相机:",
                ["label.analysis"] = "分析:",
                
                // Analysis results
                ["result.gold"] = "金 (Au)",
                ["result.platinum"] = "铂 (Pt)",
                ["result.silver"] = "银 (Ag)",
                ["result.copper"] = "铜 (Cu)",
                ["result.indeterminate"] = "不确定",
                ["result.confidence.high"] = "高置信度",
                ["result.confidence.medium"] = "中置信度",
                ["result.confidence.low"] = "低置信度",
                
                // Quality indicators
                ["quality.official"] = "正式",
                ["quality.preliminary"] = "初步",
                ["quality.invalid"] = "无效",
                
                // Menu items
                ["menu.file"] = "📁 文件",
                ["menu.camera"] = "🎥 相机",
                ["menu.analysis"] = "🧪 分析",
                ["menu.reports"] = "📄 报告",
                ["menu.tools"] = "🔧 工具",
                ["menu.help"] = "❓ 帮助"
            };
        }
    }
}
