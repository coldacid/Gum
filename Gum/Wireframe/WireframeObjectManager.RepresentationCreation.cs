﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RenderingLibrary;
using Gum.DataTypes;
using Gum.Managers;
using RenderingLibrary.Graphics;
using RenderingLibrary.Content;
using Gum.DataTypes.Variables;
using RenderingLibrary.Math.Geometry;
using Gum.DataTypes;
using Gum.RenderingLibrary;
using Gum.ToolStates;
using RenderingLibrary.Graphics.Fonts;
using System.Collections;
using ToolsUtilities;
using Microsoft.Xna.Framework;
using Gum.Converters;
using Gum.PropertyGridHelpers.Converters;
using GumRuntime;

namespace Gum.Wireframe
{
    public partial class WireframeObjectManager
    {

        #region Fields


        public string[] PositionAndSizeVariables = new string[]{
            "Width",
            "Height",
            "Width Units",
            "Height Units",
            "X Origin",
            "Y Origin",
            "X",
            "Y",
            "X Units",
            "Y Units",
            "Guide",
            "Parent"
        
        };

        public string[] ColorAndAlpha = new string[]{
            "Red",
            "Green",
            "Blue",
            "Alpha"
        };

        public string[] Color = new string[]{
            "Red",
            "Green",
            "Blue"
        };

        #endregion

        private void CreateIpsoForElement(ElementSave elementSave)
        {
            GraphicalUiElement rootIpso = null;

            bool isScreen = elementSave is ScreenSave;

            if (isScreen == false)
            {
                rootIpso = new GraphicalUiElement(null, null);

                // We used to not add the IPSO for the root element to the list of graphical elements
                // and this prevented selection.  I'm not sure if this was intentionally left out or not
                // but I think it should be here
                mGraphicalElements.Add(rootIpso);

                if (elementSave.BaseType == "Sprite" || elementSave.Name == "Sprite")
                {
                    CreateSpriteFor(elementSave, rootIpso);
                }
                else if (elementSave.BaseType == "Text" || elementSave.Name == "Text")
                {
                    CreateTextFor(elementSave, rootIpso);
                }
                else if (elementSave.BaseType == "NineSlice" || elementSave.Name == "NineSlice")
                {
                    CreateNineSliceFor(elementSave, rootIpso);
                }
                else if (elementSave.BaseType == "ColoredRectangle" || elementSave.Name == "ColoredRectangle")
                {
                    CreateSolidRectangleFor(elementSave, rootIpso);
                }
                else
                {
                    CreateRectangleFor(elementSave, rootIpso);
                }


                RecursiveVariableFinder rvf = new DataTypes.RecursiveVariableFinder(SelectedState.Self.SelectedStateSave);

                string guide = rvf.GetValue<string>("Guide");
                SetGuideParent(null, rootIpso, guide, true);
            }
            List<GraphicalUiElement> newlyAdded = new List<GraphicalUiElement>();


            List<ElementWithState> elementStack = new List<ElementWithState>();

            ElementWithState elementWithState = new ElementWithState(elementSave);
            if (elementSave == SelectedState.Self.SelectedElement)
            {
                if (SelectedState.Self.SelectedStateSave != null)
                {
                    elementWithState.StateName = SelectedState.Self.SelectedStateSave.Name;
                }
                else
                {
                    elementWithState.StateName = "Default";
                }
            }

            elementStack.Add(elementWithState);

            // parallel screws up the ordering of objects, so we'll do it on the primary thread for now
            // and parallelize it later:
            //Parallel.ForEach(elementSave.Instances, instance =>
            foreach (var instance in elementSave.Instances)
            {
                GraphicalUiElement child = CreateRepresentationForInstance(instance, null, elementStack, rootIpso);

                if (child == null)
                {
                    // This can occur
                    // if an instance references
                    // a component that doesn't exist.
                    // I don't think we need to do anything
                    // here.
                }
                else
                {
                    newlyAdded.Add(child);
                    mGraphicalElements.Add(child);
                }
            }

                
            SetUpParentRelationship(newlyAdded, elementStack, elementSave.Instances);

            //);
            elementStack.Remove(elementStack.FirstOrDefault(item => item.Element == elementSave));
        }

        private GraphicalUiElement CreateRepresentationForInstance(InstanceSave instance, InstanceSave parentInstance, List<ElementWithState> elementStack, GraphicalUiElement container)
        {
            IPositionedSizedObject newIpso = null;

            GraphicalUiElement element = newIpso as GraphicalUiElement;

            if (instance.BaseType == "Sprite")
            {

                element = new GraphicalUiElement(null, container);

                CreateSpriteFor(instance, elementStack, container, element);
            }
            else if (instance.BaseType == "Text")
            {
                element = new GraphicalUiElement(null, container);
                CreateTextFor(instance, elementStack, container, element);
            }
            else if (instance.BaseType == "ColoredRectangle")
            {
                element = new GraphicalUiElement(null, container);
                CreateSolidRectangleFor(instance, elementStack, element);
            }
            else if (instance.BaseType == "NineSlice")
            {
                element = new GraphicalUiElement(null, container);
                CreateNineSliceFor(instance, elementStack, element);
            }
            else if (instance.IsComponent())
            {
                // Here we need to prefix the name before going any deeper, so we won't won't prefix the name at the end of the method
                //shouldPrefixParentName = false;

                //string prefixName = null;
                //if (parentIpso != null)
                //{
                //prefixName = parentIpso.Name + ".";
                //}
                newIpso = CreateRepresentationsForInstanceFromComponent(instance, elementStack, parentInstance, container, ObjectFinder.Self.GetComponent(instance.BaseType));
            }
            else
            {
                // Make sure the base type is valid.
                // This could be null if a base type changed
                // its name but the derived wasn't updated, or 
                // if someone screwed with the XML files.  Who knows...
                var baseElement = ObjectFinder.Self.GetElementSave(instance.BaseType);

                if (baseElement != null)
                {
                    element = new GraphicalUiElement(null, container);

                    CreateRectangleFor(instance, elementStack, element);
                }
            }
            //if (shouldPrefixParentName)
            //{
            //    string prefixName = null;
            //    if (parentIpso != null)
            //    {
            //        prefixName = parentIpso.Name + ".";
            //    }

            //    toReturn.Name = prefixName + toReturn.Name;

            //}

            // This was an attempt to fix sorting when parallelized
            //if (toReturn != null)
            //{
            //    if(parentInstance == null)
            //    {
            //        var parent = instance.ParentContainer;

            //        if(parent != null)
            //        {
            //            toReturn.Z = parent.Instances.IndexOf(instance);
            //        }
            //    }
            //}



            if (newIpso != null && (newIpso is GraphicalUiElement) == false)
            {
                element = new GraphicalUiElement(newIpso as IRenderable, container);
            }
            else if (newIpso is GraphicalUiElement)
            {
                element = newIpso as GraphicalUiElement;
            }

            if(element != null)
            {
                var selectedState = SelectedState.Self.SelectedStateSave;
                if (selectedState == null)
                {
                    selectedState = SelectedState.Self.SelectedElement.DefaultState;
                }

                RecursiveVariableFinder rvf = new DataTypes.RecursiveVariableFinder(selectedState);

                string guide = rvf.GetValue<string>("Guide");
                string parent = rvf.GetValue<string>(instance.Name + ".Parent");

                SetGuideParent(container, element, guide, container == null || parent == AvailableInstancesConverter.ScreenBoundsName);
            }



            return element;
        }

        private IPositionedSizedObject CreateRepresentationsForInstanceFromComponent(InstanceSave instance, 
            List<ElementWithState> elementStack, InstanceSave parentInstance, GraphicalUiElement parentIpso, 
            ComponentSave baseComponentSave)
        {
            StandardElementSave ses = ObjectFinder.Self.GetRootStandardElementSave(instance);

            GraphicalUiElement rootIpso = null;
            if (ses != null)
            {
                rootIpso = new GraphicalUiElement(null, parentIpso);

                if (ses.Name == "Sprite")
                {
                    CreateSpriteFor(instance, elementStack, parentIpso, rootIpso);
                }
                else if (ses.Name == "Text")
                {
                    CreateTextFor(instance, elementStack, parentIpso, rootIpso);
                }
                else if (ses.Name == "ColoredRectangle")
                {
                    CreateSolidRectangleFor(instance, elementStack, rootIpso);
                }
                else if (ses.Name == "NineSlice")
                {
                    CreateNineSliceFor(instance, elementStack, rootIpso);
                }
                else
                {
                    CreateRectangleFor(instance, elementStack, rootIpso);
                }

                var selectedState = SelectedState.Self.SelectedStateSave;

                if (selectedState == null)
                {
                    selectedState = SelectedState.Self.SelectedElement.DefaultState;
                }

                RecursiveVariableFinder rvf = new DataTypes.RecursiveVariableFinder(selectedState);

                string guide = rvf.GetValue<string>("Guide");
                SetGuideParent(parentIpso, rootIpso, guide, false);

                ElementWithState elementWithState = new ElementWithState(baseComponentSave);
                var state = new DataTypes.RecursiveVariableFinder(instance, elementStack).GetValue("State") as string;
                elementWithState.StateName = state;
                elementWithState.InstanceName = instance.Name;
                elementStack.Add(elementWithState);

                foreach (InstanceSave internalInstance in baseComponentSave.Instances)
                {
                    GraphicalUiElement createdIpso = CreateRepresentationForInstance(internalInstance, instance, elementStack, rootIpso);

                }

                SetUpParentRelationship(rootIpso.ContainedElements, elementStack, baseComponentSave.Instances);

                elementStack.Remove(elementStack.FirstOrDefault(item => item.Element == baseComponentSave));
            }

            return rootIpso;
        }

        private void SetUpParentRelationship(IEnumerable<GraphicalUiElement> siblings, List<ElementWithState> elementStack, IEnumerable<InstanceSave> childrenInstances)
        {
            // Now that we have created all instances, we can establish parent relationships

            foreach (GraphicalUiElement contained in siblings)
            {
                if (contained.Tag is InstanceSave)
                {
                    InstanceSave childInstanceSave = contained.Tag as InstanceSave;
                    RecursiveVariableFinder rvf = new DataTypes.RecursiveVariableFinder(childInstanceSave, elementStack);

                    string parentName = rvf.GetValue<string>("Parent");

                    if (!string.IsNullOrEmpty(parentName) && parentName != AvailableInstancesConverter.ScreenBoundsName)
                    {
                        IPositionedSizedObject newParent = siblings.FirstOrDefault(item => item.Name == parentName);

                        // This may have bad XML so if it doesn't exist, then let's ignore this:
                        if (newParent != null)
                        {

                            contained.Parent = newParent;

                        }
                    }
                }

            }
        }




        private GraphicalUiElement CreateSpriteFor(ElementSave elementSave, GraphicalUiElement graphicalUiElement)
        {

            RecursiveVariableFinder rvf = GetRvfForCurrentElementState(elementSave);

            Sprite sprite = CreateSpriteInternal(elementSave, elementSave.Name);
            graphicalUiElement.SetContainedObject(sprite);
      

            InitializeSprite(sprite, rvf);
            
            graphicalUiElement.Visible = (bool)rvf.GetValue("Visible");

            graphicalUiElement.SetGueWidthAndPositionValues(rvf);


            return graphicalUiElement;
        }
        private GraphicalUiElement CreateSpriteFor(InstanceSave instance, List<ElementWithState> elementStack, IPositionedSizedObject parentRepresentation, GraphicalUiElement graphicalUiElement)
        {
            ElementSave parent = elementStack.Last().Element;

            RecursiveVariableFinder rvf = new DataTypes.RecursiveVariableFinder(instance, elementStack);

            Sprite sprite = CreateSpriteInternal(instance, instance.Name);
            graphicalUiElement.SetContainedObject(sprite);
                
                
            InitializeSprite(sprite, rvf);

            // Sprite may be dependent on the texture for its location, so set the dimensions and positions *after* texture

            graphicalUiElement.SetGueWidthAndPositionValues(rvf);

            graphicalUiElement.Visible = (bool)rvf.GetValue("Visible");
            return graphicalUiElement;
        }
        private Sprite CreateSpriteInternal(object tag, string name)
        {
            Sprite sprite = new Sprite(LoaderManager.Self.InvalidTexture);

            // Add it to the manager first because the positioning code may need to access the source element/instance
            SpriteManager.Self.Add(sprite);
            sprite.Name = name;
            sprite.Tag = tag;

            mSprites.Add(sprite);

            return sprite;
        }

        private IPositionedSizedObject CreateNineSliceFor(InstanceSave instance, List<ElementWithState> elementStack, GraphicalUiElement graphicalUiElement)
        {
            ElementSave parent = elementStack.Last().Element;
            RecursiveVariableFinder rvf = new DataTypes.RecursiveVariableFinder(instance, elementStack);

            
            NineSlice nineSlice = CreateNineSliceInternal(instance, instance.Name, rvf);
            
            InitializeNineSlice(nineSlice, rvf);

            // NineSlice may be dependent on the texture for its location, so set the dimensions and positions *after* texture

            graphicalUiElement.SetGueWidthAndPositionValues(rvf);

            graphicalUiElement.SetContainedObject(nineSlice);

            return graphicalUiElement;
        }
        private IPositionedSizedObject CreateNineSliceFor(ElementSave elementSave, GraphicalUiElement graphicalUiElement)
        {

            RecursiveVariableFinder rvf = GetRvfForCurrentElementState(elementSave);

            NineSlice nineSlice = CreateNineSliceInternal(elementSave, elementSave.Name, rvf);


            InitializeNineSlice(nineSlice, rvf);


            graphicalUiElement.SetGueWidthAndPositionValues(rvf);

            graphicalUiElement.SetContainedObject(nineSlice);

            return graphicalUiElement;
        }
        private NineSlice CreateNineSliceInternal(object tag, string name, RecursiveVariableFinder rvf)
        {
            NineSlice nineSlice = new NineSlice();

            // Add it to the manager first because the positioning code may need to access the source element/instance
            SpriteManager.Self.Add(nineSlice);
            nineSlice.Name = name;
            nineSlice.Tag = tag;

            mNineSlices.Add(nineSlice);


            return nineSlice;
        }
        
        private IPositionedSizedObject CreateSolidRectangleFor(ElementSave elementSave, GraphicalUiElement graphicalUiElement)
        {

            RecursiveVariableFinder rvf = GetRvfForCurrentElementState(elementSave);

            string baseType = elementSave.BaseType;
            if (string.IsNullOrEmpty(baseType))
            {
                baseType = elementSave.Name;
            }

            GumRuntime.InstanceSaveExtensionMethods.SetGraphicalUiElement(
                rvf, baseType, graphicalUiElement, null);



            SolidRectangle solidRectangle = graphicalUiElement.RenderableComponent as SolidRectangle;

            InitializeSolidRectangle(solidRectangle, elementSave.Name);
            solidRectangle.Tag = elementSave;



            

            return graphicalUiElement;           
        }

        private IPositionedSizedObject CreateSolidRectangleFor(InstanceSave instance, List<ElementWithState> elementStack, GraphicalUiElement graphicalUiElement)
        {
            ElementSave instanceBase = ObjectFinder.Self.GetElementSave(instance.BaseType);
            
            RecursiveVariableFinder rvf = new DataTypes.RecursiveVariableFinder(instance, elementStack);


            GumRuntime.InstanceSaveExtensionMethods.SetGraphicalUiElement(
                rvf, instance.BaseType, graphicalUiElement, null);

            SolidRectangle solidRectangle = graphicalUiElement.RenderableComponent as SolidRectangle;

            InitializeSolidRectangle(solidRectangle, instance.Name);
            solidRectangle.Tag = instance;
            
            return graphicalUiElement;
        }

        private void InitializeSolidRectangle(SolidRectangle solidRectangle, string name)
        {

            // Add it to the manager first because the positioning code may need to access the source element/instance
            ShapeManager.Self.Add(solidRectangle);
            solidRectangle.Name = name;
            mSolidRectangles.Add(solidRectangle);

        }


        private IPositionedSizedObject CreateRectangleFor(ElementSave elementSave, GraphicalUiElement graphicalUiElement)
        {
            LineRectangle lineRectangle = InstantiateAndNameRectangle(elementSave.Name);
            lineRectangle.Tag = elementSave;

            RecursiveVariableFinder rvf = GetRvfForCurrentElementState(elementSave);

            graphicalUiElement.SetContainedObject(lineRectangle);

            graphicalUiElement.SetGueWidthAndPositionValues(rvf);

            return graphicalUiElement;
        }

        private static DataTypes.RecursiveVariableFinder GetRvfForCurrentElementState(ElementSave elementSave)
        {
            StateSave sourceStateSave = elementSave.DefaultState;
            if (elementSave == SelectedState.Self.SelectedElement)
            {
                sourceStateSave = SelectedState.Self.SelectedStateSave;
            }

            RecursiveVariableFinder rvf = new DataTypes.RecursiveVariableFinder(sourceStateSave);
            return rvf;
        }
        private IPositionedSizedObject CreateRectangleFor(InstanceSave instance, List<ElementWithState> elementStack, GraphicalUiElement graphicalUiElement)
        {

            ElementSave instanceBase = ObjectFinder.Self.GetElementSave(instance.BaseType);

            LineRectangle lineRectangle = InstantiateAndNameRectangle(instance.Name);
            lineRectangle.Tag = instance;

            RecursiveVariableFinder rvf = new DataTypes.RecursiveVariableFinder(instance, elementStack);
            
            lineRectangle.Visible = rvf.GetValue<bool>("Visible");

            graphicalUiElement.SetContainedObject(lineRectangle);

            graphicalUiElement.SetGueWidthAndPositionValues(rvf);

            return graphicalUiElement;
        }
        private LineRectangle InstantiateAndNameRectangle(string name)
        {
            LineRectangle lineRectangle = new LineRectangle();

            // Add it to the manager first because the positioning code may need to access the source element/instance
            ShapeManager.Self.Add(lineRectangle);
            lineRectangle.Name = name;
            mLineRectangles.Add(lineRectangle);
            return lineRectangle;
        }

        private void SetGuideParent(IPositionedSizedObject parentIpso, GraphicalUiElement ipso, string guideName, bool setParentToBoundsIfNoGuide)
        {
            // I dont't think we want to do this anymore because it should be handled by the GraphicalUiElement
            ipso.Parent = parentIpso;

            if (!string.IsNullOrEmpty(guideName))
            {
                IPositionedSizedObject guideIpso = GetGuide(guideName);

                if (guideIpso != null)
                {
                    ipso.Parent = guideIpso;
                }
            }
            else if (setParentToBoundsIfNoGuide) 
            {
                ipso.Parent = mWireframeControl.ScreenBounds;
            }
        }


        private IPositionedSizedObject CreateTextFor(ElementSave elementSave, GraphicalUiElement graphicalUiElement)
        {

            RecursiveVariableFinder rvf = GetRvfForCurrentElementState(elementSave);

            Text text = CreateTextInternal(elementSave, elementSave.Name, rvf);


            graphicalUiElement.SetGueWidthAndPositionValues(rvf);

            text.EnableTextureCreation();

            graphicalUiElement.SetContainedObject(text);

            return graphicalUiElement;
        }
        private IPositionedSizedObject CreateTextFor(InstanceSave instance, List<ElementWithState> elementStack, IPositionedSizedObject parentRepresentation, GraphicalUiElement graphicalUiElement)
        {
            ElementSave parent = elementStack.Last().Element;
            RecursiveVariableFinder rvf = new DataTypes.RecursiveVariableFinder(instance, elementStack);

            Text text = CreateTextInternal(instance, instance.Name, rvf);
            
            graphicalUiElement.SetContainedObject(text);
            graphicalUiElement.SetGueWidthAndPositionValues(rvf);

            return graphicalUiElement;
        }
        private Text CreateTextInternal(object tag, string name, RecursiveVariableFinder rvf)
        {
            Text text = new Text(null);
            text.SuppressTextureCreation();
            text.Tag = tag;
            text.Name = name;

            text.RenderBoundary = ProjectManager.Self.GeneralSettingsFile.ShowTextOutlines;

            // Add it to the manager first because the positioning code may need to access the source element/instance
            TextManager.Self.Add(text);
            mTexts.Add(text);

            text.Alpha = rvf.GetValue<int>("Alpha") ;
            text.Red = rvf.GetValue<int>("Red");
            text.Green = rvf.GetValue<int>("Green");
            text.Blue = rvf.GetValue<int>("Blue");

            text.Visible = rvf.GetValue<bool>("Visible");



            text.RawText = (string)rvf.GetValue("Text");
            text.HorizontalAlignment = (HorizontalAlignment)rvf.GetValue("HorizontalAlignment");
            text.VerticalAlignment = (VerticalAlignment)rvf.GetValue("VerticalAlignment");

            if(rvf.GetValue<bool>("UseCustomFont"))
            {
                string customFontFile = rvf.GetValue<string>("CustomFontFile");

                if (!string.IsNullOrEmpty(customFontFile))
                {
                    customFontFile = ProjectManager.Self.MakeAbsoluteIfNecessary(customFontFile);

                    if (System.IO.File.Exists(customFontFile))
                    {
                        BitmapFont font = new BitmapFont(customFontFile, null);
                        text.BitmapFont = font;
                    }
                }
            }
            else
            {
                string fontName = (string)rvf.GetValue("Font");
                int fontSize = (int)rvf.GetValue("FontSize");
                int outlineThickness = rvf.GetValue<int>("OutlineThickness");

                BmfcSave.CreateBitmapFontFilesIfNecessary(
                    fontSize,
                    fontName,
                    outlineThickness);

                text.BitmapFont = FontManager.Self.GetBitmapFontFor(fontName, fontSize, outlineThickness);
            }
            text.EnableTextureCreation();
            return text;
        }

                
        StateSave temporaryStateSave = new StateSave();


        //public void FillStateWithPositionAndSizeVariables(ElementSave elementSave, StateSave stateSave)
        //{
        //    FillStateWithVariables(elementSave, stateSave, PositionAndSizeVariables);
        //}
        //public void FillStateWithPositionAndSizeVariables(InstanceSave instance, ElementSave parent, StateSave stateSave)
        //{
        //    FillStateWithVariables(instance, parent, stateSave, PositionAndSizeVariables);
        //}
        public void FillStateWithVariables(RecursiveVariableFinder rvf, StateSave stateSave, IEnumerable variables)
        {
            foreach (string variable in variables)
            {
                stateSave.SetValue(variable, rvf.GetValue(variable));
            }
        }
        
        private void InitializeSprite(Sprite sprite, RecursiveVariableFinder rvf)
        {
            string textureName = (string)rvf.GetValue("SourceFile");
            bool animate = rvf.GetValue<bool>("Animate");
            List<string> animations = (List<string>)rvf.GetValue("AnimationFrames");

            sprite.Animate = animate;

            textureName = ProjectManager.Self.MakeAbsoluteIfNecessary(textureName);

            if (animations != null && animations.Count != 0)
            {
                for (int i = 0; i < animations.Count; i++)
                {
                    animations[i] = ProjectManager.Self.MakeAbsoluteIfNecessary(animations[i]);
                }

                TextureFlipAnimation tfa = TextureFlipAnimation.FromStringList(animations, null);
                
                sprite.Animation = tfa;

            }
            else
            {
                if (!string.IsNullOrEmpty(textureName))
                {
                    if (System.IO.File.Exists(textureName))
                    {
                        string error;
                        sprite.Texture = LoaderManager.Self.LoadOrInvalid(textureName, null, out error);
                        // Do we want to print this out somewhere?  Well, for now we'll just
                        // tolearte it silently because the invalid texture is shown
                    }
                    else
                    {
                        // The file doesn't exist, let's give this Sprite the null texture
                        sprite.Texture = LoaderManager.Self.InvalidTexture;
                    }
                }
            }


            if (rvf.GetValue("FlipHorizontal") != null)
            {
                sprite.FlipHorizontal = (bool)rvf.GetValue("FlipHorizontal");
            }
            if (rvf.GetValue("FlipVertical") != null)
            {
                sprite.FlipVertical = (bool)rvf.GetValue("FlipVertical");
            }

            var color = GetColorFromRvf(rvf);

            sprite.Color = color;

            if (rvf.GetValue("Blend") != null)
            {
                var blend = (Blend)rvf.GetValue("Blend");
                if (blend == Blend.Normal)
                {
                    // do nothing?
                }
                else if (blend == Blend.Additive)
                {
                    sprite.BlendState = Microsoft.Xna.Framework.Graphics.BlendState.Additive;
                }
                else
                {
                    throw new Exception("This blend mode is not supported");
                }
            }

            bool useCustomSource = rvf.GetValue<bool>("Custom Texture Coordinates");
            if (useCustomSource)
            {
                sprite.SourceRectangle = new Rectangle(
                    rvf.GetValue<int>("Texture Left"),
                    rvf.GetValue<int>("Texture Top"),
                    rvf.GetValue<int>("Texture Width"),
                    rvf.GetValue<int>("Texture Height"));

                sprite.Wrap = rvf.GetValue<bool>("Wrap");
            }
        }

        private static Microsoft.Xna.Framework.Color GetColorFromRvf(RecursiveVariableFinder rvf)
        {
            int alpha = 255;
            int red = 255;
            int green = 255;
            int blue = 255;

            alpha = rvf.GetValue<int>("Alpha");
            red = rvf.GetValue<int>("Red");
            green = rvf.GetValue<int>("Green");
            blue = rvf.GetValue<int>("Blue");

            var color = new Color(red, green, blue, alpha);
            return color;
        }

        private void InitializeNineSlice(NineSlice nineSlice, RecursiveVariableFinder rvf)
        {
            string textureName = (string)rvf.GetValue("SourceFile");
            string absoluteTexture = ProjectManager.Self.MakeAbsoluteIfNecessary(textureName);

            nineSlice.SetTexturesUsingPattern(absoluteTexture, null);

            var color = GetColorFromRvf(rvf);
            nineSlice.Color = color;

            if (rvf.GetValue("Blend") != null)
            {
                var blend = (Blend)rvf.GetValue("Blend");
                if (blend == Blend.Normal)
                {
                    // do nothing?
                }
                else if (blend == Blend.Additive)
                {
                    nineSlice.BlendState = Microsoft.Xna.Framework.Graphics.BlendState.Additive;
                }
                else
                {
                    throw new Exception("This blend mode is not supported");
                }
            }


            nineSlice.Visible = (bool)rvf.GetValue("Visible");

        }



    }
}
