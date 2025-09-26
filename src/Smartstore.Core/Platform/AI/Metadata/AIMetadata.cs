﻿#nullable enable

using Newtonsoft.Json;

namespace Smartstore.Core.AI.Metadata
{
    /// <summary>
    /// Root object for metadata.json.
    /// </summary>
    public class AIMetadata
    {
        #region Properties

        /// <summary>
        /// Arbitrary config version or timestamp string.
        /// </summary>
        public string Version { get; set; } = default!;

        /// <summary>
        /// Internal provider ID (e.g. "openai").
        /// </summary>
        public string ProviderId { get; set; } = default!;

        /// <summary>
        /// Human-readable provider name.
        /// </summary>
        public string? ProviderName { get; set; }

        /// <summary>
        /// Specifies the capabilities of this provider (e.g. text generation, image generation, translation etc.).
        /// </summary>
        [JsonConverter(typeof(AIProviderFeaturesConverter))]
        public AIProviderFeatures Capabilities { get; set; }

        /// <summary>
        /// List of LLM models available under this provider.
        /// </summary>
        public AIModelCollection Models { get; set; } = default!;

        #endregion

        #region Supports...

        public bool Supports(AIProviderFeatures feature)
            => Capabilities.HasFlag(feature);

        public bool SupportsTextGeneration
            => Supports(AIProviderFeatures.TextGeneration);

        public bool SupportsTranslation
            => Supports(AIProviderFeatures.Translation);

        public bool SupportsImageGeneration
            => Supports(AIProviderFeatures.ImageGeneration);

        public bool SupportsImageAnalysis
            => Supports(AIProviderFeatures.ImageAnalysis);

        public bool SuportsThemeVarGeneration
            => Supports(AIProviderFeatures.ThemeVarGeneration);

        public bool SupportsAssistance
            => Supports(AIProviderFeatures.Assistance);

        #endregion

        #region Query models

        /// <summary>
        /// Gets all text models.
        /// </summary>
        /// <param name="preferred">If true, returns only preferred models. If false, returns all other models. If null, returns all undeprecated models.</param>
        public IEnumerable<AIModelEntry> GetTextModels(bool? preferred = null)
            => GetModels(AIOutputType.Text, preferred);

        /// <summary>
        /// Gets all image models.
        /// </summary>
        /// <param name="preferred">If true, returns only preferred models. If false, returns all other models. If null, returns all undeprecated models.</param>
        public IEnumerable<AIModelEntry> GetImageModels(bool? preferred = null)
            => GetModels(AIOutputType.Image, preferred);

        /// <summary>
        /// Gets all models for the given topic.
        /// </summary>
        /// <param name="preferred">If true, returns only preferred models. If false, returns all other models. If null, returns all undeprecated models.</param>
        public IEnumerable<AIModelEntry> GetModels(AIChatTopic topic, bool? preferred = null)
            => GetModels(topic == AIChatTopic.Image ? AIOutputType.Image : AIOutputType.Text, preferred);

        /// <summary>
        /// Gets all models for the given output type.
        /// </summary>
        /// <param name="preferred">If true, returns only preferred models. If false, returns all other models. If null, returns all undeprecated models.</param>
        public IEnumerable<AIModelEntry> GetModels(AIOutputType type, bool? preferred = null)
        {
            return Models.Where(x => x.Type == type && !x.Deprecated && (preferred == null || x.Preferred == preferred.Value)).OrderByDescending(x => x.Preferred);
        }

        /// <summary>
        /// Gets all models that support vision (image analysis).
        /// </summary>
        /// <param name="preferred">If true, returns only preferred models. If false, returns all other models. If null, returns all undeprecated models.</param>
        public IEnumerable<AIModelEntry> GetVisionModels(bool? preferred = null)
        {
            return Models.Where(x => x.Vision && !x.Deprecated && (preferred == null || x.Preferred == preferred.Value)).OrderByDescending(x => x.Preferred);
        }

        /// <summary>
        /// Gets a model by its ID.
        /// </summary>
        /// <param name="mapDeprecated">If true, tries to resolve deprecated models to their alias.</param>
        public AIModelEntry? GetModelById(string modelId, bool mapDeprecated = true)
        {
            if (Models.TryFindModel(modelId, out var modelEntry) && modelEntry.Deprecated && modelEntry.Alias.HasValue() && mapDeprecated)
            {
                // Try to resolve by alias
                modelEntry = Models.FindModel(modelEntry.Alias);
            }

            return modelEntry;
        }

        #endregion

        #region Edit & validate models

        public string ValidateModelName(string modelId, AIOutputType type)
        {
            if (!Models.TryFindModel(modelId, out var modelEntry) || modelEntry.Type != type)
            {
                return GetModels(type, preferred: true).FirstOrDefault()!.Id;
            }

            if (modelEntry != null && modelEntry.Deprecated && modelEntry.Alias.HasValue())
            {
                // Always map deprecated models when validating
                var aliasEntry = Models.FindModel(modelEntry.Alias);
                if (aliasEntry != null && aliasEntry.Type == type && !aliasEntry.Deprecated)
                {
                    modelId = aliasEntry.Id;
                }
            }

            return modelId;
        }

        public string ValidateVisionModelName(string modelId)
        {
            if (!Models.TryFindModel(modelId, out var modelEntry) || modelEntry.Type != AIOutputType.Text || !modelEntry.Vision)
            {
                return GetVisionModels(preferred: true).FirstOrDefault()!.Id;
            }

            if (modelEntry != null && modelEntry.Deprecated && modelEntry.Alias.HasValue())
            {
                // Always map deprecated models when validating
                var aliasEntry = Models.FindModel(modelEntry.Alias);
                if (aliasEntry != null && aliasEntry.Type == AIOutputType.Text && modelEntry.Vision && !aliasEntry.Deprecated)
                {
                    modelId = aliasEntry.Id;
                }
            }

            return modelId;
        }

        public string[] MergeTextModels(string[] preferredModelNames)
        {
            return MergeModels(GetTextModels(), preferredModelNames);
        }

        public string[] MergeImageModels(string[] preferredModelNames)
        {
            return MergeModels(GetImageModels(), preferredModelNames);
        }

        public string[] MergeModels(IEnumerable<AIModelEntry> models, string[] preferredModelNames)
        {
            return models
                .Select(x => x.Id)
                .Union(preferredModelNames)
                .ToArray();
        }

        public IEnumerable<AIModelEntry> MergeModels(AIOutputType outputType, string[] preferredModelNames)
        {
            if (preferredModelNames.IsNullOrEmpty())
            {
                return GetModels(outputType);
            }
            
            var mergedModels = new List<AIModelEntry>();

            foreach (var modelName in preferredModelNames.Distinct())
            {
                var modelEntry = GetModelById(modelName);
                if (modelEntry != null && modelEntry.Type == outputType)
                {
                    mergedModels.Add(modelEntry);
                }
                else
                {
                    mergedModels.Add(new AIModelEntry
                    {
                        Id = modelName,
                        Type = outputType,
                        Preferred = true,
                        IsCustom = true
                    });
                }
            }

            mergedModels.AddRange(GetModels(outputType, preferred: false));

            return mergedModels;
        }

        #endregion
    }
}
