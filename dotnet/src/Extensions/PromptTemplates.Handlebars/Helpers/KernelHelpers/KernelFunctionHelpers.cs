﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using HandlebarsDotNet;
using HandlebarsDotNet.Compiler;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Microsoft.SemanticKernel.PromptTemplates.Handlebars.Helpers;

/// <summary>
/// Utility class for registering kernel functions as helpers in Handlebars.
/// </summary>
internal static class KernelFunctionHelpers
{
    /// <summary>
    /// Register all (default) or specific categories.
    /// </summary>
    /// <param name="handlebarsInstance">The <see cref="IHandlebars"/>-context.</param>
    /// <param name="kernel">Kernel instance.</param>
    /// <param name="executionContext">Kernel arguments maintained as the executing context.</param>
    /// <param name="nameDelimiter">The character used to delimit the plugin name and function name in a Handlebars template.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    public static void Register(
        IHandlebars handlebarsInstance,
        Kernel kernel,
        KernelArguments executionContext,
        string nameDelimiter,
        CancellationToken cancellationToken)
    {
        foreach (var function in kernel.Plugins.GetFunctionsMetadata())
        {
            RegisterFunctionAsHelper(kernel, executionContext, handlebarsInstance, function, nameDelimiter, cancellationToken);
        }
    }

    #region private

    private static void RegisterFunctionAsHelper(
        Kernel kernel,
        KernelArguments executionContext,
        IHandlebars handlebarsInstance,
        KernelFunctionMetadata functionMetadata,
        string nameDelimiter,
        CancellationToken cancellationToken)
    {
        string fullyResolvedFunctionName = functionMetadata.PluginName + nameDelimiter + functionMetadata.Name;

        KernelHelpersUtils.RegisterHelperSafe(
            handlebarsInstance,
            fullyResolvedFunctionName,
            (Context context, Arguments handlebarsArguments) =>
            {
                // Get the parameters from the template arguments
                if (handlebarsArguments.Length is not 0)
                {
                    if (handlebarsArguments[0].GetType() == typeof(HashParameterDictionary))
                    {
                        ProcessHashArguments(functionMetadata, executionContext, (IDictionary<string, object>)handlebarsArguments[0], nameDelimiter);
                    }
                    else
                    {
                        ProcessPositionalArguments(functionMetadata, executionContext, handlebarsArguments);
                    }
                }
                else if (functionMetadata.Parameters.Any(p => p.IsRequired))
                {
                    throw new ArgumentException($"No arguments are provided for {fullyResolvedFunctionName}.");
                }

                KernelFunction function = kernel.Plugins.GetFunction(functionMetadata.PluginName, functionMetadata.Name);

                // Invoke the function and write the result to the template
                return InvokeKernelFunction(kernel, function, executionContext, cancellationToken);
            });
    }

    /// <summary>
    /// Checks if handlebars argument is a valid type for the function parameter.
    /// Must satisfy one of the following:
    /// Types are an exact match.
    /// Argument is any kind of numeric type if function parameter requires a numeric type.
    /// Argument type is an object (this covers complex types).
    /// Function parameter is a generic type.
    /// </summary>
    /// <param name="parameterMetadata">Function parameter metadata.</param>
    /// <param name="argument">Handlebar argument.</param>
    private static bool IsExpectedParameterType(KernelParameterMetadata parameterMetadata, object argument)
    {
        var actualParameterType = parameterMetadata.ParameterType.TryGetGenericNullableType(out var nullableType) ? nullableType : parameterMetadata.ParameterType;

        bool parameterIsNumeric = KernelHelpersUtils.IsNumericType(actualParameterType)
            || (parameterMetadata.Schema?.RootElement.TryGetProperty("type", out JsonElement typeProperty) == true && typeProperty.GetString() == "number");

        bool argIsNumeric = KernelHelpersUtils.IsNumericType(argument.GetType())
            || KernelHelpersUtils.TryParseAnyNumber(argument.ToString());

        return actualParameterType is null
            || actualParameterType == argument.GetType()
            || (parameterIsNumeric && argIsNumeric);
    }

    /// <summary>
    /// Processes the hash arguments passed to a Handlebars helper function.
    /// </summary>
    /// <param name="functionMetadata">Metadata for the function being invoked.</param>
    /// <param name="executionContext">Arguments maintained in the executing context.</param>
    /// <param name="handlebarsArguments">Arguments passed to the Handlebars helper.</param>
    /// <param name="nameDelimiter">The character used to delimit the plugin name and function name in a Handlebars template.</param>
    /// <exception cref="KernelException">Thrown when a required parameter is missing.</exception>
    private static void ProcessHashArguments(
        KernelFunctionMetadata functionMetadata,
        KernelArguments executionContext,
        IDictionary<string, object>? handlebarsArguments,
        string nameDelimiter)
    {
        // Prepare the input parameters for the function
        foreach (var param in functionMetadata.Parameters)
        {
            var fullyQualifiedParamName = functionMetadata.Name + nameDelimiter + param.Name;
            if (handlebarsArguments is not null && (handlebarsArguments.TryGetValue(fullyQualifiedParamName, out var value) || handlebarsArguments.TryGetValue(param.Name, out value)))
            {
                if (IsExpectedParameterType(param, value))
                {
                    executionContext[param.Name] = value;
                }
                else
                {
                    throw new KernelException($"Invalid argument type for function {functionMetadata.Name}. Parameter {param.Name} expects type {param.ParameterType ?? (object?)param.Schema} but received {value.GetType()}.");
                }
            }
            else if (param.IsRequired)
            {
                throw new KernelException($"Parameter {param.Name} is required for function {functionMetadata.Name}.");
            }
        }
    }

    /// <summary>
    /// Processes the positional arguments passed to a Handlebars helper function.
    /// </summary>
    /// <param name="functionMetadata">KernelFunctionMetadata for the function being invoked.</param>
    /// <param name="executionContext">Arguments maintained in the executing context.</param>
    /// <param name="handlebarsArguments">Arguments passed to the Handlebars helper.</param>
    /// <exception cref="KernelException">Thrown when a required parameter is missing.</exception>
    private static void ProcessPositionalArguments(KernelFunctionMetadata functionMetadata, KernelArguments executionContext, Arguments handlebarsArguments)
    {
        var requiredParameters = functionMetadata.Parameters.Where(p => p.IsRequired).ToList();
        if (requiredParameters.Count <= handlebarsArguments.Length && handlebarsArguments.Length <= functionMetadata.Parameters.Count)
        {
            var argIndex = 0;
            foreach (var arg in handlebarsArguments)
            {
                var param = functionMetadata.Parameters[argIndex];
                if (IsExpectedParameterType(param, arg))
                {
                    executionContext[param.Name] = handlebarsArguments[argIndex];
                    argIndex++;
                }
                else
                {
                    throw new KernelException($"Invalid parameter type for function {functionMetadata.Name}. Parameter {param.Name} expects type {param.ParameterType ?? (object?)param.Schema} but received {handlebarsArguments[argIndex].GetType()}.");
                }
            }
        }
        else
        {
            throw new KernelException($"Invalid parameter count for function {functionMetadata.Name}. {handlebarsArguments.Length} were specified but {functionMetadata.Parameters.Count} are required.");
        }
    }

    /// <summary>
    /// Invokes an SK function and returns a typed result, if specified.
    /// </summary>
    private static object? InvokeKernelFunction(
        Kernel kernel,
        KernelFunction function,
        KernelArguments executionContext,
        CancellationToken cancellationToken)
    {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
        FunctionResult result = function.InvokeAsync(kernel, executionContext, cancellationToken: cancellationToken).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits

        return ParseResult(result);
    }

    /// <summary>
    /// Parse the <see cref="FunctionResult"/> into an object, extracting the appropriate value if the return type is <see cref="OpenAIChatMessageContent"/>.
    /// </summary>
    /// <param name="result">Function result.</param>
    /// <returns>Deserialized object</returns>
    private static object? ParseResult(FunctionResult result)
    {
        var resultAsObject = result.GetValue<object?>();
        if (result.ValueType is not null && resultAsObject is not null && result.ValueType == typeof(OpenAIChatMessageContent))
        {
            resultAsObject = ((OpenAIChatMessageContent)resultAsObject).Content;
        }

        return resultAsObject;
    }

    #endregion
}
