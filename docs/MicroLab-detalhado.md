# MicroLab HVS-MVP - Documentação Técnica Detalhada

## 1. Arquitetura do Sistema

### 1.1 Componentes Principais

O MicroLab HVS-MVP é composto pelos seguintes serviços principais:

#### SampleMaskService
Responsável pela segmentação da amostra, separando grãos metálicos do fundo claro.

**Algoritmo:**
1. Estima a cor de fundo a partir da borda da imagem
2. Calcula gradiente local (detecção de bordas)
3. Compõe índice adaptativo: `idx = (255 - gray) * TextureWeight + grad * GradientWeight`
4. Calcula limiar adaptativo: `threshold = clamp(mean + k * std, MinThreshold, MaxThreshold)`
5. Remove componentes tocando a borda (BFS/DFS)
6. Filtra regiões pequenas (remoção de poeira)
7. Fecha pequenos buracos em regiões grandes

**Parâmetros configuráveis:**
- `TextureWeight`: Peso do componente de textura (0.5)
- `GradientWeight`: Peso do componente de gradiente (0.5)
- `StdMultiplier`: Multiplicador do desvio padrão no limiar (0.5)
- `MinThreshold`: Limiar mínimo (30)
- `MaxThreshold`: Limiar máximo (180)
- `MinRegionSize`: Tamanho mínimo de região em pixels (100)

#### HvsAnalysisService
Núcleo de classificação de materiais usando análise de cores HSV.

**Fluxo de análise:**
1. Obtém máscara de amostra via SampleMaskService
2. Para cada pixel de amostra:
   - Converte RGB para HSV
   - Aplica heurísticas de detecção de ouro (LooksLikeGold)
   - Aplica heurísticas de detecção de PGM (LooksLikePgm)
   - Avalia scores contra todos os materiais configurados
   - Classifica como Metal, Cristal ou Gema
3. Agrega estatísticas (contagem de pixels, porcentagens, PPM estimado)
4. Calcula diagnósticos de imagem (foco, clipping, fração de amostra)

### 1.2 Heurísticas de Detecção

#### Detecção de Ouro (LooksLikeGold)

```csharp
// Critérios para pixel de ouro:
- Hue: 35° a 75° (faixa amarela/dourada)
- Saturation: > 15% (não é cinza)
- Value: 25% a 98% (brilho razoável)
- avgRG > B + 10 (tons quentes)
- |R - G| < 60 (não muito vermelho nem verde)
- R >= 100 ou G >= 80 (mínimo de luminosidade)
- R >= B * 1.2 e G >= B * 1.1 (tons amarelados)
```

#### Detecção de PGM (LooksLikePgm)

```csharp
// Critérios para pixel de PGM (Platina e grupo):
- Saturation: < 20% (cinza metálico)
- Value: 20% a 95% (brilho metálico)
- max(R,G,B) - min(R,G,B) < 40 (neutro)
- Evita brancos puros e pretos
```

### 1.3 Sistema de Scoring

Cada pixel classificado recebe um score baseado em:
- `HueScore`: Proximidade ao Hue central do material
- `SaturationScore`: Proximidade à faixa de saturação
- `ValueScore`: Proximidade à faixa de brilho
- `Score = (HueScore + SatScore + ValScore) / 3`

Scores mínimos para classificação:
- Score padrão: 0.45
- Score com boost de ouro: 0.85
- Score com boost de PGM: 0.70

## 2. Fluxo Completo de Laboratório

### 2.1 Configuração Inicial

1. **Sessão de trabalho**: Definir projeto, amostra, cliente, operador
2. **Configuração de câmera**: Índice, resolução (640x480 a 1920x1080)
3. **Calibração de branco**: Ajuste de balanço de cores (placeholder)
4. **Calibração de escala**: Definir µm/pixel (placeholder)

### 2.2 Aquisição de Imagem

**Via arquivo:**
- Formatos suportados: PNG, JPG, JPEG, BMP, TIF, TIFF
- Botão "📂 Abrir imagem"

**Via câmera (Live):**
- Botão "▶ Live" inicia captura
- Botão "⏹ Parar" encerra
- Serviço: MicroscopeCameraService + OpenCvCameraService

### 2.3 Análise

**Análise única:**
- Botão "🧪 Analisar"
- Executa HvsAnalysisService.AnalyzeScene()
- Atualiza UI com resultados

**Análise contínua:**
- Botão "⚙ Contínuo" inicia loop de análise
- Intervalo configurável (padrão: 800ms)
- Botão "⏸ Parar contínuo" encerra

### 2.4 Exploração Visual

- **🎨 Máscara**: Mostra máscara verde/preto
- **🖼 Fundo mascarado**: Imagem com fundo azul translúcido
- **🔍 Zoom +/-**: Ampliação/redução (0.125x a 8x)
- **Tooltip HVS**: Informações por pixel ao mover mouse

### 2.5 Resultados

Os resultados são exibidos em três listas:
- **Metais**: Nome, ID, Grupo, %Sample, PPM, Score
- **Cristais**: Nome, ID, %Sample, Score
- **Gemas**: Nome, ID, %Sample, Score

### 2.6 Exportação

**TXT (Laudo):**
```
═══════════════════════════════════════════
       RESUMO DA ANÁLISE HVS-MVP
═══════════════════════════════════════════

Data/Hora (UTC): 2024-01-15 10:30:45
ID da Análise:   abc123-...

─── DIAGNÓSTICOS ───
  Foco (0..1):       0.456
  Clipping:          2.50%
  Fração amostra:    15.30%

─── METAIS (top 5) ───
  • Ouro (Au) [Nobre]
      12.3456% · 123456 ppm · score=0.85
```

**JSON:**
```json
{
  "id": "...",
  "utc": "2024-01-15T10:30:45Z",
  "diagnostics": {
    "focus": 0.456,
    "clipping": 0.025,
    "foreground": 0.153
  },
  "metals": [
    {"id": "Au", "name": "Ouro", "pct": 0.123456, "ppm": 123456, "score": 0.85}
  ]
}
```

**CSV:**
```csv
Tipo,Id,Nome,Grupo,PctSample,PPM,Score
Metal,Au,Ouro,Nobre,0.123456,123456,0.85
```

## 3. Configuração de Materiais

O arquivo `hvs-config.json` define:

### 3.1 Estrutura de Material

```json
{
  "id": "Au",              // Identificador único
  "nome": "Ouro",          // Nome de exibição
  "grupo": "Nobre",        // Grupo (Nobre, PGM, comum, etc.)
  "optico": {
    "brilho_lustre": "muito alto",
    "cor_hsv": {
      "h": [40, 65],       // Faixa de Hue (0-360)
      "s": [0.20, 1.0],    // Faixa de Saturation (0-1)
      "v": [0.30, 1.0]     // Faixa de Value (0-1)
    },
    "fluorescencia_uv": "muito baixa"
  }
}
```

### 3.2 Faixas HSV Típicas

| Material | Hue | Saturação | Valor |
|----------|-----|-----------|-------|
| Ouro (Au) | 40-65 | 0.20-1.0 | 0.30-1.0 |
| Prata (Ag) | 0-10 | 0.0-0.15 | 0.85-1.0 |
| Platina (Pt) | any | 0.0-0.15 | 0.40-0.85 |
| Cobre (Cu) | 15-25 | 0.5-0.9 | 0.5-0.95 |
| Quartzo | any | 0.0-0.20 | 0.70-1.0 |

## 4. Diagnósticos de Qualidade

### 4.1 FocusScore (Foco)
- Baseado em gradiente médio na amostra
- Valores: 0.0 (desfocado) a 1.0 (nítido)
- Recomendado: > 0.3

### 4.2 SaturationClippingFraction
- Porcentagem de pixels saturados (muito claro ou escuro)
- Valores: 0.0% a 100%
- Ideal: < 5%

### 4.3 ForegroundFraction (Fração de Amostra)
- Porcentagem de pixels classificados como amostra
- Valores típicos: 5% a 50%
- Muito baixo: pode indicar fundo uniforme
- Muito alto: pode indicar máscara imprecisa

## 5. Extensibilidade

### 5.1 Adicionar Novos Materiais

Edite `hvs-config.json` na seção `materials`:
1. Adicione entrada em `metais`, `cristais` ou `gemas`
2. Defina `id`, `nome`, `grupo` e `optico.cor_hsv`
3. Reinicie a aplicação

### 5.2 Ajustar Heurísticas

Modifique em `HvsAnalysisService.cs`:
- `LooksLikeGold()`: Critérios para ouro
- `LooksLikePgm()`: Critérios para PGM
- Propriedades configuráveis: `GoldBoostScore`, `PgmBoostScore`

### 5.3 Ajustar Segmentação

Modifique em `SampleMaskService`:
- Propriedades: `TextureWeight`, `GradientWeight`, `MinThreshold`, etc.
- Métodos auxiliares: `CloseSmallHoles()`, `FilterSmallRegions()`

## 6. Limitações e Considerações

### 6.1 Limitações Conhecidas
- Análise baseada apenas em cor (sem espectroscopia)
- Requer iluminação consistente
- Melhor desempenho com lâminas de fundo claro

### 6.2 Fatores que Afetam Precisão
- Qualidade da iluminação
- Foco da imagem
- Contaminação da amostra
- Oxidação de metais

### 6.3 Recomendações
- Use iluminação brightfield uniforme
- Garanta foco adequado antes de analisar
- Limpe a lâmina antes da análise
- Calibre o balanço de branco periodicamente

## 7. Glossário

- **HSV**: Hue (matiz), Saturation (saturação), Value (brilho)
- **PGM**: Platinum Group Metals (Pt, Pd, Rh, Ir, Ru, Os)
- **PPM**: Parts Per Million (partes por milhão)
- **BFS/DFS**: Breadth/Depth-First Search (algoritmos de busca em grafo)
- **Segmentação**: Separação de amostra vs. fundo
- **Máscara**: Imagem binária indicando pixels de amostra
