﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.UI
{
    public class InteractableShaderTheme : InteractableThemeBase
    {
        /// <inheritdoc />
        public override bool AreShadersSupported => true;

        private static ThemePropertyValue emptyValue = new ThemePropertyValue();

        protected MaterialPropertyBlock propertyBlock;
        protected List<ThemeStateProperty> shaderProperties;
        protected Renderer renderer;

        private ThemePropertyValue startValue = new ThemePropertyValue();

        protected const string DefaultShaderProperty = "_Color";
        protected const string DefaultShaderName = "Mixed Reality Toolkit/Standard";

        public InteractableShaderTheme()
        {
            Types = new Type[] { typeof(Renderer) };
            Name = "Shader Float";
        }

        /// <inheritdoc />
        public override ThemeDefinition GetDefaultThemeDefinition()
        {
            return new ThemeDefinition()
            {
                ThemeType = GetType(),
                StateProperties = new List<ThemeStateProperty>()
                {
                    new ThemeStateProperty()
                    {
                        Name = "Shader Value",
                        Type = ThemePropertyTypes.ShaderFloat,
                        Values = new List<ThemePropertyValue>(),
                        Default = new ThemePropertyValue() { Float = 0},
                        TargetShader = Shader.Find(DefaultShaderName),
                        ShaderPropertyName = DefaultShaderProperty,
                    },
                },
                CustomProperties =  new List<ThemeProperty>(),
            };
        }

        /// <inheritdoc />
        public override void Init(GameObject host, ThemeDefinition definition)
        {
            base.Init(host, definition);

            renderer = Host.GetComponent<Renderer>();

            shaderProperties = new List<ThemeStateProperty>();
            foreach (var prop in StateProperties)
            {
                if (ThemeStateProperty.IsShaderPropertyType(prop.Type))
                {
                    shaderProperties.Add(prop);
                }
            }

            // TODO: Troy - Why do we need this?
            propertyBlock = InteractableThemeShaderUtils.GetMaterialPropertyBlock(host, shaderProperties);
        }

        /// <inheritdoc />
        public override void SetValue(ThemeStateProperty property, int index, float percentage)
        {
            if (renderer != null)
            {
                renderer.GetPropertyBlock(propertyBlock);

                int propId = property.GetShaderPropertyId();
                switch (property.Type)
                {
                    case ThemePropertyTypes.Color:
                        Color newColor = Color.Lerp(property.StartValue.Color, property.Values[index].Color, percentage);
                        propertyBlock = SetColor(propertyBlock, newColor, propId);
                        break;
                    case ThemePropertyTypes.ShaderFloat:
                        float floatValue = LerpFloat(property.StartValue.Float, property.Values[index].Float, percentage);
                        propertyBlock = SetFloat(propertyBlock, floatValue, propId);
                        break;
                    case ThemePropertyTypes.ShaderRange:
                        float rangeValue = LerpFloat(property.StartValue.Float, property.Values[index].Float, percentage);
                        propertyBlock = SetFloat(propertyBlock, rangeValue, propId);
                        break;
                    default:
                        break;
                }

                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        /// <inheritdoc />
        public override ThemePropertyValue GetProperty(ThemeStateProperty property)
        {
            if (renderer == null)
            {
                return null;
            }

            renderer.GetPropertyBlock(propertyBlock);

            startValue.Reset();

            int propId = property.GetShaderPropertyId();
            switch (property.Type)
            {
                case ThemePropertyTypes.Color:
                    startValue.Color = propertyBlock.GetVector(propId);
                    break;
                case ThemePropertyTypes.ShaderFloat:
                    startValue.Float = propertyBlock.GetFloat(propId);
                    break;
                case ThemePropertyTypes.ShaderRange:
                    startValue.Float = propertyBlock.GetFloat(propId);
                    break;
                default:
                    break;
            }

            return startValue;
        }

        public static float GetFloat(GameObject host, int propId)
        {
            if (host == null)
                return 0;

            MaterialPropertyBlock block = InteractableThemeShaderUtils.GetPropertyBlock(host);
            return block.GetFloat(propId);
        }

        public static void SetPropertyBlock(GameObject host, MaterialPropertyBlock block)
        {
            Renderer renderer = host.GetComponent<Renderer>();
            renderer.SetPropertyBlock(block);
        }

        public static MaterialPropertyBlock SetFloat(MaterialPropertyBlock block, float value, int propId)
        {
            if (block == null)
            {
                return null;
            }

            block.SetFloat(propId, value);
            return block;
        }

        public static Color GetColor(GameObject host, int propId)
        {
            if (host == null)
            {
                return Color.white;
            }

            MaterialPropertyBlock block = InteractableThemeShaderUtils.GetPropertyBlock(host);
            return block.GetVector(propId);
        }

        public static MaterialPropertyBlock SetColor(MaterialPropertyBlock block, Color color, int propId)
        {
            if (block == null)
                return null;

            block.SetColor(propId, color);
            return block;

        }
    }
}
