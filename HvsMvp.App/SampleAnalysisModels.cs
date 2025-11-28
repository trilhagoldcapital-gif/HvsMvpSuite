using System;
using System.Collections.Generic;

namespace HvsMvp.App
{
    public class MetalResult
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Group { get; set; } = "";
        public double PctSample { get; set; }
        public double? PpmEstimated { get; set; }
        public double Score { get; set; }
    }

    public class CrystalResult
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public double PctSample { get; set; }
        public double Score { get; set; }
    }

    public class GemResult
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public double PctSample { get; set; }
        public double Score { get; set; }
    }

    public class ImageDiagnosticsResult
    {
        public double FocusScore { get; set; }
        public double SaturationClippingFraction { get; set; }
        public double ForegroundFraction { get; set; }
    }

    public class SampleFullAnalysisResult
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? ImagePath { get; set; }
        public DateTime CaptureDateTimeUtc { get; set; } = DateTime.UtcNow;

        public ImageDiagnosticsResult Diagnostics { get; set; } = new ImageDiagnosticsResult();

        public List<MetalResult> Metals { get; set; } = new List<MetalResult>();
        public List<CrystalResult> Crystals { get; set; } = new List<CrystalResult>();
        public List<GemResult> Gems { get; set; } = new List<GemResult>();

        public string ShortReport { get; set; } = "";
    }
}
