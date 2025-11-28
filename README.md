# MicroLab HVS-MVP

Sistema profissional de análise microscópica de metais, cristais e gemas com foco em detecção de metais nobres (Ouro, Platina, PGMs).

## Visão Geral

O MicroLab HVS-MVP é uma aplicação Windows Forms para análise óptica de amostras minerais utilizando microscopia digital. O sistema realiza:

- **Segmentação automática** de amostra vs. fundo utilizando algoritmos adaptativos
- **Classificação de metais** baseada em análise de cores HSV com heurísticas especializadas para Au (Ouro) e Pt/PGM (Platina e metais do grupo da platina)
- **Identificação de cristais e gemas** através de assinaturas ópticas
- **Diagnósticos de qualidade** da imagem (foco, clipping, fração de amostra)
- **Exportação de resultados** em TXT, JSON e CSV

## Requisitos do Sistema

### Software
- Windows 10/11 (64-bit)
- .NET 8.0 Runtime ou SDK
- OpenCvSharp4 (incluído via NuGet)

### Hardware Recomendado
- Microscópio com câmera USB ou HDMI
- Resolução mínima: 1280x720 (recomendado: 1920x1080)
- Iluminação adequada (brightfield recomendado)

## Como Compilar

```bash
# Clone o repositório
git clone https://github.com/trilhagoldcapital-gif/HvsMvpSuite.git
cd HvsMvpSuite

# Restaurar pacotes e compilar
dotnet restore
dotnet build

# Executar (apenas em Windows)
dotnet run --project HvsMvp.App
```

## Fluxo Básico de Uso

### 1. Carregar Imagem
- Clique em **📂 Abrir imagem** para selecionar uma imagem de amostra (PNG, JPG, BMP, TIFF)
- Ou utilize o modo **▶ Live** para captura em tempo real da câmera

### 2. Analisar
- Clique em **🧪 Analisar** para executar a análise completa
- A máscara de segmentação será calculada automaticamente
- A classificação de metais, cristais e gemas será exibida

### 3. Visualizar Resultados
- Use **🎨 Máscara** para alternar a visualização da máscara de amostra
- Use **🖼 Fundo mascarado** para ver a imagem com fundo destacado em azul
- Os resultados aparecem nas listas de Metais, Cristais e Gemas

### 4. Exportar
- **📝 TXT**: Relatório resumido em texto
- **{} JSON**: Dados completos em formato JSON
- **📊 CSV**: Dados tabulares para análise em planilhas

## Estrutura do Projeto

```
HvsMvpSuite/
├── HvsMvp.App/                       # Aplicação principal WinForms
│   ├── MainForm.cs                   # Interface principal
│   ├── HvsAnalysisService.cs         # Núcleo de análise HVS (metais/cristais/gemas)
│   ├── SampleMaskService.cs          # Serviço de segmentação de amostra
│   ├── SampleMaskClass.cs            # Modelo de máscara por pixel
│   ├── SampleFullAnalysisResult.cs   # Modelos de resultado de análise
│   ├── FullSceneAnalysis.cs          # Contêiner de análise de cena completa
│   ├── PixelLabel.cs                 # Rótulo por pixel (material, confiança, HSV)
│   ├── ParticleRecord.cs             # Registro de partícula/cluster
│   ├── VisualizationService.cs       # Renderização de máscaras e overlays
│   ├── MicroscopeCameraService.cs    # Captura de vídeo via OpenCvSharp
│   ├── ContinuousAnalysisController.cs # Análise contínua em background
│   ├── ImageDiagnosticsService.cs    # Diagnósticos de qualidade de imagem
│   ├── HvsConfig.cs                  # Modelos de configuração JSON
│   ├── hvs-config.json               # Configuração de materiais e parâmetros
│   └── ...
├── HvsMvp.Debug/                     # Projeto de debug/testes
├── docs/                             # Documentação técnica
│   └── MicroLab-detalhado.md         # Documentação técnica detalhada
└── _deprecated_off/                  # Código depreciado (não compilado)
```

## Configuração de Materiais

O arquivo `hvs-config.json` define os materiais detectáveis e suas características ópticas:

```json
{
  "materials": {
    "metais": [
      {
        "id": "Au",
        "nome": "Ouro",
        "grupo": "Nobre",
        "optico": {
          "cor_hsv": {
            "h": [40, 65],      // Matiz (amarelo/dourado)
            "s": [0.20, 1.0],   // Saturação
            "v": [0.30, 1.0]    // Valor (brilho)
          }
        }
      }
    ]
  }
}
```

## Heurísticas de Detecção

### Ouro (Au)
- Matiz na faixa amarela (35-75°)
- Saturação moderada a alta (>15%)
- Canais R+G dominam sobre B
- Brilho alto (V > 25%)

### Platina e PGMs
- Saturação muito baixa (<20%)
- Aparência cinza metálica
- R, G, B próximos entre si (neutro)
- Brilho moderado a alto

## Licença

Este projeto é proprietário da TGC (Trilha Gold Capital).

## Suporte

Para suporte técnico ou dúvidas, entre em contato com a equipe de desenvolvimento.
