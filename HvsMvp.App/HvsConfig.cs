using System.Collections.Generic;

namespace HvsMvp.App
{
    // Raiz do arquivo hvs-config.json
    public class HvsConfig
    {
        public HvsAppInfo? App { get; set; }
        public HvsHardware? Hardware { get; set; }
        public HvsCalibration? Calibration { get; set; }
        public HvsFeatures? Features { get; set; }
        public HvsAiModels? Ai_Models { get; set; }
        public HvsLogic? Logic { get; set; }
        public HvsMaterials? Materials { get; set; }
        public HvsScoring? Scoring { get; set; }
        public Dictionary<string, HvsRange3>? Thresholds { get; set; }
        public HvsQualityControl? Quality_Control { get; set; }
        public HvsPipelineExample? Pipeline_Example { get; set; }
        public HvsExport? Export { get; set; }
        public HvsNotes? Notes { get; set; }
    }

    public class HvsAppInfo
    {
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? Locale { get; set; }
        public string? Purpose { get; set; }
    }

    public class HvsHardware
    {
        public HvsMicroscope? Microscope { get; set; }
        public HvsCamera? Camera { get; set; }
    }

    public class HvsMicroscope
    {
        public List<int>? Objective_Magnification { get; set; }
        public List<double>? Numerical_Aperture_Range { get; set; }
        public List<string>? Illumination_Modes { get; set; }
        public string? Stage_Control { get; set; }
        public string? Camera_Mount { get; set; }
    }

    public class HvsCamera
    {
        public string? Sensor_Type { get; set; }
        public List<int>? Resolution_Px { get; set; }
        public int Bit_Depth { get; set; }
        public int Frame_Rate_Fps { get; set; }
        public bool Raw_Capture { get; set; }
        public List<string>? White_Balance_Modes { get; set; }
        public List<string>? Exposure_Modes { get; set; }
        public HvsLens? Lens { get; set; }
    }

    public class HvsLens
    {
        public double Focal_Length_Mm { get; set; }
        public string? Aperture_Range { get; set; }
    }

    public class HvsCalibration
    {
        public HvsColorCalibration? Color { get; set; }
        public HvsFocusCalibration? Focus { get; set; }
        public HvsScaleCalibration? Scale { get; set; }
        public HvsNoiseCalibration? Noise { get; set; }
    }

    public class HvsColorCalibration
    {
        public string? Workflow { get; set; }
        public string? Reference_White { get; set; }
        public double Gamma { get; set; }
        public string? Illumination_Profile { get; set; }
    }

    public class HvsFocusCalibration
    {
        public string? Metric { get; set; }
        public HvsFocusThresholds? Thresholds { get; set; }
        public HvsZStack? Z_Stack { get; set; }
    }

    public class HvsFocusThresholds
    {
        public double Min_Sharpness { get; set; }
        public double Ideal_Sharpness { get; set; }
    }

    public class HvsZStack
    {
        public int Steps { get; set; }
        public string? Merge { get; set; }
    }

    public class HvsScaleCalibration
    {
        public double Micrometer_Slide_Um_Per_Px { get; set; }
        public string? Unit { get; set; }
    }

    public class HvsNoiseCalibration
    {
        public string? Denoise_Method { get; set; }
        public string? Sigma_Estimation { get; set; }
    }

    public class HvsFeatures
    {
        public List<string>? Optical { get; set; }
        public List<string>? Morfologia { get; set; }
        public List<string>? Textura { get; set; }
        public List<string>? Espectral { get; set; }
        public List<string>? Defeitos { get; set; }
    }

    public class HvsAiModels
    {
        public HvsFeatureExtraction? Feature_Extraction { get; set; }
        public HvsClassification? Classification { get; set; }
        public HvsAnomalyDetection? Anomaly_Detection { get; set; }
    }

    public class HvsFeatureExtraction
    {
        public string? Cnn_Backbone { get; set; }
        public double Layers_Frozen { get; set; }
        public int Emb_Dim { get; set; }
    }

    public class HvsClassification
    {
        public string? Type { get; set; }
        public List<HvsClassifierMember>? Members { get; set; }
        public string? Output { get; set; }
    }

    public class HvsClassifierMember
    {
        public string? Model { get; set; }
        public List<string>? Inputs { get; set; }
    }

    public class HvsAnomalyDetection
    {
        public string? Model { get; set; }
        public double Contamination { get; set; }
    }

    public class HvsLogic
    {
        public List<HvsPreprocessStep>? Preprocess { get; set; }
        public HvsSegmentation? Segmentation { get; set; }
        public Dictionary<string, string>? Measurement { get; set; }
        public HvsFusion? Fusion { get; set; }
        public HvsInterference? Interference { get; set; }
        public HvsDecision? Decision { get; set; }
        public HvsLogicOutput? Output { get; set; }
    }

    public class HvsPreprocessStep
    {
        public string? Step { get; set; }
        public string? When { get; set; }
        public string? Method { get; set; }
    }

    public class HvsSegmentation
    {
        public string? Method { get; set; }
        public List<string>? Outputs { get; set; }
    }

    public class HvsFusion
    {
        public string? Probabilistic { get; set; }
        public HvsFusionPriors? Priors { get; set; }
        public string? Calibration { get; set; }
    }

    public class HvsFusionPriors
    {
        public Dictionary<string, double>? Contexto_Lab { get; set; }
        public string? Ajuste_Usuario { get; set; }
    }

    public class HvsInterference
    {
        public List<string>? Tipos { get; set; }
        public Dictionary<string, string>? Deteccao { get; set; }
        public List<HvsMitigacao>? Mitigacao { get; set; }
        public HvsAjusteConfianca? Ajuste_Confiança { get; set; }
    }

    public class HvsMitigacao
    {
        public string? Acao { get; set; }
        public List<string>? Para { get; set; }
        public string? Usar { get; set; }
        public string? Nota { get; set; }
    }

    public class HvsAjusteConfianca
    {
        public Dictionary<string, double>? Penalidades { get; set; }
    }

    public class HvsDecision
    {
        public List<HvsRule>? Rule_Set { get; set; }
        public HvsConfidenceAggregation? Confidence_Aggregation { get; set; }
    }

    public class HvsRule
    {
        public List<string>? If { get; set; }
        public string? Then { get; set; }
        public Dictionary<string, double>? Weights { get; set; }
    }

    public class HvsConfidenceAggregation
    {
        public string? Method { get; set; }
        public double Accept_Threshold { get; set; }
        public List<double>? Review_Zone { get; set; }
        public double Reject_Threshold { get; set; }
    }

    public class HvsLogicOutput
    {
        public List<string>? Reports { get; set; }
        public List<string>? Visuals { get; set; }
    }

    public class HvsMaterials
    {
        public List<HvsMaterial>? Metais { get; set; }
        public List<HvsMaterial>? Cristais { get; set; }
        public List<HvsMaterial>? Gemas { get; set; }
    }

    public class HvsMaterial
    {
        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Nome) ? (Id ?? "(material)") : Nome!;
        }

        public string? Id { get; set; }
        public string? Nome { get; set; }
        public string? Grupo { get; set; }
        public Dictionary<string, object?>? Optico { get; set; }
        public Dictionary<string, object?>? Morfologia { get; set; }
        public Dictionary<string, object?>? Espectral { get; set; }
        public List<string>? Interferencia_Tendencias { get; set; }
        public List<string>? Defeitos { get; set; }
    }

    public class HvsScoring
    {
        public Dictionary<string, double>? Weights { get; set; }
        public List<HvsMaterialBoost>? Material_Specific_Boosts { get; set; }
        public List<HvsInterferencePenalty>? Interference_Penalties { get; set; }
    }

    public class HvsMaterialBoost
    {
        public string? Material { get; set; }
        public List<string>? Conditions { get; set; }
        public double Boost { get; set; }
    }

    public class HvsInterferencePenalty
    {
        public string? Type { get; set; }
        public string? Severity { get; set; }
        public double Penalty { get; set; }
    }

    public class HvsRange3
    {
        public double Low { get; set; }
        public double Mid { get; set; }
        public double High { get; set; }
    }

    public class HvsQualityControl
    {
        public List<HvsQcRule>? Image_Qc { get; set; }
        public List<HvsQcRule>? Sample_Qc { get; set; }
    }

    public class HvsQcRule
    {
        public string? Metric { get; set; }
        public double? Min { get; set; }
        public object? Max { get; set; }
    }

    public class HvsPipelineExample
    {
        public List<string>? Sequence { get; set; }
        public List<HvsModalSwitching>? Modal_Switching { get; set; }
    }

    public class HvsModalSwitching
    {
        public string? Condition { get; set; }
        public string? Switch_To { get; set; }
    }

    public class HvsExport
    {
        public List<string>? Formats { get; set; }
        public List<string>? Include { get; set; }
    }

    public class HvsNotes
    {
        public string? Robustez { get; set; }
        public string? Seguranca { get; set; }
        public string? Adaptacao { get; set; }
    }
}

