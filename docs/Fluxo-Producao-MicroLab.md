# Fluxo de Produção - MicroLab HVS-MVP

## Índice
1. [Introdução](#1-introdução)
2. [Configuração Inicial](#2-configuração-inicial)
3. [Fluxo de Análise](#3-fluxo-de-análise)
4. [Geração de Laudos](#4-geração-de-laudos)
5. [Exportação BI](#5-exportação-bi)
6. [Dataset IA e QA](#6-dataset-ia-e-qa)
7. [Checklist de Qualidade](#7-checklist-de-qualidade)
8. [Troubleshooting](#8-troubleshooting)

---

## 1. Introdução

O **MicroLab HVS-MVP** é um sistema de análise mineralógica por imagem projetado para laboratórios de prospecção de metais preciosos. Esta documentação descreve o fluxo completo de uso em ambiente de produção.

### Requisitos do Sistema
- Windows 10/11 (64-bit)
- .NET 8.0 Runtime
- Câmera USB compatível (opcional, para modo Live)
- Monitor com resolução mínima de 1280x720

### Estrutura de Diretórios
```
MicroLab/
├── exports/           # Laudos e exportações
│   ├── reports/       # Laudos TXT e PDF
│   ├── bi/           # CSVs para Power BI
│   └── dataset-ia/   # Datasets para treinamento IA
├── sessions/          # Sessões de trabalho
├── images/            # Imagens de amostra
├── logs/              # Logs do sistema
└── datasets/          # Dados de calibração
```

---

## 2. Configuração Inicial

### 2.1 Primeiro Acesso

1. **Abrir Configurações**: Clique no botão `⚙️ Configurações` na barra de ferramentas.

2. **Aba Geral**:
   - Configure o **Diretório de imagens** onde as amostras serão salvas
   - Configure o **Diretório de laudos** para exportação de relatórios
   - Configure o **Diretório de sessões** para salvar sessões de trabalho
   - Configure o **Diretório de logs** para arquivos de log

3. **Aba Câmera**:
   - Selecione o **Índice da câmera** (normalmente 0 ou 1)
   - Escolha a **Resolução preferida** (recomendado: 1920x1080)

4. **Aba Análise**:
   - Ajuste a **Sensibilidade da máscara** (padrão: 0.30)
   - Configure o **Limiar de foco mínimo** (padrão: 0.15)
   - Configure o **Limiar de clipping** (padrão: 0.025)

5. **Aba Perfil**:
   - Preencha o **Nome do laboratório**
   - Configure o caminho do **Logo** (opcional, para PDFs)
   - Defina o **Operador padrão**
   - Configure o **Contato WhatsApp** para compartilhamento

6. Clique em **💾 Salvar**

### 2.2 Seleção de Idioma

O MicroLab suporta múltiplos idiomas:
- Português (pt-BR) - padrão
- English (en-US)
- Español (es-ES)
- Français (fr-FR)
- العربية (ar)
- 中文 (zh-CN)

Para trocar o idioma, clique no botão **Idioma ▾** na barra superior.

---

## 3. Fluxo de Análise

### 3.1 Análise de Imagem Estática

1. **Carregar imagem**: Clique em `📂 Abrir imagem`
2. Selecione a imagem da amostra (formatos: PNG, JPG, BMP, TIFF)
3. A imagem será exibida no painel central
4. **Executar análise**: Clique em `🧪 Analisar`
5. Aguarde o processamento (alguns segundos)
6. Os resultados aparecerão nas listas de Metais, Cristais e Gemas

### 3.2 Análise Live (Câmera)

1. **Iniciar câmera**: Clique em `▶ Live`
2. Posicione a amostra sob o microscópio
3. Ajuste o foco visualmente
4. **Capturar e analisar**: Clique em `🧪 Analisar`
5. **Parar câmera**: Clique em `⏹ Parar`

### 3.3 Análise Contínua

Para monitoramento contínuo de amostras:

1. Inicie o modo Live
2. Clique em `⚙ Contínuo`
3. O sistema analisará automaticamente a cada ~800ms
4. Para parar, clique em `⏸ Parar contínuo`

### 3.4 Análise Seletiva

Para focar em um material específico:

1. Execute uma análise normal primeiro
2. Selecione o material alvo no combo **Alvo:**
3. Clique em `🎯 Análise seletiva`
4. O sistema destacará apenas pixels do material selecionado

---

## 4. Geração de Laudos

### 4.1 Laudo TXT (Texto)

O laudo TXT contém:
- **Cabeçalho**: Nome do laboratório, ID, amostra, operador, data
- **Resumo Executivo**: Principais metais e status de qualidade
- **Seção Metais**: Tabela com Score, %, PPM e Grupo
- **Seção Minerais**: Tabela de cristais e gemas
- **Seção Qualidade**: FocusScore, Exposição, Máscara, avisos

**Para gerar:**
1. Execute uma análise
2. Clique em `📝 TXT`
3. O arquivo será salvo em `exports/reports/`

### 4.2 Laudo PDF

O laudo PDF é formatado para impressão profissional:

**Para gerar:**
1. Execute uma análise
2. Clique em `📝 TXT` e depois em `📄 PDF` (ou use o menu de exportação)
3. O PDF será salvo em `exports/reports/`

### 4.3 Localização dos Arquivos

Os laudos são salvos com o padrão:
```
exports/reports/laudo_YYYYMMDD_HHMMSS_<ID>.txt
exports/reports/laudo_YYYYMMDD_HHMMSS_<ID>.pdf
```

### 4.4 Compartilhamento

- **WhatsApp**: Clique em `📱 WhatsApp` para abrir o WhatsApp Web com mensagem pré-preenchida
- **E-mail**: Anexe o arquivo TXT ou PDF manualmente
- **LIMS**: Importe o JSON ou CSV para sistemas externos

---

## 5. Exportação BI

### 5.1 Visão Geral

O export BI gera um CSV consolidado com uma linha por análise, ideal para:
- Power BI
- Excel
- Tableau
- Outros sistemas de BI

### 5.2 Gerando Export BI

1. Execute uma análise
2. Clique em `📈 BI CSV`
3. O arquivo será adicionado ao CSV diário em `exports/bi/`

### 5.3 Estrutura do CSV BI

| Campo | Descrição |
|-------|-----------|
| AnalysisId | ID único da análise (GUID) |
| DateTimeUtc | Data/hora UTC (ISO 8601) |
| Sample | Nome da amostra |
| ClientProject | Cliente/projeto |
| Operator | Operador |
| CaptureMode | Modo (Image/Live/Continuous) |
| ReportStatus | Status (Official/Preliminary/Invalid) |
| QualityIndex | Índice 0-100 |
| FocusScore | Foco 0-100 |
| ExposureScore | Exposição 0-100 |
| MaskScore | Máscara 0-100 |
| ParticleCount | Número de partículas |
| Pct_Au | % Ouro |
| Pct_Pt | % Platina |
| Pct_Ag | % Prata |
| ... | Outros metais |

### 5.4 Importando no Power BI

1. Abra o Power BI Desktop
2. **Obter Dados** > **Texto/CSV**
3. Selecione o arquivo `bi_consolidado_YYYYMMDD.csv`
4. Configure o delimitador como **vírgula**
5. Clique em **Carregar**

---

## 6. Dataset IA e QA

### 6.1 Exportando Dataset IA

O Dataset IA exporta recortes de partículas para treinamento de modelos:

1. Execute uma análise
2. Clique em `🤖 Dataset IA`
3. Os arquivos serão salvos em `exports/dataset-ia/particles/`

**Estrutura de saída:**
```
dataset-ia/
├── particles/
│   ├── Au/              # Partículas de ouro
│   │   ├── p_xxx.png    # Imagem do recorte
│   │   └── p_xxx.json   # Metadados
│   ├── Pt/              # Partículas de platina
│   └── ...
└── particles_index_xxx.csv  # Índice geral
```

### 6.2 Metadados por Partícula

Cada partícula exportada inclui:
- ID da partícula e da análise
- Material previsto automaticamente
- Área em pixels
- Circularidade e aspect ratio
- Confiança média
- Valores HSV
- Scores HVS e IA
- Status de qualidade da análise

### 6.3 Modo QA (Rotulação Manual)

Para criar ground truth para treinamento de IA:

1. Execute uma análise
2. No menu, acesse **QA de Partículas**
3. Na janela de QA:
   - **Filtre** por material, área ou confiança
   - **Selecione** uma partícula na lista
   - **Visualize** o recorte e informações
   - **Atribua** o rótulo correto:
     - Clique em um material no combo e **✅ Aplicar**
     - Ou clique em **🚫 Ruído** para marcar como artefato
     - Ou clique em **↩️ Manter** para confirmar a predição
   - **Adicione notas** se necessário
4. Clique em **💾 Salvar QA**

### 6.4 Arquivo de QA Labels

O arquivo `qa_labels_xxx.csv` contém:
```csv
ParticleId,AnalysisId,MaterialPredicted,MaterialHuman,Timestamp,Operator,Notes
```

Este arquivo pode ser usado para:
- Re-treinar modelos de classificação
- Auditoria de qualidade
- Análise de concordância entre predição e humano

---

## 7. Checklist de Qualidade

### 7.1 Indicadores de Qualidade

O MicroLab avalia automaticamente:

| Indicador | Bom | Atenção | Ruim |
|-----------|-----|---------|------|
| **Foco** | ≥50 | 30-50 | <30 |
| **Exposição** | ≥70 | 50-70 | <50 |
| **Máscara** | 30-80% | <30% ou >80% | <10% ou >95% |

### 7.2 Status do Laudo

- **Official** (✅): QualityIndex ≥ 85
- **Preliminary** (⚠️): QualityIndex 70-85
- **Invalid** (❌): QualityIndex < 70
- **OfficialRechecked** (✅✅): Confirmado por reanálise
- **ReviewRequired** (⚠️⚠️): Divergência na reanálise

### 7.3 Interpretando Avisos

**Foco baixo:**
- Verifique o foco do microscópio
- Limpe a lente
- Reduza vibrações

**Exposição inadequada:**
- Ajuste a iluminação
- Verifique saturação (pixels muito claros/escuros)

**Máscara anormal:**
- Fração muito baixa: amostra pode estar fora do campo
- Fração muito alta: possível problema de segmentação

---

## 8. Troubleshooting

### 8.1 Câmera não detectada

1. Verifique se a câmera está conectada
2. Tente diferentes índices (0, 1, 2)
3. Reinicie o MicroLab
4. Verifique drivers da câmera

### 8.2 Análise muito lenta

1. Reduza a resolução da câmera
2. Feche outros programas
3. Verifique uso de CPU/memória

### 8.3 Resultados inconsistentes

1. Verifique a iluminação (deve ser uniforme)
2. Calibre o balanço de branco
3. Verifique o foco antes de analisar
4. Use o modo de reanálise automática

### 8.4 Exportação falha

1. Verifique permissões nas pastas de destino
2. Verifique espaço em disco
3. Feche arquivos abertos no Excel/Power BI

### 8.5 Contato Suporte

Para suporte técnico:
- Consulte a documentação em `docs/`
- Abra uma issue no repositório GitHub
- Contate: suporte@trilhagold.com

---

## Anexos

### A. Atalhos de Teclado

| Tecla | Ação |
|-------|------|
| F5 | Executar análise |
| Ctrl+O | Abrir imagem |
| Ctrl+S | Exportar TXT |
| + / - | Zoom in/out |

### B. Formatos de Exportação

| Formato | Uso |
|---------|-----|
| TXT | Laudo textual para leitura |
| PDF | Laudo formatado para impressão |
| JSON | Integração com sistemas |
| CSV | Excel e análises simples |
| BI CSV | Power BI e dashboards |

### C. Materiais Detectados

**Metais:**
- Au (Ouro), Pt (Platina), Ag (Prata)
- Pd (Paládio), Rh (Ródio), Ir (Irídio)
- Cu (Cobre), Fe (Ferro), Ni (Níquel)
- Zn (Zinco), Pb (Chumbo), Al (Alumínio)

**Cristais:**
- SiO2 (Quartzo), CaCO3 (Calcita)
- Feldspato, Mica, CaF2 (Fluorita)

**Gemas:**
- C (Diamante), Safira, Rubi
- Esmeralda, Ametista

---

*Documentação atualizada para MicroLab HVS-MVP v1.0*
*Trilha Gold Capital - 2024*
