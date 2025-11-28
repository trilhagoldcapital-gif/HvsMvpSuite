using System;

namespace HvsMvp.App
{
    /// <summary>
    /// Classe de máscara por pixel usada em toda a aplicação.
    /// </summary>
    public class SampleMaskClass
    {
        /// <summary>
        /// True se o pixel pertence à região de amostra (não é fundo).
        /// </summary>
        public bool IsSample { get; set; }
    }
}
