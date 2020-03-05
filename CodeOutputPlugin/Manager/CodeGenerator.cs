﻿using Gum.Converters;
using Gum.DataTypes;
using Gum.DataTypes.Variables;
using Gum.Managers;
using Gum.ToolStates;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeOutputPlugin.Manager
{
    public enum VisualApi
    {
        Gum,
        XamarinForms
    }

    public static class CodeGenerator
    {

        public static string GetCodeForElement(ElementSave element, VisualApi visualApi)
        {
            int tabCount = 0;

            var stringBuilder = new StringBuilder();

            FillWithStateEnums(element, stringBuilder, tabCount);

            FillWithCurrentState(element, stringBuilder, tabCount);

            foreach (var instance in element.Instances)
            {
                FillWithInstanceDeclaration(instance, stringBuilder, visualApi, tabCount);
            }
            stringBuilder.AppendLine();

            FillWithExposedVariables(element, stringBuilder, visualApi, tabCount);
            // -- no need for AppendLine here since FillWithExposedVariables does it after every variable --

            GenerateConstructor(element, visualApi, tabCount, stringBuilder);

            stringBuilder.AppendLine(ToTabs(tabCount) + "partial void CustomInitialize();");


            return stringBuilder.ToString();
        }

        private static void GenerateConstructor(ElementSave element, VisualApi visualApi, int tabCount, StringBuilder stringBuilder)
        {
            var elementName = GetClassNameForType(element.Name, visualApi);
            stringBuilder.AppendLine(ToTabs(tabCount) + $"public {elementName}(bool fullInstantiation = true)");

            stringBuilder.AppendLine(ToTabs(tabCount) + "{");
            tabCount++;

            stringBuilder.AppendLine(ToTabs(tabCount) + "if(fullInstantiation)");
            stringBuilder.AppendLine(ToTabs(tabCount) + "{");

            tabCount++;

            stringBuilder.AppendLine(ToTabs(tabCount) + "this.SetContainedObject(new InvisibleRenderable());");

            stringBuilder.AppendLine();

            FillWithVariableAssignments(element, visualApi, stringBuilder, tabCount);

            stringBuilder.AppendLine();

            foreach (var instance in element.Instances)
            {
                FillWithInstanceInstantiation(instance, visualApi, stringBuilder, tabCount);
            }
            stringBuilder.AppendLine();

            foreach (var instance in element.Instances)
            {
                FillWithVariableAssignments(instance, element, visualApi, stringBuilder, tabCount);
                stringBuilder.AppendLine();
            }

            stringBuilder.AppendLine(ToTabs(tabCount) + "CustomInitialize();");


            tabCount--;
            stringBuilder.AppendLine(ToTabs(tabCount) + "}");


            tabCount--;
            stringBuilder.AppendLine(ToTabs(tabCount) + "}");
        }

        public static string GetCodeForState(ElementSave container, StateSave stateSave, VisualApi visualApi)
        {
            var stringBuilder = new StringBuilder();

            FillWithVariablesInState(container, stateSave, visualApi, stringBuilder, 0);

            var code = stringBuilder.ToString();
            return code;
        }

        private static void FillWithVariablesInState(ElementSave container, StateSave stateSave, VisualApi visualApi, StringBuilder stringBuilder, int tabCount)
        {
            VariableSave[] variablesToConsider = stateSave.Variables
                // make "Parent" first
                .Where(item => item.GetRootName() != "Parent")
                .ToArray();

            string last = null;

            foreach (var variable in variablesToConsider)
            {
                InstanceSave instance = null;

                var instanceName = variable.SourceObject;

                if (instanceName != null)
                {
                    instance = container.GetInstance(instanceName);
                }

                if (string.IsNullOrWhiteSpace(last) == false && last != instanceName)
                {
                    stringBuilder.AppendLine();
                }

                if (instance != null)
                {
                    var codeLine = GetCodeLine(instance, variable, container, visualApi);
                    stringBuilder.AppendLine(ToTabs(tabCount) + codeLine);

                    var suffixCodeLine = GetSuffixCodeLine(instance, variable, visualApi);
                    if (!string.IsNullOrEmpty(suffixCodeLine))
                    {
                        stringBuilder.AppendLine(ToTabs(tabCount) + suffixCodeLine);
                    }
                }
                last = instanceName;
            }
        }

        private static void FillWithStateEnums(ElementSave element, StringBuilder stringBuilder, int tabCount)
        {
            // for now we'll just do categories. We may need to get uncategorized at some point...
            foreach(var category in element.Categories)
            {
                string enumName = category.Name;

                stringBuilder.AppendLine(ToTabs(tabCount) + $"public enum {category.Name}");
                stringBuilder.AppendLine("{");
                tabCount++;

                foreach(var state in category.States)
                {
                    stringBuilder.AppendLine(ToTabs(tabCount) + $"{state.Name},");
                }

                stringBuilder.AppendLine("}");
                tabCount--;
            }
        }

        private static void FillWithCurrentState(ElementSave element, StringBuilder stringBuilder, int tabCount)
        {
            foreach (var category in element.Categories)
            {
                stringBuilder.AppendLine();
                string enumName = category.Name;

                stringBuilder.AppendLine(ToTabs(tabCount) + $"{category.Name} m{category.Name}State;");
                stringBuilder.AppendLine(ToTabs(tabCount) + $"public {category.Name} {category.Name}State");

                stringBuilder.AppendLine(ToTabs(tabCount) + "{");
                tabCount++;
                stringBuilder.AppendLine(ToTabs(tabCount) + $"get => m{category.Name}State;");
                stringBuilder.AppendLine(ToTabs(tabCount) + $"set");

                stringBuilder.AppendLine(ToTabs(tabCount) + "{");
                tabCount++;
                stringBuilder.AppendLine(ToTabs(tabCount) + $"m{category.Name}State = value;");

                stringBuilder.AppendLine(ToTabs(tabCount) + $"switch (value)");
                stringBuilder.AppendLine(ToTabs(tabCount) + "{");
                tabCount++;

                foreach(var state in category.States)
                {
                    stringBuilder.AppendLine(ToTabs(tabCount) + $"case {category.Name}.{state.Name}:");
                    tabCount++;

                    FillWithVariablesInState(element, state, VisualApi.Gum, stringBuilder, tabCount);

                    stringBuilder.AppendLine(ToTabs(tabCount) + $"break;");
                    tabCount--;
                }


                tabCount--;
                stringBuilder.AppendLine(ToTabs(tabCount) + "}");


                tabCount--;
                stringBuilder.AppendLine(ToTabs(tabCount) + "}");

                tabCount--;
                stringBuilder.AppendLine(ToTabs(tabCount) + "}");
            }
        }

        private static void FillWithExposedVariables(ElementSave element, StringBuilder stringBuilder, VisualApi visualApi, int tabCount)
        {
            var exposedVariables = element.DefaultState.Variables
                .Where(item => !string.IsNullOrEmpty(item.ExposedAsName))
                .ToArray();

            foreach(var exposedVariable in exposedVariables)
            {
                FillWithExposedVariable(exposedVariable, element, stringBuilder, tabCount);
                stringBuilder.AppendLine();
            }
        }

        private static void FillWithExposedVariable(VariableSave exposedVariable, ElementSave container, StringBuilder stringBuilder, int tabCount)
        {
            var type = exposedVariable.Type;

            if(exposedVariable.IsState(container, out ElementSave stateContainer, out StateSaveCategory category))
            {
                var stateContainerType = GetClassNameForType(stateContainer.Name, VisualApi.Gum);
                type = $"{stateContainerType}.{category.Name}";
            }

            stringBuilder.AppendLine(ToTabs(tabCount) + $"public {type} {exposedVariable.ExposedAsName}");
            stringBuilder.AppendLine(ToTabs(tabCount) + "{");
            tabCount++;
            stringBuilder.AppendLine(ToTabs(tabCount) + $"get => {exposedVariable.Name};");
            stringBuilder.AppendLine(ToTabs(tabCount) + $"set => {exposedVariable.Name} = value;");
            tabCount--;

            stringBuilder.AppendLine(ToTabs(tabCount) + "}");

        }

        public static string GetCodeForInstance(InstanceSave instance, ElementSave element, VisualApi visualApi)
        {
            var stringBuilder = new StringBuilder();

            FillWithInstanceDeclaration(instance, stringBuilder, visualApi);

            FillWithInstanceInstantiation(instance, visualApi, stringBuilder);

            FillWithVariableAssignments(instance, element, visualApi, stringBuilder);

            var code = stringBuilder.ToString();
            return code;
        }

        private static void FillWithInstanceInstantiation(InstanceSave instance, VisualApi visualApi, StringBuilder stringBuilder, int tabCount = 0)
        {
            var strippedType = instance.BaseType;
            if (strippedType.Contains("/"))
            {
                strippedType = strippedType.Substring(strippedType.LastIndexOf("/") + 1);
            }
            var tabs = new String(' ', 4 * tabCount);

            string suffix = visualApi == VisualApi.Gum ? "Runtime" : "";
            var className = $"{strippedType}{suffix}";
            stringBuilder.AppendLine($"{tabs}{instance.Name} = new {className}();");
        }

        private static void FillWithVariableAssignments(ElementSave element, VisualApi visualApi, StringBuilder stringBuilder, int tabCount = 0)
        {
            var defaultState = SelectedState.Self.SelectedElement.DefaultState;
            var variablesToConsider = defaultState.Variables
                .Where(item =>
                {
                    return
                        item.Value != null &&
                        item.SetsValue &&
                        string.IsNullOrEmpty(item.SourceObject);
                })
                .ToArray();

            var tabs = new String(' ', 4 * tabCount);

            foreach (var variable in variablesToConsider)
            {
                var codeLine = GetCodeLine(null, variable, element, visualApi);
                stringBuilder.AppendLine(tabs + codeLine);

                var suffixCodeLine = GetSuffixCodeLine(null, variable, visualApi);
                if (!string.IsNullOrEmpty(suffixCodeLine))
                {
                    stringBuilder.AppendLine(tabs + suffixCodeLine);
                }
            }
        }

        private static void FillWithVariableAssignments(InstanceSave instance, ElementSave container, VisualApi visualApi, StringBuilder stringBuilder, int tabCount = 0)
        {
            VariableSave[] variablesToConsider = GetVariablesToConsider(instance)
                // make "Parent" first
                .OrderBy(item => item.GetRootName() != "Parent")
                .ToArray();

            var hasParent = variablesToConsider.FirstOrDefault()?.GetRootName() == "Parent";

            var tabs = new String(' ', 4 * tabCount);

            stringBuilder.AppendLine($"{tabs}{instance.Name}.Name = \"{instance.Name}\";");

            if (!hasParent)
            {
                // add it to "this"
                stringBuilder.AppendLine($"{tabs}this.Children.Add({instance.Name});");
            }

            foreach (var variable in variablesToConsider)
            {
                var codeLine = GetCodeLine(instance, variable, container, visualApi);

                // the line of code could be " ", a string with a space. This happens
                // if we want to skip a variable so we dont return null or empty.
                // But we also don't want a ton of spaces generated.
                if(!string.IsNullOrWhiteSpace(codeLine))
                {
                    stringBuilder.AppendLine(tabs + codeLine);
                }

                var suffixCodeLine = GetSuffixCodeLine(instance, variable, visualApi);
                if (!string.IsNullOrEmpty(suffixCodeLine))
                {
                    stringBuilder.AppendLine(tabs + suffixCodeLine);
                }
            }
        }

        private static void FillWithInstanceDeclaration(InstanceSave instance, StringBuilder stringBuilder, VisualApi visualApi, int tabCount = 0)
        {
            var tabs = new String(' ', 4 * tabCount);

            string className = GetClassNameForType(instance.BaseType, visualApi);

            bool isPublic = true;
            string accessString = isPublic ? "public " : "";

            stringBuilder.AppendLine($"{tabs}{accessString}{className} {instance.Name};");
        }

        private static string GetClassNameForType(string type, VisualApi visualApi)
        {
            var strippedType = type;
            if (strippedType.Contains("/"))
            {
                strippedType = strippedType.Substring(strippedType.LastIndexOf("/") + 1);
            }

            string suffix = visualApi == VisualApi.Gum ? "Runtime" : "";
            var className = $"{strippedType}{suffix}";
            return className;
        }

        private static string GetSuffixCodeLine(InstanceSave instance, VariableSave variable, VisualApi visualApi)
        {
            if(visualApi == VisualApi.XamarinForms)
            {
                var rootName = variable.GetRootName();

                switch(rootName)
                {
                    case "Width": return $"{instance.Name}.HorizontalOptions = LayoutOptions.Start;";
                    case "Height": return $"{instance.Name}.VerticalOptions = LayoutOptions.Start;";
                }
            }

            return null;
        }

        private static string GetCodeLine(InstanceSave instance, VariableSave variable, ElementSave container, VisualApi visualApi)
        {
            string instancePrefix = instance != null ? $"{instance.Name}." : "this.";

            if (visualApi == VisualApi.Gum)
            {
                var fullLineReplacement = TryGetFullGumLineReplacement(instance, variable);

                if(fullLineReplacement != null)
                {
                    return fullLineReplacement;
                }
                else
                {
                    return $"{instancePrefix}{GetGumVariableName(variable, container)} = {VariableValueToGumCodeValue(variable, container)};";
                }

            }
            else // xamarin forms
            {
                var fullLineReplacement = TryGetFullXamarinFormsLineReplacement(instance, container, variable);
                if(fullLineReplacement != null)
                {
                    return fullLineReplacement;
                }
                else
                {
                    return $"{instancePrefix}{GetXamarinFormsVariableName(variable)} = {VariableValueToXamarinFormsCodeValue(variable)};";
                }

            }
        }

        private static string TryGetFullXamarinFormsLineReplacement(InstanceSave instance, ElementSave container, VariableSave variable)
        {
            var rootName = variable.GetRootName();

            if(rootName == "X")
            {
                var defaultState = container.DefaultState;
                var variableFinder = new RecursiveVariableFinder(instance, container);

                var x = variableFinder.GetValue<float>("X");
                var y = variableFinder.GetValue<float>("Y");
                var width = variableFinder.GetValue<float>("Width");
                var height = variableFinder.GetValue<float>("Height");

                var xUnits = variableFinder.GetValue<PositionUnitType>("X Units");
                var yUnits = variableFinder.GetValue<PositionUnitType>("Y Units");
                var widthUnits = variableFinder.GetValue<DimensionUnitType>("Width Units");
                var heightUnits = variableFinder.GetValue<DimensionUnitType>("Height Units");

                List<string> proportionalFlags = new List<string>();


                if (widthUnits == DimensionUnitType.Percentage)
                {
                    width /= 100.0f;
                    proportionalFlags.Add("AbsoluteLayoutFlags.WidthProportional");
                }
                if (heightUnits == DimensionUnitType.Percentage)
                {
                    height /= 100.0f;
                    proportionalFlags.Add("AbsoluteLayoutFlags.HeightProportional");
                }

                // Xamarin forms uses a weird anchoring system to combine both position and anchor into one value. Gum splits those into two values
                // We need to convert from the gum units to xamforms units:
                // for now assume it's all %'s:
                if(xUnits == PositionUnitType.PercentageWidth)
                {
                    x /= 100.0f;
                    var adjustedCanvasWidth = 1 - width;
                    if(adjustedCanvasWidth > 0)
                    {
                        x /= adjustedCanvasWidth;
                    }
                    proportionalFlags.Add("AbsoluteLayoutFlags.XProportional");
                }
                if(yUnits == PositionUnitType.PercentageHeight)
                {
                    y /= 100.0f;
                    var adjustedCanvasHeight = 1 - height;
                    if(adjustedCanvasHeight > 0)
                    {
                        y /= adjustedCanvasHeight;
                    }
                    proportionalFlags.Add("AbsoluteLayoutFlags.YProportional");
                }
                

                var xString = x.ToString(CultureInfo.InvariantCulture) + "f";
                var yString = y.ToString(CultureInfo.InvariantCulture) + "f";
                var widthString = width.ToString(CultureInfo.InvariantCulture) + "f";
                var heightString = height.ToString(CultureInfo.InvariantCulture) + "f";



                string boundsText =
                    $"AbsoluteLayout.SetLayoutBounds({instance.Name}, new Rectangle({xString}, {yString}, {widthString}, {heightString}));";
                string flagsText = null;
                if(proportionalFlags.Count > 0)
                {
                    string flagsArguments = null;
                    for(int i = 0; i < proportionalFlags.Count; i++)
                    {
                        if(i > 0)
                        {
                            flagsArguments += " | ";
                        }
                        flagsArguments += proportionalFlags[i];
                    }
                    flagsText = $"AbsoluteLayout.SetLayoutFlags({instance.Name}, {flagsArguments});";
                }
                // assume every object has X, which it won't, so we will have to improve this
                if(string.IsNullOrWhiteSpace(flagsText))
                {
                    return boundsText;
                }
                else
                {
                    return $"{boundsText}\n{flagsText}";
                }
                //AbsoluteLayout.SetLayoutFlags(rightBox, AbsoluteLayoutFlags.PositionProportional);

            }
            else if(rootName == "Y" || 
                rootName == "Width" || 
                rootName == "Height" || 
                rootName == "Width Units" || 
                rootName == "Height Units" || 
                rootName == "X Units" || 
                rootName == "Y Units")
            {
                return " "; // force it to not process these:
            }

            return null;
        }

        private static string TryGetFullGumLineReplacement(InstanceSave instance, VariableSave variable)
        {
            var rootName = variable.GetRootName();
            if (rootName == "Parent")
            {
                return $"{variable.Value}.Children.Add({instance.Name});";
            }
            return null;
        }

        private static string VariableValueToGumCodeValue(VariableSave variable, ElementSave container)
        {
            if(variable.Value is float asFloat)
            {
                return asFloat.ToString(CultureInfo.InvariantCulture) + "f";
            }
            else if(variable.Value is string)
            {
                if(variable.GetRootName() == "Parent")
                {
                    return variable.Value.ToString();
                }
                else if(variable.IsState(container, out ElementSave categoryContainer, out StateSaveCategory category))
                {
                    var containerClassName = GetClassNameForType(categoryContainer.Name, VisualApi.Gum);
                    return $"{containerClassName}.{category.Name}.{variable.Value}";
                }
                else
                {
                    return "\"" + variable.Value + "\"";
                }
            }
            else if(variable.Value is bool)
            {
                return variable.Value.ToString().ToLowerInvariant();
            }
            else if(variable.Value.GetType().IsEnum)
            {
                var type = variable.Value.GetType();
                if(type == typeof(PositionUnitType))
                {
                    var converted = UnitConverter.ConvertToGeneralUnit(variable.Value);
                    return $"GeneralUnitType.{converted}";
                }
                else
                {
                    return variable.Value.GetType().Name + "." + variable.Value.ToString();
                }
            }
            else
            {
                return variable.Value?.ToString();
            }
        }

        private static string VariableValueToXamarinFormsCodeValue(VariableSave variable)
        {
            if (variable.Value is float asFloat)
            {
                var rootName = variable.GetRootName();
                // X and Y go to PixelX and PixelY
                if(rootName == "X" || rootName == "Y")
                {
                    return asFloat.ToString(CultureInfo.InvariantCulture) + "f";
                }
                else
                {
                    return asFloat.ToString(CultureInfo.InvariantCulture) + " / DeviceDisplay.MainDisplayInfo.Density";
                }
            }
            else if (variable.Value is string)
            {
                if (variable.GetRootName() == "Parent")
                {
                    return variable.Value.ToString();
                }
                else
                {
                    return "\"" + variable.Value + "\"";
                }
            }
            else if (variable.Value.GetType().IsEnum)
            {
                var type = variable.Value.GetType();
                if (type == typeof(PositionUnitType))
                {
                    var converted = UnitConverter.ConvertToGeneralUnit(variable.Value);
                    return $"GeneralUnitType.{converted}";
                }
                else
                {
                    return variable.Value.GetType().Name + "." + variable.Value.ToString();
                }
            }
            else
            {
                return variable.Value?.ToString();
            }
        }

        private static object GetGumVariableName(VariableSave variable, ElementSave container)
        {
            if(variable.IsState(container))
            {
                return variable.GetRootName().Replace(" ", "");
            }
            else
            {
                return variable.GetRootName().Replace(" ", "");
            }
        }

        private static string GetXamarinFormsVariableName(VariableSave variable)
        {
            var rootName = variable.GetRootName();

            switch(rootName)
            {
                case "Height": return "HeightRequest";
                case "Width": return "WidthRequest";
                case "X": return "PixelX";
                case "Y": return "PixelY";

                default: return rootName;
            }
        }

        private static VariableSave[] GetVariablesToConsider(InstanceSave instance)
        {
            var defaultState = SelectedState.Self.SelectedElement.DefaultState;
            var variablesToConsider = defaultState.Variables
                .Where(item =>
                {
                    return
                        item.Value != null &&
                        item.SetsValue &&
                        item.SourceObject == instance.Name;
                })
                .ToArray();
            return variablesToConsider;
        }

        private static string ToTabs(int tabCount) => new string(' ', tabCount * 4);
    }
}
