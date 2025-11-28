using System;

namespace HvsMvp.App
{
    /// <summary>
    /// Rótulo por pixel após análise HVS.
    /// </summary>
    public class PixelLabel
    {
        public bool IsSample { get; set; }
        public int ParticleId { get; set; }
        public string? MaterialId { get; set; }
        public PixelMaterialType MaterialType { get; set; } = PixelMaterialType.None;
        public double MaterialConfidence { get; set; }
        public double RawScore { get; set; }
        public double H { get; set; }
        public double S { get; set; }
        public double V { get; set; }
    }

    public enum PixelMaterialType
    {
        None = 0,
        Metal = 1,
        Crystal = 2,
        Gem = 3,
        Background = 4
    }
}