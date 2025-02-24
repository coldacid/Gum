﻿using CodeOutputPlugin.Models;
using Gum.Converters;
using Gum.DataTypes;
using Gum.DataTypes.Variables;
using Gum.Managers;
using Gum.ToolStates;
using RenderingLibrary.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeOutputPlugin.Manager
{
    #region Enums

    public enum VisualApi
    {
        Gum,
        XamarinForms
    }

    #endregion

    public static class CodeGenerator
    {
        public static int CanvasWidth { get; set; } = 480;
        public static int CanvasHeight { get; set; } = 854;

        /// <summary>
        /// if true, then pixel sizes are maintained regardless of pixel density. This allows layouts to maintain pixel-perfect
        /// </summary>
        public static bool AdjustPixelValuesForDensity { get; set; } = true;

        public static string GetCodeForElement(ElementSave element, CodeOutputElementSettings elementSettings, CodeOutputProjectSettings projectSettings)
        {
            #region Determine if XamarinForms Control
            VisualApi visualApi;
            var defaultState = element.DefaultState;
            var isXamForms = defaultState.GetValueRecursive($"IsXamarinFormsControl") as bool?;
            if (isXamForms == true)
            {
                visualApi = VisualApi.XamarinForms;
            }
            else
            {
                visualApi = VisualApi.Gum;
            }
            #endregion

            var stringBuilder = new StringBuilder();
            int tabCount = 0;

            #region Using Statements

            if(!string.IsNullOrWhiteSpace(projectSettings?.CommonUsingStatements))
            {
                stringBuilder.AppendLine(projectSettings.CommonUsingStatements);
            }

            if (!string.IsNullOrEmpty(elementSettings?.UsingStatements))
            {
                stringBuilder.AppendLine(elementSettings.UsingStatements);
            }
            #endregion

            #region Namespace Header/Opening {

            if (!string.IsNullOrEmpty(elementSettings?.Namespace))
            {
                stringBuilder.AppendLine(ToTabs(tabCount) + $"namespace {elementSettings.Namespace}");
                stringBuilder.AppendLine(ToTabs(tabCount) + "{");
                tabCount++;
            }

            #endregion

            #region Class Header/Opening {

            stringBuilder.AppendLine(ToTabs(tabCount) + $"partial class {GetClassNameForType(element.Name, visualApi)}");
            stringBuilder.AppendLine(ToTabs(tabCount) + "{");
            tabCount++;
            #endregion

            FillWithStateEnums(element, stringBuilder, tabCount);

            FillWithCurrentState(element, stringBuilder, tabCount);

            foreach (var instance in element.Instances.Where(item => item.DefinedByBase == false))
            {
                FillWithInstanceDeclaration(instance, element, stringBuilder, tabCount);
            }

            AddAbsoluteLayoutIfNecessary(element, tabCount, stringBuilder);

            stringBuilder.AppendLine();

            FillWithExposedVariables(element, stringBuilder, visualApi, tabCount);
            // -- no need for AppendLine here since FillWithExposedVariables does it after every variable --

            GenerateConstructor(element, visualApi, tabCount, stringBuilder);

            stringBuilder.AppendLine(ToTabs(tabCount) + "partial void CustomInitialize();");

            #region Class Closing }
            tabCount--;
            stringBuilder.AppendLine(ToTabs(tabCount) + "}");
            #endregion

            if (!string.IsNullOrEmpty(elementSettings?.Namespace))
            {
                tabCount--;
                stringBuilder.AppendLine(ToTabs(tabCount) + "}");
            }

            return stringBuilder.ToString();
        }

        private static void AddAbsoluteLayoutIfNecessary(ElementSave element, int tabCount, StringBuilder stringBuilder)
        {
            var elementBaseType = element?.BaseType;
            var isThisAbsoluteLayout = elementBaseType?.EndsWith("/AbsoluteLayout") == true;

            var isSkiaCanvasView = elementBaseType?.EndsWith("/SkiaGumCanvasView") == true;

            var isContainer = elementBaseType == "Container";

            if (!isThisAbsoluteLayout && !isSkiaCanvasView && !isContainer)
            {
                var shouldAddMainLayout = true;
                if (element is ScreenSave && !string.IsNullOrEmpty(element.BaseType))
                {
                    shouldAddMainLayout = false;
                }

                if (shouldAddMainLayout)
                {
                    stringBuilder.Append(ToTabs(tabCount) + "protected AbsoluteLayout MainLayout{get; private set;}");
                }
            }
        }

        private static void GenerateConstructor(ElementSave element, VisualApi visualApi, int tabCount, StringBuilder stringBuilder)
        {
            var elementName = GetClassNameForType(element.Name, visualApi);

            if(visualApi == VisualApi.Gum)
            {
                #region Constructor Header

                stringBuilder.AppendLine(ToTabs(tabCount) + $"public {elementName}(bool fullInstantiation = true)");

                stringBuilder.AppendLine(ToTabs(tabCount) + "{");
                tabCount++;

                #endregion

                #region Gum-required constructor code

                stringBuilder.AppendLine(ToTabs(tabCount) + "if(fullInstantiation)");
                stringBuilder.AppendLine(ToTabs(tabCount) + "{");
                tabCount++;

                if(element.BaseType == "Container")
                {
                    stringBuilder.AppendLine(ToTabs(tabCount) + "this.SetContainedObject(new InvisibleRenderable());");
                }

                stringBuilder.AppendLine();
                #endregion
            }
            else // xamarin forms
            {
                #region Constructor Header
                stringBuilder.AppendLine(ToTabs(tabCount) + $"public {elementName}()");

                stringBuilder.AppendLine(ToTabs(tabCount) + "{");
                tabCount++;

                #endregion


                stringBuilder.AppendLine(ToTabs(tabCount) + "var wasSuspended = GraphicalUiElement.IsAllLayoutSuspended;");
                stringBuilder.AppendLine(ToTabs(tabCount) + "GraphicalUiElement.IsAllLayoutSuspended = true;");

                var elementBaseType = element?.BaseType;
                var isThisAbsoluteLayout = elementBaseType?.EndsWith("/AbsoluteLayout") == true;

                var isSkiaCanvasView = elementBaseType?.EndsWith("/SkiaGumCanvasView") == true;

                if(isThisAbsoluteLayout)
                {
                    stringBuilder.AppendLine(ToTabs(tabCount) + "var MainLayout = this;");
                }
                else if(!isSkiaCanvasView)
                {
                    var shouldAddMainLayout = true;
                    if(element is ScreenSave && !string.IsNullOrEmpty(element.BaseType))
                    {
                        shouldAddMainLayout = false;
                    }

                    if(shouldAddMainLayout)
                    {
                        stringBuilder.AppendLine(ToTabs(tabCount) + "MainLayout = new AbsoluteLayout();");
                        stringBuilder.AppendLine(ToTabs(tabCount) + "BaseGrid.Children.Add(MainLayout);");
                    }
                }

            }

            FillWithVariableAssignments(element, visualApi, stringBuilder, tabCount);

            stringBuilder.AppendLine();

            foreach (var instance in element.Instances.Where(item => item.DefinedByBase == false))
            {
                FillWithInstanceInstantiation(instance, element, stringBuilder, tabCount);
            }
            stringBuilder.AppendLine();

            foreach (var instance in element.Instances)
            {
                FillWithVariableAssignments(instance, element, stringBuilder, tabCount);
                stringBuilder.AppendLine();
            }

            stringBuilder.AppendLine(ToTabs(tabCount) + "CustomInitialize();");

            if(visualApi == VisualApi.Gum)
            {
                // close the if check
                tabCount--;
                stringBuilder.AppendLine(ToTabs(tabCount) + "}");
            }
            else
            {
                stringBuilder.AppendLine(ToTabs(tabCount) + "GraphicalUiElement.IsAllLayoutSuspended = wasSuspended;");

            }


            tabCount--;
            stringBuilder.AppendLine(ToTabs(tabCount) + "}");
        }

        public static string GetCodeForState(ElementSave container, StateSave stateSave, VisualApi visualApi)
        {
            var stringBuilder = new StringBuilder();

            FillWithVariablesInState(container, stateSave, stringBuilder, 0);

            var code = stringBuilder.ToString();
            return code;
        }

        private static void FillWithVariablesInState(ElementSave container, StateSave stateSave, StringBuilder stringBuilder, int tabCount)
        {
            VariableSave[] variablesToConsider = stateSave.Variables
                // make "Parent" first
                .Where(item => item.GetRootName() != "Parent")
                .ToArray();

            var variableGroups = variablesToConsider.GroupBy(item => item.SourceObject);


            foreach(var group in variableGroups)
            {
                InstanceSave instance = null;
                var instanceName = group.Key;

                if (instanceName != null)
                {
                    instance = container.GetInstance(instanceName);
                }

                #region Determine visual API (Gum or Forms)

                VisualApi visualApi = VisualApi.Gum;

                var defaultState = container.DefaultState;
                bool? isXamForms = false;
                if (instance == null)
                {
                    isXamForms = defaultState.GetValueRecursive($"IsXamarinFormsControl") as bool?;
                }
                else
                {
                    isXamForms = defaultState.GetValueRecursive($"{instance.Name}.IsXamarinFormsControl") as bool?;
                }
                if (isXamForms == true)
                {
                    visualApi = VisualApi.XamarinForms;
                }

                #endregion

                ElementSave baseElement = null;
                if(instance == null)
                {
                    baseElement = Gum.Managers.ObjectFinder.Self.GetElementSave(container.BaseType) ?? container;
                }
                else
                {
                    baseElement = Gum.Managers.ObjectFinder.Self.GetElementSave(instance?.BaseType);
                }
                var baseDefaultState = baseElement?.DefaultState;
                RecursiveVariableFinder baseRecursiveVariableFinder = new RecursiveVariableFinder(baseDefaultState);


                List<VariableSave> variablesForThisInstance = group
                    .Where(item => GetIfVariableShouldBeIncludedForInstance(instance, item, baseRecursiveVariableFinder))
                    .ToList();


                ProcessVariableGroups(variablesForThisInstance, stateSave, instance, container, visualApi, stringBuilder, tabCount);

                // Now that they've been processed, we can process the remainder regularly
                foreach (var variable in variablesForThisInstance)
                {
                    var codeLine = GetCodeLine(instance, variable, container, visualApi, stateSave);
                    stringBuilder.AppendLine(ToTabs(tabCount) + codeLine);
                    var suffixCodeLine = GetSuffixCodeLine(instance, variable, visualApi);
                    if (!string.IsNullOrEmpty(suffixCodeLine))
                    {
                        stringBuilder.AppendLine(ToTabs(tabCount) + suffixCodeLine);
                    }
                }

            }
        }

        private static void FillWithStateEnums(ElementSave element, StringBuilder stringBuilder, int tabCount)
        {
            // for now we'll just do categories. We may need to get uncategorized at some point...
            foreach(var category in element.Categories)
            {
                string enumName = category.Name;

                stringBuilder.AppendLine(ToTabs(tabCount) + $"public enum {category.Name}");
                stringBuilder.AppendLine(ToTabs(tabCount) + "{");
                tabCount++;

                foreach(var state in category.States)
                {
                    stringBuilder.AppendLine(ToTabs(tabCount) + $"{state.Name},");
                }

                stringBuilder.AppendLine(ToTabs(tabCount) + "}");
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

                    FillWithVariablesInState(element, state, stringBuilder, tabCount);

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

            FillWithInstanceDeclaration(instance, element, stringBuilder);

            FillWithInstanceInstantiation(instance, element, stringBuilder);

            FillWithVariableAssignments(instance, element, stringBuilder);

            var code = stringBuilder.ToString();
            return code;
        }

        private static void FillWithInstanceInstantiation(InstanceSave instance, ElementSave element, StringBuilder stringBuilder, int tabCount = 0)
        {
            var strippedType = instance.BaseType;
            if (strippedType.Contains("/"))
            {
                strippedType = strippedType.Substring(strippedType.LastIndexOf("/") + 1);
            }
            var tabs = new String(' ', 4 * tabCount);

            VisualApi visualApi = VisualApi.Gum;

            var defaultState = element.DefaultState;
            var isXamForms = defaultState.GetValueRecursive($"{instance.Name}.IsXamarinFormsControl") as bool?;
            if(isXamForms == true)
            {
                visualApi = VisualApi.XamarinForms;
            }

            stringBuilder.AppendLine($"{tabs}{instance.Name} = new {GetClassNameForType(instance.BaseType, visualApi)}();");
        }

        private static void FillWithVariableAssignments(ElementSave element, VisualApi visualApi, StringBuilder stringBuilder, int tabCount = 0)
        {
            #region Get variables to consider
            var defaultState = SelectedState.Self.SelectedElement.DefaultState;

            var baseElement = ObjectFinder.Self.GetElementSave(element.BaseType);
            RecursiveVariableFinder recursiveVariableFinder = null;

            // This is null if it's a screen, or there's some bad reference
            if(baseElement != null)
            {
                recursiveVariableFinder = new RecursiveVariableFinder(baseElement.DefaultState);
            }

            var variablesToConsider = defaultState.Variables
                .Where(item =>
                {
                    var shouldInclude = 
                        item.Value != null &&
                        item.SetsValue &&
                        string.IsNullOrEmpty(item.SourceObject);

                    if(shouldInclude)
                    {
                        if(recursiveVariableFinder != null)
                        {
                            // We want to make sure that the variable is defined in the base object. If it isn't, then
                            // it could be a leftover variable caused by having this object be of one type, using a variable
                            // specific to that type, then changing it to another type. Gum holds on to these varibles in case
                            // the type change was accidental, but it means we have to watch for these orphan variables when generating.
                            var foundVariable = recursiveVariableFinder.GetVariable(item.Name);
                            shouldInclude = foundVariable != null;
                        }
                        else
                        {
                            if(item.Name.EndsWith("State"))
                            {
                                var type = item.Type.Substring(item.Type.Length - 5);
                                var hasCategory = element.GetStateSaveCategoryRecursively(type) != null;

                                if(!hasCategory)
                                {
                                    shouldInclude = false;
                                }
                            }
                        }

                    }

                    return shouldInclude;
                })
                .ToList();

            #endregion

            var tabs = new String(' ', 4 * tabCount);

            ProcessVariableGroups(variablesToConsider, defaultState, null, element, visualApi, stringBuilder, tabCount);
            
            foreach (var variable in variablesToConsider)
            {
                var codeLine = GetCodeLine(null, variable, element, visualApi, defaultState);
                stringBuilder.AppendLine(tabs + codeLine);

                var suffixCodeLine = GetSuffixCodeLine(null, variable, visualApi);
                if (!string.IsNullOrEmpty(suffixCodeLine))
                {
                    stringBuilder.AppendLine(tabs + suffixCodeLine);
                }
            }
        }

        private static void FillWithVariableAssignments(InstanceSave instance, ElementSave container, StringBuilder stringBuilder, int tabCount = 0)
        {
            #region Get variables to consider

            var variablesToConsider = GetVariablesToConsider(instance)
                // make "Parent" first
                // .. actually we need to make parent last so that it can properly assign parent on scrollables
                .OrderBy(item => item.GetRootName() == "Parent")
                .ToList();

            #endregion

            #region Determine visual API (Gum or Forms)

            VisualApi visualApi = VisualApi.Gum;

            var defaultState = container.DefaultState;
            var isXamForms = defaultState.GetValueRecursive($"{instance.Name}.IsXamarinFormsControl") as bool?;
            if (isXamForms == true)
            {
                visualApi = VisualApi.XamarinForms;
            }

            #endregion

            var tabs = new String(' ', 4 * tabCount);

            #region Name/Automation Id

            if (visualApi == VisualApi.Gum)
            {
                stringBuilder.AppendLine($"{tabs}{instance.Name}.Name = \"{instance.Name}\";");
            }
            else
            {
                // If defined by base, then the automation ID will already be set there, and 
                // Xamarin.Forms doesn't like an automation ID being set 2x
                if(instance.DefinedByBase == false)
                {
                    stringBuilder.AppendLine($"{tabs}{instance.Name}.AutomationId = \"{instance.Name}\";");
                }
            }

            #endregion


            // sometimes variables have to be processed in groups. For example, RGB values
            // have to be assigned all at once in a Color value in XamForms;
            ProcessVariableGroups(variablesToConsider, container.DefaultState, instance, container, visualApi, stringBuilder, tabCount);

            foreach (var variable in variablesToConsider)
            {
                var codeLine = GetCodeLine(instance, variable, container, visualApi, defaultState);

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

            // For scrollable GumContainers we need to have the parent assigned *after* the AbsoluteLayout rectangle:
            #region Assign Parent

            var hasParent = variablesToConsider.Any(item => item.GetRootName() == "Parent");

            if (!hasParent && !instance.DefinedByBase)
            {

                if(visualApi == VisualApi.Gum)
                {
                    // add it to "this"
                    stringBuilder.AppendLine($"{tabs}this.Children.Add({instance.Name});");
                }
                else // forms
                {
                    var instanceBaseType = instance.BaseType;
                    var isGumCollectionView = instanceBaseType.EndsWith("/GumCollectionView");

                    if(isGumCollectionView)
                    {
                        stringBuilder.AppendLine($"{tabs}var tempFor{instance.Name} = GumScrollBar.CreateScrollableAbsoluteLayout({instance.Name}, ScrollableLayoutParentPlacement.Free);");
                        stringBuilder.AppendLine($"{tabs}MainLayout.Children.Add(tempFor{instance.Name});");
                    }
                    else
                    {
                        stringBuilder.AppendLine($"{tabs}MainLayout.Children.Add({instance.Name});");
                    }
                }
            }

            #endregion
        }

        private static void ProcessVariableGroups(List<VariableSave> variablesToConsider, StateSave defaultState, InstanceSave instance, ElementSave container, VisualApi visualApi, StringBuilder stringBuilder, int tabCount)
        {
            if(visualApi == VisualApi.XamarinForms)
            {
                string baseType = null;
                if (instance != null)
                {
                    baseType = instance.BaseType;
                }
                else
                {
                    baseType = container.BaseType;
                }
                switch(baseType)
                {
                    case "Text":
                        ProcessColorForLabel(variablesToConsider, defaultState, instance, stringBuilder);
                        ProcessPositionAndSize(variablesToConsider, defaultState, instance, stringBuilder, tabCount);
                        break;
                    default:
                        ProcessPositionAndSize(variablesToConsider, defaultState, instance, stringBuilder, tabCount);
                        break;
                }
            }
        }

        private static void ProcessColorForLabel(List<VariableSave> variablesToConsider, StateSave defaultState, InstanceSave instance, StringBuilder stringBuilder)
        {
            var instanceName = instance.Name;
            var rfv = new RecursiveVariableFinder(defaultState);

            var red = rfv.GetValue<int>(instanceName + ".Red");
            var green = rfv.GetValue<int>(instanceName + ".Green");
            var blue = rfv.GetValue<int>(instanceName + ".Blue");
            var alpha = rfv.GetValue<int>(instanceName + ".Alpha");

            variablesToConsider.RemoveAll(item => item.Name == instanceName + ".Red");
            variablesToConsider.RemoveAll(item => item.Name == instanceName + ".Green");
            variablesToConsider.RemoveAll(item => item.Name == instanceName + ".Blue");
            variablesToConsider.RemoveAll(item => item.Name == instanceName + ".Alpha");

            stringBuilder.AppendLine($"{instanceName}.TextColor = Color.FromRgba({red}, {green}, {blue}, {alpha});");
        }

        private static void ProcessPositionAndSize(List<VariableSave> variablesToConsider, StateSave defaultState, InstanceSave instance, StringBuilder stringBuilder, int tabCount)
        {


            string prefix = instance?.Name == null ? "" : instance.Name + ".";

            var setsAny =
                defaultState.Variables.Any(item =>
                    item.Name == prefix + "X" ||
                    item.Name == prefix + "Y" ||
                    item.Name == prefix + "Width" ||
                    item.Name == prefix + "Height" ||

                    item.Name == prefix + "X Units" ||
                    item.Name == prefix + "Y Units" ||
                    item.Name == prefix + "Width Units" ||
                    item.Name == prefix + "Height Units"||
                    item.Name == prefix + "X Origin" ||
                    item.Name == prefix + "Y Origin" 
                    
                    );

            if(setsAny)
            {
                var variableFinder = new RecursiveVariableFinder(defaultState);

                var x = variableFinder.GetValue<float>(prefix + "X");
                var y = variableFinder.GetValue<float>(prefix + "Y");
                var width = variableFinder.GetValue<float>(prefix + "Width");
                var height = variableFinder.GetValue<float>(prefix + "Height");

                var xUnits = variableFinder.GetValue<PositionUnitType>(prefix + "X Units");
                var yUnits = variableFinder.GetValue<PositionUnitType>(prefix + "Y Units");
                var widthUnits = variableFinder.GetValue<DimensionUnitType>(prefix + "Width Units");
                var heightUnits = variableFinder.GetValue<DimensionUnitType>(prefix + "Height Units");

                var xOrigin = variableFinder.GetValue<HorizontalAlignment>(prefix + "X Origin");
                var yOrigin = variableFinder.GetValue<VerticalAlignment>(prefix + "Y Origin");

                variablesToConsider.RemoveAll(item => item.Name == prefix + "X");
                variablesToConsider.RemoveAll(item => item.Name == prefix + "Y");
                variablesToConsider.RemoveAll(item => item.Name == prefix + "Width");
                variablesToConsider.RemoveAll(item => item.Name == prefix + "Height");
                variablesToConsider.RemoveAll(item => item.Name == prefix + "X Units");
                variablesToConsider.RemoveAll(item => item.Name == prefix + "Y Units");
                variablesToConsider.RemoveAll(item => item.Name == prefix + "Width Units");
                variablesToConsider.RemoveAll(item => item.Name == prefix + "Height Units");

                List<string> proportionalFlags = new List<string>();

                const string WidthProportionalFlag = "AbsoluteLayoutFlags.WidthProportional";
                const string HeightProportionalFlag = "AbsoluteLayoutFlags.HeightProportional";
                const string XProportionalFlag = "AbsoluteLayoutFlags.XProportional";
                const string YProportionalFlag = "AbsoluteLayoutFlags.YProportional";

                if (widthUnits == DimensionUnitType.Percentage)
                {
                    width /= 100.0f;
                    proportionalFlags.Add(WidthProportionalFlag);
                }
                else if(widthUnits == DimensionUnitType.RelativeToContainer)
                {
                    if(width == 0)
                    {
                        width = 1;
                        proportionalFlags.Add(WidthProportionalFlag);
                    }
                    else
                    {
                        // not allowed!!!
                    }
                }
                if (heightUnits == DimensionUnitType.Percentage)
                {
                    height /= 100.0f;
                    proportionalFlags.Add(HeightProportionalFlag);
                }
                else if(heightUnits == DimensionUnitType.RelativeToContainer)
                {
                    if(height == 0)
                    {
                        height = 1;
                        proportionalFlags.Add(HeightProportionalFlag);
                    }
                    else
                    {
                        // not allowed!
                    }
                }

                // special case
                // If we're using the center with x=0 we'll pretend it's the same as 50% 
                if(xUnits == PositionUnitType.PixelsFromCenterX && widthUnits == DimensionUnitType.Absolute && xOrigin == HorizontalAlignment.Center)
                {
                    if(x == 0)
                    {
                        // treat it like it's 50%:
                        x = .5f;
                        proportionalFlags.Add(XProportionalFlag);
                    }
                }
                // Xamarin forms uses a weird anchoring system to combine both position and anchor into one value. Gum splits those into two values
                // We need to convert from the gum units to xamforms units:
                // for now assume it's all %'s:

                else if (xUnits == PositionUnitType.PercentageWidth)
                {
                    x /= 100.0f;
                    var adjustedCanvasWidth = 1 - width;
                    if (adjustedCanvasWidth > 0)
                    {
                        x /= adjustedCanvasWidth;
                    }
                    proportionalFlags.Add(XProportionalFlag);
                }
                else if(xUnits == PositionUnitType.PixelsFromLeft)
                {

                }
                else if(xUnits == PositionUnitType.PixelsFromCenterX)
                {
                    if(widthUnits == DimensionUnitType.Absolute)
                    {
                        x = (CanvasWidth - width) / 2.0f;
                    }
                }

                if(yUnits == PositionUnitType.PixelsFromCenterY && heightUnits == DimensionUnitType.Absolute && yOrigin == VerticalAlignment.Center)
                {
                    if(y == 0)
                    {
                        y = .5f;
                        proportionalFlags.Add(YProportionalFlag);
                    }
                }
                else if (yUnits == PositionUnitType.PercentageHeight)
                {
                    y /= 100.0f;
                    var adjustedCanvasHeight = 1 - height;
                    if (adjustedCanvasHeight > 0)
                    {
                        y /= adjustedCanvasHeight;
                    }
                    proportionalFlags.Add(YProportionalFlag);
                }
                else if(yUnits == PositionUnitType.PixelsFromCenterY)
                {
                    if(heightUnits == DimensionUnitType.Absolute)
                    {
                        y = (CanvasHeight - height) / 2.0f;
                    }
                }
                else if(yUnits == PositionUnitType.PixelsFromBottom)
                {
                    y += CanvasHeight;

                    if(yOrigin == VerticalAlignment.Bottom)
                    {
                        y -= height;
                    }
                }




                var xString = x.ToString(CultureInfo.InvariantCulture) + "f";
                var yString = y.ToString(CultureInfo.InvariantCulture) + "f";
                var widthString = width.ToString(CultureInfo.InvariantCulture) + "f";
                var heightString = height.ToString(CultureInfo.InvariantCulture) + "f";

                if(AdjustPixelValuesForDensity)
                {
                    if(proportionalFlags.Contains(XProportionalFlag) == false)
                    {
                        xString += "/Xamarin.Essentials.DeviceDisplay.MainDisplayInfo.Density";
                    }
                    if(proportionalFlags.Contains(YProportionalFlag) == false)
                    {
                        yString += "/Xamarin.Essentials.DeviceDisplay.MainDisplayInfo.Density";
                    }
                    if(proportionalFlags.Contains(WidthProportionalFlag) == false)
                    {
                        widthString += "/Xamarin.Essentials.DeviceDisplay.MainDisplayInfo.Density";
                    }
                    if(proportionalFlags.Contains(HeightProportionalFlag) == false)
                    {
                        heightString += "/Xamarin.Essentials.DeviceDisplay.MainDisplayInfo.Density";
                    }
                }

                string boundsText =
                    $"{ToTabs(tabCount)}AbsoluteLayout.SetLayoutBounds({instance?.Name ?? "this"}, new Rectangle({xString}, {yString}, {widthString}, {heightString}));";
                string flagsText = null;
                if (proportionalFlags.Count > 0)
                {
                    string flagsArguments = null;
                    for (int i = 0; i < proportionalFlags.Count; i++)
                    {
                        if (i > 0)
                        {
                            flagsArguments += " | ";
                        }
                        flagsArguments += proportionalFlags[i];
                    }
                    flagsText = $"{ToTabs(tabCount)}AbsoluteLayout.SetLayoutFlags({instance?.Name ?? "this"}, {flagsArguments});";
                }
                // assume every object has X, which it won't, so we will have to improve this
                if (string.IsNullOrWhiteSpace(flagsText))
                {
                    stringBuilder.AppendLine(boundsText);
                }
                else
                {
                    stringBuilder.AppendLine($"{boundsText}\n{flagsText}");
                }
            }

        }

        private static void FillWithInstanceDeclaration(InstanceSave instance, ElementSave container, StringBuilder stringBuilder, int tabCount = 0)
        {
            VisualApi visualApi = VisualApi.Gum;

            var defaultState = container.DefaultState;
            var isXamForms = defaultState.GetValueRecursive($"{instance.Name}.IsXamarinFormsControl") as bool?;
            if (isXamForms == true)
            {
                visualApi = VisualApi.XamarinForms;
            }

            var tabs = new String(' ', 4 * tabCount);

            string className = GetClassNameForType(instance.BaseType, visualApi);

            bool isPublic = true;
            string accessString = isPublic ? "public " : "";

            stringBuilder.AppendLine($"{tabs}{accessString}{className} {instance.Name} {{ get; private set; }}");
        }

        private static string GetClassNameForType(string gumType, VisualApi visualApi)
        {
            string className = null;
            var specialHandledCase = false;

            if(visualApi == VisualApi.XamarinForms)
            {
                switch(gumType)
                {
                    case "Text":
                        className = "Label";
                        specialHandledCase = true;
                        break;
                }
            }

            if(!specialHandledCase)
            {

                var strippedType = gumType;
                if (strippedType.Contains("/"))
                {
                    strippedType = strippedType.Substring(strippedType.LastIndexOf("/") + 1);
                }

                string suffix = visualApi == VisualApi.Gum ? "Runtime" : "";
                className = $"{strippedType}{suffix}";

            }
            return className;
        }

        private static string GetSuffixCodeLine(InstanceSave instance, VariableSave variable, VisualApi visualApi)
        {
            if(visualApi == VisualApi.XamarinForms)
            {
                var rootName = variable.GetRootName();

                //switch(rootName)
                //{
                    // We don't do this anymore now that we are stuffing forms objects in absolute layouts
                    //case "Width": return $"{instance.Name}.HorizontalOptions = LayoutOptions.Start;";
                    //case "Height": return $"{instance.Name}.VerticalOptions = LayoutOptions.Start;";
                //}
            }

            return null;
        }

        private static string GetCodeLine(InstanceSave instance, VariableSave variable, ElementSave container, VisualApi visualApi, StateSave state)
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
                var fullLineReplacement = TryGetFullXamarinFormsLineReplacement(instance, container, variable, state);
                if(fullLineReplacement != null)
                {
                    return fullLineReplacement;
                }
                else
                {
                    return $"{instancePrefix}{GetXamarinFormsVariableName(variable)} = {VariableValueToXamarinFormsCodeValue(variable, container)};";
                }

            }
        }

        private static string TryGetFullXamarinFormsLineReplacement(InstanceSave instance, ElementSave container, VariableSave variable, StateSave state)
        {
            var rootName = variable.GetRootName();
            
            if(rootName == "IsXamarinFormsControl" ||
                rootName == "Name" ||
                rootName == "X Origin" ||
                rootName == "XOrigin" ||
                rootName == "Y Origin" ||
                rootName == "YOrigin")
            {
                return " "; // Don't do anything with these variables::
            }
            else if(rootName == "Parent")
            {
                var parentName = variable.Value as string;

                var parentInstance = container.GetInstance(parentName);

                var hasContent =
                    parentInstance?.BaseType.EndsWith("/ScrollView") == true ||
                    parentInstance?.BaseType.EndsWith("/StickyScrollView") == true;
                if(hasContent)
                {
                    return $"{parentName}.Content = {instance.Name};";
                }
                else
                {
                    return $"{parentName}.Children.Add({instance.Name});";
                }
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
            // ignored variables:
            else if(rootName == "IsXamarinFormsControl" ||
                rootName == "ClipsChildren" ||
                rootName == "ExposeChildrenEvents" ||
                rootName == "HasEvents")
            {
                return " "; 
            }
            return null;
        }

        private static string VariableValueToGumCodeValue(VariableSave variable, ElementSave container)
        {
            if(variable.Value is float asFloat)
            {
                return asFloat.ToString(CultureInfo.InvariantCulture) + "f";
            }
            else if(variable.Value is string asString)
            {
                if(variable.GetRootName() == "Parent")
                {
                    return asString;
                }
                else if(variable.IsState(container, out ElementSave categoryContainer, out StateSaveCategory category))
                {
                    if(categoryContainer != null && category != null)
                    {
                        string containerClassName = "VariableState";
                        if (categoryContainer != null)
                        {
                            containerClassName = GetClassNameForType(categoryContainer.Name, VisualApi.Gum);
                        }
                        return $"{containerClassName}.{category.Name}.{asString}";
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return "\"" + asString.Replace("\n", "\\n") + "\"";
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

        private static string VariableValueToXamarinFormsCodeValue(VariableSave variable, ElementSave container)
        {
            if (variable.Value is float asFloat)
            {
                var rootName = variable.GetRootName();
                // X and Y go to PixelX and PixelY
                if(rootName == "X" || rootName == "Y")
                {
                    return asFloat.ToString(CultureInfo.InvariantCulture) + "f";
                }
                else if(rootName == "CornerRadius")
                {
                    return $"(int)({asFloat.ToString(CultureInfo.InvariantCulture)} / DeviceDisplay.MainDisplayInfo.Density)";
                }
                else
                {
                    return $"{asFloat.ToString(CultureInfo.InvariantCulture)} / DeviceDisplay.MainDisplayInfo.Density";
                }
            }
            else if (variable.Value is string asString)
            {
                if (variable.GetRootName() == "Parent")
                {
                    return variable.Value.ToString();
                }
                else if (variable.IsState(container, out ElementSave categoryContainer, out StateSaveCategory category))
                {
                    var containerClassName = GetClassNameForType(categoryContainer.Name, VisualApi.XamarinForms);
                    return $"{containerClassName}.{category.Name}.{variable.Value}";
                }
                else
                {
                    return "\"" + asString.Replace("\n", "\\n") + "\"";
                }
            }
            else if(variable.Value is bool)
            {
                return variable.Value.ToString().ToLowerInvariant();
            }
            else if (variable.Value.GetType().IsEnum)
            {
                var type = variable.Value.GetType();
                if (type == typeof(PositionUnitType))
                {
                    var converted = UnitConverter.ConvertToGeneralUnit(variable.Value);
                    return $"GeneralUnitType.{converted}";
                }
                else if(type == typeof(HorizontalAlignment))
                {
                    switch((HorizontalAlignment)variable.Value)
                    {
                        case HorizontalAlignment.Left:
                            return "Xamarin.Forms.TextAlignment.Start";
                        case HorizontalAlignment.Center:
                            return "Xamarin.Forms.TextAlignment.Center";
                        case HorizontalAlignment.Right:
                            return "Xamarin.Forms.TextAlignment.End";
                        default:
                            return "";
                    }
                }
                else if(type == typeof(VerticalAlignment))
                {
                    switch((VerticalAlignment)variable.Value)
                    {
                        case VerticalAlignment.Top:
                            return "Xamarin.Forms.TextAlignment.Start";
                        case VerticalAlignment.Center:
                            return "Xamarin.Forms.TextAlignment.Center";
                        case VerticalAlignment.Bottom:
                            return "Xamarin.Forms.TextAlignment.End";
                        default:
                            return "";
                    }
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
                case "Visible": return "IsVisible";
                case "HorizontalAlignment": return "HorizontalTextAlignment";
                case "VerticalAlignment": return "VerticalTextAlignment";

                default: return rootName;
            }
        }

        private static VariableSave[] GetVariablesToConsider(InstanceSave instance)
        {
            var baseElement = Gum.Managers.ObjectFinder.Self.GetElementSave(instance.BaseType);
            if(baseElement == null)
            {
                // this could happen if the project references an object that has a missing type. Tolerate it, return an empty l ist
                return new VariableSave[0];
            }
            else
            {
                var baseDefaultState = baseElement?.DefaultState;
                RecursiveVariableFinder baseRecursiveVariableFinder = new RecursiveVariableFinder(baseDefaultState);

                var defaultState = SelectedState.Self.SelectedElement.DefaultState;
                var variablesToConsider = defaultState.Variables
                    .Where(item =>
                    {
                        return GetIfVariableShouldBeIncludedForInstance(instance, item, baseRecursiveVariableFinder);
                    })
                    .ToArray();
                return variablesToConsider;
            }
        }

        private static bool GetIfVariableShouldBeIncludedForInstance(InstanceSave instance, VariableSave item, RecursiveVariableFinder baseRecursiveVariableFinder)
        {
            var shouldInclude =
                                    item.Value != null &&
                                    item.SetsValue &&
                                    item.SourceObject == instance?.Name;

            if (shouldInclude)
            {
                var foundVariable = baseRecursiveVariableFinder.GetVariable(item.GetRootName());
                shouldInclude = foundVariable != null;
            }

            return shouldInclude;
        }

        private static string ToTabs(int tabCount) => new string(' ', tabCount * 4);
    }
}
