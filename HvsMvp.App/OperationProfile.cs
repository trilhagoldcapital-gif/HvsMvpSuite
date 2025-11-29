using System;

namespace HvsMvp.App
{
    /// <summary>
    /// PR15: Operation profile enum defining the user interface complexity level.
    /// </summary>
    public enum OperationProfile
    {
        /// <summary>
        /// MVP Rápido / Básico - Reduced flow with minimum necessary options.
        /// Essential masks and tools enabled by default.
        /// </summary>
        Basic = 0,

        /// <summary>
        /// Avançado / Profissional - All tools and advanced settings visible.
        /// Access to fine-tuned analysis, AI, masks, etc.
        /// </summary>
        Advanced = 1
    }

    /// <summary>
    /// PR15: Helper methods for operation profiles.
    /// </summary>
    public static class OperationProfileExtensions
    {
        /// <summary>
        /// Gets the display name for the profile.
        /// </summary>
        public static string GetDisplayName(this OperationProfile profile)
        {
            return profile switch
            {
                OperationProfile.Basic => "MVP Rápido (Básico)",
                OperationProfile.Advanced => "Avançado (Profissional)",
                _ => profile.ToString()
            };
        }

        /// <summary>
        /// Gets the description for the profile.
        /// </summary>
        public static string GetDescription(this OperationProfile profile)
        {
            return profile switch
            {
                OperationProfile.Basic => "Fluxo direto e simplificado. Ideal para análises rápidas do dia a dia.",
                OperationProfile.Advanced => "Acesso completo a todas as ferramentas e configurações avançadas.",
                _ => string.Empty
            };
        }
    }
}
