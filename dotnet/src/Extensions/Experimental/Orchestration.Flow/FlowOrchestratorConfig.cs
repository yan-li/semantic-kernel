﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using Microsoft.SemanticKernel.Experimental.Orchestration.Execution;
using Microsoft.SemanticKernel.SemanticFunctions;

#pragma warning disable IDE0130
namespace Microsoft.SemanticKernel.Experimental.Orchestration;
#pragma warning restore IDE0130

/// <summary>
/// Configuration for flow planner instances.
/// </summary>
public sealed class FlowOrchestratorConfig
{
    /// <summary>
    /// A list of plugins to exclude from the plan creation request.
    /// </summary>
    public HashSet<string> ExcludedPlugins { get; } = new();

    /// <summary>
    /// A list of functions to exclude from the plan creation request.
    /// </summary>
    public HashSet<string> ExcludedFunctions { get; } = new();

    /// <summary>
    /// The maximum number of tokens to allow in a plan.
    /// </summary>
    public int MaxTokens { get; set; } = 1024;

    /// <summary>
    /// The maximum length of a string variable.
    /// </summary>
    /// <remarks>
    /// In most cases, the required variables are passed to ReAct engine to infer the next plugin and parameters to execute.
    /// However when the variable is too long, it will either be truncated or decrease the robustness of value passing.
    /// To mitigate that, the <see cref="ReActEngine"/> will avoid rendering the variables exceeding MaxVariableLength in the prompt.
    /// And the variables should be accessed implicitly from SKContext instead of function parameters by the plugins.
    /// </remarks>
    public int MaxVariableLength { get; set; } = 400;

    /// <summary>
    /// The maximum number of iterations to allow for a step.
    /// </summary>
    public int MaxStepIterations { get; set; } = 10;

    /// <summary>
    /// The minimum time to wait between iterations in milliseconds.
    /// </summary>
    public int MinIterationTimeMs { get; set; } = 0;

    /// <summary>
    /// Optional. The prompt template override for ReAct engine.
    /// </summary>
    public string? ReActPromptTemplate { get; set; } = null;

    /// <summary>
    /// Optional. The prompt template configuration override for the ReAct engine.
    /// </summary>
    public PromptTemplateConfig? ReActPromptTemplateConfig { get; set; } = null;

    /// <summary>
    /// Optional. The model to use for the ReAct engine.
    /// </summary>
    /// <remarks>
    /// Prompt used for reasoning may be different for different models.
    /// if the built in prompt template does not work for your model, suggest to override it with <see cref="ReActPromptTemplate"/>.
    /// </remarks>
    public ModelName? ReActModel { get; set; } = null;

    /// <summary>
    /// Name of LLM model
    /// </summary>
    public enum ModelName
    {
#pragma warning disable CA1707 // Identifiers should not contain underscores
        // ReSharper disable once IdentifierTypo
        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// text-davinci-003 model
        /// </summary>
        TEXT_DAVINCI_003,

        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// gpt-35-turbo model
        /// </summary>
        GPT35_TURBO,

        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// gpt4-32k model
        /// </summary>
        GPT4_32k,
#pragma warning restore CA1707 // Identifiers should not contain underscores
    }
}
